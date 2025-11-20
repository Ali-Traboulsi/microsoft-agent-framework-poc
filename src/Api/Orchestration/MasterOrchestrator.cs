using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator that coordinates multiple specialized sub-agents
/// </summary>
public class MasterOrchestrator : IMasterOrchestrator
{
    // OpenTelemetry observability
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.MasterOrchestrator",
        "2.0.0"
    );
    private static readonly Meter Meter = new("InvestmentBanking.MasterOrchestrator", "2.0.0");
    private static readonly Counter<long> DelegationCounter = Meter.CreateCounter<long>(
        "orchestrator.delegations",
        description: "Number of sub-agent delegations"
    );
    private static readonly Histogram<double> DelegationDuration = Meter.CreateHistogram<double>(
        "orchestrator.delegation.duration",
        unit: "ms",
        description: "Duration of sub-agent delegations"
    );
    private static readonly Counter<long> DelegationErrorCounter = Meter.CreateCounter<long>(
        "orchestrator.delegation.errors",
        description: "Number of delegation failures"
    );

    private readonly IChatClient _chatClient;
    private readonly IEnumerable<ISubAgent> _subAgents;
    private readonly ILogger<MasterOrchestrator> _logger;
    private readonly Lazy<AIAgent> _masterAgent;
    private readonly Dictionary<string, ISubAgent> _subAgentLookup;
    private readonly WebSearchTools _webSearchTools;
    private readonly StructuredResponseHandler _structuredResponseHandler;

    public MasterOrchestrator(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        WebSearchTools webSearchTools,
        ILogger<MasterOrchestrator> logger,
        StructuredResponseHandler structuredResponseHandler
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _webSearchTools = webSearchTools;
        _logger = logger;
        _structuredResponseHandler = structuredResponseHandler;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
        _masterAgent = new Lazy<AIAgent>(CreateMasterAgentWithMiddleware);
    }

    private AIAgent CreateMasterAgent()
    {
        var instructions = AgentInstructionsLoader.LoadMasterAgentInstructions(_subAgents);

        return _chatClient.CreateAIAgent(
            name: "MasterAgent",
            instructions: instructions,
            tools:
            [
                AIFunctionFactory.Create(DelegateToSubAgent),
                AIFunctionFactory.Create(DelegateToMultipleSubAgents),
                AIFunctionFactory.Create(GetAvailableSubAgentsAsString),
                AIFunctionFactory.Create(_webSearchTools.SearchWeb),
            ]
        );
    }

    /// <summary>
    /// Create master agent with middleware for event tracking
    /// </summary>
    private AIAgent CreateMasterAgentWithMiddleware()
    {
        var baseAgent = CreateMasterAgent();

        // Wrap agent with delegation event middleware
        return baseAgent
            .AsBuilder()
            .Use(DelegationEventMiddleware.FunctionInvocationMiddleware)
            .Build();
    }

    [Description("Delegate a request to a specific sub-agent specialist")]
    public async Task<string> DelegateToSubAgent(
        [Description(
            "The name of the sub-agent: PortfolioManager, InvestmentAdvisor, AccountServices, or ComplianceOfficer"
        )]
            string subAgentName,
        [Description("The specific task or question for the sub-agent")] string request,
        [Description("Optional context as JSON string")] string? context = null
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.DelegateToSubAgent",
            ActivityKind.Internal
        );
        activity?.SetTag("subagent.name", subAgentName);
        activity?.SetTag("request.length", request.Length);

        _logger.LogInformation(
            "Master Agent delegating to {SubAgent}: {Request}",
            subAgentName,
            request
        );

        if (!_subAgentLookup.TryGetValue(subAgentName, out var subAgent))
        {
            var availableAgents = string.Join(", ", _subAgentLookup.Keys);
            var errorMsg =
                $"❌ **Error:** Sub-agent '{subAgentName}' not found.\n\n"
                + $"**Available sub-agents:** {availableAgents}";

            _logger.LogWarning(
                "Invalid sub-agent requested: {SubAgent}. Available: {Available}",
                subAgentName,
                availableAgents
            );

            activity?.SetStatus(ActivityStatusCode.Error, "Sub-agent not found");
            DelegationErrorCounter.Add(1, new KeyValuePair<string, object?>("error", "not_found"));

            return errorMsg;
        }

        var contextDict = string.IsNullOrEmpty(context)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(context);

        var sw = Stopwatch.StartNew();
        var response = await subAgent.HandleRequestAsync(request, contextDict);
        sw.Stop();

        // Record metrics
        DelegationCounter.Add(
            1,
            new KeyValuePair<string, object?>("subagent", subAgentName),
            new KeyValuePair<string, object?>("success", response.Success)
        );
        DelegationDuration.Record(
            response.DurationMs,
            new KeyValuePair<string, object?>("subagent", subAgentName)
        );

        if (response.Success)
        {
            var result = new StringBuilder();
            result.AppendLine($"✅ **{subAgent.Name} Response:**\n");
            result.AppendLine(response.Result);
            result.AppendLine();
            result.AppendLine($"📊 *Tools used:* {string.Join(", ", response.ToolsUsed)}");
            result.AppendLine($"⏱️ *Duration:* {response.DurationMs}ms");

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.tools_used", response.ToolsUsed.Count);
            activity?.SetTag("response.duration_ms", response.DurationMs);

            _logger.LogInformation(
                "{SubAgent} delegation successful in {Duration}ms",
                subAgentName,
                response.DurationMs
            );

            return result.ToString();
        }
        else
        {
            var errorResult = $"❌ **{subAgent.Name} Error:**\n{response.ErrorMessage}";

            activity?.SetStatus(ActivityStatusCode.Error, response.ErrorMessage);
            DelegationErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("subagent", subAgentName),
                new KeyValuePair<string, object?>("error", "execution_failed")
            );

            _logger.LogError(
                "{SubAgent} delegation failed: {Error}",
                subAgentName,
                response.ErrorMessage
            );

            return errorResult;
        }
    }

    [Description("Delegate to multiple sub-agents for complex multi-domain requests")]
    public async Task<string> DelegateToMultipleSubAgents(
        [Description(
            "JSON array of delegation requests: [{\"subAgent\": \"name\", \"request\": \"task\", \"context\": \"optional\"}]"
        )]
            string delegationsJson
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.DelegateToMultipleSubAgents",
            ActivityKind.Internal
        );

        _logger.LogInformation("Master Agent delegating to multiple sub-agents");

        List<DelegationRequest> delegations;
        try
        {
            delegations =
                JsonSerializer.Deserialize<List<DelegationRequest>>(delegationsJson)
                ?? throw new JsonException("Failed to parse delegations");

            activity?.SetTag("delegations.count", delegations.Count);
            activity?.SetTag(
                "delegations.subagents",
                string.Join(",", delegations.Select(d => d.SubAgent))
            );
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse delegation JSON: {Json}", delegationsJson);
            activity?.SetStatus(ActivityStatusCode.Error, "Invalid JSON format");
            DelegationErrorCounter.Add(
                1,
                new KeyValuePair<string, object?>("error", "invalid_json")
            );

            return $"❌ **Error:** Invalid delegation format. {ex.Message}";
        }

        var results = new StringBuilder();
        results.AppendLine("## Multi-Agent Coordination Results\n");

        var totalSw = Stopwatch.StartNew();

        foreach (var delegation in delegations)
        {
            results.AppendLine($"### {delegation.SubAgent}\n");

            var result = await DelegateToSubAgent(
                delegation.SubAgent,
                delegation.Request,
                delegation.Context
            );

            results.AppendLine(result);
            results.AppendLine("\n---\n");
        }

        totalSw.Stop();
        results.AppendLine($"**Total coordination time:** {totalSw.ElapsedMilliseconds}ms");

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("total.duration_ms", totalSw.ElapsedMilliseconds);

        // Record multi-agent coordination metric
        DelegationDuration.Record(
            totalSw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("delegation_type", "multiple"),
            new KeyValuePair<string, object?>("count", delegations.Count)
        );

        _logger.LogInformation(
            "Multi-agent delegation completed in {Duration}ms for {Count} agents",
            totalSw.ElapsedMilliseconds,
            delegations.Count
        );

        return results.ToString();
    }

    [Description("Get information about all available sub-agents and their capabilities")]
    public string GetAvailableSubAgentsAsString()
    {
        var result = new StringBuilder();
        result.AppendLine("## Available Sub-Agents\n");

        foreach (var agent in _subAgents)
        {
            result.AppendLine($"### {agent.Name}");
            result.AppendLine($"**Domain:** {agent.Domain}\n");
            result.AppendLine("**Capabilities:**");
            foreach (var capability in agent.Capabilities)
            {
                result.AppendLine($"- {capability}");
            }
            result.AppendLine();
        }

        return result.ToString();
    }

    public List<SubAgentInfo> GetAvailableSubAgents()
    {
        return _subAgents
            .Select(sa => new SubAgentInfo
            {
                Name = sa.Name,
                Domain = sa.Domain,
                Capabilities = sa.Capabilities,
            })
            .ToList();
    }

    public async Task<OrchestratorResult> ProcessRequestAsync(
        string userMessage,
        string conversationId
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequest",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("message.length", userMessage.Length);

        var sw = Stopwatch.StartNew();
        var subAgentsUsed = new List<string>();

        try
        {
            _logger.LogInformation(
                "Processing request for conversation {ConversationId}: {Message}",
                conversationId,
                userMessage
            );

            var result = await _masterAgent.Value.RunAsync(userMessage);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.length", responseText.Length);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation("Request completed in {Duration}ms", sw.ElapsedMilliseconds);

            return new OrchestratorResult
            {
                Success = true,
                Response = responseText,
                SubAgentsUsed = subAgentsUsed,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);
            activity?.AddTag("exception.stacktrace", ex.StackTrace);

            _logger.LogError(
                ex,
                "Error processing request for conversation {ConversationId}: {Error}",
                conversationId,
                ex.Message
            );

            return new OrchestratorResult
            {
                Success = false,
                Response = $"An error occurred: {ex.Message}",
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    public async IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingAsync(
        string userMessage,
        string conversationId
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequestStreaming",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("message.length", userMessage.Length);

        // Set conversation ID for middleware to track events
        DelegationEventMiddleware.CurrentConversationId = conversationId;

        _logger.LogInformation(
            "Processing streaming request for conversation {ConversationId}: {Message}",
            conversationId,
            userMessage
        );

        var fullResponse = new StringBuilder();
        var isInThinkingBlock = false;
        var chunkCount = 0;
        var lastEventCheck = DateTime.UtcNow;
        var hasSeenDelegation = false;
        var hasStartedThinking = false;

        try
        {
            await foreach (var chunk in _masterAgent.Value.RunStreamingAsync(userMessage))
            {
                // Check for new delegation events FIRST
                while (
                    DelegationEventMiddleware.TryGetNextEvent(
                        conversationId,
                        out var delegationEvent
                    )
                )
                {
                    hasSeenDelegation = true;
                    isInThinkingBlock = false; // End thinking when delegation starts
                    yield return ConvertDelegationEventToResponse(delegationEvent);
                }

                if (chunk.Text != null)
                {
                    fullResponse.Append(chunk.Text);
                    chunkCount++;

                    // Everything BEFORE delegation is thinking (agent's reasoning process)
                    if (!hasSeenDelegation)
                    {
                        if (!hasStartedThinking)
                        {
                            isInThinkingBlock = true;
                            hasStartedThinking = true;
                        }
                    }
                    else
                    {
                        // Everything AFTER delegation is content (final response)
                        isInThinkingBlock = false;
                    }

                    var responseType = isInThinkingBlock
                        ? ResponseType.Thinking
                        : ResponseType.Content;

                    _logger.LogInformation(
                        "Streaming chunk: Type={Type}, HasSeenDelegation={HasSeenDelegation}, ChunkPreview={Preview}",
                        responseType,
                        hasSeenDelegation,
                        chunk.Text.Length > 50 ? chunk.Text[..50] + "..." : chunk.Text
                    );

                    yield return new OrchestratorResponse
                    {
                        Type = responseType,
                        Content = chunk.Text,
                    };
                }

                // Also check for events periodically during streaming
                if ((DateTime.UtcNow - lastEventCheck).TotalMilliseconds > 100)
                {
                    while (
                        DelegationEventMiddleware.TryGetNextEvent(
                            conversationId,
                            out var delegationEvent
                        )
                    )
                    {
                        yield return ConvertDelegationEventToResponse(delegationEvent);
                    }
                    lastEventCheck = DateTime.UtcNow;
                }
            }

            // Final check for any remaining events
            while (
                DelegationEventMiddleware.TryGetNextEvent(conversationId, out var delegationEvent)
            )
            {
                yield return ConvertDelegationEventToResponse(delegationEvent);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.total_length", fullResponse.Length);
            activity?.SetTag("response.chunk_count", chunkCount);

            _logger.LogInformation(
                "Streaming completed for conversation {ConversationId}",
                conversationId
            );

            yield return new OrchestratorResponse
            {
                Type = ResponseType.Complete,
                Content = "",
                Metadata = new Dictionary<string, object>
                {
                    ["traceId"] = activity?.TraceId.ToString() ?? "",
                    ["totalChunks"] = chunkCount,
                    ["responseLength"] = fullResponse.Length,
                },
            };
        }
        finally
        {
            // Cleanup
            DelegationEventMiddleware.CleanupConversation(conversationId);
            DelegationEventMiddleware.CurrentConversationId = null;
        }
    }

    /// <summary>
    /// Convert middleware delegation event to orchestrator response
    /// </summary>
    private OrchestratorResponse ConvertDelegationEventToResponse(DelegationEvent delegationEvent)
    {
        return delegationEvent.Type switch
        {
            DelegationEventType.SubAgentDelegationStart => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentDelegation,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"🔄 Delegating to **{delegationEvent.SubAgentName}**...",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["functionName"] = delegationEvent.FunctionName ?? "",
                },
            },
            DelegationEventType.SubAgentDelegationComplete => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentComplete,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"✅ **{delegationEvent.SubAgentName}** completed",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["duration"] = (
                        delegationEvent.Timestamp - (delegationEvent.Timestamp)
                    ).TotalMilliseconds,
                },
            },
            DelegationEventType.SubAgentDelegationError => new OrchestratorResponse
            {
                Type = ResponseType.SubAgentComplete,
                SubAgentName = delegationEvent.SubAgentName,
                Content = $"❌ **{delegationEvent.SubAgentName}** error: {delegationEvent.Error}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["error"] = delegationEvent.Error ?? "",
                },
            },
            DelegationEventType.ToolExecutionStart => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"🔧 Executing **{delegationEvent.ToolName}**...",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            DelegationEventType.ToolExecutionComplete => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"✅ Tool **{delegationEvent.ToolName}** completed",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                },
            },
            DelegationEventType.ToolExecutionError => new OrchestratorResponse
            {
                Type = ResponseType.ToolExecution,
                ToolName = delegationEvent.ToolName,
                Content = $"❌ Tool **{delegationEvent.ToolName}** error: {delegationEvent.Error}",
                Metadata = new Dictionary<string, object>
                {
                    ["timestamp"] = delegationEvent.Timestamp,
                    ["error"] = delegationEvent.Error ?? "",
                },
            },
            _ => new OrchestratorResponse { Type = ResponseType.Content, Content = "" },
        };
    }

    public async Task<StructuredOrchestratorResult> ProcessRequestStructuredAsync(
        string userMessage,
        string conversationId
    )
    {
        return await _structuredResponseHandler.ProcessAsync(
            userMessage,
            conversationId,
            DelegateToSubAgent,
            DelegateToMultipleSubAgents
        );
    }

    public async Task<OrchestratorResult> ProcessMultiModalRequestAsync(
        List<AIContent> contents,
        string conversationId
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessMultiModalRequest",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("content.count", contents.Count);
        activity?.SetTag("content.types", string.Join(",", contents.Select(c => c.GetType().Name)));

        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing multi-modal request for conversation {ConversationId} with {ContentCount} content items",
                conversationId,
                contents.Count
            );

            // Create chat message with all content types
            var chatMessage = new ChatMessage(ChatRole.User, contents);

            // Run the agent with multi-modal content
            var result = await _masterAgent.Value.RunAsync(chatMessage);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated.";

            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation(
                "Multi-modal request completed in {Duration}ms for conversation {ConversationId}",
                sw.ElapsedMilliseconds,
                conversationId
            );

            return new OrchestratorResult
            {
                Success = true,
                Response = responseText,
                SubAgentsUsed = new List<string>(), // Will be populated by middleware events
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);

            _logger.LogError(
                ex,
                "Error processing multi-modal request for conversation {ConversationId}: {Error}",
                conversationId,
                ex.Message
            );

            return new OrchestratorResult
            {
                Success = false,
                Response = $"Error processing multi-modal request: {ex.Message}",
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    public async IAsyncEnumerable<OrchestratorResponse> ProcessMultiModalRequestStreamingAsync(
        List<AIContent> contents,
        string conversationId
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessMultiModalRequestStreaming",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("content.count", contents.Count);
        activity?.SetTag("content.types", string.Join(",", contents.Select(c => c.GetType().Name)));

        _logger.LogInformation(
            "Processing streaming multi-modal request for conversation {ConversationId} with {ContentCount} content items",
            conversationId,
            contents.Count
        );

        var contentBuilder = new StringBuilder();
        var startTime = Stopwatch.GetTimestamp();

        // Create chat message with all content types
        var chatMessage = new ChatMessage(ChatRole.User, contents);

        // Stream the agent's response
        await foreach (var update in _masterAgent.Value.RunStreamingAsync(chatMessage))
        {
            if (update.Contents is { Count: > 0 })
            {
                foreach (var contentItem in update.Contents)
                {
                    if (contentItem is TextContent textContent)
                    {
                        contentBuilder.Append(textContent.Text);

                        yield return new OrchestratorResponse
                        {
                            Type = ResponseType.Content,
                            Content = textContent.Text,
                            Metadata = new Dictionary<string, object>
                            {
                                ["timestamp"] = DateTime.UtcNow,
                                ["isStreaming"] = true,
                            },
                        };
                    }
                }
            }
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("duration_ms", elapsedMs);

        _logger.LogInformation(
            "Multi-modal streaming request completed in {Duration}ms for conversation {ConversationId}",
            elapsedMs,
            conversationId
        );

        yield return new OrchestratorResponse
        {
            Type = ResponseType.Complete,
            Content = contentBuilder.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["totalDurationMs"] = elapsedMs,
                ["contentCount"] = contents.Count,
            },
        };
    }
}

/// <summary>
/// Delegation request for multiple sub-agents
/// </summary>
internal class DelegationRequest
{
    public required string SubAgent { get; set; }
    public required string Request { get; set; }
    public string? Context { get; set; }
}

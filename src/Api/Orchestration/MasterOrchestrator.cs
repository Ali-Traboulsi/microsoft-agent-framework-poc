using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
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

    public MasterOrchestrator(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        ILogger<MasterOrchestrator> logger
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _logger = logger;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
        _masterAgent = new Lazy<AIAgent>(CreateMasterAgent);
    }

    private AIAgent CreateMasterAgent()
    {
        var subAgentDescriptions = string.Join(
            "\n",
            _subAgents.Select(sa =>
                $"**{sa.Name}** - {sa.Domain}\n"
                + $"  Capabilities: {string.Join(", ", sa.Capabilities)}"
            )
        );

        return _chatClient.CreateAIAgent(
            name: "MasterAgent",
            instructions: $@"You are an intelligent Master Agent coordinating a team of specialized financial services experts.

**Your Team of Sub-Agents:**

{subAgentDescriptions}

**Your Core Responsibilities:**

1. **Analyze User Intent**
   - Understand what the user is asking for
   - Identify which domain(s) are involved
   - Determine complexity (single vs. multi-agent task)

2. **Think Out Loud (CRITICAL)**
   Before taking action, you MUST explain your reasoning:
   
   🤔 **My Analysis:**
   - **User Request:** [What is the user asking for?]
   - **Domain(s) Needed:** [Which area(s) does this involve?]
   - **Selected Sub-Agent(s):** [Which specialist(s) should handle this?]
   - **Approach:** [Will I delegate to one agent or coordinate multiple?]
   
   This makes your decision-making transparent to users.

3. **Delegate Intelligently**
   - Use DelegateToSubAgent for single-domain requests
   - Use DelegateToMultipleSubAgents for cross-domain requests
   - Pass clear, specific instructions to each sub-agent

4. **Coordinate and Synthesize**
   - When multiple agents are involved, combine their insights
   - Provide a unified, coherent response
   - Highlight key information from each specialist

5. **Maintain Context**
   - Remember previous interactions in the conversation
   - Build on earlier responses
   - Reference specific accounts, portfolios, or funds mentioned

**Delegation Guidelines:**

- **Portfolio questions** → PortfolioManager
- **Investment advice/fund recommendations** → InvestmentAdvisor
- **Account operations/balances** → AccountServices
- **Compliance/risk assessment** → ComplianceOfficer
- **Complex workflows** → Multiple agents in sequence or parallel

**Response Quality:**
- Always explain which sub-agent you're consulting
- Present information clearly and professionally
- Include relevant metrics and data
- Provide actionable recommendations
- Use markdown formatting for better readability

**Error Handling:**
- If a sub-agent fails, explain the issue clearly
- Suggest alternatives or next steps
- Never leave the user without a response",
            tools:
            [
                AIFunctionFactory.Create(DelegateToSubAgent),
                AIFunctionFactory.Create(DelegateToMultipleSubAgents),
                AIFunctionFactory.Create(GetAvailableSubAgentsAsString),
            ]
        );
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

        _logger.LogInformation(
            "Processing streaming request for conversation {ConversationId}: {Message}",
            conversationId,
            userMessage
        );

        var fullResponse = new StringBuilder();
        var isInThinkingBlock = false;
        var chunkCount = 0;

        await foreach (var chunk in _masterAgent.Value.RunStreamingAsync(userMessage))
        {
            if (chunk.Text != null)
            {
                fullResponse.Append(chunk.Text);
                chunkCount++;

                // Detect thinking blocks
                if (chunk.Text.Contains("🤔"))
                {
                    isInThinkingBlock = true;
                }

                yield return new OrchestratorResponse
                {
                    Type = isInThinkingBlock ? ResponseType.Thinking : ResponseType.Content,
                    Content = chunk.Text,
                };

                // End of thinking block
                if (
                    isInThinkingBlock && (chunk.Text.Contains("\n\n") || chunk.Text.Contains("---"))
                )
                {
                    isInThinkingBlock = false;
                }
            }
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("response.total_length", fullResponse.Length);
        activity?.SetTag("response.chunk_count", chunkCount);

        _logger.LogInformation(
            "Streaming completed for conversation {ConversationId}",
            conversationId
        );

        yield return new OrchestratorResponse { Type = ResponseType.Complete, Content = "" };
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

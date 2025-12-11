using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Swarm pattern execution
/// This partial class adds OpenAI Swarm-inspired sub-agent coordination.
/// Sub-agents are exposed as tools, and the LLM naturally decides which to call.
/// </summary>
public partial class MasterOrchestrator
{
    // Conversation state for Swarm pattern (maintains history across turns)
    private static readonly Dictionary<string, SwarmConversationHistory> SwarmHistories = new();
    private static readonly object SwarmHistoryLock = new();

    /// <summary>
    /// Execute using Swarm pattern - sub-agents as tools, LLM decides routing.
    /// This is the ONLY execution path for text requests.
    /// The LLM sees the full conversation history and decides what to do.
    /// Uses streaming for real-time response delivery.
    /// </summary>
    private async IAsyncEnumerable<UnifiedStreamingChunk> StreamSwarmAsync(
        string userMessage,
        UserIntent intent,
        string conversationId,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var history = GetOrCreateSwarmHistory(conversationId);
        var agentsUsed = new List<string>();

        // Add user message to history
        history.Messages.Add(new ChatMessage(ChatRole.User, userMessage));

        _logger.LogInformation(
            "🔄 Swarm START: ConversationId={ConversationId}, Message='{Message}', HistoryCount={Count}",
            conversationId,
            userMessage.Length > 80 ? userMessage[..80] + "..." : userMessage,
            history.Messages.Count
        );

        // Sanitize history - remove any incomplete tool call sequences
        SanitizeConversationHistory(history);

        var iterationCount = 0;
        const int maxIterations = 10;

        while (iterationCount < maxIterations)
        {
            iterationCount++;
            cancellationToken.ThrowIfCancellationRequested();

            // Build system prompt and messages
            var systemPrompt = BuildSwarmSystemPrompt(intent);
            var messagesForModel = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };
            messagesForModel.AddRange(history.Messages);

            // Build tools from sub-agents
            var tools = BuildSubAgentTools(conversationId);
            var options = new ChatOptions { Tools = tools, ToolMode = ChatToolMode.Auto };

            _logger.LogInformation(
                "🤖 Swarm iteration {Iteration}: Sending {MessageCount} messages with {ToolCount} tools",
                iterationCount,
                messagesForModel.Count,
                tools.Count
            );

            // Use STREAMING to get real-time response
            var streamedText = new StringBuilder();
            var toolCalls = new List<FunctionCallContent>();

            await foreach (
                var update in _chatClient
                    .GetStreamingResponseAsync(messagesForModel, options, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                // Check for tool calls in this update
                foreach (var content in update.Contents)
                {
                    if (content is FunctionCallContent functionCall)
                    {
                        toolCalls.Add(functionCall);
                    }
                    else if (
                        content is TextContent textContent
                        && !string.IsNullOrEmpty(textContent.Text)
                    )
                    {
                        // Stream text content immediately
                        streamedText.Append(textContent.Text);

                        yield return new UnifiedStreamingChunk
                        {
                            Type = StreamingChunkType.Content,
                            Content = textContent.Text,
                        };
                    }
                }

                // Also check the Text property directly (some providers use this)
                if (!string.IsNullOrEmpty(update.Text) && update.Contents.Count == 0)
                {
                    streamedText.Append(update.Text);

                    yield return new UnifiedStreamingChunk
                    {
                        Type = StreamingChunkType.Content,
                        Content = update.Text,
                    };
                }
            }

            _logger.LogDebug(
                "📨 Stream complete: {TextLength} chars, {ToolCount} tool calls",
                streamedText.Length,
                toolCalls.Count
            );

            // If there were tool calls, process them
            if (toolCalls.Count > 0)
            {
                _logger.LogInformation("🔧 Processing {Count} tool calls", toolCalls.Count);

                // Build assistant message with tool calls for history
                var assistantContents = toolCalls.Cast<AIContent>().ToList();
                var assistantMessage = new ChatMessage(ChatRole.Assistant, assistantContents);

                // Process all tool calls
                var toolResults = new List<FunctionResultContent>();
                var delegationChunks = new List<UnifiedStreamingChunk>();

                foreach (var toolCall in toolCalls)
                {
                    var agentName = ExtractAgentNameFromTool(toolCall.Name);

                    // Emit delegation event
                    if (!string.IsNullOrEmpty(agentName) && !agentsUsed.Contains(agentName))
                    {
                        agentsUsed.Add(agentName);
                        yield return new UnifiedStreamingChunk
                        {
                            Type = StreamingChunkType.SubAgentDelegation,
                            SubAgentName = agentName,
                            Content = enableThinking ? $"Consulting {agentName}...\n" : null,
                        };
                    }

                    // Execute the tool
                    string result;
                    try
                    {
                        result = await ExecuteSubAgentToolAsync(
                            toolCall,
                            conversationId,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Tool execution failed: {Tool}", toolCall.Name);
                        result = $"Error: {ex.Message}";
                    }

                    _logger.LogInformation(
                        "  ✅ {Tool} result: {Preview}",
                        toolCall.Name,
                        result.Length > 100 ? result[..100] + "..." : result
                    );

                    toolResults.Add(
                        new FunctionResultContent(toolCall.CallId ?? toolCall.Name, result)
                    );

                    // Emit completion event
                    if (!string.IsNullOrEmpty(agentName))
                    {
                        yield return new UnifiedStreamingChunk
                        {
                            Type = StreamingChunkType.SubAgentComplete,
                            SubAgentName = agentName,
                            Content = enableThinking ? $"✓ {agentName} completed\n" : null,
                        };
                    }
                }

                // Add to history atomically
                history.Messages.Add(assistantMessage);
                history.Messages.Add(
                    new ChatMessage(ChatRole.Tool, toolResults.Cast<AIContent>().ToList())
                );

                // Continue to next iteration
                continue;
            }

            // No tool calls - we're done
            if (streamedText.Length > 0)
            {
                history.Messages.Add(new ChatMessage(ChatRole.Assistant, streamedText.ToString()));
            }

            _logger.LogInformation(
                "✅ Swarm COMPLETE: {Iterations} iterations, agents: [{Agents}]",
                iterationCount,
                string.Join(", ", agentsUsed)
            );

            yield break;
        }

        // Max iterations reached
        _logger.LogWarning("⚠️ Swarm hit max iterations ({Max})", maxIterations);
        yield return new UnifiedStreamingChunk
        {
            Type = StreamingChunkType.Content,
            Content = "I apologize, but I couldn't complete your request. Please try again.",
        };
    }

    /// <summary>
    /// Build system prompt for Swarm execution with context from intent classification
    /// </summary>
    private string BuildSwarmSystemPrompt(UserIntent intent)
    {
        var agentDescriptions = string.Join(
            "\n",
            _subAgents.Select(a =>
                $"- ask_{a.Name.ToLowerInvariant()}: {a.Domain}. Capabilities: {string.Join(", ", a.Capabilities)}"
            )
        );

        // Include extracted entities for context
        var entityContext =
            intent.Entities.Count > 0
                ? $"\n\nEXTRACTED INFORMATION FROM CURRENT MESSAGE:\n{string.Join("\n", intent.Entities.Select(e => $"- {e.EntityType}: {e.NormalizedValue ?? e.RawValue}"))}"
                : "";

        return $"""
            You are a Master Investment Banking Assistant coordinating specialized agents.

            AVAILABLE SPECIALIST AGENTS (call as tools when needed):
            {agentDescriptions}

            CRITICAL ROUTING RULES - ALWAYS FOLLOW:

            **Profit Projections & Growth Calculations → ask_profitprojection**
            When user asks about ANY of these, you MUST call ask_profitprojection:
            - "project growth", "calculate returns", "estimate profit"
            - "how much will I earn", "what will my investment grow to"
            - Any mention of: amount + time horizon + (optional) risk profile
            - "200000 SAR for 10 years" → MUST call ask_profitprojection
            - Keywords: project, projection, growth, calculate, estimate, earn, return, profit

            **Fund Recommendations & Analysis → ask_investmentadvisor**
            - "find the best fund", "recommend a fund", "which fund should I invest in"
            - Fund comparisons and suitability analysis

            **COMPOSITE REQUESTS - CHAIN MULTIPLE AGENTS:**
            When a request has MULTIPLE parts, call MULTIPLE agents in sequence!

            Example: "find the best fund and project growth for 200000 SAR over 10 years"
            1. FIRST call ask_investmentadvisor to get fund recommendation
            2. THEN call ask_profitprojection with the amount (200000), duration (10 years), risk profile

            Example: "show my portfolio and calculate projections"
            1. FIRST call ask_portfoliomanager to get portfolio
            2. THEN call ask_profitprojection for projections

            NEVER try to calculate projections yourself. ALWAYS delegate to ask_profitprojection.
            NEVER skip the second part of a composite request.

            **Other Routing:**
            - Portfolio questions → ask_portfoliomanager
            - Account operations/balances → ask_accountservices
            - Compliance/risk assessment → ask_complianceofficer
            - External APIs (SNB Capital, mutual funds) → ask_externalapiservices
            - CIF lookups / customer data → ask_externalapiservices
            - Web search / current information → SearchWeb

            MULTI-TURN CONVERSATION HANDLING:
            You are in a MULTI-TURN conversation. The chat history above shows previous exchanges.

            When the user sends a SHORT message like:
            - "ACC001" → This is an ACCOUNT ID they're providing (use it with AccountServices)
            - "100000" or "$100,000" → This is an AMOUNT they're providing
            - "yes" or "no" → This is a CONFIRMATION to your previous question
            - "conservative" or "aggressive" → This is a RISK PROFILE
            - Any code/ID pattern → This is likely an answer to something you asked

            ALWAYS look at the PREVIOUS assistant message to understand what you asked for.
            {entityContext}

            RESPONSE FORMAT:
            - Be clear and professional
            - Provide actionable information
            - For financial data, include relevant numbers and details
            - Guide users on next steps when appropriate
            """;
    }

    /// <summary>
    /// Build tools from available sub-agents
    /// </summary>
    private List<AITool> BuildSubAgentTools(string conversationId)
    {
        var tools = new List<AITool>();

        foreach (var agent in _subAgents)
        {
            var capturedAgent = agent;
            var capturedConversationId = conversationId;

            async Task<string> InvokeAgent(string request)
            {
                _logger.LogInformation(
                    "Sub-agent tool invoked: {Agent}, Request: {Request}",
                    capturedAgent.Name,
                    request.Length > 100 ? request[..100] + "..." : request
                );

                try
                {
                    var response = await capturedAgent.HandleRequestAsync(
                        request,
                        capturedConversationId
                    );
                    return response.Result ?? "No response from agent";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sub-agent {Agent} error", capturedAgent.Name);
                    return $"Error from {capturedAgent.Name}: {ex.Message}";
                }
            }

            var toolName = $"ask_{agent.Name.ToLowerInvariant().Replace(" ", "_")}";
            var toolDescription =
                $"{agent.Domain}. Use for: {string.Join(", ", agent.Capabilities)}";

            tools.Add(AIFunctionFactory.Create(InvokeAgent, toolName, toolDescription));
        }

        return tools;
    }

    /// <summary>
    /// Execute a sub-agent tool call
    /// </summary>
    private async Task<string> ExecuteSubAgentToolAsync(
        FunctionCallContent toolCall,
        string conversationId,
        CancellationToken cancellationToken
    )
    {
        var toolName = toolCall.Name;

        _logger.LogDebug("Executing sub-agent tool: {Tool}", toolName);

        try
        {
            if (toolName.StartsWith("ask_"))
            {
                var agentKey = toolName[4..].Replace("_", "");

                // Find the agent (case-insensitive, handle spaces/underscores)
                var agent = _subAgents.FirstOrDefault(a =>
                    a.Name.Replace(" ", "").Equals(agentKey, StringComparison.OrdinalIgnoreCase)
                );

                if (agent != null)
                {
                    var request = ExtractRequestFromToolArgs(toolCall.Arguments);
                    var response = await agent.HandleRequestAsync(request, conversationId);
                    return response.Result ?? "No response";
                }
            }

            return $"Unknown tool: {toolName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution error: {Tool}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Extract agent name from tool name (ask_portfoliomanager -> PortfolioManager)
    /// </summary>
    private static string ExtractAgentNameFromTool(string toolName)
    {
        if (!toolName.StartsWith("ask_"))
            return string.Empty;

        var namePart = toolName[4..];
        return string.Concat(
            namePart
                .Split('_')
                .Select(s => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..])
        );
    }

    /// <summary>
    /// Extract request string from tool arguments
    /// </summary>
    private static string ExtractRequestFromToolArgs(IDictionary<string, object?>? args)
    {
        if (args == null)
            return "Process request";

        // Try common parameter names
        foreach (var key in new[] { "request", "task", "query", "message" })
        {
            if (args.TryGetValue(key, out var value) && value != null)
            {
                if (value is string s)
                    return s;
                if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
                    return je.GetString() ?? "Process request";
            }
        }

        // Try first string argument
        foreach (var value in args.Values)
        {
            if (value is string s && !string.IsNullOrEmpty(s))
                return s;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
                return je.GetString() ?? "Process request";
        }

        return "Process request";
    }

    /// <summary>
    /// Get or create Swarm conversation history
    /// </summary>
    private static SwarmConversationHistory GetOrCreateSwarmHistory(string conversationId)
    {
        lock (SwarmHistoryLock)
        {
            if (!SwarmHistories.TryGetValue(conversationId, out var history))
            {
                history = new SwarmConversationHistory { ConversationId = conversationId };
                SwarmHistories[conversationId] = history;
            }
            return history;
        }
    }

    /// <summary>
    /// Clear Swarm conversation history (for testing or reset)
    /// </summary>
    public static void ClearSwarmHistory(string conversationId)
    {
        lock (SwarmHistoryLock)
        {
            SwarmHistories.Remove(conversationId);
        }
    }

    /// <summary>
    /// Sanitize conversation history to remove incomplete tool call sequences.
    /// OpenAI requires that every assistant message with tool_calls must be followed
    /// by tool response messages for ALL tool_call_ids.
    /// </summary>
    private void SanitizeConversationHistory(SwarmConversationHistory history)
    {
        if (history.Messages.Count == 0)
            return;

        var sanitizedMessages = new List<ChatMessage>();
        var i = 0;

        while (i < history.Messages.Count)
        {
            var message = history.Messages[i];

            // Check if this is an assistant message with tool calls
            var toolCalls = message.Contents.OfType<FunctionCallContent>().ToList();

            if (message.Role == ChatRole.Assistant && toolCalls.Count > 0)
            {
                // Get all required tool call IDs
                var requiredCallIds = toolCalls.Select(tc => tc.CallId ?? tc.Name).ToHashSet();

                // Look for the next message which should be a Tool message with results
                if (i + 1 < history.Messages.Count)
                {
                    var nextMessage = history.Messages[i + 1];
                    if (nextMessage.Role == ChatRole.Tool)
                    {
                        // Check if it has results for ALL tool calls
                        var resultIds = nextMessage
                            .Contents.OfType<FunctionResultContent>()
                            .Select(r => r.CallId)
                            .ToHashSet();

                        if (requiredCallIds.All(id => resultIds.Contains(id)))
                        {
                            // Complete sequence - keep both
                            sanitizedMessages.Add(message);
                            sanitizedMessages.Add(nextMessage);
                            i += 2;
                            continue;
                        }
                    }
                }

                // Incomplete tool call sequence - skip this assistant message
                _logger.LogWarning(
                    "🧹 Removing incomplete tool call sequence from history (missing results for: {CallIds})",
                    string.Join(", ", requiredCallIds)
                );
                i++;
                continue;
            }

            // Regular message - keep it
            sanitizedMessages.Add(message);
            i++;
        }

        // Replace history with sanitized version
        if (sanitizedMessages.Count != history.Messages.Count)
        {
            _logger.LogInformation(
                "🧹 Sanitized history: {Before} -> {After} messages",
                history.Messages.Count,
                sanitizedMessages.Count
            );
            history.Messages.Clear();
            history.Messages.AddRange(sanitizedMessages);
        }
    }
}

/// <summary>
/// Tracks conversation history for Swarm pattern execution
/// </summary>
public class SwarmConversationHistory
{
    public required string ConversationId { get; init; }
    public List<ChatMessage> Messages { get; } = [];
}

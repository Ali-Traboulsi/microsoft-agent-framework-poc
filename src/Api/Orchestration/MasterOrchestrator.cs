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

    public MasterOrchestrator(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        WebSearchTools webSearchTools,
        ILogger<MasterOrchestrator> logger
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _webSearchTools = webSearchTools;
        _logger = logger;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
        _masterAgent = new Lazy<AIAgent>(CreateMasterAgentWithMiddleware);
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
   - Determine if current/real-time information from the web is needed

2. **Think Out Loud (CRITICAL)**
   Before taking action, you MUST explain your reasoning:
   
   🤔 **My Analysis:**
   - **User Request:** [What is the user asking for?]
   - **Domain(s) Needed:** [Which area(s) does this involve?]
   - **Selected Sub-Agent(s):** [Which specialist(s) should handle this?]
   - **Approach:** [Will I delegate to one agent or coordinate multiple?]
   - **Web Search Needed:** [Does this require current information from the internet?]
   
   This makes your decision-making transparent to users.

3. **Use Web Search When Needed**
   - For questions requiring current/real-time information (news, current events, latest data)
   - For topics outside financial services domain
   - For market updates, economic news, or current trends
   - When users explicitly ask for online/web information
   
4. **Delegate Intelligently**
   - Use DelegateToSubAgent for single-domain requests
   - Use DelegateToMultipleSubAgents for cross-domain requests
   - Use SearchWeb for current information needs
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
- **Current/real-time information** → SearchWeb (web search tool)
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
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequestStructured",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("message.length", userMessage.Length);
        activity?.SetTag("response.format", "structured_json");

        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing structured request for conversation {ConversationId}: {Message}",
                conversationId,
                userMessage
            );

            // Create a specialized agent that returns JSON-only responses
            var structuredAgent = _chatClient.CreateAIAgent(
                name: "StructuredMasterAgent",
                instructions: $@"You are a Master Agent that coordinates sub-agents and returns ONLY valid JSON responses.

**Available Sub-Agents:**
{string.Join("\n", _subAgents.Select(sa => $"- **{sa.Name}**: {sa.Domain}"))}

**CRITICAL: JSON Format Rules**
1. Return ONLY valid JSON (no markdown, no code blocks, no text before/after)
2. Use null for any unused fields
3. All numbers must be actual numbers, not strings (e.g., 75000 not ""75000"")
4. Empty arrays should be [] not null
5. referenceLinks MUST be an array of objects, not strings
6. Follow this EXACT structure:

{{
  ""summary"": ""your summary here"",
  ""referenceLinks"": [
    {{
      ""title"": ""Investment Advice Guide"",
      ""url"": ""https://example.com/guide"",
      ""source"": ""Web Search"",
      ""snippet"": ""Brief description""
    }}
  ],
  ""portfolios"": [
    {{
      ""portfolioId"": ""PORT001"",
      ""portfolioName"": ""Portfolio Name"",
      ""totalValue"": 75000,
      ""holdings"": [
        {{
          ""fundSymbol"": ""GTGF"",
          ""fundName"": ""Fund Name"",
          ""shares"": 100,
          ""currentValue"": 50000,
          ""percentOfPortfolio"": 66.7
        }}
      ],
      ""performance"": {{
        ""totalReturn"": 5000,
        ""returnPercentage"": 7.5,
        ""period"": ""YTD""
      }}
    }}
  ],
  ""accounts"": null,
  ""fundRecommendations"": null,
  ""complianceAlerts"": null,
  ""keyMetrics"": {{
    ""totalValue"": 75000,
    ""count"": 1
  }},
  ""suggestedActions"": [""Action 1"", ""Action 2""],
  ""subAgentsUsed"": [""PortfolioManager""],
  ""webSearchesExecuted"": [""best investment advice""]
}}

**Process:**
1. Analyze the user's request
2. Use DelegateToSubAgent to get data (e.g., for portfolios, delegate to PortfolioManager)
3. If web search is needed, use SearchWeb tool
4. Parse the sub-agent's response to extract data
5. Format into the JSON structure above
6. Return ONLY the JSON object

**Important Rules:**
- When you get portfolio data from PortfolioManager, extract the actual values
- Convert any text data into the proper JSON types (numbers as numbers, not strings)
- If holdings list is empty, use []
- Track which sub-agents you used in the subAgentsUsed array
- **CRITICAL**: referenceLinks must be an array of objects with title, url, source, snippet properties
  - WRONG: ""referenceLinks"": [""https://example.com""]
  - RIGHT: ""referenceLinks"": [{{""title"": ""Example"", ""url"": ""https://example.com"", ""source"": ""Web Search"", ""snippet"": ""Description""}}]
- When SearchWeb returns results, convert them to proper referenceLink objects
- Use empty array [] not null for referenceLinks if no web sources
- **CRITICAL**: webSearchesExecuted must be an array of strings (the queries you searched)
  - WRONG: ""webSearchesExecuted"": ""best investment advice""
  - RIGHT: ""webSearchesExecuted"": [""best investment advice""]
  - Use null if no web searches were performed
- **CRITICAL**: subAgentsUsed must be an array of strings
  - WRONG: ""subAgentsUsed"": ""PortfolioManager""
  - RIGHT: ""subAgentsUsed"": [""PortfolioManager""]",
                tools:
                [
                    AIFunctionFactory.Create(DelegateToSubAgent),
                    AIFunctionFactory.Create(DelegateToMultipleSubAgents),
                    AIFunctionFactory.Create(_webSearchTools.SearchWeb),
                ]
            );

            // Run the agent
            var result = await structuredAgent.RunAsync(userMessage);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "{}";

            // Extract JSON from response (remove markdown code blocks if present)
            var jsonResponse = responseText.Trim();
            if (jsonResponse.StartsWith("```"))
            {
                var lines = jsonResponse.Split('\n');
                jsonResponse = string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
            }

            // Log the raw JSON for debugging
            _logger.LogInformation(
                "Raw JSON response (first 500 chars): {Json}",
                jsonResponse.Length > 500 ? jsonResponse.Substring(0, 500) + "..." : jsonResponse
            );

            // Try to fix common JSON issues before parsing
            try
            {
                // Parse as dynamic first to check structure
                using var jsonDoc = JsonDocument.Parse(jsonResponse);
                var root = jsonDoc.RootElement;

                // Check if webSearchesExecuted is a string instead of array
                if (
                    root.TryGetProperty("webSearchesExecuted", out var webSearchProp)
                    && webSearchProp.ValueKind == JsonValueKind.String
                )
                {
                    var webSearchValue = webSearchProp.GetString();
                    jsonResponse = jsonResponse.Replace(
                        $"\"webSearchesExecuted\": \"{webSearchValue}\"",
                        $"\"webSearchesExecuted\": [\"{webSearchValue}\"]"
                    );
                    _logger.LogWarning(
                        "Fixed webSearchesExecuted from string to array: {Value}",
                        webSearchValue
                    );
                }

                // Check if subAgentsUsed is a string instead of array
                if (
                    root.TryGetProperty("subAgentsUsed", out var subAgentsProp)
                    && subAgentsProp.ValueKind == JsonValueKind.String
                )
                {
                    var subAgentsValue = subAgentsProp.GetString();
                    jsonResponse = jsonResponse.Replace(
                        $"\"subAgentsUsed\": \"{subAgentsValue}\"",
                        $"\"subAgentsUsed\": [\"{subAgentsValue}\"]"
                    );
                    _logger.LogWarning(
                        "Fixed subAgentsUsed from string to array: {Value}",
                        subAgentsValue
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not pre-process JSON, will attempt direct parse");
            }

            // Parse the JSON response with more lenient options
            var structuredResponse = JsonSerializer.Deserialize<StructuredAgentResponse>(
                jsonResponse,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System
                        .Text
                        .Json
                        .Serialization
                        .JsonNumberHandling
                        .AllowReadingFromString,
                    DefaultIgnoreCondition = System
                        .Text
                        .Json
                        .Serialization
                        .JsonIgnoreCondition
                        .WhenWritingNull,
                }
            );

            if (structuredResponse == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize response. JSON: {jsonResponse}"
                );
            }

            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.size_bytes", jsonResponse.Length);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation(
                "Structured request completed in {Duration}ms",
                sw.ElapsedMilliseconds
            );

            return new StructuredOrchestratorResult
            {
                Success = true,
                StructuredResponse = structuredResponse,
                SubAgentsUsed = structuredResponse.SubAgentsUsed,
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
                "Error processing structured request for conversation {ConversationId}: {Error}",
                conversationId,
                ex.Message
            );

            return new StructuredOrchestratorResult
            {
                Success = false,
                StructuredResponse = new StructuredAgentResponse
                {
                    Summary = $"An error occurred: {ex.Message}",
                    ReferenceLinks = new List<ReferenceLink>(),
                    SubAgentsUsed = new List<string>(),
                },
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
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

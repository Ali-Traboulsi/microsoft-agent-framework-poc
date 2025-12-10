using System.Diagnostics;
using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Domain.Reasoning;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Services.Intelligence;

/// <summary>
/// Chain-of-Thought reasoning engine that generates structured reasoning
/// for complex decisions. Uses ReAct-style (Reason + Act) pattern.
/// </summary>
public class ReasoningEngine(IChatClient chatClient, ILogger<ReasoningEngine> logger)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Intelligence.ReasoningEngine",
        "1.0.0"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string ReasoningPrompt = """
        You are a reasoning engine for an investment banking AI assistant.
        Your job is to think step-by-step about a user's request and determine the best action.

        ## Available Sub-Agents
        - **PortfolioManager**: Portfolio creation, analysis, holdings, rebalancing
        - **InvestmentAdvisor**: Fund recommendations, investment advice, comparisons
        - **AccountServices**: Account balance, deposits, transfers, transaction history
        - **ComplianceOfficer**: Risk assessment, regulatory compliance, verification
        - **ProfitProjection**: Investment projections, returns calculation, scenarios
        - **ExternalApiServices**: SNB Capital API, mutual funds, CIF lookups, fund-in operations

        ## Your Task
        Think through the request step-by-step using this format:

        1. **OBSERVATION**: What is the user asking for? What are the key elements?
        2. **ENTITY EXTRACTION**: What specific values/identifiers are mentioned? (amounts, CIFs, account numbers, etc.)
        3. **INTENT ANALYSIS**: What is the user's underlying goal? Are there implicit needs?
        4. **CONSTRAINT CHECK**: Are there any compliance/risk concerns? Any missing required info?
        5. **DELEGATION REASONING**: Which sub-agent(s) can handle this? Why?
        6. **DECISION**: What action should be taken? What confidence level?

        ## Response Format
        Respond with a JSON object containing your reasoning chain:
        {
            "observation": "Clear description of what the user wants",
            "entities": [
                {"type": "customer_id", "value": "100000000005", "confidence": 0.95}
            ],
            "intentAnalysis": "Analysis of the user's underlying intent",
            "constraints": ["Any constraints or concerns identified"],
            "delegationReasoning": "Why this sub-agent is the best choice",
            "decision": {
                "action": "delegate_to_subagent",
                "subAgents": ["ExternalApiServices"],
                "refinedRequest": "Clear, specific request for the sub-agent",
                "confidence": 0.9,
                "reasoning": "Brief explanation of the decision"
            },
            "requiresConfirmation": false,
            "confirmationReason": null,
            "identifiedRisks": []
        }

        ## Examples

        ### Example 1: CIF Lookup
        User: "give me account details for CIF 100000000005"
        {
            "observation": "User wants customer account data for a specific CIF number",
            "entities": [{"type": "customer_id", "value": "100000000005", "confidence": 1.0}],
            "intentAnalysis": "User needs to view customer information, likely for account review or verification",
            "constraints": [],
            "delegationReasoning": "ExternalApiServices has access to SNB Capital API which can retrieve customer data by CIF",
            "decision": {
                "action": "delegate_to_subagent",
                "subAgents": ["ExternalApiServices"],
                "refinedRequest": "Get complete customer data for CIF 100000000005",
                "confidence": 0.95,
                "reasoning": "CIF lookup requires ExternalApiServices which has GetCompleteCustomerData tool"
            },
            "requiresConfirmation": false,
            "identifiedRisks": []
        }

        ### Example 2: High-Value Transfer (needs confirmation)
        User: "transfer 500000 SAR to my portfolio"
        {
            "observation": "User wants to transfer a large sum to their investment portfolio",
            "entities": [{"type": "amount", "value": 500000, "confidence": 1.0}, {"type": "currency", "value": "SAR", "confidence": 1.0}],
            "intentAnalysis": "User wants to fund their portfolio, likely for investment purposes",
            "constraints": ["Large amount may require additional verification", "Missing source account and target portfolio"],
            "delegationReasoning": "ExternalApiServices handles fund-in operations, but missing required parameters",
            "decision": {
                "action": "request_clarification",
                "subAgents": ["ExternalApiServices"],
                "refinedRequest": "Need source account number and target portfolio number to proceed with 500,000 SAR transfer",
                "confidence": 0.7,
                "reasoning": "Missing required parameters for fund-in; also high value warrants confirmation"
            },
            "requiresConfirmation": true,
            "confirmationReason": "Large transfer amount (500,000 SAR) - please confirm source account and target portfolio",
            "identifiedRisks": ["High value transaction", "Missing source/target details"]
        }

        Think carefully and respond with valid JSON only:
        """;

    /// <summary>
    /// Generate a chain-of-thought reasoning for the given request
    /// </summary>
    public async Task<ThoughtChain> ReasonAsync(
        string userMessage,
        UserIntent classifiedIntent,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("ReasoningEngine.Reason");
        var sw = Stopwatch.StartNew();

        var thoughtChain = new ThoughtChain
        {
            ConversationId = context?.ConversationId ?? "unknown",
            UserRequest = userMessage,
        };

        try
        {
            logger.LogInformation(
                "Starting chain-of-thought reasoning for: {Message}",
                userMessage.Length > 100 ? userMessage[..100] + "..." : userMessage
            );

            // Step 1: Observation
            var observationStep = new ReasoningStep
            {
                Type = ReasoningStepType.Observation,
                Thought = $"User request: \"{userMessage}\"",
                Action = "Analyze request structure and content",
                Confidence = 1.0,
            };
            thoughtChain.AddStep(observationStep);

            // Step 2: Use classified intent as hypothesis
            var hypothesisStep = new ReasoningStep
            {
                Type = ReasoningStepType.Hypothesis,
                Thought =
                    $"Classified as {classifiedIntent.PrimaryIntent} with {classifiedIntent.Confidence:P0} confidence",
                Action = classifiedIntent.IntentSummary,
                Confidence = classifiedIntent.Confidence,
                Evidence = classifiedIntent.RequiredSubAgents,
            };
            thoughtChain.AddStep(hypothesisStep);

            // Step 3: Entity extraction (from classified intent)
            if (classifiedIntent.Entities.Count > 0)
            {
                var entityDescriptions = classifiedIntent
                    .Entities.Select(e => $"{e.EntityType}={e.RawValue}")
                    .ToList();

                var entityStep = new ReasoningStep
                {
                    Type = ReasoningStepType.EntityExtraction,
                    Thought =
                        $"Extracted {classifiedIntent.Entities.Count} entities: {string.Join(", ", entityDescriptions)}",
                    Action = "Validate and normalize entity values",
                    Confidence = 0.95,
                    Evidence = entityDescriptions,
                };
                thoughtChain.AddStep(entityStep);
            }

            // Step 4: For complex cases or low confidence, use LLM reasoning
            if (classifiedIntent.Confidence < 0.8 || classifiedIntent.IsComposite)
            {
                var llmReasoning = await PerformLLMReasoningAsync(
                    userMessage,
                    classifiedIntent,
                    context,
                    cancellationToken
                );

                if (llmReasoning != null)
                {
                    // Add LLM reasoning steps
                    foreach (var step in llmReasoning.Steps)
                    {
                        thoughtChain.AddStep(step);
                    }

                    if (llmReasoning.FinalDecision != null)
                    {
                        thoughtChain.FinalDecision = llmReasoning.FinalDecision;
                    }
                }
            }

            // Step 5: Delegation reasoning
            if (classifiedIntent.RequiredSubAgents.Count > 0)
            {
                var delegationStep = new ReasoningStep
                {
                    Type = ReasoningStepType.DelegationReasoning,
                    Thought =
                        $"Request requires expertise from: {string.Join(", ", classifiedIntent.RequiredSubAgents)}",
                    Action = $"Delegate to {classifiedIntent.RequiredSubAgents.First()}",
                    Confidence = classifiedIntent.Confidence,
                    Evidence = classifiedIntent.RequiredSubAgents,
                };
                thoughtChain.AddStep(delegationStep);
            }

            // Step 6: Final decision
            thoughtChain.FinalDecision ??= new ReasoningDecision
            {
                Action = DetermineAction(classifiedIntent),
                DelegateToSubAgents = classifiedIntent.RequiredSubAgents,
                RefinedRequest = classifiedIntent.RefinedRequest,
                Confidence = classifiedIntent.Confidence,
                Reasoning = $"Based on {classifiedIntent.PrimaryIntent} intent classification",
                RequiresConfirmation = ShouldRequireConfirmation(classifiedIntent),
                ConfirmationReason = GetConfirmationReason(classifiedIntent),
            };

            var conclusionStep = new ReasoningStep
            {
                Type = ReasoningStepType.Conclusion,
                Thought = thoughtChain.FinalDecision.Reasoning,
                Action = thoughtChain.FinalDecision.Action,
                Confidence = thoughtChain.FinalDecision.Confidence,
            };
            thoughtChain.AddStep(conclusionStep);

            sw.Stop();
            thoughtChain.TotalDurationMs = sw.ElapsedMilliseconds;
            thoughtChain.CompletedAt = DateTime.UtcNow;

            logger.LogInformation(
                "Completed reasoning in {Duration}ms with {StepCount} steps. Decision: {Decision}",
                sw.ElapsedMilliseconds,
                thoughtChain.Steps.Count,
                thoughtChain.FinalDecision.Action
            );

            activity?.SetTag("reasoning.steps", thoughtChain.Steps.Count);
            activity?.SetTag("reasoning.decision", thoughtChain.FinalDecision.Action);
            activity?.SetTag("reasoning.confidence", thoughtChain.FinalDecision.Confidence);

            return thoughtChain;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during chain-of-thought reasoning");

            // Return basic thought chain with error
            thoughtChain.FinalDecision = new ReasoningDecision
            {
                Action = "fallback_to_master_agent",
                Confidence = 0.5,
                Reasoning = $"Reasoning error: {ex.Message}",
            };

            return thoughtChain;
        }
    }

    /// <summary>
    /// Perform detailed LLM-based reasoning for complex cases
    /// </summary>
    private async Task<ThoughtChain?> PerformLLMReasoningAsync(
        string userMessage,
        UserIntent classifiedIntent,
        ConversationContext? context,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var contextInfo =
                context != null ? $"\n\nPrior context: {context.GenerateSummary()}" : "";

            var intentInfo =
                $"\n\nInitial classification: {classifiedIntent.PrimaryIntent} ({classifiedIntent.Confidence:P0})";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, ReasoningPrompt),
                new(ChatRole.User, $"User Message: {userMessage}{contextInfo}{intentInfo}"),
            };

            var options = new ChatOptions { Temperature = 0.3f };

            var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);
            var responseText = response.Text ?? "{}";

            // Try to parse as JSON
            var reasoningResult = JsonSerializer.Deserialize<LLMReasoningResult>(
                responseText,
                JsonOptions
            );

            if (reasoningResult == null)
                return null;

            var chain = new ThoughtChain
            {
                ConversationId = context?.ConversationId ?? "unknown",
                UserRequest = userMessage,
            };

            // Convert LLM result to reasoning steps
            if (!string.IsNullOrEmpty(reasoningResult.Observation))
            {
                chain.AddStep(
                    new ReasoningStep
                    {
                        Type = ReasoningStepType.Observation,
                        Thought = reasoningResult.Observation,
                        Confidence = 0.9,
                    }
                );
            }

            if (!string.IsNullOrEmpty(reasoningResult.IntentAnalysis))
            {
                chain.AddStep(
                    new ReasoningStep
                    {
                        Type = ReasoningStepType.Analysis,
                        Thought = reasoningResult.IntentAnalysis,
                        Confidence = 0.85,
                    }
                );
            }

            if (reasoningResult.Constraints?.Count > 0)
            {
                chain.AddStep(
                    new ReasoningStep
                    {
                        Type = ReasoningStepType.ConstraintCheck,
                        Thought =
                            $"Identified constraints: {string.Join("; ", reasoningResult.Constraints)}",
                        Evidence = reasoningResult.Constraints,
                        Confidence = 0.9,
                    }
                );
            }

            if (!string.IsNullOrEmpty(reasoningResult.DelegationReasoning))
            {
                chain.AddStep(
                    new ReasoningStep
                    {
                        Type = ReasoningStepType.DelegationReasoning,
                        Thought = reasoningResult.DelegationReasoning,
                        Confidence = 0.85,
                    }
                );
            }

            // Extract decision
            if (reasoningResult.Decision != null)
            {
                chain.FinalDecision = new ReasoningDecision
                {
                    Action = reasoningResult.Decision.Action ?? "delegate_to_subagent",
                    DelegateToSubAgents = reasoningResult.Decision.SubAgents ?? [],
                    RefinedRequest = reasoningResult.Decision.RefinedRequest,
                    Confidence = reasoningResult.Decision.Confidence,
                    Reasoning = reasoningResult.Decision.Reasoning ?? "LLM reasoning",
                    RequiresConfirmation = reasoningResult.RequiresConfirmation,
                    ConfirmationReason = reasoningResult.ConfirmationReason,
                    IdentifiedRisks = reasoningResult.IdentifiedRisks ?? [],
                };
            }

            return chain;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM reasoning failed, using heuristic reasoning");
            return null;
        }
    }

    private static string DetermineAction(UserIntent intent)
    {
        return intent.Strategy switch
        {
            ExecutionStrategy.DirectResponse => "respond_directly",
            ExecutionStrategy.NeedsClarification => "request_clarification",
            ExecutionStrategy.SingleAgent => "delegate_to_subagent",
            ExecutionStrategy.Sequential => "delegate_sequential",
            ExecutionStrategy.Parallel => "delegate_parallel",
            ExecutionStrategy.Workflow => "execute_workflow",
            _ => "delegate_to_subagent",
        };
    }

    private static bool ShouldRequireConfirmation(UserIntent intent)
    {
        // High-value transactions
        var amount = intent.GetEntityValue<double>("amount", 0);
        if (amount >= 100000)
            return true;

        // Fund transfers
        if (intent.PrimaryIntent == IntentType.FundInOperation && amount > 50000)
            return true;

        // Low confidence decisions
        if (intent.Confidence < 0.6)
            return true;

        return false;
    }

    private static string? GetConfirmationReason(UserIntent intent)
    {
        var amount = intent.GetEntityValue<double>("amount", 0);

        if (amount >= 100000)
            return $"High-value transaction (SAR {amount:N0}) - please confirm details";

        if (intent.Confidence < 0.6)
            return "I want to make sure I understood your request correctly";

        return null;
    }

    /// <summary>
    /// Generate a user-friendly reasoning summary for streaming
    /// </summary>
    public static string GenerateStreamingSummary(ThoughtChain chain)
    {
        var steps = chain.Steps.Take(4); // Limit for streaming
        var lines = new List<string>();

        foreach (var step in steps)
        {
            var emoji = step.Type switch
            {
                ReasoningStepType.Observation => "👀",
                ReasoningStepType.Analysis => "🔍",
                ReasoningStepType.EntityExtraction => "📋",
                ReasoningStepType.DelegationReasoning => "🤝",
                ReasoningStepType.Conclusion => "🎯",
                _ => "•",
            };

            lines.Add($"{emoji} {step.Thought}");
        }

        if (chain.FinalDecision != null)
        {
            lines.Add($"→ **Action**: {chain.FinalDecision.Action}");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Structure for parsing LLM reasoning response
/// </summary>
internal class LLMReasoningResult
{
    public string? Observation { get; set; }
    public List<EntityInfo>? Entities { get; set; }
    public string? IntentAnalysis { get; set; }
    public List<string>? Constraints { get; set; }
    public string? DelegationReasoning { get; set; }
    public DecisionInfo? Decision { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationReason { get; set; }
    public List<string>? IdentifiedRisks { get; set; }

    internal class EntityInfo
    {
        public string? Type { get; set; }
        public object? Value { get; set; }
        public double Confidence { get; set; }
    }

    internal class DecisionInfo
    {
        public string? Action { get; set; }
        public List<string>? SubAgents { get; set; }
        public string? RefinedRequest { get; set; }
        public double Confidence { get; set; }
        public string? Reasoning { get; set; }
    }
}

using System.Runtime.CompilerServices;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Domain.Reasoning;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Helper methods and utilities
/// </summary>
public partial class MasterOrchestrator
{
    [System.ComponentModel.Description(
        "Get information about all available sub-agents and their capabilities"
    )]
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

    /// <summary>
    /// Format reasoning chain as human-readable text.
    /// Converts structured reasoning steps into natural language.
    /// </summary>
    public static string FormatReasoningAsHumanReadable(ThoughtChain thoughtChain)
    {
        var sb = new StringBuilder();

        // Start with what we observed
        var observation = thoughtChain.Steps.FirstOrDefault(s =>
            s.Type == ReasoningStepType.Observation
        );
        if (observation != null)
        {
            sb.AppendLine($"Looking at your request: {observation.Thought}");
        }

        // Add analysis insight
        var analysis = thoughtChain.Steps.FirstOrDefault(s => s.Type == ReasoningStepType.Analysis);
        if (analysis != null)
        {
            sb.AppendLine($"I'm thinking: {analysis.Thought}");
        }

        // Check for constraints or concerns
        var constraints = thoughtChain.Steps.FirstOrDefault(s =>
            s.Type == ReasoningStepType.ConstraintCheck
        );
        if (constraints != null && !string.IsNullOrEmpty(constraints.Thought))
        {
            sb.AppendLine($"I should note: {constraints.Thought}");
        }

        // Add delegation reasoning
        var delegation = thoughtChain.Steps.FirstOrDefault(s =>
            s.Type == ReasoningStepType.DelegationReasoning
        );
        if (delegation != null)
        {
            sb.AppendLine($"My plan: {delegation.Thought}");
        }

        // Final decision
        if (thoughtChain.FinalDecision != null)
        {
            var decision = thoughtChain.FinalDecision;
            if (!string.IsNullOrEmpty(decision.Reasoning))
            {
                sb.AppendLine($"Decision: {decision.Reasoning}");
            }

            // Mention if confirmation is needed
            if (decision.RequiresConfirmation && !string.IsNullOrEmpty(decision.ConfirmationReason))
            {
                sb.AppendLine($"⚠️ {decision.ConfirmationReason}");
            }
        }

        // If we have no structured reasoning, fall back to a simple message
        if (sb.Length == 0)
        {
            sb.Append("Analyzing your request and determining the best approach to help you...");
        }

        return sb.ToString().TrimEnd();
    }

    /// Stream multi-modal response
    /// </summary>
    public async IAsyncEnumerable<UnifiedStreamingChunk> StreamMultiModalAsync(
        List<AIContent> contents,
        string conversationId,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);
        var chatMessage = new ChatMessage(ChatRole.User, contents);

        await foreach (var update in _masterAgent.Value.RunStreamingAsync(chatMessage, thread))
        {
            if (update.Contents is { Count: > 0 })
            {
                foreach (var contentItem in update.Contents)
                {
                    if (contentItem is TextContent textContent)
                    {
                        yield return new UnifiedStreamingChunk
                        {
                            Type = StreamingChunkType.Content,
                            Content = textContent.Text,
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restore prior messages to context (for conversation continuity)
    /// </summary>
    public async Task RestorePriorMessagesAsync(
        string conversationId,
        IEnumerable<ConversationMessage> priorMessages
    )
    {
        foreach (var msg in priorMessages)
        {
            if (msg.Role == "assistant" && !string.IsNullOrEmpty(msg.SubAgentName))
            {
                await _contextStore.AddSubAgentFindingAsync(
                    conversationId,
                    new SubAgentFinding
                    {
                        SubAgentName = msg.SubAgentName,
                        Summary =
                            msg.Content.Length > 200 ? msg.Content[..200] + "..." : msg.Content,
                        FoundAt = DateTime.UtcNow,
                    }
                );
            }
        }
    }

    /// <summary>
    /// Extract transcription from multi-modal contents if available
    /// </summary>
    public static string? ExtractTranscriptionFromContents(List<AIContent> contents)
    {
        var textContent = contents.OfType<TextContent>().FirstOrDefault();
        if (
            textContent?.AdditionalProperties?.TryGetValue("transcription", out var transcription)
            == true
        )
        {
            return transcription?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Detect if the response is asking for information and create a pending action.
    /// This enables context-aware follow-up detection in subsequent messages.
    /// </summary>
    public PendingAction? DetectPendingActionFromResponse(
        string responseText,
        UserIntent classifiedIntent
    )
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        var responseLower = responseText.ToLowerInvariant();

        // Patterns that indicate a request for information
        var infoRequestPatterns = new Dictionary<string, (string Description, string EntityType)>
        {
            // Account-related requests
            { "account id", ("Provide account ID", "account_id") },
            { "account number", ("Provide account number", "account_id") },
            { "what is your account", ("Provide account ID", "account_id") },
            { "please provide your account", ("Provide account ID", "account_id") },
            { "which account", ("Specify account", "account_id") },
            // Customer-related requests
            { "cif number", ("Provide CIF number", "customer_id") },
            { "customer id", ("Provide customer ID", "customer_id") },
            { "client id", ("Provide client ID", "customer_id") },
            // Amount-related requests
            { "how much", ("Specify amount", "amount") },
            { "investment amount", ("Specify investment amount", "amount") },
            { "what amount", ("Specify amount", "amount") },
            // Portfolio-related requests
            { "portfolio name", ("Provide portfolio name", "portfolio_name") },
            { "which portfolio", ("Specify portfolio", "portfolio_id") },
            // Fund-related requests
            { "which fund", ("Specify fund", "fund_id") },
            { "fund name", ("Specify fund name", "fund_name") },
            // Confirmation requests
            { "would you like to proceed", ("Confirm to proceed", "confirmation") },
            { "please confirm", ("Confirm action", "confirmation") },
            { "do you want to continue", ("Confirm to continue", "confirmation") },
            { "shall i proceed", ("Confirm to proceed", "confirmation") },
            // Risk profile
            { "risk tolerance", ("Specify risk tolerance", "risk_profile") },
            { "risk profile", ("Specify risk profile", "risk_profile") },
            { "investment style", ("Specify investment style", "risk_profile") },
        };

        // Check each pattern
        foreach (var (pattern, info) in infoRequestPatterns)
        {
            if (responseLower.Contains(pattern))
            {
                // Determine which agent should handle the response
                var requiredAgent = DetermineAgentForEntityType(info.EntityType, classifiedIntent);

                return new PendingAction
                {
                    Description = info.Description,
                    RequiredAgent = requiredAgent,
                    CreatedAt = DateTime.UtcNow,
                    Parameters = new Dictionary<string, object>
                    {
                        ["entityType"] = info.EntityType,
                    },
                };
            }
        }

        // Check for question marks combined with common patterns
        if (responseLower.Contains("?"))
        {
            // Look for general info-seeking patterns
            if (
                responseLower.Contains("what is")
                || responseLower.Contains("could you provide")
                || responseLower.Contains("can you tell me")
                || responseLower.Contains("please provide")
                || responseLower.Contains("i need")
            )
            {
                var requiredAgent =
                    classifiedIntent.RequiredSubAgents.FirstOrDefault() ?? "AccountServices";
                return new PendingAction
                {
                    Description = "Provide requested information",
                    RequiredAgent = requiredAgent,
                    CreatedAt = DateTime.UtcNow,
                    Parameters = new Dictionary<string, object> { ["entityType"] = "general_info" },
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Determine which agent should handle a response based on entity type
    /// </summary>
    public static string DetermineAgentForEntityType(string entityType, UserIntent intent)
    {
        return entityType switch
        {
            "account_id" or "customer_id" => "AccountServices",
            "portfolio_id" or "portfolio_name" => "PortfolioManager",
            "fund_id" or "fund_name" => "InvestmentAdvisor",
            "risk_profile" => "InvestmentAdvisor",
            "confirmation" => intent.RequiredSubAgents.FirstOrDefault() ?? "AccountServices",
            "amount" => intent.RequiredSubAgents.FirstOrDefault() ?? "AccountServices",
            _ => intent.RequiredSubAgents.FirstOrDefault() ?? "AccountServices",
        };
    }

    /// <summary>
    /// Generate human-readable thinking text instead of technical jargon.
    /// Produces natural language like "I understand you want to... I'll help by..."
    /// </summary>
    public static string GenerateHumanReadableThinking(UserIntent intent)
    {
        var sb = new StringBuilder();

        // Start with understanding what the user wants
        var intentDescription = intent.PrimaryIntent switch
        {
            IntentType.PortfolioCreation => "create a new portfolio",
            IntentType.PortfolioAnalysis => "analyze your portfolio",
            IntentType.PortfolioRebalancing => "rebalance your portfolio",
            IntentType.HoldingsInquiry => "check your holdings",
            IntentType.InvestmentAdvice => "get investment advice",
            IntentType.FundRecommendation => "get fund recommendations",
            IntentType.AccountBalance => "check your account balance",
            IntentType.AccountCreation => "create a new account",
            IntentType.FundTransfer => "make a fund transfer",
            IntentType.TransactionHistory => "view your transaction history",
            IntentType.ComplianceCheck => "verify compliance requirements",
            IntentType.RiskAssessment => "assess risk levels",
            IntentType.ProfitProjection => "see profit projections",
            IntentType.MutualFundSearch => "search for mutual funds",
            IntentType.MutualFundDetails => "get mutual fund details",
            IntentType.FundInOperation => "perform a fund-in operation",
            IntentType.CustomerDataLookup => "look up customer data",
            IntentType.SNBCapitalQuery => "query SNB Capital data",
            IntentType.WebSearch => "search for information",
            IntentType.GeneralInquiry => "answer your question",
            IntentType.Greeting => "greet you",
            IntentType.Clarification => "clarify your request",
            IntentType.Confirmation => "confirm your action",
            IntentType.CompleteInvestmentWorkflow => "guide you through the investment process",
            IntentType.ComprehensiveAnalysis => "provide a comprehensive analysis",
            _ => "help you with your request",
        };

        sb.Append($"I understand you want to {intentDescription}. ");

        // Add what we'll do about it
        if (intent.RequiredSubAgents.Count > 0)
        {
            var agentDescriptions = intent.RequiredSubAgents.Select(GetFriendlyAgentDescription);
            if (intent.RequiredSubAgents.Count == 1)
            {
                sb.Append($"I'll {agentDescriptions.First()} to help with this.");
            }
            else
            {
                sb.Append(
                    $"I'll need to {string.Join(" and ", agentDescriptions)} to complete this request."
                );
            }
        }
        else
        {
            sb.Append("Let me look into this for you.");
        }

        // Mention any entities we understood
        if (intent.Entities.Count > 0)
        {
            var entityMentions = intent
                .Entities.Take(3)
                .Select(e => GetFriendlyEntityDescription(e.EntityType, e.RawValue));

            sb.Append($" I noted {string.Join(", ", entityMentions)}.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get a friendly description of what an agent does (action-oriented)
    /// </summary>
    private static string GetFriendlyAgentDescription(string agentName)
    {
        return agentName switch
        {
            "PortfolioManager" => "check your portfolio",
            "InvestmentAdvisor" => "review investment options",
            "AccountServices" => "look up your account details",
            "ComplianceOfficer" => "verify compliance requirements",
            "ProfitProjection" => "calculate projections",
            "ExternalApiServices" => "fetch the latest data",
            _ => $"consult {agentName.ToLower()}",
        };
    }

    /// <summary>
    /// Get a friendly description of an extracted entity
    /// </summary>
    private static string GetFriendlyEntityDescription(string entityType, string value)
    {
        var displayValue = value.Length > 20 ? value[..17] + "..." : value;
        return entityType switch
        {
            "amount" => $"an amount of {displayValue}",
            "account_id" => $"account {displayValue}",
            "customer_id" or "cif" => $"customer ID {displayValue}",
            "portfolio_id" => $"portfolio {displayValue}",
            "fund_id" or "fund_name" => $"the fund {displayValue}",
            "risk_profile" => $"risk preference: {displayValue}",
            "currency" => $"currency {displayValue}",
            "date" => $"date {displayValue}",
            _ => $"{entityType}: {displayValue}",
        };
    }
}

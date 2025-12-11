using System.Diagnostics;
using System.Text.RegularExpressions;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;

namespace AgentFrameworkQuickStart.Services.Intelligence;

/// <summary>
/// Fast pattern-based intent matcher for obvious intents.
/// Runs BEFORE the LLM classifier to speed up common requests.
/// Uses regex patterns and keyword matching for high-confidence cases.
/// </summary>
public partial class FastIntentMatcher(ILogger<FastIntentMatcher> logger)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Intelligence.FastIntentMatcher",
        "1.0.0"
    );

    /// <summary>
    /// Attempt to quickly match intent using patterns (no context).
    /// Returns null if no high-confidence match is found (falls back to LLM).
    /// </summary>
    public FastMatchResult? TryMatch(string message) => TryMatch(message, null);

    /// <summary>
    /// Attempt to quickly match intent using patterns WITH conversation context.
    /// This enables detection of follow-up responses like "ACC001" after being asked for account ID.
    /// Returns null if no high-confidence match is found (falls back to LLM).
    /// </summary>
    public FastMatchResult? TryMatch(string message, ConversationContext? context)
    {
        using var activity = ActivitySource.StartActivity("FastIntentMatcher.TryMatch");
        var sw = Stopwatch.StartNew();

        // Log context state at entry
        logger.LogInformation(
            "FastIntentMatcher.TryMatch: Message='{Message}', ConversationId={ConvId}, PendingActions={PendingCount}, Entities={EntityCount}",
            message.Length > 50 ? message[..50] + "..." : message,
            context?.ConversationId ?? "null",
            context?.PendingActions.Count ?? 0,
            context?.Entities.Count ?? 0
        );

        if (context?.PendingActions.Count > 0)
        {
            foreach (var pa in context.PendingActions)
            {
                logger.LogInformation(
                    "  PendingAction: Id={Id}, Description='{Desc}', Agent={Agent}, EntityType={EntityType}",
                    pa.ActionId,
                    pa.Description,
                    pa.RequiredAgent,
                    pa.Parameters.GetValueOrDefault("entityType", "unknown")
                );
            }
        }

        var normalizedMessage = message.Trim().ToLowerInvariant();

        // FIRST: Check if this is a follow-up response to a pending action
        var followUpResult = TryMatchFollowUpResponse(message, normalizedMessage, context);
        if (followUpResult != null)
        {
            followUpResult.MatchDurationMs = sw.ElapsedMilliseconds;
            logger.LogInformation(
                "FastIntentMatcher matched FOLLOW-UP: {Intent} with confidence {Confidence:P0} - Reason: {Reason}",
                followUpResult.Intent,
                followUpResult.Confidence,
                followUpResult.MatchReason
            );
            activity?.SetTag("matched", true);
            activity?.SetTag("match_type", "follow_up");
            activity?.SetTag("intent", followUpResult.Intent.ToString());
            return followUpResult;
        }

        // Try each pattern matcher in order of specificity
        // IMPORTANT: Composite workflows FIRST (most specific) to catch multi-step requests
        var result =
            TryMatchCompositeWorkflow(message, normalizedMessage)
            ?? TryMatchCifLookup(message, normalizedMessage)
            ?? TryMatchFundIn(message, normalizedMessage)
            ?? TryMatchPortfolioQuery(message, normalizedMessage)
            ?? TryMatchMutualFundQuery(message, normalizedMessage)
            ?? TryMatchProfitProjection(message, normalizedMessage)
            ?? TryMatchGreeting(normalizedMessage)
            ?? TryMatchAccountBalance(normalizedMessage);

        sw.Stop();

        if (result != null)
        {
            result.MatchDurationMs = sw.ElapsedMilliseconds;
            logger.LogInformation(
                "FastIntentMatcher matched: {Intent} with confidence {Confidence:P0} in {Duration}ms",
                result.Intent,
                result.Confidence,
                sw.ElapsedMilliseconds
            );
            activity?.SetTag("matched", true);
            activity?.SetTag("intent", result.Intent.ToString());
        }
        else
        {
            logger.LogDebug("FastIntentMatcher: No pattern match, falling back to LLM");
            activity?.SetTag("matched", false);
        }

        return result;
    }

    /// <summary>
    /// Detect follow-up responses to pending actions.
    /// E.g., when user sends "ACC001" after being asked for account ID.
    /// </summary>
    private FastMatchResult? TryMatchFollowUpResponse(
        string original,
        string normalized,
        ConversationContext? context
    )
    {
        // Log context state for debugging
        logger.LogDebug(
            "TryMatchFollowUpResponse: Message='{Message}', Context={HasContext}, PendingActions={PendingCount}",
            original,
            context != null,
            context?.PendingActions.Count ?? 0
        );

        if (context == null || context.PendingActions.Count == 0)
        {
            logger.LogDebug("No pending actions to match against");
            return null;
        }

        // Short messages (< 50 chars) without action verbs are likely follow-up responses
        if (original.Length > 50)
        {
            logger.LogDebug(
                "Message too long ({Length} chars) for follow-up detection",
                original.Length
            );
            return null;
        }

        // Check if message looks like an identifier (alphanumeric, possibly with dashes)
        var looksLikeIdentifier = IdentifierPatternRegex().IsMatch(original.Trim());
        var looksLikeNumber = NumberPatternRegex().IsMatch(original.Trim());
        var looksLikeConfirmation = IsConfirmationResponse(normalized);

        logger.LogDebug(
            "Pattern checks: Identifier={IsIdentifier}, Number={IsNumber}, Confirmation={IsConfirmation}",
            looksLikeIdentifier,
            looksLikeNumber,
            looksLikeConfirmation
        );

        if (!looksLikeIdentifier && !looksLikeNumber && !looksLikeConfirmation)
        {
            logger.LogDebug("Message doesn't match any follow-up patterns");
            return null;
        }

        // Find the most recent pending action
        var pendingAction = context
            .PendingActions.OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (pendingAction == null)
            return null;

        logger.LogInformation(
            "Detected potential follow-up response '{Message}' for pending action: {Action}",
            original,
            pendingAction.Description
        );

        // Determine what entity type this likely is based on pending action
        var (entityType, intentType, subAgent) = InferEntityFromPendingAction(
            pendingAction,
            original
        );

        if (entityType == null)
            return null;

        var entities = new List<ExtractedEntity>
        {
            new()
            {
                EntityType = entityType,
                RawValue = original.Trim(),
                NormalizedValue = original.Trim(),
                Confidence = 0.9,
            },
        };

        // Copy existing entities from context
        foreach (var existingEntity in context.Entities.Values)
        {
            if (!entities.Any(e => e.EntityType == existingEntity.EntityType))
            {
                entities.Add(existingEntity);
            }
        }

        // Determine the refined request based on the original goal
        var primaryGoal = context.GetPrimaryGoal();
        var refinedRequest =
            primaryGoal != null
                ? $"Continue with: {primaryGoal.Description}. User provided {entityType}: {original.Trim()}"
                : $"User provided {entityType}: {original.Trim()}. Continue with the pending action: {pendingAction.Description}";

        return new FastMatchResult
        {
            Intent = intentType,
            Confidence = 0.92,
            SubAgent = subAgent,
            Strategy = ExecutionStrategy.Sequential,
            Entities = entities,
            RefinedRequest = refinedRequest,
            MatchReason = $"Follow-up response detected: provided {entityType} for pending action",
            IsFollowUp = true,
            PendingActionId = pendingAction.ActionId,
        };
    }

    /// <summary>
    /// Infer what entity type the user is providing based on the pending action
    /// </summary>
    private static (
        string? entityType,
        IntentType intent,
        string? subAgent
    ) InferEntityFromPendingAction(PendingAction action, string userInput)
    {
        var actionLower = action.Description.ToLowerInvariant();
        var inputTrimmed = userInput.Trim();

        // Check if we have an explicit entity type stored in parameters
        if (
            action.Parameters.TryGetValue("entityType", out var entityTypeObj)
            && entityTypeObj is string storedEntityType
        )
        {
            return storedEntityType switch
            {
                EntityTypes.AccountId or "account_id" => (
                    EntityTypes.AccountId,
                    IntentType.CompleteInvestmentWorkflow,
                    "AccountServices"
                ),
                EntityTypes.CustomerId or "customer_id" => (
                    EntityTypes.CustomerId,
                    IntentType.CustomerDataLookup,
                    "ExternalApiServices"
                ),
                EntityTypes.PortfolioId or "portfolio_id" => (
                    EntityTypes.PortfolioId,
                    IntentType.PortfolioAnalysis,
                    "PortfolioManager"
                ),
                EntityTypes.FundId or "fund_id" or "fund_name" => (
                    EntityTypes.FundId,
                    IntentType.MutualFundDetails,
                    "ExternalApiServices"
                ),
                EntityTypes.Amount or "amount" => (
                    EntityTypes.Amount,
                    IntentType.ProfitProjection,
                    action.RequiredAgent
                ),
                "risk_profile" => (
                    "risk_profile",
                    IntentType.InvestmentAdvice,
                    "InvestmentAdvisor"
                ),
                "confirmation" => (
                    "confirmation",
                    IntentType.CompleteInvestmentWorkflow,
                    action.RequiredAgent
                ),
                "general_info" => (
                    null,
                    IntentType.CompleteInvestmentWorkflow,
                    action.RequiredAgent
                ),
                _ => (
                    storedEntityType,
                    IntentType.CompleteInvestmentWorkflow,
                    action.RequiredAgent
                ),
            };
        }

        // Check for account-related pending actions
        if (actionLower.Contains("account") || actionLower.Contains("verify"))
        {
            // User input looks like an account ID
            if (
                inputTrimmed.StartsWith("ACC", StringComparison.OrdinalIgnoreCase)
                || inputTrimmed.StartsWith("6") && inputTrimmed.Length >= 10
                || Regex.IsMatch(inputTrimmed, @"^\d{10,14}$")
            )
            {
                return (
                    EntityTypes.AccountId,
                    IntentType.CompleteInvestmentWorkflow,
                    "AccountServices"
                );
            }
        }

        // Check for CIF/customer ID requests
        if (actionLower.Contains("cif") || actionLower.Contains("customer"))
        {
            if (
                Regex.IsMatch(inputTrimmed, @"^\d{12}$")
                || inputTrimmed.StartsWith("1000", StringComparison.OrdinalIgnoreCase)
            )
            {
                return (
                    EntityTypes.CustomerId,
                    IntentType.CustomerDataLookup,
                    "ExternalApiServices"
                );
            }
        }

        // Check for portfolio-related requests
        if (actionLower.Contains("portfolio"))
        {
            if (
                inputTrimmed.StartsWith("0") && inputTrimmed.Length >= 10
                || inputTrimmed.StartsWith("PORT", StringComparison.OrdinalIgnoreCase)
            )
            {
                return (EntityTypes.PortfolioId, IntentType.PortfolioAnalysis, "PortfolioManager");
            }
        }

        // Check for fund-related requests
        if (actionLower.Contains("fund"))
        {
            return (EntityTypes.FundId, IntentType.MutualFundDetails, "ExternalApiServices");
        }

        // Check for amount requests
        if (actionLower.Contains("amount") || actionLower.Contains("how much"))
        {
            if (Regex.IsMatch(inputTrimmed, @"^\$?[\d,]+\.?\d*[kKmM]?$"))
            {
                return (EntityTypes.Amount, IntentType.ProfitProjection, "ProfitProjection");
            }
        }

        // Generic identifier - try to match based on format
        if (inputTrimmed.StartsWith("ACC", StringComparison.OrdinalIgnoreCase))
        {
            return (
                EntityTypes.AccountId,
                IntentType.CompleteInvestmentWorkflow,
                "AccountServices"
            );
        }

        // If we have any pending investment workflow, treat unknown IDs as account IDs
        if (action.RequiredAgent == "AccountServices" || actionLower.Contains("invest"))
        {
            return (
                EntityTypes.AccountId,
                IntentType.CompleteInvestmentWorkflow,
                "AccountServices"
            );
        }

        return (null, IntentType.Unknown, null);
    }

    /// <summary>
    /// Check if the message is a confirmation (yes, ok, proceed, etc.)
    /// </summary>
    private static bool IsConfirmationResponse(string normalized)
    {
        var confirmations = new[]
        {
            "yes",
            "yeah",
            "yep",
            "ok",
            "okay",
            "sure",
            "proceed",
            "go ahead",
            "confirm",
            "confirmed",
            "do it",
            "let's do it",
            "sounds good",
            "نعم",
            "موافق",
            "حسنا",
            "تمام",
        };

        return confirmations.Any(c => normalized.Equals(c) || normalized.StartsWith(c + " "));
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-_]{3,20}$")]
    private static partial Regex IdentifierPatternRegex();

    [GeneratedRegex(@"^[\d,]+\.?\d*[kKmM]?$")]
    private static partial Regex NumberPatternRegex();

    #region Pattern Matchers

    /// <summary>
    /// Match composite workflow requests that require multiple sub-agents.
    /// Detects patterns like: "verify account, check compliance, get recommendations, create portfolio"
    /// These require coordinated multi-agent execution.
    /// </summary>
    private FastMatchResult? TryMatchCompositeWorkflow(string original, string normalized)
    {
        // Action verbs that indicate multi-step requests
        var accountActions = new[]
        {
            "verify",
            "check account",
            "validate account",
            "account verification",
        };
        var complianceActions = new[]
        {
            "compliance",
            "kyc",
            "aml",
            "regulatory",
            "check compliance",
        };
        var investmentActions = new[]
        {
            "recommend",
            "advice",
            "suggest",
            "investment advice",
            "recommendations",
        };
        var portfolioActions = new[]
        {
            "create portfolio",
            "create a portfolio",
            "build portfolio",
            "build a portfolio",
            "portfolio creation",
            "set up portfolio",
            "setup portfolio",
            "make a portfolio",
            "open portfolio",
        };
        var analysisActions = new[] { "analyze", "analysis", "evaluate", "assess", "review" };
        var projectionActions = new[] { "project", "projection", "forecast", "returns", "profit" };

        // Count how many action categories are present
        var hasAccountAction = accountActions.Any(a => normalized.Contains(a));
        var hasComplianceAction = complianceActions.Any(a => normalized.Contains(a));
        var hasInvestmentAction = investmentActions.Any(a => normalized.Contains(a));
        var hasPortfolioAction = portfolioActions.Any(a => normalized.Contains(a));
        var hasAnalysisAction = analysisActions.Any(a => normalized.Contains(a));
        var hasProjectionAction = projectionActions.Any(a => normalized.Contains(a));

        var actionCount = new[]
        {
            hasAccountAction,
            hasComplianceAction,
            hasInvestmentAction,
            hasPortfolioAction,
            hasAnalysisAction,
            hasProjectionAction,
        }.Count(x => x);

        // Also detect explicit workflow language
        var hasWorkflowIndicators =
            normalized.Contains(" and ")
            || normalized.Contains(" then ")
            || normalized.Contains("first,")
            || normalized.Contains("after that")
            || normalized.Contains("finally")
            || normalized.Contains("steps")
            || normalized.Contains("workflow")
            || normalized.Contains("complete investment")
            || normalized.Contains("end-to-end")
            || normalized.Contains("full process")
            ||
            // Comma-separated actions (e.g., "verify, check, recommend, create")
            CommaListRegex().IsMatch(normalized);

        // Need at least 2 distinct action types OR explicit workflow indicators + 1 action
        if (actionCount < 2 && !(hasWorkflowIndicators && actionCount >= 1))
            return null;

        // Determine the specific composite intent type
        var (intentType, agents, matchReason) = DetermineCompositeIntent(
            hasAccountAction,
            hasComplianceAction,
            hasInvestmentAction,
            hasPortfolioAction,
            hasAnalysisAction,
            hasProjectionAction
        );

        // Extract entities from the message
        var entities = ExtractEntitiesFromComposite(original, normalized);

        logger.LogInformation(
            "CompositeWorkflow detected: {Intent} requiring {AgentCount} agents: {Agents}",
            intentType,
            agents.Count,
            string.Join(", ", agents)
        );

        return new FastMatchResult
        {
            Intent = intentType,
            Confidence = 0.90 + (actionCount * 0.02), // Higher confidence with more actions
            SubAgent = null, // Multi-agent, will be handled by coordinator
            Strategy = ExecutionStrategy.Sequential, // Composite workflows are sequential
            Entities = entities,
            RefinedRequest = original,
            MatchReason = matchReason,
            RequiredSubAgents = agents,
            IsComposite = true,
        };
    }

    private static (IntentType intent, List<string> agents, string reason) DetermineCompositeIntent(
        bool hasAccount,
        bool hasCompliance,
        bool hasInvestment,
        bool hasPortfolio,
        bool hasAnalysis,
        bool hasProjection
    )
    {
        // CompleteInvestmentWorkflow: Account + Compliance + Investment + Portfolio
        if ((hasAccount || hasCompliance) && (hasInvestment || hasPortfolio))
        {
            var agents = new List<string>();
            if (hasAccount)
                agents.Add("AccountServices");
            if (hasCompliance)
                agents.Add("ComplianceOfficer");
            if (hasInvestment)
                agents.Add("InvestmentAdvisor");
            if (hasPortfolio)
                agents.Add("PortfolioManager");
            if (hasProjection)
                agents.Add("ProfitProjection");

            return (
                IntentType.CompleteInvestmentWorkflow,
                agents.Count > 0
                    ? agents
                    :
                    [
                        "AccountServices",
                        "ComplianceOfficer",
                        "InvestmentAdvisor",
                        "PortfolioManager",
                    ],
                $"Complete investment workflow detected: {string.Join(" → ", agents)}"
            );
        }

        // PortfolioWithProjection: Portfolio + Analysis/Projection
        if (hasPortfolio && (hasAnalysis || hasProjection))
        {
            return (
                IntentType.PortfolioWithProjection,
                ["PortfolioManager", "ProfitProjection"],
                "Portfolio with projection workflow detected"
            );
        }

        // ComprehensiveAnalysis: Multiple analysis aspects
        if (hasAnalysis && (hasInvestment || hasPortfolio || hasProjection))
        {
            var agents = new List<string>();
            if (hasInvestment)
                agents.Add("InvestmentAdvisor");
            if (hasPortfolio)
                agents.Add("PortfolioManager");
            if (hasProjection)
                agents.Add("ProfitProjection");
            if (hasAnalysis)
                agents.Add("ComplianceOfficer"); // Compliance does risk analysis

            return (
                IntentType.ComprehensiveAnalysis,
                agents,
                "Comprehensive analysis workflow detected"
            );
        }

        // Default to general composite with all relevant agents
        var defaultAgents = new List<string>();
        if (hasAccount)
            defaultAgents.Add("AccountServices");
        if (hasCompliance)
            defaultAgents.Add("ComplianceOfficer");
        if (hasInvestment)
            defaultAgents.Add("InvestmentAdvisor");
        if (hasPortfolio)
            defaultAgents.Add("PortfolioManager");
        if (hasProjection)
            defaultAgents.Add("ProfitProjection");

        return (
            IntentType.CompleteInvestmentWorkflow,
            defaultAgents,
            $"Multi-agent workflow detected: {string.Join(", ", defaultAgents)}"
        );
    }

    private List<ExtractedEntity> ExtractEntitiesFromComposite(string original, string normalized)
    {
        var entities = new List<ExtractedEntity>();

        // Extract amount
        var amountMatch = AmountPatternRegex().Match(original);
        if (amountMatch.Success)
        {
            var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
            if (double.TryParse(amountStr, out var amount))
            {
                if (amountMatch.Value.Contains('k', StringComparison.OrdinalIgnoreCase))
                    amount *= 1000;

                entities.Add(
                    new ExtractedEntity
                    {
                        EntityType = EntityTypes.Amount,
                        RawValue = amountMatch.Value,
                        NormalizedValue = amount,
                        Confidence = 0.95,
                    }
                );
            }
        }

        // Extract risk level
        if (
            normalized.Contains("conservative")
            || normalized.Contains("safe")
            || normalized.Contains("low risk")
        )
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.RiskLevel,
                    RawValue = "conservative",
                    NormalizedValue = "Conservative",
                    Confidence = 0.9,
                }
            );
        }
        else if (normalized.Contains("moderate") || normalized.Contains("balanced"))
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.RiskLevel,
                    RawValue = "moderate",
                    NormalizedValue = "Moderate",
                    Confidence = 0.9,
                }
            );
        }
        else if (normalized.Contains("aggressive") || normalized.Contains("high risk"))
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.RiskLevel,
                    RawValue = "aggressive",
                    NormalizedValue = "Aggressive",
                    Confidence = 0.9,
                }
            );
        }

        // Extract CIF if present
        var cifMatch = CifPatternRegex().Match(original);
        if (cifMatch.Success)
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.CustomerId,
                    RawValue = cifMatch.Groups[1].Value,
                    NormalizedValue = cifMatch.Groups[1].Value,
                    Confidence = 1.0,
                }
            );
        }

        // Extract investment type preferences
        if (normalized.Contains("mutual fund"))
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.InvestmentType,
                    RawValue = "mutual fund",
                    NormalizedValue = "MutualFund",
                    Confidence = 0.9,
                }
            );
        }

        return entities;
    }

    /// <summary>
    /// Match CIF-based customer data lookups
    /// Patterns: "CIF 100000000005", "customer 100000000001", "account details for 100..."
    /// </summary>
    private FastMatchResult? TryMatchCifLookup(string original, string normalized)
    {
        // CIF pattern: 12-digit number starting with 100
        var cifMatch = CifPatternRegex().Match(original);
        if (!cifMatch.Success)
            return null;

        var cif = cifMatch.Groups[1].Value;

        // Check for context clues
        var isLookupIntent =
            normalized.Contains("cif")
            || normalized.Contains("customer")
            || normalized.Contains("account")
            || normalized.Contains("portfolio")
            || normalized.Contains("details")
            || normalized.Contains("show")
            || normalized.Contains("get")
            || normalized.Contains("what")
            || normalized.Contains("find");

        if (!isLookupIntent)
            return null;

        return new FastMatchResult
        {
            Intent = IntentType.SNBCapitalQuery,
            Confidence = 0.95,
            SubAgent = "ExternalApiServices",
            Entities =
            [
                new ExtractedEntity
                {
                    EntityType = EntityTypes.CustomerId,
                    RawValue = cif,
                    NormalizedValue = cif,
                    Confidence = 1.0,
                },
            ],
            RefinedRequest = $"Get complete customer data for CIF {cif}",
            MatchReason = "CIF pattern detected with lookup context",
        };
    }

    /// <summary>
    /// Match Fund-In transfer requests
    /// Patterns: "transfer X from account Y to portfolio Z", "fund-in", "add money"
    /// </summary>
    private FastMatchResult? TryMatchFundIn(string original, string normalized)
    {
        var isFundInIntent =
            normalized.Contains("fund-in")
            || normalized.Contains("fundin")
            || normalized.Contains("fund in")
            || (
                normalized.Contains("transfer")
                && (normalized.Contains("portfolio") || normalized.Contains("investment"))
            )
            || (normalized.Contains("add") && normalized.Contains("money"))
            || (normalized.Contains("deposit") && normalized.Contains("portfolio"));

        if (!isFundInIntent)
            return null;

        var entities = new List<ExtractedEntity>();

        // Extract amount
        var amountMatch = AmountPatternRegex().Match(original);
        if (amountMatch.Success)
        {
            var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
            if (double.TryParse(amountStr, out var amount))
            {
                // Handle "k" suffix
                if (
                    amountMatch.Value.Contains('k', StringComparison.OrdinalIgnoreCase)
                    || amountMatch.Value.Contains('K')
                )
                {
                    amount *= 1000;
                }

                entities.Add(
                    new ExtractedEntity
                    {
                        EntityType = EntityTypes.Amount,
                        RawValue = amountMatch.Value,
                        NormalizedValue = amount,
                        Confidence = 0.95,
                    }
                );
            }
        }

        // Extract account number (typically starts with 6)
        var accountMatch = AccountNumberRegex().Match(original);
        if (accountMatch.Success)
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.AccountId,
                    RawValue = accountMatch.Value,
                    NormalizedValue = accountMatch.Value,
                    Confidence = 0.9,
                }
            );
        }

        // Extract portfolio number (typically starts with 0)
        var portfolioMatch = PortfolioNumberRegex().Match(original);
        if (portfolioMatch.Success)
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.PortfolioId,
                    RawValue = portfolioMatch.Value,
                    NormalizedValue = portfolioMatch.Value,
                    Confidence = 0.9,
                }
            );
        }

        return new FastMatchResult
        {
            Intent = IntentType.FundInOperation,
            Confidence = 0.9,
            SubAgent = "ExternalApiServices",
            Entities = entities,
            RefinedRequest = original,
            MatchReason = "Fund-in keywords detected",
        };
    }

    /// <summary>
    /// Match portfolio queries
    /// Patterns: "my portfolio", "show portfolios", "what's in my portfolio"
    /// </summary>
    private FastMatchResult? TryMatchPortfolioQuery(string original, string normalized)
    {
        var isPortfolioQuery =
            (normalized.Contains("portfolio") || normalized.Contains("portfolios"))
            && (
                normalized.Contains("show")
                || normalized.Contains("my")
                || normalized.Contains("what")
                || normalized.Contains("list")
                || normalized.Contains("get")
            );

        if (!isPortfolioQuery)
            return null;

        // Check if it's asking about holdings specifically
        var isHoldings =
            normalized.Contains("holding")
            || normalized.Contains("what's in")
            || normalized.Contains("contain");

        var entities = new List<ExtractedEntity>();

        // Check for CIF
        var cifMatch = CifPatternRegex().Match(original);
        if (cifMatch.Success)
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.CustomerId,
                    RawValue = cifMatch.Groups[1].Value,
                    NormalizedValue = cifMatch.Groups[1].Value,
                    Confidence = 1.0,
                }
            );
        }

        return new FastMatchResult
        {
            Intent = isHoldings ? IntentType.HoldingsInquiry : IntentType.PortfolioAnalysis,
            Confidence = 0.85,
            SubAgent = "ExternalApiServices",
            Entities = entities,
            RefinedRequest = original,
            MatchReason = "Portfolio query pattern detected",
        };
    }

    /// <summary>
    /// Match mutual fund queries
    /// Patterns: "available funds", "mutual funds", "search funds", "fund details"
    /// </summary>
    private FastMatchResult? TryMatchMutualFundQuery(string original, string normalized)
    {
        var isFundQuery =
            (
                normalized.Contains("mutual fund")
                || normalized.Contains("funds")
                || normalized.Contains("صناديق")
            )
            && (
                normalized.Contains("available")
                || normalized.Contains("show")
                || normalized.Contains("list")
                || normalized.Contains("search")
                || normalized.Contains("find")
                || normalized.Contains("details")
                || normalized.Contains("what")
            );

        if (!isFundQuery)
            return null;

        var isDetails =
            normalized.Contains("detail")
            || normalized.Contains("about")
            || normalized.Contains("info");

        return new FastMatchResult
        {
            Intent = isDetails ? IntentType.MutualFundDetails : IntentType.MutualFundSearch,
            Confidence = 0.85,
            SubAgent = "ExternalApiServices",
            RefinedRequest = original,
            MatchReason = "Mutual fund query pattern detected",
        };
    }

    /// <summary>
    /// Match profit projection / investment requests
    /// Patterns: "invest X", "what if I invest", "project returns", "calculate profit"
    /// </summary>
    private FastMatchResult? TryMatchProfitProjection(string original, string normalized)
    {
        var isProjectionIntent =
            normalized.Contains("invest")
            || normalized.Contains("project")
            || normalized.Contains("returns")
            || normalized.Contains("profit")
            || normalized.Contains("calculate")
            || normalized.Contains("how much")
            || normalized.Contains("what if")
            || normalized.Contains("استثمار")
            || normalized.Contains("أرباح");

        if (!isProjectionIntent)
            return null;

        // Must NOT be a fund-in (transfer) operation
        if (
            normalized.Contains("transfer")
            || normalized.Contains("fund-in")
            || normalized.Contains("portfolio")
        )
            return null;

        var entities = new List<ExtractedEntity>();

        // Extract amount
        var amountMatch = AmountPatternRegex().Match(original);
        if (amountMatch.Success)
        {
            var amountStr = amountMatch.Groups[1].Value.Replace(",", "");
            if (double.TryParse(amountStr, out var amount))
            {
                if (
                    amountMatch.Value.Contains('k', StringComparison.OrdinalIgnoreCase)
                    || amountMatch.Value.Contains('K')
                )
                {
                    amount *= 1000;
                }

                entities.Add(
                    new ExtractedEntity
                    {
                        EntityType = EntityTypes.Amount,
                        RawValue = amountMatch.Value,
                        NormalizedValue = amount,
                        Confidence = 0.95,
                    }
                );
            }
        }

        // Extract duration
        var durationMatch = DurationPatternRegex().Match(normalized);
        if (durationMatch.Success)
        {
            var value = int.Parse(durationMatch.Groups[1].Value);
            var unit = durationMatch.Groups[2].Value;
            var months = unit.StartsWith("year") ? value * 12 : value;

            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.Duration,
                    RawValue = durationMatch.Value,
                    NormalizedValue = months,
                    Confidence = 0.95,
                }
            );
        }

        // Extract risk level
        if (normalized.Contains("conservative") || normalized.Contains("safe"))
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.RiskLevel,
                    RawValue = "conservative",
                    NormalizedValue = "Conservative",
                    Confidence = 0.9,
                }
            );
        }
        else if (normalized.Contains("aggressive") || normalized.Contains("high risk"))
        {
            entities.Add(
                new ExtractedEntity
                {
                    EntityType = EntityTypes.RiskLevel,
                    RawValue = "aggressive",
                    NormalizedValue = "Aggressive",
                    Confidence = 0.9,
                }
            );
        }

        return new FastMatchResult
        {
            Intent = IntentType.ProfitProjection,
            Confidence = 0.85,
            SubAgent = "ProfitProjection",
            Entities = entities,
            RefinedRequest = original,
            MatchReason = "Investment/projection keywords detected",
        };
    }

    /// <summary>
    /// Match simple greetings
    /// </summary>
    private static FastMatchResult? TryMatchGreeting(string normalized)
    {
        var greetings = new[]
        {
            "hello",
            "hi",
            "hey",
            "good morning",
            "good afternoon",
            "good evening",
            "مرحبا",
            "السلام عليكم",
            "أهلا",
        };

        // Only match if the message is primarily a greeting (short)
        if (normalized.Length > 30)
            return null;

        var isGreeting = greetings.Any(g => normalized.Contains(g));
        if (!isGreeting)
            return null;

        return new FastMatchResult
        {
            Intent = IntentType.Greeting,
            Confidence = 0.95,
            SubAgent = null, // Master handles directly
            Strategy = ExecutionStrategy.DirectResponse,
            RefinedRequest = normalized,
            MatchReason = "Greeting pattern detected",
        };
    }

    /// <summary>
    /// Match account balance queries
    /// </summary>
    private static FastMatchResult? TryMatchAccountBalance(string normalized)
    {
        var isBalanceQuery =
            (normalized.Contains("balance") || normalized.Contains("رصيد"))
            && (
                normalized.Contains("account")
                || normalized.Contains("my")
                || normalized.Contains("check")
                || normalized.Contains("حساب")
            );

        if (!isBalanceQuery)
            return null;

        return new FastMatchResult
        {
            Intent = IntentType.AccountBalance,
            Confidence = 0.85,
            SubAgent = "AccountServices",
            RefinedRequest = normalized,
            MatchReason = "Account balance pattern detected",
        };
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"(?:CIF\s*:?\s*|customer\s*:?\s*|cif\s*)?(\d{12})", RegexOptions.IgnoreCase)]
    private static partial Regex CifPatternRegex();

    [GeneratedRegex(
        @"(?:SAR|USD|sar|usd)?\s*([\d,]+(?:\.\d{2})?)\s*(?:k|K|SAR|USD)?",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex AmountPatternRegex();

    [GeneratedRegex(@"6\d{13}", RegexOptions.None)]
    private static partial Regex AccountNumberRegex();

    [GeneratedRegex(@"0\d{11}", RegexOptions.None)]
    private static partial Regex PortfolioNumberRegex();

    [GeneratedRegex(@"(\d+)\s*(year|month|yr|mo)s?", RegexOptions.IgnoreCase)]
    private static partial Regex DurationPatternRegex();

    // Pattern to detect comma-separated action lists like "verify, check, recommend, create"
    [GeneratedRegex(
        @"\b(verify|check|validate|recommend|create|build|analyze|assess|get)\b.*,.*\b(verify|check|validate|recommend|create|build|analyze|assess|get)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex CommaListRegex();

    #endregion
}

/// <summary>
/// Result from fast pattern matching
/// </summary>
public class FastMatchResult
{
    public required IntentType Intent { get; set; }
    public double Confidence { get; set; }
    public string? SubAgent { get; set; }
    public ExecutionStrategy Strategy { get; set; } = ExecutionStrategy.SingleAgent;
    public List<ExtractedEntity> Entities { get; set; } = [];
    public string RefinedRequest { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public long MatchDurationMs { get; set; }

    /// <summary>
    /// For composite workflows: list of required sub-agents
    /// </summary>
    public List<string> RequiredSubAgents { get; set; } = [];

    /// <summary>
    /// Whether this is a composite workflow requiring coordination
    /// </summary>
    public bool IsComposite { get; set; }

    /// <summary>
    /// Whether this is a follow-up response to a previous question
    /// </summary>
    public bool IsFollowUp { get; set; }

    /// <summary>
    /// The pending action ID this follow-up is responding to
    /// </summary>
    public string? PendingActionId { get; set; }

    /// <summary>
    /// Convert to full UserIntent for pipeline compatibility
    /// </summary>
    public UserIntent ToUserIntent(string originalMessage) =>
        new()
        {
            OriginalMessage = originalMessage,
            PrimaryIntent = Intent,
            Confidence = Confidence,
            RequiredSubAgents =
                RequiredSubAgents.Count > 0
                    ? RequiredSubAgents
                    : (SubAgent != null ? [SubAgent] : []),
            Strategy = Strategy,
            Entities = Entities,
            RefinedRequest = RefinedRequest,
            IntentSummary = MatchReason,
            ClassificationDurationMs = MatchDurationMs,
        };
}

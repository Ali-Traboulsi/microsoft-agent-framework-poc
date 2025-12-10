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
    /// Attempt to quickly match intent using patterns.
    /// Returns null if no high-confidence match is found (falls back to LLM).
    /// </summary>
    public FastMatchResult? TryMatch(string message)
    {
        using var activity = ActivitySource.StartActivity("FastIntentMatcher.TryMatch");
        var sw = Stopwatch.StartNew();

        var normalizedMessage = message.Trim().ToLowerInvariant();

        // Try each pattern matcher in order of specificity
        var result =
            TryMatchCifLookup(message, normalizedMessage)
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

    #region Pattern Matchers

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
    /// Convert to full UserIntent for pipeline compatibility
    /// </summary>
    public UserIntent ToUserIntent(string originalMessage) =>
        new()
        {
            OriginalMessage = originalMessage,
            PrimaryIntent = Intent,
            Confidence = Confidence,
            RequiredSubAgents = SubAgent != null ? [SubAgent] : [],
            Strategy = Strategy,
            Entities = Entities,
            RefinedRequest = RefinedRequest,
            IntentSummary = MatchReason,
            ClassificationDurationMs = MatchDurationMs,
        };
}

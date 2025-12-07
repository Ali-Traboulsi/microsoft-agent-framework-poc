using System.Text.Json.Serialization;

namespace AgentFrameworkQuickStart.Api.DTOs;

/// <summary>
/// Structured JSON response format for Master Agent responses
/// </summary>
public class StructuredAgentResponse
{
    /// <summary>
    /// Natural language summary of the response
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Reference links/sources used in the response (from web search, documentation, etc.)
    /// </summary>
    [JsonPropertyName("referenceLinks")]
    public List<ReferenceLink> ReferenceLinks { get; set; } = new();

    /// <summary>
    /// Portfolio data if the query relates to portfolios
    /// </summary>
    [JsonPropertyName("portfolios")]
    public List<PortfolioData>? Portfolios { get; set; }

    /// <summary>
    /// Account data if the query relates to accounts
    /// </summary>
    [JsonPropertyName("accounts")]
    public List<AccountData>? Accounts { get; set; }

    /// <summary>
    /// Fund recommendations if the query relates to investment advice
    /// </summary>
    [JsonPropertyName("fundRecommendations")]
    public List<FundRecommendation>? FundRecommendations { get; set; }

    /// <summary>
    /// Compliance alerts or risk assessments
    /// </summary>
    [JsonPropertyName("complianceAlerts")]
    public List<ComplianceAlert>? ComplianceAlerts { get; set; }

    /// <summary>
    /// Key metrics and insights
    /// </summary>
    [JsonPropertyName("keyMetrics")]
    public Dictionary<string, object>? KeyMetrics { get; set; }

    /// <summary>
    /// Actions the user can take based on this response
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<string>? SuggestedActions { get; set; }

    /// <summary>
    /// Sub-agents that were consulted
    /// </summary>
    [JsonPropertyName("subAgentsUsed")]
    public List<string> SubAgentsUsed { get; set; } = new();

    /// <summary>
    /// Web search queries executed (if any)
    /// </summary>
    [JsonPropertyName("webSearchesExecuted")]
    public List<string>? WebSearchesExecuted { get; set; }
}

public class ReferenceLink
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty; // "Web Search", "Internal Documentation", etc.

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }
}

public class PortfolioData
{
    [JsonPropertyName("portfolioId")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("portfolioName")]
    public string PortfolioName { get; set; } = string.Empty;

    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }

    [JsonPropertyName("holdings")]
    public List<HoldingData>? Holdings { get; set; }

    [JsonPropertyName("performance")]
    public PerformanceData? Performance { get; set; }
}

public class HoldingData
{
    [JsonPropertyName("fundSymbol")]
    public string FundSymbol { get; set; } = string.Empty;

    [JsonPropertyName("fundName")]
    public string FundName { get; set; } = string.Empty;

    [JsonPropertyName("shares")]
    public decimal Shares { get; set; }

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("percentOfPortfolio")]
    public decimal PercentOfPortfolio { get; set; }
}

public class PerformanceData
{
    [JsonPropertyName("totalReturn")]
    public decimal TotalReturn { get; set; }

    [JsonPropertyName("returnPercentage")]
    public decimal ReturnPercentage { get; set; }

    [JsonPropertyName("period")]
    public string Period { get; set; } = string.Empty;
}

public class AccountData
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("accountType")]
    public string AccountType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class FundRecommendation
{
    [JsonPropertyName("fundSymbol")]
    public string FundSymbol { get; set; } = string.Empty;

    [JsonPropertyName("fundName")]
    public string FundName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty;

    [JsonPropertyName("expectedReturn")]
    public decimal? ExpectedReturn { get; set; }

    [JsonPropertyName("expenseRatio")]
    public decimal? ExpenseRatio { get; set; }

    [JsonPropertyName("recommendationReason")]
    public string RecommendationReason { get; set; } = string.Empty;

    [JsonPropertyName("matchScore")]
    public decimal MatchScore { get; set; } // 0-100 score
}

public class ComplianceAlert
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty; // "Low", "Medium", "High", "Critical"

    [JsonPropertyName("alertType")]
    public string AlertType { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }

    [JsonPropertyName("regulationReference")]
    public string? RegulationReference { get; set; }
}

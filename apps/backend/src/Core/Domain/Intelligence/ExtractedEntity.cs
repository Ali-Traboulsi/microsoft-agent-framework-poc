namespace AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// Represents an entity extracted from user input
/// </summary>
public class ExtractedEntity
{
    /// <summary>
    /// Type of entity (Amount, AccountId, Duration, RiskLevel, etc.)
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// The raw value as extracted from text
    /// </summary>
    public required string RawValue { get; set; }

    /// <summary>
    /// Normalized/parsed value (e.g., "50k" → 50000)
    /// </summary>
    public object? NormalizedValue { get; set; }

    /// <summary>
    /// Confidence in the extraction (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// Position in the original text where this was found
    /// </summary>
    public int? StartPosition { get; set; }

    /// <summary>
    /// Additional metadata about the entity
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Common entity types for the investment domain
/// </summary>
public static class EntityTypes
{
    // Financial
    public const string Amount = "amount";
    public const string Currency = "currency";
    public const string Percentage = "percentage";

    // Identifiers
    public const string AccountId = "account_id";
    public const string PortfolioId = "portfolio_id";
    public const string FundId = "fund_id";
    public const string CustomerId = "customer_id";
    public const string TransactionId = "transaction_id";

    // Time
    public const string Duration = "duration";
    public const string Date = "date";
    public const string TimeHorizon = "time_horizon";

    // Investment
    public const string RiskLevel = "risk_level";
    public const string InvestmentType = "investment_type";
    public const string FundName = "fund_name";
    public const string Strategy = "strategy";

    // Boolean Flags
    public const string ShariahCompliant = "shariah_compliant";

    // Names
    public const string PortfolioName = "portfolio_name";
    public const string CustomerName = "customer_name";
}

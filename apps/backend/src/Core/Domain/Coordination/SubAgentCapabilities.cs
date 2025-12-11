namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Declares what a sub-agent can provide and what it needs.
/// Used for automatic coordination and dependency resolution.
/// </summary>
public record SubAgentCapabilityDeclaration
{
    /// <summary>
    /// What fact categories this agent can provide
    /// </summary>
    public List<string> ProvidesData { get; init; } = [];

    /// <summary>
    /// What fact categories this agent needs from others
    /// </summary>
    public List<string> RequiresData { get; init; } = [];

    /// <summary>
    /// Task types this agent specializes in
    /// </summary>
    public List<string> SpecializesIn { get; init; } = [];

    /// <summary>
    /// Other agents this typically works with
    /// </summary>
    public List<string> CollaboratesWith { get; init; } = [];

    /// <summary>
    /// Whether this agent can run in parallel with others
    /// </summary>
    public bool CanRunInParallel { get; init; } = true;

    /// <summary>
    /// Priority for conflict resolution (higher = more authoritative)
    /// </summary>
    public int DomainAuthorityPriority { get; init; } = 5;

    /// <summary>
    /// Check if this agent can potentially provide what another needs
    /// </summary>
    public bool CanProvide(string dataCategory) =>
        ProvidesData.Contains(dataCategory, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Check if this agent needs data from a category
    /// </summary>
    public bool Needs(string dataCategory) =>
        RequiresData.Contains(dataCategory, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Check if this agent specializes in a task type
    /// </summary>
    public bool SpecializesInTask(string taskType) =>
        SpecializesIn.Any(s => taskType.Contains(s, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Common specialization categories
/// </summary>
public static class Specializations
{
    // Task types
    public const string CustomerLookup = "customer_lookup";
    public const string PortfolioManagement = "portfolio_management";
    public const string InvestmentAdvice = "investment_advice";
    public const string ComplianceCheck = "compliance_check";
    public const string RiskAssessment = "risk_assessment";
    public const string ProfitProjection = "profit_projection";
    public const string FundOperations = "fund_operations";
    public const string AccountOperations = "account_operations";
    public const string ExternalApi = "external_api";
    public const string Transactions = "transactions";
}

/// <summary>
/// Result of checking if an agent can handle a task
/// </summary>
public record CapabilityMatch
{
    /// <summary>
    /// Whether the agent can handle the task
    /// </summary>
    public bool CanHandle { get; init; }

    /// <summary>
    /// Confidence in the match (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Reason for the match/no-match
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Prerequisites that must be met first
    /// </summary>
    public List<string> MissingPrerequisites { get; init; } = [];

    /// <summary>
    /// Other agents that should run first
    /// </summary>
    public List<string> SuggestedDependencies { get; init; } = [];

    /// <summary>
    /// Create a positive match
    /// </summary>
    public static CapabilityMatch Yes(double confidence = 1.0, string? reason = null) =>
        new()
        {
            CanHandle = true,
            Confidence = confidence,
            Reason = reason,
        };

    /// <summary>
    /// Create a negative match
    /// </summary>
    public static CapabilityMatch No(string reason) =>
        new()
        {
            CanHandle = false,
            Confidence = 0,
            Reason = reason,
        };

    /// <summary>
    /// Create a conditional match (needs prerequisites)
    /// </summary>
    public static CapabilityMatch Conditional(
        double confidence,
        List<string> prerequisites,
        string? reason = null
    ) =>
        new()
        {
            CanHandle = true,
            Confidence = confidence,
            Reason = reason,
            MissingPrerequisites = prerequisites,
        };
}

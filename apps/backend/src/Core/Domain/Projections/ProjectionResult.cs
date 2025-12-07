namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Complete projection result with scenarios and recommendations
/// </summary>
public sealed record ProjectionResult
{
    public string ProjectionId { get; init; } = $"PROJ{DateTime.Now:yyyyMMddHHmmssfff}";
    public required ProjectionSummary InputSummary { get; init; }
    public required ScenarioSet Scenarios { get; init; }
    public List<FundRecommendation> RecommendedFunds { get; init; } = [];
    public List<string> RiskWarnings { get; init; } = [];
    public List<string> RiskWarningsAr { get; init; } = [];
    public CallToAction CallToAction { get; init; } = new();
    public ProjectionMetadata Metadata { get; init; } = new();
}

public sealed record ProjectionSummary
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "SAR";
    public string Horizon { get; init; } = "";
    public string HorizonAr { get; init; } = "";
    public string RiskProfile { get; init; } = "";
    public string RiskProfileAr { get; init; } = "";
    public string InvestmentType { get; init; } = "LumpSum";
    public bool ShariahCompliant { get; init; }
}

public sealed record CallToAction
{
    public string PrimaryAction { get; init; } = "Subscribe Now";
    public string PrimaryActionAr { get; init; } = "اشترك الآن";
    public string Link { get; init; } = "";
    public string SecondaryAction { get; init; } = "Save for Later";
    public string SecondaryActionAr { get; init; } = "احفظ للمراجعة لاحقاً";
    public string SecondaryLink { get; init; } = "";
}

public sealed record ProjectionMetadata
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddDays(7);
    public string WorkflowVersion { get; init; } = "2.0.0";
    public long ExecutionTimeMs { get; init; }
    public List<string> DataSources { get; init; } = [];
    public string? CustomerId { get; init; }
    public bool UsedHistoricalData { get; init; }
    public bool UsedMarketData { get; init; }
}

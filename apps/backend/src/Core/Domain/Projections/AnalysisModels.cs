namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Historical analysis results
/// </summary>
public sealed record HistoricalAnalysis
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    public int FundsAnalyzed { get; init; }
    public List<FundHistoricalData> FundPerformance { get; init; } = [];
    public Dictionary<string, RiskCategoryStats> RiskCategoryStats { get; init; } = [];
}

public sealed record RiskCategoryStats
{
    public string RiskCategory { get; init; } = "Moderate";
    public int FundCount { get; init; }
    public decimal AverageReturn { get; init; }
    public decimal AverageVolatility { get; init; }
    public decimal AverageSharpeRatio { get; init; }
    public decimal BestReturn { get; init; }
    public decimal WorstReturn { get; init; }
}

/// <summary>
/// Market conditions analysis
/// </summary>
public sealed record MarketAnalysis
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    public string MarketSentiment { get; init; } = "Neutral";
    public decimal MarketConditionScore { get; init; }
    public List<SectorOutlook> SectorOutlooks { get; init; } = [];
    public EconomicIndicators EconomicIndicators { get; init; } = new();
    public decimal ReturnAdjustmentFactor { get; init; } = 1.0m;
    public decimal RiskAdjustmentFactor { get; init; } = 1.0m;
}

public sealed record SectorOutlook
{
    public required string SectorName { get; init; }
    public string SectorNameAr { get; init; } = "";
    public string Outlook { get; init; } = "Neutral";
    public decimal ExpectedReturnAdjustment { get; init; }
    public string Rationale { get; init; } = "";
}

public sealed record EconomicIndicators
{
    public decimal GdpGrowth { get; init; }
    public decimal InflationRate { get; init; }
    public decimal InterestRate { get; init; }
    public decimal OilPrice { get; init; }
    public string CurrencyOutlook { get; init; } = "Stable";
}

/// <summary>
/// Fund selection/matching results
/// </summary>
public sealed record FundSelectionResult
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    public string RiskProfileUsed { get; init; } = "Moderate";
    public int FundsConsidered { get; init; }
    public int FundsMatched { get; init; }
    public List<RankedFund> RankedFunds { get; init; } = [];
    public List<FundAllocation> RecommendedAllocation { get; init; } = [];
}

/// <summary>
/// Aggregated results from all analyses
/// </summary>
public sealed record AggregatedAnalysis
{
    public DateTime AggregatedAt { get; init; } = DateTime.UtcNow;
    public required ProjectionRequest OriginalRequest { get; init; }
    public CustomerContext CustomerContext { get; init; } = CustomerContext.Anonymous;
    public HistoricalAnalysis HistoricalAnalysis { get; init; } = new();
    public MarketAnalysis MarketAnalysis { get; init; } = new();
    public FundSelectionResult FundSelection { get; init; } = new();
    public decimal WeightedExpectedReturn { get; init; }
    public decimal ExpectedVolatility { get; init; }
    public decimal ConfidenceLevel { get; init; }
}

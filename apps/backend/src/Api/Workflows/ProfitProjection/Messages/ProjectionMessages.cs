namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

#region Input Messages

/// <summary>
/// Initial request for profit projection calculation
/// احتساب الأرباح التقديرية
/// </summary>
public record ProjectionRequest
{
    /// <summary>Investment amount in specified currency</summary>
    public required decimal InvestmentAmount { get; init; }

    /// <summary>Currency code (SAR, USD, EUR)</summary>
    public string Currency { get; init; } = "SAR";

    /// <summary>Investment time horizon in months</summary>
    public required int TimeHorizonMonths { get; init; }

    /// <summary>Risk profile: Conservative, Moderate, Aggressive</summary>
    public required string RiskProfile { get; init; }

    /// <summary>Investment type: LumpSum or Monthly (SIP)</summary>
    public string InvestmentType { get; init; } = "LumpSum";

    /// <summary>Monthly investment amount (for SIP only)</summary>
    public decimal? MonthlyAmount { get; init; }

    /// <summary>Optional customer ID (CIF) for personalized projections</summary>
    public string? CustomerId { get; init; }

    /// <summary>Specific fund codes to analyze (optional)</summary>
    public List<string>? TargetFundCodes { get; init; }

    /// <summary>Include Sharia-compliant funds only</summary>
    public bool ShariahCompliantOnly { get; init; } = false;

    /// <summary>Request timestamp</summary>
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

#endregion

#region Customer Context

/// <summary>
/// Customer context enriched from existing data
/// </summary>
public record CustomerContext
{
    public string? CustomerId { get; init; }
    public bool IsExistingCustomer { get; init; }
    public string? CustomerName { get; init; }
    public string? CurrentRiskProfile { get; init; }

    /// <summary>Existing portfolio holdings (if customer exists)</summary>
    public List<ExistingHolding> ExistingHoldings { get; init; } = new();

    /// <summary>Total current portfolio value</summary>
    public decimal CurrentPortfolioValue { get; init; }

    /// <summary>Historical investment behavior</summary>
    public InvestmentBehavior? Behavior { get; init; }
}

public record ExistingHolding
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public decimal Units { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal CostBasis { get; init; }
    public decimal UnrealizedGain { get; init; }
    public decimal UnrealizedGainPercent { get; init; }
}

public record InvestmentBehavior
{
    public int TotalTransactions { get; init; }
    public decimal AverageInvestmentSize { get; init; }
    public string PreferredFundType { get; init; } = "Balanced";
    public int MonthsAsCustomer { get; init; }
    public decimal HistoricalReturns { get; init; }
}

#endregion

#region Analysis Results

/// <summary>
/// Historical performance analysis for funds
/// </summary>
public record HistoricalAnalysis
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    public int FundsAnalyzed { get; init; }

    /// <summary>Performance data for each analyzed fund</summary>
    public List<FundHistoricalData> FundPerformance { get; init; } = new();

    /// <summary>Risk-adjusted return statistics by risk category</summary>
    public Dictionary<string, RiskCategoryStats> RiskCategoryStats { get; init; } = new();
}

public record FundHistoricalData
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public string? FundNameAr { get; init; }
    public string FundType { get; init; } = "Balanced";
    public string RiskLevel { get; init; } = "Medium";
    public bool IsShariahCompliant { get; init; }

    // Returns (percentages)
    public decimal YtdReturn { get; init; }
    public decimal OneYearReturn { get; init; }
    public decimal ThreeYearReturn { get; init; }
    public decimal FiveYearReturn { get; init; }
    public decimal SinceInceptionReturn { get; init; }

    // Risk metrics
    public decimal StandardDeviation { get; init; }
    public decimal SharpeRatio { get; init; }
    public decimal Beta { get; init; }
    public decimal Alpha { get; init; }
    public decimal MaxDrawdown { get; init; }

    // Percentile returns (for scenario building)
    public decimal P10Return { get; init; } // 10th percentile (bad case)
    public decimal P25Return { get; init; } // 25th percentile
    public decimal P50Return { get; init; } // Median
    public decimal P75Return { get; init; } // 75th percentile
    public decimal P90Return { get; init; } // 90th percentile (good case)

    // Fees
    public decimal ManagementFee { get; init; }
    public decimal TotalExpenseRatio { get; init; }
}

public record RiskCategoryStats
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
/// Current market conditions analysis
/// </summary>
public record MarketAnalysis
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Overall market sentiment: Bullish, Neutral, Bearish</summary>
    public string MarketSentiment { get; init; } = "Neutral";

    /// <summary>Market condition score: -1 (bearish) to +1 (bullish)</summary>
    public decimal MarketConditionScore { get; init; }

    /// <summary>Sector performance outlook</summary>
    public List<SectorOutlook> SectorOutlooks { get; init; } = new();

    /// <summary>Economic indicators</summary>
    public EconomicIndicators EconomicIndicators { get; init; } = new();

    /// <summary>Return adjustment factor based on market conditions</summary>
    public decimal ReturnAdjustmentFactor { get; init; } = 1.0m;

    /// <summary>Risk adjustment factor based on market conditions</summary>
    public decimal RiskAdjustmentFactor { get; init; } = 1.0m;
}

public record SectorOutlook
{
    public required string SectorName { get; init; }
    public string SectorNameAr { get; init; } = "";
    public string Outlook { get; init; } = "Neutral"; // Bullish, Neutral, Bearish
    public decimal ExpectedReturnAdjustment { get; init; } // +/- percentage
    public string Rationale { get; init; } = "";
}

public record EconomicIndicators
{
    public decimal GdpGrowth { get; init; }
    public decimal InflationRate { get; init; }
    public decimal InterestRate { get; init; }
    public decimal OilPrice { get; init; } // Important for Saudi market
    public string CurrencyOutlook { get; init; } = "Stable";
}

/// <summary>
/// Fund selection/matching results
/// </summary>
public record FundSelectionResult
{
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
    public string RiskProfileUsed { get; init; } = "Moderate";
    public int FundsConsidered { get; init; }
    public int FundsMatched { get; init; }

    /// <summary>Ranked list of matching funds</summary>
    public List<RankedFund> RankedFunds { get; init; } = new();

    /// <summary>Recommended allocation</summary>
    public List<FundAllocation> RecommendedAllocation { get; init; } = new();
}

public record RankedFund
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public string? FundNameAr { get; init; }
    public string FundType { get; init; } = "Balanced";
    public string RiskLevel { get; init; } = "Medium";

    /// <summary>Overall score (0-100)</summary>
    public decimal Score { get; init; }

    /// <summary>Ranking among matched funds</summary>
    public int Rank { get; init; }

    /// <summary>Match reasons</summary>
    public List<string> MatchReasons { get; init; } = new();

    /// <summary>Expected return based on risk profile</summary>
    public decimal ExpectedReturn { get; init; }

    /// <summary>Current NAV</summary>
    public decimal CurrentNav { get; init; }

    /// <summary>Minimum investment</summary>
    public decimal MinimumInvestment { get; init; }
}

public record FundAllocation
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public decimal AllocationPercent { get; init; }
    public decimal InvestmentAmount { get; init; }
    public decimal ExpectedContribution { get; init; }
}

#endregion

#region Aggregated Analysis

/// <summary>
/// Aggregated results from parallel analysis (Fan-in)
/// </summary>
public record AggregatedAnalysis
{
    public DateTime AggregatedAt { get; init; } = DateTime.UtcNow;

    public ProjectionRequest OriginalRequest { get; init; } = null!;
    public CustomerContext CustomerContext { get; init; } = new();
    public HistoricalAnalysis HistoricalAnalysis { get; init; } = new();
    public MarketAnalysis MarketAnalysis { get; init; } = new();
    public FundSelectionResult FundSelection { get; init; } = new();

    /// <summary>Weighted expected return (adjusted for market conditions)</summary>
    public decimal WeightedExpectedReturn { get; init; }

    /// <summary>Expected volatility</summary>
    public decimal ExpectedVolatility { get; init; }

    /// <summary>Confidence level in projections</summary>
    public decimal ConfidenceLevel { get; init; }
}

#endregion

#region Scenarios

/// <summary>
/// Individual projection scenario
/// </summary>
public record Scenario
{
    /// <summary>Scenario type: Conservative, Expected, Optimistic</summary>
    public required string ScenarioType { get; init; }

    /// <summary>Arabic description</summary>
    public string ScenarioTypeAr { get; init; } = "";

    /// <summary>Confidence level (percentage)</summary>
    public decimal Confidence { get; init; }

    /// <summary>Description of the scenario</summary>
    public string Description { get; init; } = "";
    public string DescriptionAr { get; init; } = "";

    /// <summary>Assumed annual return rate</summary>
    public decimal AssumedReturnRate { get; init; }

    /// <summary>Initial investment</summary>
    public decimal InitialInvestment { get; init; }

    /// <summary>Final projected value</summary>
    public decimal ProjectedValue { get; init; }

    /// <summary>Total return amount</summary>
    public decimal TotalReturn { get; init; }

    /// <summary>Annualized return percentage</summary>
    public decimal AnnualizedReturn { get; init; }

    /// <summary>Monthly projection values</summary>
    public List<MonthlyProjection> MonthlyProjections { get; init; } = new();
}

public record MonthlyProjection
{
    public int Month { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public decimal CumulativeReturn { get; init; }
    public decimal MonthlyContribution { get; init; } // For SIP
}

/// <summary>
/// Complete set of projection scenarios
/// </summary>
public record ScenarioSet
{
    public Scenario Conservative { get; init; } = null!;
    public Scenario Expected { get; init; } = null!;
    public Scenario Optimistic { get; init; } = null!;

    /// <summary>Investment strategy comparison</summary>
    public StrategyComparison? StrategyComparison { get; init; }
}

public record StrategyComparison
{
    public StrategyResult LumpSum { get; init; } = null!;
    public StrategyResult MonthlySip { get; init; } = null!;
    public string RecommendedStrategy { get; init; } = "LumpSum";
    public string RecommendationRationale { get; init; } = "";
    public string RecommendationRationaleAr { get; init; } = "";
}

public record StrategyResult
{
    public string StrategyName { get; init; } = "LumpSum";
    public decimal TotalInvestment { get; init; }
    public decimal ProjectedValue { get; init; }
    public decimal TotalReturn { get; init; }
    public string Benefit { get; init; } = "";
    public string BenefitAr { get; init; } = "";
}

#endregion

#region Final Output

/// <summary>
/// Final profit projection result
/// </summary>
public record ProjectionResult
{
    /// <summary>Unique projection ID for tracking</summary>
    public string ProjectionId { get; init; } = $"PROJ{DateTime.Now:yyyyMMddHHmmss}";

    /// <summary>Request summary</summary>
    public ProjectionSummary InputSummary { get; init; } = null!;

    /// <summary>All three scenarios</summary>
    public ScenarioSet Scenarios { get; init; } = null!;

    /// <summary>Recommended fund allocations</summary>
    public List<FundRecommendation> RecommendedFunds { get; init; } = new();

    /// <summary>Risk warnings and disclaimers</summary>
    public List<string> RiskWarnings { get; init; } = new();
    public List<string> RiskWarningsAr { get; init; } = new();

    /// <summary>Call to action</summary>
    public CallToAction CallToAction { get; init; } = new();

    /// <summary>Processing metadata</summary>
    public ProjectionMetadata Metadata { get; init; } = new();
}

public record ProjectionSummary
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

public record FundRecommendation
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public string? FundNameAr { get; init; }
    public string FundType { get; init; } = "Balanced";

    /// <summary>Allocation percentage in the portfolio</summary>
    public decimal AllocationPercent { get; init; }

    /// <summary>Investment amount based on allocation</summary>
    public decimal InvestmentAmount { get; init; }

    /// <summary>Expected contribution to total return</summary>
    public decimal ExpectedContribution { get; init; }

    /// <summary>Expected return for this fund</summary>
    public decimal ExpectedReturn { get; init; }

    /// <summary>Why this fund was recommended</summary>
    public List<string> Reasons { get; init; } = new();

    /// <summary>Current NAV</summary>
    public decimal CurrentNav { get; init; }

    /// <summary>Is Sharia compliant</summary>
    public bool IsShariahCompliant { get; init; }
}

public record CallToAction
{
    public string PrimaryAction { get; init; } = "Subscribe Now";
    public string PrimaryActionAr { get; init; } = "اشترك الآن";
    public string Link { get; init; } = "/subscribe";
    public string? SecondaryAction { get; init; }
    public string? SecondaryActionAr { get; init; }
    public string? SecondaryLink { get; init; }
}

public record ProjectionMetadata
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddDays(7);
    public string WorkflowVersion { get; init; } = "1.0.0";
    public int ExecutionTimeMs { get; init; }
    public List<string> DataSources { get; init; } = new();
    public string? CustomerId { get; init; }
    public bool UsedHistoricalData { get; init; }
    public bool UsedMarketData { get; init; }
}

#endregion

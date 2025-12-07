namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Historical fund performance data
/// </summary>
public sealed record FundHistoricalData
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
    public decimal P10Return { get; init; }
    public decimal P25Return { get; init; }
    public decimal P50Return { get; init; }
    public decimal P75Return { get; init; }
    public decimal P90Return { get; init; }

    // Fees
    public decimal ManagementFee { get; init; }
    public decimal TotalExpenseRatio { get; init; }
}

/// <summary>
/// Ranked fund with matching score
/// </summary>
public sealed record RankedFund
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public string? FundNameAr { get; init; }
    public string FundType { get; init; } = "Balanced";
    public string RiskLevel { get; init; } = "Medium";
    public decimal Score { get; init; }
    public int Rank { get; init; }
    public List<string> MatchReasons { get; init; } = [];
    public decimal ExpectedReturn { get; init; }
    public decimal CurrentNav { get; init; }
    public decimal MinimumInvestment { get; init; }
}

/// <summary>
/// Fund allocation in portfolio
/// </summary>
public sealed record FundAllocation
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public decimal AllocationPercent { get; init; }
    public decimal InvestmentAmount { get; init; }
    public decimal ExpectedContribution { get; init; }
}

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a mutual fund available for investment
/// </summary>
public class MutualFund
{
    public string FundId { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string FundSymbol { get; set; } = string.Empty;
    public FundCategory Category { get; set; }
    public decimal CurrentNAV { get; set; } // Net Asset Value
    public decimal ExpenseRatio { get; set; }
    public decimal MinimumInvestment { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal YTDReturn { get; set; }
    public decimal OneYearReturn { get; set; }
    public decimal ThreeYearReturn { get; set; }
}

public enum FundCategory
{
    Equity,
    Bond,
    Balanced,
    MoneyMarket,
    Index,
    International,
    Sector,
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    VeryHigh,
}

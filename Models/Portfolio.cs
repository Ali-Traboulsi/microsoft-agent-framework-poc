namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents an investment portfolio
/// </summary>
public class Portfolio
{
    public string PortfolioId { get; set; } = string.Empty;
    public string PortfolioName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public List<PortfolioHolding> Holdings { get; set; } = new();
    public decimal TotalValue { get; set; }
    public PortfolioStrategy Strategy { get; set; }
    public RiskProfile RiskProfile { get; set; }
}

public enum PortfolioStrategy
{
    Conservative,
    Moderate,
    Aggressive,
    Income,
    Growth,
    Balanced,
}

public enum RiskProfile
{
    VeryConservative,
    Conservative,
    Moderate,
    Aggressive,
    VeryAggressive,
}

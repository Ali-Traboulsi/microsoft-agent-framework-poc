namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a holding of a mutual fund within a portfolio
/// </summary>
public class PortfolioHolding
{
    public string HoldingId { get; set; } = string.Empty;
    public string PortfolioId { get; set; } = string.Empty;
    public string FundId { get; set; } = string.Empty;
    public string FundSymbol { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal GainLoss { get; set; }
    public decimal GainLossPercentage { get; set; }
    public DateTime PurchaseDate { get; set; }
}

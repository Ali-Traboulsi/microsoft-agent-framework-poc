namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a financial transaction
/// </summary>
public class Transaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string? PortfolioId { get; set; }
    public string? FundId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal? Shares { get; set; }
    public decimal? PricePerShare { get; set; }
    public DateTime TransactionDate { get; set; }
    public TransactionStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Fee { get; set; }
}

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Buy,
    Sell,
    Transfer,
    Dividend,
    Fee,
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled,
}

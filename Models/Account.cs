namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a customer account with available balance for investments
/// </summary>
public class Account
{
    public string AccountId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedDate { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
}

public enum AccountStatus
{
    Active,
    Suspended,
    Closed,
}

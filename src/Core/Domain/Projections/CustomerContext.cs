namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Customer context for personalized projections
/// </summary>
public sealed record CustomerContext
{
    public string? CustomerId { get; init; }
    public bool IsExistingCustomer { get; init; }
    public string? CustomerName { get; init; }
    public string? CurrentRiskProfile { get; init; }
    public List<ExistingHolding> ExistingHoldings { get; init; } = [];
    public decimal CurrentPortfolioValue { get; init; }
    public InvestmentBehavior? Behavior { get; init; }

    public static CustomerContext Anonymous => new() { IsExistingCustomer = false };
}

public sealed record ExistingHolding
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public decimal Units { get; init; }
    public decimal CurrentValue { get; init; }
    public decimal CostBasis { get; init; }
    public decimal UnrealizedGain { get; init; }
    public decimal UnrealizedGainPercent { get; init; }
}

public sealed record InvestmentBehavior
{
    public int TotalTransactions { get; init; }
    public decimal AverageInvestmentSize { get; init; }
    public string PreferredFundType { get; init; } = "Balanced";
    public int MonthsAsCustomer { get; init; }
    public decimal HistoricalReturns { get; init; }
}

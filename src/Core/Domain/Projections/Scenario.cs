namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Individual projection scenario (Conservative, Expected, Optimistic)
/// </summary>
public sealed record Scenario
{
    public required string ScenarioType { get; init; }
    public string ScenarioTypeAr { get; init; } = "";
    public decimal Confidence { get; init; }
    public string Description { get; init; } = "";
    public string DescriptionAr { get; init; } = "";
    public decimal AssumedReturnRate { get; init; }
    public decimal InitialInvestment { get; init; }
    public decimal ProjectedValue { get; init; }
    public decimal TotalReturn { get; init; }
    public decimal AnnualizedReturn { get; init; }
    public List<MonthlyProjection> MonthlyProjections { get; init; } = [];
}

public sealed record MonthlyProjection
{
    public int Month { get; init; }
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public decimal CumulativeReturn { get; init; }
    public decimal MonthlyContribution { get; init; }
}

/// <summary>
/// Complete set of three projection scenarios
/// </summary>
public sealed record ScenarioSet
{
    public required Scenario Conservative { get; init; }
    public required Scenario Expected { get; init; }
    public required Scenario Optimistic { get; init; }
    public StrategyComparison? StrategyComparison { get; init; }
}

public sealed record StrategyComparison
{
    public required StrategyResult LumpSum { get; init; }
    public required StrategyResult MonthlySip { get; init; }
    public string RecommendedStrategy { get; init; } = "LumpSum";
    public string RecommendationRationale { get; init; } = "";
    public string RecommendationRationaleAr { get; init; } = "";
}

public sealed record StrategyResult
{
    public string StrategyName { get; init; } = "LumpSum";
    public decimal TotalInvestment { get; init; }
    public decimal ProjectedValue { get; init; }
    public decimal TotalReturn { get; init; }
    public string Benefit { get; init; } = "";
    public string BenefitAr { get; init; } = "";
}

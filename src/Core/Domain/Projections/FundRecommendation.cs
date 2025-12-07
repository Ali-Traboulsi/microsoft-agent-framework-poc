namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Fund recommendation with allocation details
/// </summary>
public sealed record FundRecommendation
{
    public required string FundCode { get; init; }
    public required string FundName { get; init; }
    public string? FundNameAr { get; init; }
    public string FundType { get; init; } = "Balanced";
    public decimal AllocationPercent { get; init; }
    public decimal InvestmentAmount { get; init; }
    public decimal ExpectedContribution { get; init; }
    public decimal ExpectedReturn { get; init; }
    public List<string> Reasons { get; init; } = [];
    public decimal CurrentNav { get; init; }
    public bool IsShariahCompliant { get; init; }
}

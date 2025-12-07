namespace AgentFrameworkQuickStart.Core.Domain.Projections;

/// <summary>
/// Investment projection request - احتساب الأرباح التقديرية
/// </summary>
public sealed record ProjectionRequest
{
    public required decimal InvestmentAmount { get; init; }
    public string Currency { get; init; } = "SAR";
    public required int TimeHorizonMonths { get; init; }
    public required string RiskProfile { get; init; }
    public string InvestmentType { get; init; } = "LumpSum";
    public decimal? MonthlyAmount { get; init; }
    public string? CustomerId { get; init; }
    public List<string>? TargetFundCodes { get; init; }
    public bool ShariahCompliantOnly { get; init; }
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;

    public string NormalizedRiskProfile =>
        RiskProfile.ToLower() switch
        {
            "low" or "conservative" or "متحفظ" => "Conservative",
            "medium" or "moderate" or "balanced" or "متوازن" => "Moderate",
            "high" or "aggressive" or "جريء" => "Aggressive",
            _ => "Moderate",
        };

    public void Validate()
    {
        if (InvestmentAmount < 1000)
            throw new ArgumentException("Minimum investment is 1000", nameof(InvestmentAmount));
        if (TimeHorizonMonths < 1 || TimeHorizonMonths > 360)
            throw new ArgumentException(
                "Time horizon must be 1-360 months",
                nameof(TimeHorizonMonths)
            );
    }
}

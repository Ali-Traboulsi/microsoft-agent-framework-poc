using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Input for recommendation engine
/// </summary>
public sealed record RecommendationInput(AggregatedAnalysis Analysis, ScenarioSet Scenarios);

/// <summary>
/// Generates final recommendations with fund details and CTAs
/// </summary>
public sealed class RecommendationEngine : ExecutorBase<RecommendationInput, ProjectionResult>
{
    public RecommendationEngine(ILogger<RecommendationEngine> logger)
        : base(logger) { }

    protected override Task<ProjectionResult> ExecuteCoreAsync(
        RecommendationInput input,
        CancellationToken ct
    )
    {
        var analysis = input.Analysis;
        var request = analysis.OriginalRequest;
        var scenarios = input.Scenarios;

        var fundRecommendations = BuildFundRecommendations(analysis);
        var summary = BuildSummary(request);
        var warnings = GetRiskWarnings();
        var cta = BuildCallToAction(request);

        return Task.FromResult(
            new ProjectionResult
            {
                InputSummary = summary,
                Scenarios = scenarios,
                RecommendedFunds = fundRecommendations,
                RiskWarnings = warnings.English,
                RiskWarningsAr = warnings.Arabic,
                CallToAction = cta,
                Metadata = new ProjectionMetadata
                {
                    ExecutionTimeMs = 0, // Will be set by workflow
                    DataSources =
                    [
                        "SNB Capital Mutual Funds API",
                        "Historical Performance Data",
                        "Market Conditions Analysis",
                    ],
                    CustomerId = request.CustomerId,
                    UsedHistoricalData = true,
                    UsedMarketData = true,
                },
            }
        );
    }

    private static List<FundRecommendation> BuildFundRecommendations(AggregatedAnalysis analysis)
    {
        var allocations = analysis.FundSelection.RecommendedAllocation;
        var funds = analysis.HistoricalAnalysis.FundPerformance;
        var ranked = analysis.FundSelection.RankedFunds;

        return allocations
            .Select(alloc =>
            {
                var fund = funds.FirstOrDefault(f => f.FundCode == alloc.FundCode);
                var rank = ranked.FirstOrDefault(r => r.FundCode == alloc.FundCode);

                return new FundRecommendation
                {
                    FundCode = alloc.FundCode,
                    FundName = alloc.FundName,
                    FundNameAr = fund?.FundNameAr,
                    FundType = fund?.FundType ?? "Balanced",
                    AllocationPercent = alloc.AllocationPercent,
                    InvestmentAmount = alloc.InvestmentAmount,
                    ExpectedContribution = alloc.ExpectedContribution,
                    ExpectedReturn = fund?.OneYearReturn ?? 0,
                    Reasons = rank?.MatchReasons ?? [],
                    CurrentNav = rank?.CurrentNav ?? 0,
                    IsShariahCompliant = fund?.IsShariahCompliant ?? false,
                };
            })
            .ToList();
    }

    private static ProjectionSummary BuildSummary(ProjectionRequest request)
    {
        var horizonYears = request.TimeHorizonMonths / 12.0;
        var horizonText =
            horizonYears >= 1
                ? $"{horizonYears:F0} year{(horizonYears > 1 ? "s" : "")}"
                : $"{request.TimeHorizonMonths} months";

        var (riskEn, riskAr) = request.NormalizedRiskProfile switch
        {
            "Conservative" => ("Conservative", "متحفظ"),
            "Aggressive" => ("Aggressive", "جريء"),
            _ => ("Moderate", "متوازن"),
        };

        return new ProjectionSummary
        {
            Amount = request.InvestmentAmount,
            Currency = request.Currency,
            Horizon = horizonText,
            HorizonAr = $"{request.TimeHorizonMonths} شهر",
            RiskProfile = riskEn,
            RiskProfileAr = riskAr,
            InvestmentType = request.InvestmentType,
            ShariahCompliant = request.ShariahCompliantOnly,
        };
    }

    private static (List<string> English, List<string> Arabic) GetRiskWarnings() =>
        (
            [
                "Past performance does not guarantee future results. The projections shown are estimates based on historical data and may not reflect actual returns.",
            ],
            [
                "الأداء السابق لا يضمن النتائج المستقبلية. التوقعات المعروضة هي تقديرات بناءً على البيانات التاريخية وقد لا تعكس العوائد الفعلية.",
            ]
        );

    private static CallToAction BuildCallToAction(ProjectionRequest request) =>
        new()
        {
            PrimaryAction = "Subscribe Now",
            PrimaryActionAr = "اشترك الآن",
            Link = $"/subscribe?amount={request.InvestmentAmount}&risk={request.RiskProfile}",
            SecondaryAction = "Schedule Consultation",
            SecondaryActionAr = "حجز استشارة",
            SecondaryLink = "/consultation/schedule",
        };
}

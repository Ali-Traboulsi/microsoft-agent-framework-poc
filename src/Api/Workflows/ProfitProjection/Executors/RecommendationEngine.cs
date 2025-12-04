using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Generates final recommendations and call-to-action
/// Produces the complete ProjectionResult
/// </summary>
public class RecommendationEngine
{
    private readonly ILogger<RecommendationEngine> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.RecommendationEngine",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.RecommendationEngine",
        "1.0.0"
    );
    private static readonly Counter<int> RecommendationsCounter = Meter.CreateCounter<int>(
        "recommendations_generated",
        "recommendations",
        "Number of recommendations generated"
    );

    public RecommendationEngine(ILogger<RecommendationEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate final projection result with recommendations
    /// </summary>
    public Task<ProjectionResult> ExecuteAsync(
        AggregatedAnalysis analysis,
        ScenarioSet scenarios,
        int executionTimeMs
    )
    {
        using var activity = ActivitySource.StartActivity("RecommendationGeneration");

        try
        {
            _logger.LogInformation("Generating final recommendations");

            var request = analysis.OriginalRequest;

            // Build input summary
            var inputSummary = BuildInputSummary(request);

            // Build fund recommendations from allocation
            var fundRecommendations = BuildFundRecommendations(
                analysis.FundSelection,
                analysis.HistoricalAnalysis,
                request
            );

            // Generate risk warnings
            var (warnings, warningsAr) = GenerateRiskWarnings(analysis, scenarios);

            // Generate call to action
            var cta = GenerateCallToAction(request);

            // Build metadata
            var metadata = BuildMetadata(analysis, executionTimeMs);

            RecommendationsCounter.Add(1);

            var result = new ProjectionResult
            {
                ProjectionId = $"PROJ{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}",
                InputSummary = inputSummary,
                Scenarios = scenarios,
                RecommendedFunds = fundRecommendations,
                RiskWarnings = warnings,
                RiskWarningsAr = warningsAr,
                CallToAction = cta,
                Metadata = metadata,
            };

            activity?.SetTag("projection_id", result.ProjectionId);
            activity?.SetTag("funds_recommended", fundRecommendations.Count);

            _logger.LogInformation(
                "Projection {ProjectionId} generated with {FundCount} recommended funds",
                result.ProjectionId,
                fundRecommendations.Count
            );

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private ProjectionSummary BuildInputSummary(ProjectionRequest request)
    {
        var horizonText = request.TimeHorizonMonths switch
        {
            12 => "1 year",
            24 => "2 years",
            36 => "3 years",
            60 => "5 years",
            _ => $"{request.TimeHorizonMonths} months",
        };

        var horizonTextAr = request.TimeHorizonMonths switch
        {
            12 => "سنة واحدة",
            24 => "سنتان",
            36 => "3 سنوات",
            60 => "5 سنوات",
            _ => $"{request.TimeHorizonMonths} شهر",
        };

        var riskProfileAr = request.RiskProfile.ToLower() switch
        {
            "conservative" => "متحفظ",
            "moderate" => "متوازن",
            "aggressive" => "جريء",
            _ => request.RiskProfile,
        };

        return new ProjectionSummary
        {
            Amount = request.InvestmentAmount,
            Currency = request.Currency,
            Horizon = horizonText,
            HorizonAr = horizonTextAr,
            RiskProfile = request.RiskProfile,
            RiskProfileAr = riskProfileAr,
            InvestmentType = request.InvestmentType,
            ShariahCompliant = request.ShariahCompliantOnly,
        };
    }

    private List<FundRecommendation> BuildFundRecommendations(
        FundSelectionResult fundSelection,
        HistoricalAnalysis historicalAnalysis,
        ProjectionRequest request
    )
    {
        var recommendations = new List<FundRecommendation>();

        foreach (var allocation in fundSelection.RecommendedAllocation.Take(5))
        {
            var rankedFund = fundSelection.RankedFunds.FirstOrDefault(f =>
                f.FundCode == allocation.FundCode
            );

            var historicalData = historicalAnalysis.FundPerformance.FirstOrDefault(f =>
                f.FundCode == allocation.FundCode
            );

            if (rankedFund != null)
            {
                recommendations.Add(
                    new FundRecommendation
                    {
                        FundCode = rankedFund.FundCode,
                        FundName = rankedFund.FundName,
                        FundNameAr = rankedFund.FundNameAr,
                        FundType = rankedFund.FundType,
                        AllocationPercent = allocation.AllocationPercent,
                        InvestmentAmount = allocation.InvestmentAmount,
                        ExpectedContribution = allocation.ExpectedContribution,
                        ExpectedReturn = rankedFund.ExpectedReturn,
                        Reasons = rankedFund.MatchReasons,
                        CurrentNav = rankedFund.CurrentNav,
                        IsShariahCompliant = historicalData?.IsShariahCompliant ?? false,
                    }
                );
            }
        }

        return recommendations;
    }

    private (List<string> Warnings, List<string> WarningsAr) GenerateRiskWarnings(
        AggregatedAnalysis analysis,
        ScenarioSet scenarios
    )
    {
        var warnings = new List<string>();
        var warningsAr = new List<string>();

        // Standard disclaimer
        warnings.Add(
            "Past performance does not guarantee future results. The projections shown are estimates based on historical data and may not reflect actual returns."
        );
        warningsAr.Add(
            "الأداء السابق لا يضمن النتائج المستقبلية. التوقعات المعروضة هي تقديرات بناءً على البيانات التاريخية وقد لا تعكس العوائد الفعلية."
        );

        // Market volatility warning
        if (analysis.ExpectedVolatility > 20m)
        {
            warnings.Add(
                "This investment carries higher than average volatility. Be prepared for significant short-term fluctuations."
            );
            warningsAr.Add(
                "هذا الاستثمار يحمل تقلبات أعلى من المتوسط. كن مستعداً لتقلبات كبيرة على المدى القصير."
            );
        }

        // Market conditions warning
        if (analysis.MarketAnalysis.MarketSentiment == "Bearish")
        {
            warnings.Add(
                "Current market conditions are challenging. Consider a longer investment horizon or more conservative allocation."
            );
            warningsAr.Add(
                "ظروف السوق الحالية صعبة. فكر في أفق استثماري أطول أو توزيع أكثر تحفظاً."
            );
        }

        // High investment amount warning
        if (analysis.OriginalRequest.InvestmentAmount > 500000)
        {
            warnings.Add(
                "For large investment amounts, we recommend consulting with a financial advisor."
            );
            warningsAr.Add("للمبالغ الكبيرة، ننصح بالتشاور مع مستشار مالي.");
        }

        // Short horizon with aggressive profile warning
        if (
            analysis.OriginalRequest.TimeHorizonMonths < 24
            && analysis.OriginalRequest.RiskProfile.ToLower() == "aggressive"
        )
        {
            warnings.Add(
                "Aggressive investments are better suited for longer time horizons. Consider a more conservative approach for short-term goals."
            );
            warningsAr.Add(
                "الاستثمارات الجريئة تناسب الآفاق الزمنية الأطول. فكر في نهج أكثر تحفظاً للأهداف قصيرة المدى."
            );
        }

        // Conservative scenario warning
        var conservativeReturn = scenarios.Conservative.TotalReturn;
        if (conservativeReturn < 0)
        {
            warnings.Add(
                "In conservative scenarios, there is a possibility of negative returns. Only invest what you can afford to lose."
            );
            warningsAr.Add(
                "في السيناريوهات المتحفظة، هناك احتمال لعوائد سلبية. استثمر فقط ما يمكنك تحمل خسارته."
            );
        }

        // Currency risk (if not SAR)
        if (analysis.OriginalRequest.Currency != "SAR")
        {
            warnings.Add(
                "Currency fluctuations may affect your returns. SAR is pegged to USD, but other currencies may vary."
            );
            warningsAr.Add(
                "تقلبات العملات قد تؤثر على عوائدك. الريال مرتبط بالدولار، لكن العملات الأخرى قد تتغير."
            );
        }

        return (warnings, warningsAr);
    }

    private CallToAction GenerateCallToAction(ProjectionRequest request)
    {
        var projectionId = $"PROJ{DateTime.Now:yyyyMMddHHmmss}";

        return new CallToAction
        {
            PrimaryAction = "Subscribe Now",
            PrimaryActionAr = "اشترك الآن",
            Link = $"/subscribe?projection={projectionId}&amount={request.InvestmentAmount}",
            SecondaryAction = "Save for Later",
            SecondaryActionAr = "احفظ للمراجعة لاحقاً",
            SecondaryLink = $"/projections/save?id={projectionId}",
        };
    }

    private ProjectionMetadata BuildMetadata(AggregatedAnalysis analysis, int executionTimeMs)
    {
        var dataSources = new List<string> { "SNB Capital Mutual Funds API" };

        if (analysis.HistoricalAnalysis.FundsAnalyzed > 0)
            dataSources.Add("Historical Performance Data");

        if (analysis.MarketAnalysis.MarketSentiment != null)
            dataSources.Add("Market Conditions Analysis");

        if (analysis.CustomerContext.IsExistingCustomer)
            dataSources.Add("Customer Portfolio Data");

        return new ProjectionMetadata
        {
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            WorkflowVersion = "1.0.0",
            ExecutionTimeMs = executionTimeMs,
            DataSources = dataSources,
            CustomerId = analysis.CustomerContext.CustomerId,
            UsedHistoricalData = analysis.HistoricalAnalysis.FundsAnalyzed > 0,
            UsedMarketData = true,
        };
    }
}

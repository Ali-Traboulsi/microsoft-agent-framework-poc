using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Builds three projection scenarios with monthly projections
/// </summary>
public sealed class ScenarioBuilder : ExecutorBase<AggregatedAnalysis, ScenarioSet>
{
    public ScenarioBuilder(ILogger<ScenarioBuilder> logger)
        : base(logger) { }

    protected override Task<ScenarioSet> ExecuteCoreAsync(
        AggregatedAnalysis input,
        CancellationToken ct
    )
    {
        var request = input.OriginalRequest;

        var conservativeRate = CalculateRate(input, "Conservative");
        var expectedRate = CalculateRate(input, "Expected");
        var optimisticRate = CalculateRate(input, "Optimistic");

        var conservative = BuildScenario(
            "Conservative",
            "متحفظ",
            80m,
            conservativeRate,
            request,
            "Even in challenging market conditions, you can expect at least this return",
            "حتى في ظروف السوق الصعبة، من المتوقع أن تحقق هذا العائد على الأقل"
        );

        var expected = BuildScenario(
            "Expected",
            "متوقع",
            50m,
            expectedRate,
            request,
            "Most likely return based on historical performance and market conditions",
            "العائد الأكثر احتمالاً بناءً على الأداء التاريخي وظروف السوق"
        );

        var optimistic = BuildScenario(
            "Optimistic",
            "متفائل",
            20m,
            optimisticRate,
            request,
            "Possible return if market conditions are favorable",
            "العائد الممكن في حال تحقق أفضل ظروف السوق"
        );

        var strategyComparison =
            request.InvestmentType == "LumpSum"
                ? BuildStrategyComparison(request, expectedRate)
                : null;

        return Task.FromResult(
            new ScenarioSet
            {
                Conservative = conservative,
                Expected = expected,
                Optimistic = optimistic,
                StrategyComparison = strategyComparison,
            }
        );
    }

    private static decimal CalculateRate(AggregatedAnalysis analysis, string scenario)
    {
        var allocation = analysis.FundSelection.RecommendedAllocation;
        var funds = analysis.HistoricalAnalysis.FundPerformance;

        if (!allocation.Any() || !funds.Any())
            return scenario switch
            {
                "Conservative" => Math.Max(0, analysis.WeightedExpectedReturn - 3),
                "Optimistic" => analysis.WeightedExpectedReturn + 3,
                _ => analysis.WeightedExpectedReturn,
            };

        decimal weightedRate = 0;
        foreach (var alloc in allocation)
        {
            var fund = funds.FirstOrDefault(f => f.FundCode == alloc.FundCode);
            if (fund == null)
                continue;

            var percentile = scenario switch
            {
                "Conservative" => fund.P25Return,
                "Optimistic" => fund.P75Return,
                _ => fund.P50Return,
            };
            weightedRate += percentile * (alloc.AllocationPercent / 100m);
        }

        weightedRate *= analysis.MarketAnalysis.ReturnAdjustmentFactor;
        return scenario == "Conservative" ? Math.Max(0, weightedRate - 1.5m) : weightedRate - 1.5m;
    }

    private static Scenario BuildScenario(
        string type,
        string typeAr,
        decimal confidence,
        decimal rate,
        ProjectionRequest request,
        string desc,
        string descAr
    )
    {
        var amount = request.InvestmentAmount;
        var months = request.TimeHorizonMonths;
        var monthlyRate = rate / 100m / 12m;

        var projections = new List<MonthlyProjection>();
        var currentValue = amount;

        for (int m = 1; m <= months; m++)
        {
            currentValue *= (1 + monthlyRate);
            projections.Add(
                new MonthlyProjection
                {
                    Month = m,
                    Date = DateTime.UtcNow.AddMonths(m),
                    Value = Math.Round(currentValue, 2),
                    CumulativeReturn = Math.Round(currentValue - amount, 2),
                    MonthlyContribution = 0,
                }
            );
        }

        var finalValue = projections.Last().Value;
        var years = months / 12.0;
        var annualized =
            years > 0
                ? (decimal)(Math.Pow((double)(finalValue / amount), 1.0 / years) - 1) * 100
                : rate;

        return new Scenario
        {
            ScenarioType = type,
            ScenarioTypeAr = typeAr,
            Confidence = confidence,
            Description = desc,
            DescriptionAr = descAr,
            AssumedReturnRate = Math.Round(rate, 2),
            InitialInvestment = amount,
            ProjectedValue = Math.Round(finalValue, 2),
            TotalReturn = Math.Round(finalValue - amount, 2),
            AnnualizedReturn = Math.Round(annualized, 2),
            MonthlyProjections = projections,
        };
    }

    private static StrategyComparison BuildStrategyComparison(
        ProjectionRequest request,
        decimal rate
    )
    {
        var amount = request.InvestmentAmount;
        var months = request.TimeHorizonMonths;
        var monthlyRate = rate / 100m / 12m;

        // Lump sum
        var lumpSumFinal = amount * (decimal)Math.Pow((double)(1 + monthlyRate), months);

        // Monthly SIP
        var monthlyAmount = amount / months;
        var sipFinal = 0m;
        for (int m = 0; m < months; m++)
            sipFinal += monthlyAmount * (decimal)Math.Pow((double)(1 + monthlyRate), months - m);

        var lumpSumReturn = lumpSumFinal - amount;
        var sipReturn = sipFinal - amount;
        var advantage =
            lumpSumReturn > sipReturn
                ? (lumpSumReturn - sipReturn) / sipReturn * 100
                : (sipReturn - lumpSumReturn) / lumpSumReturn * 100;

        var recommended = lumpSumReturn > sipReturn ? "LumpSum" : "Monthly";

        return new StrategyComparison
        {
            LumpSum = new StrategyResult
            {
                StrategyName = "Lump Sum",
                TotalInvestment = amount,
                ProjectedValue = Math.Round(lumpSumFinal, 2),
                TotalReturn = Math.Round(lumpSumReturn, 2),
                Benefit = "Immediate full market exposure, potentially higher returns",
                BenefitAr = "تعرض كامل للسوق فوراً، عوائد محتملة أعلى",
            },
            MonthlySip = new StrategyResult
            {
                StrategyName = "Monthly SIP",
                TotalInvestment = amount,
                ProjectedValue = Math.Round(sipFinal, 2),
                TotalReturn = Math.Round(sipReturn, 2),
                Benefit = "Rupee cost averaging, reduces timing risk",
                BenefitAr = "متوسط تكلفة الريال، تقليل مخاطر التوقيت",
            },
            RecommendedStrategy = recommended,
            RecommendationRationale =
                $"{recommended} investment is recommended as it generates {advantage:F1}% higher returns over this period.",
            RecommendationRationaleAr =
                $"ننصح بـ{(recommended == "LumpSum" ? "الاستثمار المباشر" : "الاستثمار الشهري")} لأنه يحقق عوائد أعلى بنسبة {advantage:F1}%.",
        };
    }
}

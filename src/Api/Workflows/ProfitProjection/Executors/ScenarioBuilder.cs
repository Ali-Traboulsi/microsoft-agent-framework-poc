using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Builds three projection scenarios: Conservative, Expected, Optimistic
/// Calculates monthly projections and strategy comparisons
/// </summary>
public class ScenarioBuilder
{
    private readonly ILogger<ScenarioBuilder> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.ScenarioBuilder",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.ScenarioBuilder",
        "1.0.0"
    );
    private static readonly Counter<int> ScenariosBuiltCounter = Meter.CreateCounter<int>(
        "scenarios_built",
        "scenarios",
        "Number of scenario sets built"
    );

    public ScenarioBuilder(ILogger<ScenarioBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Build complete scenario set from aggregated analysis
    /// </summary>
    public Task<ScenarioSet> ExecuteAsync(AggregatedAnalysis analysis)
    {
        using var activity = ActivitySource.StartActivity("ScenarioBuilding");

        try
        {
            _logger.LogInformation(
                "Building projection scenarios for {Amount} {Currency} over {Months} months",
                analysis.OriginalRequest.InvestmentAmount,
                analysis.OriginalRequest.Currency,
                analysis.OriginalRequest.TimeHorizonMonths
            );

            var request = analysis.OriginalRequest;

            // Calculate return rates for each scenario
            var conservativeRate = CalculateConservativeRate(analysis);
            var expectedRate = CalculateExpectedRate(analysis);
            var optimisticRate = CalculateOptimisticRate(analysis);

            activity?.SetTag("conservative_rate", conservativeRate);
            activity?.SetTag("expected_rate", expectedRate);
            activity?.SetTag("optimistic_rate", optimisticRate);

            // Build each scenario
            var conservative = BuildScenario(
                "Conservative",
                "متحفظ",
                80m,
                conservativeRate,
                request,
                "حتى في ظروف السوق الصعبة، من المتوقع أن تحقق هذا العائد على الأقل",
                "Even in challenging market conditions, you can expect at least this return"
            );

            var expected = BuildScenario(
                "Expected",
                "متوقع",
                50m,
                expectedRate,
                request,
                "العائد الأكثر احتمالاً بناءً على الأداء التاريخي وظروف السوق",
                "Most likely return based on historical performance and market conditions"
            );

            var optimistic = BuildScenario(
                "Optimistic",
                "متفائل",
                20m,
                optimisticRate,
                request,
                "العائد الممكن في حال تحقق أفضل ظروف السوق",
                "Possible return if market conditions are favorable"
            );

            // Build strategy comparison if investment type is LumpSum
            StrategyComparison? strategyComparison = null;
            if (request.InvestmentType == "LumpSum")
            {
                strategyComparison = BuildStrategyComparison(request, expectedRate);
            }

            ScenariosBuiltCounter.Add(1);

            var result = new ScenarioSet
            {
                Conservative = conservative,
                Expected = expected,
                Optimistic = optimistic,
                StrategyComparison = strategyComparison,
            };

            _logger.LogInformation(
                "Scenarios built: Conservative={C}, Expected={E}, Optimistic={O}",
                conservative.ProjectedValue,
                expected.ProjectedValue,
                optimistic.ProjectedValue
            );

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building scenarios");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private decimal CalculateConservativeRate(AggregatedAnalysis analysis)
    {
        // Use P25 (25th percentile) from historical data or adjusted expected return
        var fundPerformance = analysis.HistoricalAnalysis.FundPerformance;

        if (fundPerformance.Any())
        {
            // Weighted average of P25 returns based on allocation
            var allocation = analysis.FundSelection.RecommendedAllocation;
            if (allocation.Any())
            {
                decimal weightedP25 = 0m;
                foreach (var alloc in allocation)
                {
                    var fund = fundPerformance.FirstOrDefault(f => f.FundCode == alloc.FundCode);
                    if (fund != null)
                    {
                        weightedP25 += fund.P25Return * (alloc.AllocationPercent / 100m);
                    }
                }
                // Apply market adjustment
                weightedP25 *= analysis.MarketAnalysis.ReturnAdjustmentFactor;
                return Math.Max(0, weightedP25 - 1.5m); // Subtract fees
            }
        }

        // Fallback: Expected - volatility buffer
        return Math.Max(0, analysis.WeightedExpectedReturn - (analysis.ExpectedVolatility * 0.5m));
    }

    private decimal CalculateExpectedRate(AggregatedAnalysis analysis)
    {
        // Use the weighted expected return from aggregation
        return analysis.WeightedExpectedReturn;
    }

    private decimal CalculateOptimisticRate(AggregatedAnalysis analysis)
    {
        // Use P75 (75th percentile) from historical data
        var fundPerformance = analysis.HistoricalAnalysis.FundPerformance;

        if (fundPerformance.Any())
        {
            var allocation = analysis.FundSelection.RecommendedAllocation;
            if (allocation.Any())
            {
                decimal weightedP75 = 0m;
                foreach (var alloc in allocation)
                {
                    var fund = fundPerformance.FirstOrDefault(f => f.FundCode == alloc.FundCode);
                    if (fund != null)
                    {
                        weightedP75 += fund.P75Return * (alloc.AllocationPercent / 100m);
                    }
                }
                // Apply market adjustment
                weightedP75 *= analysis.MarketAnalysis.ReturnAdjustmentFactor;
                return weightedP75 - 1.5m; // Subtract fees
            }
        }

        // Fallback: Expected + volatility buffer
        return analysis.WeightedExpectedReturn + (analysis.ExpectedVolatility * 0.5m);
    }

    private Scenario BuildScenario(
        string scenarioType,
        string scenarioTypeAr,
        decimal confidence,
        decimal annualReturnRate,
        ProjectionRequest request,
        string descriptionAr,
        string description
    )
    {
        var months = request.TimeHorizonMonths;
        var initialAmount = request.InvestmentAmount;
        var isMonthly = request.InvestmentType == "Monthly";
        var monthlyAmount = request.MonthlyAmount ?? (initialAmount / months);

        // Calculate projections
        List<MonthlyProjection> monthlyProjections;
        decimal finalValue;

        if (isMonthly)
        {
            (finalValue, monthlyProjections) = CalculateSipProjection(
                monthlyAmount,
                months,
                annualReturnRate
            );
        }
        else
        {
            (finalValue, monthlyProjections) = CalculateLumpSumProjection(
                initialAmount,
                months,
                annualReturnRate
            );
        }

        var totalInvested = isMonthly ? monthlyAmount * months : initialAmount;
        var totalReturn = finalValue - totalInvested;
        var annualized = CalculateAnnualizedReturn(totalInvested, finalValue, months);

        return new Scenario
        {
            ScenarioType = scenarioType,
            ScenarioTypeAr = scenarioTypeAr,
            Confidence = confidence,
            Description = description,
            DescriptionAr = descriptionAr,
            AssumedReturnRate = Math.Round(annualReturnRate, 2),
            InitialInvestment = totalInvested,
            ProjectedValue = Math.Round(finalValue, 2),
            TotalReturn = Math.Round(totalReturn, 2),
            AnnualizedReturn = Math.Round(annualized, 2),
            MonthlyProjections = monthlyProjections,
        };
    }

    private (decimal FinalValue, List<MonthlyProjection> Projections) CalculateLumpSumProjection(
        decimal principal,
        int months,
        decimal annualRate
    )
    {
        var projections = new List<MonthlyProjection>();
        var monthlyRate = annualRate / 100m / 12m;
        var currentValue = principal;

        for (int month = 1; month <= months; month++)
        {
            currentValue *= (1 + monthlyRate);

            projections.Add(
                new MonthlyProjection
                {
                    Month = month,
                    Date = DateTime.UtcNow.AddMonths(month),
                    Value = Math.Round(currentValue, 2),
                    CumulativeReturn = Math.Round(currentValue - principal, 2),
                    MonthlyContribution = 0,
                }
            );
        }

        return (currentValue, projections);
    }

    private (decimal FinalValue, List<MonthlyProjection> Projections) CalculateSipProjection(
        decimal monthlyAmount,
        int months,
        decimal annualRate
    )
    {
        var projections = new List<MonthlyProjection>();
        var monthlyRate = annualRate / 100m / 12m;
        var currentValue = 0m;
        var totalInvested = 0m;

        for (int month = 1; month <= months; month++)
        {
            // Add monthly contribution at start of month
            currentValue += monthlyAmount;
            totalInvested += monthlyAmount;

            // Apply growth
            currentValue *= (1 + monthlyRate);

            projections.Add(
                new MonthlyProjection
                {
                    Month = month,
                    Date = DateTime.UtcNow.AddMonths(month),
                    Value = Math.Round(currentValue, 2),
                    CumulativeReturn = Math.Round(currentValue - totalInvested, 2),
                    MonthlyContribution = monthlyAmount,
                }
            );
        }

        return (currentValue, projections);
    }

    private StrategyComparison BuildStrategyComparison(
        ProjectionRequest request,
        decimal expectedRate
    )
    {
        // Calculate lump sum result
        var (lumpSumFinal, _) = CalculateLumpSumProjection(
            request.InvestmentAmount,
            request.TimeHorizonMonths,
            expectedRate
        );

        // Calculate SIP result (same total, spread monthly)
        var monthlyAmount = request.InvestmentAmount / request.TimeHorizonMonths;
        var (sipFinal, _) = CalculateSipProjection(
            monthlyAmount,
            request.TimeHorizonMonths,
            expectedRate
        );

        // Determine recommendation
        var lumpSumBetter = lumpSumFinal > sipFinal;
        var difference = Math.Abs(lumpSumFinal - sipFinal);
        var differencePercent = (difference / request.InvestmentAmount) * 100m;

        string recommendation;
        string rationaleEn;
        string rationaleAr;

        if (lumpSumBetter && differencePercent > 5m)
        {
            recommendation = "LumpSum";
            rationaleEn =
                $"Lump sum investment is recommended as it generates {differencePercent:F1}% higher returns over this period due to longer market exposure.";
            rationaleAr =
                $"ننصح بالاستثمار المباشر لأنه يحقق عوائد أعلى بنسبة {differencePercent:F1}% خلال هذه الفترة بسبب التعرض الأطول للسوق.";
        }
        else if (!lumpSumBetter && differencePercent > 5m)
        {
            recommendation = "Monthly";
            rationaleEn =
                "Monthly SIP is recommended for risk averaging and disciplined investing.";
            rationaleAr = "ننصح بالاستثمار الشهري لتقليل المخاطر والانضباط في الاستثمار.";
        }
        else
        {
            recommendation = "Either";
            rationaleEn =
                "Both strategies yield similar results. Choose based on cash flow preference.";
            rationaleAr =
                "كلا الاستراتيجيتين تحققان نتائج متقاربة. اختر بناءً على تفضيلاتك في التدفق النقدي.";
        }

        return new StrategyComparison
        {
            LumpSum = new StrategyResult
            {
                StrategyName = "Lump Sum",
                TotalInvestment = request.InvestmentAmount,
                ProjectedValue = Math.Round(lumpSumFinal, 2),
                TotalReturn = Math.Round(lumpSumFinal - request.InvestmentAmount, 2),
                Benefit = "Immediate full market exposure, potentially higher returns",
                BenefitAr = "تعرض كامل للسوق فوراً، عوائد محتملة أعلى",
            },
            MonthlySip = new StrategyResult
            {
                StrategyName = "Monthly SIP",
                TotalInvestment = request.InvestmentAmount,
                ProjectedValue = Math.Round(sipFinal, 2),
                TotalReturn = Math.Round(sipFinal - request.InvestmentAmount, 2),
                Benefit = "Rupee cost averaging, reduces timing risk",
                BenefitAr = "متوسط تكلفة الريال، تقليل مخاطر التوقيت",
            },
            RecommendedStrategy = recommendation,
            RecommendationRationale = rationaleEn,
            RecommendationRationaleAr = rationaleAr,
        };
    }

    private decimal CalculateAnnualizedReturn(decimal invested, decimal finalValue, int months)
    {
        if (invested <= 0 || months <= 0)
            return 0;

        var years = months / 12m;
        if (years < 1)
            years = months / 12m; // Pro-rate for periods less than a year

        // CAGR formula: (FV/PV)^(1/n) - 1
        var ratio = (double)(finalValue / invested);
        var power = 1.0 / (double)years;
        var cagr = (decimal)(Math.Pow(ratio, power) - 1) * 100m;

        return cagr;
    }
}

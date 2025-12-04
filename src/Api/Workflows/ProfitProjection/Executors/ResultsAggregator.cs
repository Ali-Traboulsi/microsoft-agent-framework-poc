using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Aggregates results from parallel analysis executors (Fan-in)
/// Combines Historical, Market, and Fund Selection analyses
/// </summary>
public class ResultsAggregator
{
    private readonly ILogger<ResultsAggregator> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.ResultsAggregator",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.ResultsAggregator",
        "1.0.0"
    );

    public ResultsAggregator(ILogger<ResultsAggregator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Aggregate parallel analysis results into a unified model
    /// </summary>
    public Task<AggregatedAnalysis> ExecuteAsync(
        ProjectionRequest request,
        CustomerContext customerContext,
        HistoricalAnalysis historicalAnalysis,
        MarketAnalysis marketAnalysis,
        FundSelectionResult fundSelection
    )
    {
        using var activity = ActivitySource.StartActivity("ResultsAggregation");

        try
        {
            _logger.LogInformation("Aggregating analysis results");

            // Calculate weighted expected return
            var weightedReturn = CalculateWeightedExpectedReturn(
                fundSelection,
                historicalAnalysis,
                marketAnalysis
            );

            // Calculate expected volatility
            var expectedVolatility = CalculateExpectedVolatility(
                fundSelection,
                historicalAnalysis,
                marketAnalysis
            );

            // Calculate confidence level based on data quality
            var confidenceLevel = CalculateConfidenceLevel(
                historicalAnalysis,
                marketAnalysis,
                fundSelection
            );

            activity?.SetTag("weighted_return", weightedReturn);
            activity?.SetTag("expected_volatility", expectedVolatility);
            activity?.SetTag("confidence_level", confidenceLevel);

            var result = new AggregatedAnalysis
            {
                AggregatedAt = DateTime.UtcNow,
                OriginalRequest = request,
                CustomerContext = customerContext,
                HistoricalAnalysis = historicalAnalysis,
                MarketAnalysis = marketAnalysis,
                FundSelection = fundSelection,
                WeightedExpectedReturn = Math.Round(weightedReturn, 2),
                ExpectedVolatility = Math.Round(expectedVolatility, 2),
                ConfidenceLevel = Math.Round(confidenceLevel, 2),
            };

            _logger.LogInformation(
                "Aggregation complete: Expected Return={Return}%, Volatility={Vol}%, Confidence={Conf}%",
                weightedReturn,
                expectedVolatility,
                confidenceLevel
            );

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during results aggregation");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private decimal CalculateWeightedExpectedReturn(
        FundSelectionResult fundSelection,
        HistoricalAnalysis historicalAnalysis,
        MarketAnalysis marketAnalysis
    )
    {
        if (!fundSelection.RecommendedAllocation.Any())
        {
            // Fallback to category average, with safe handling for empty collections
            if (historicalAnalysis.RiskCategoryStats.Values.Any())
            {
                return historicalAnalysis.RiskCategoryStats.Values.Average(r => r.AverageReturn);
            }

            // If no category stats, try to get average from fund performance
            if (historicalAnalysis.FundPerformance.Any())
            {
                return historicalAnalysis.FundPerformance.Average(f => f.OneYearReturn);
            }

            // Ultimate fallback: conservative estimate of 5% return
            _logger.LogWarning(
                "No fund data available for return calculation, using conservative estimate"
            );
            return 5m;
        }

        // Calculate weighted return from allocation
        decimal weightedReturn = 0m;

        foreach (var allocation in fundSelection.RecommendedAllocation)
        {
            // Find the fund's historical data
            var fundData = historicalAnalysis.FundPerformance.FirstOrDefault(f =>
                f.FundCode == allocation.FundCode
            );

            if (fundData != null)
            {
                // Use median (P50) return as expected return
                var fundReturn =
                    fundData.P50Return > 0 ? fundData.P50Return : fundData.OneYearReturn;
                weightedReturn += fundReturn * (allocation.AllocationPercent / 100m);
            }
            else
            {
                // Find matching ranked fund
                var rankedFund = fundSelection.RankedFunds.FirstOrDefault(f =>
                    f.FundCode == allocation.FundCode
                );

                if (rankedFund != null)
                {
                    weightedReturn +=
                        rankedFund.ExpectedReturn * (allocation.AllocationPercent / 100m);
                }
            }
        }

        // Apply market condition adjustment
        weightedReturn *= marketAnalysis.ReturnAdjustmentFactor;

        // Subtract estimated fees (approximately 1.5% average)
        weightedReturn -= 1.5m;

        return Math.Max(0, weightedReturn);
    }

    private decimal CalculateExpectedVolatility(
        FundSelectionResult fundSelection,
        HistoricalAnalysis historicalAnalysis,
        MarketAnalysis marketAnalysis
    )
    {
        if (!fundSelection.RecommendedAllocation.Any())
        {
            return 15m; // Default moderate volatility
        }

        decimal weightedVolatility = 0m;

        foreach (var allocation in fundSelection.RecommendedAllocation)
        {
            var fundData = historicalAnalysis.FundPerformance.FirstOrDefault(f =>
                f.FundCode == allocation.FundCode
            );

            if (fundData != null)
            {
                weightedVolatility +=
                    fundData.StandardDeviation * (allocation.AllocationPercent / 100m);
            }
            else
            {
                weightedVolatility += 15m * (allocation.AllocationPercent / 100m); // Default
            }
        }

        // Apply diversification benefit (correlation < 1)
        // Reduce volatility by ~20% for diversified portfolio
        var diversificationFactor = 0.8m;
        weightedVolatility *= diversificationFactor;

        // Apply market condition adjustment
        weightedVolatility *= marketAnalysis.RiskAdjustmentFactor;

        return weightedVolatility;
    }

    private decimal CalculateConfidenceLevel(
        HistoricalAnalysis historicalAnalysis,
        MarketAnalysis marketAnalysis,
        FundSelectionResult fundSelection
    )
    {
        decimal confidence = 50m; // Base confidence

        // More funds analyzed = higher confidence
        if (historicalAnalysis.FundsAnalyzed >= 10)
            confidence += 15m;
        else if (historicalAnalysis.FundsAnalyzed >= 5)
            confidence += 10m;

        // Good fund matches = higher confidence
        if (fundSelection.FundsMatched >= 5)
            confidence += 10m;
        else if (fundSelection.FundsMatched >= 3)
            confidence += 5m;

        // Neutral market = higher confidence (less uncertainty)
        if (marketAnalysis.MarketSentiment == "Neutral")
            confidence += 10m;
        else if (Math.Abs(marketAnalysis.MarketConditionScore) < 0.3m)
            confidence += 5m;

        // Strong Sharpe ratios = higher confidence
        var avgSharpe = historicalAnalysis
            .RiskCategoryStats.Values.DefaultIfEmpty(new RiskCategoryStats())
            .Average(r => r.AverageSharpeRatio);
        if (avgSharpe > 0.7m)
            confidence += 10m;

        return Math.Min(95m, confidence);
    }
}

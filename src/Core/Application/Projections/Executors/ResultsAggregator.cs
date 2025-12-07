using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Input for results aggregation
/// </summary>
public sealed record AggregationInput(
    ProjectionRequest Request,
    CustomerContext Customer,
    HistoricalAnalysis Historical,
    MarketAnalysis Market,
    FundSelectionResult FundSelection
);

/// <summary>
/// Aggregates results from parallel analysis steps
/// </summary>
public sealed class ResultsAggregator : ExecutorBase<AggregationInput, AggregatedAnalysis>
{
    public ResultsAggregator(ILogger<ResultsAggregator> logger)
        : base(logger) { }

    protected override Task<AggregatedAnalysis> ExecuteCoreAsync(
        AggregationInput input,
        CancellationToken ct
    )
    {
        var allocation = input.FundSelection.RecommendedAllocation;
        var fundPerformance = input.Historical.FundPerformance;

        // Calculate weighted expected return
        decimal weightedReturn = 0;
        decimal weightedVolatility = 0;

        foreach (var alloc in allocation)
        {
            var fund = fundPerformance.FirstOrDefault(f => f.FundCode == alloc.FundCode);
            if (fund != null)
            {
                var weight = alloc.AllocationPercent / 100m;
                weightedReturn += fund.OneYearReturn * weight;
                weightedVolatility += fund.StandardDeviation * weight;
            }
        }

        // Apply market adjustment
        weightedReturn *= input.Market.ReturnAdjustmentFactor;
        weightedVolatility *= input.Market.RiskAdjustmentFactor;

        // Adjust for customer profile if existing
        if (input.Customer.IsExistingCustomer && input.Customer.Behavior != null)
        {
            var histReturns = input.Customer.Behavior.HistoricalReturns;
            if (histReturns > 0)
                weightedReturn = (weightedReturn + histReturns) / 2;
        }

        // Calculate confidence based on data quality
        var confidence = CalculateConfidence(input);

        return Task.FromResult(
            new AggregatedAnalysis
            {
                OriginalRequest = input.Request,
                CustomerContext = input.Customer,
                HistoricalAnalysis = input.Historical,
                MarketAnalysis = input.Market,
                FundSelection = input.FundSelection,
                WeightedExpectedReturn = Math.Round(weightedReturn, 2),
                ExpectedVolatility = Math.Round(weightedVolatility, 2),
                ConfidenceLevel = confidence,
            }
        );
    }

    private static decimal CalculateConfidence(AggregationInput input)
    {
        decimal confidence = 50;

        // More funds = more confidence
        if (input.Historical.FundsAnalyzed >= 10)
            confidence += 15;
        else if (input.Historical.FundsAnalyzed >= 5)
            confidence += 10;

        // Existing customer = more data
        if (input.Customer.IsExistingCustomer)
            confidence += 10;

        // Neutral market = more predictable
        if (input.Market.MarketSentiment == "Neutral")
            confidence += 5;

        // Time horizon
        if (input.Request.TimeHorizonMonths >= 36)
            confidence += 10;
        else if (input.Request.TimeHorizonMonths >= 12)
            confidence += 5;

        return Math.Min(90, confidence);
    }
}

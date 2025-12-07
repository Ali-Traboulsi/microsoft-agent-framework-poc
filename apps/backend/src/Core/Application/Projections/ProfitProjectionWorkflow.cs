using System.Diagnostics;
using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Application.Projections.Executors;
using AgentFrameworkQuickStart.Core.Domain.Projections;

namespace AgentFrameworkQuickStart.Core.Application.Projections;

/// <summary>
/// Profit Projection Workflow - احتساب الأرباح التقديرية
/// Orchestrates the complete projection process with parallel execution
/// </summary>
public sealed class ProfitProjectionWorkflow : WorkflowBase<ProjectionRequest, ProjectionResult>
{
    private readonly CustomerContextExecutor _customerContext;
    private readonly HistoricalAnalyzer _historicalAnalyzer;
    private readonly MarketConditionsAnalyzer _marketAnalyzer;
    private readonly FundSelectionAnalyzer _fundSelector;
    private readonly ResultsAggregator _aggregator;
    private readonly ScenarioBuilder _scenarioBuilder;
    private readonly RecommendationEngine _recommendationEngine;

    public ProfitProjectionWorkflow(
        CustomerContextExecutor customerContext,
        HistoricalAnalyzer historicalAnalyzer,
        MarketConditionsAnalyzer marketAnalyzer,
        FundSelectionAnalyzer fundSelector,
        ResultsAggregator aggregator,
        ScenarioBuilder scenarioBuilder,
        RecommendationEngine recommendationEngine,
        ILogger<ProfitProjectionWorkflow> logger
    )
        : base(logger)
    {
        _customerContext = customerContext;
        _historicalAnalyzer = historicalAnalyzer;
        _marketAnalyzer = marketAnalyzer;
        _fundSelector = fundSelector;
        _aggregator = aggregator;
        _scenarioBuilder = scenarioBuilder;
        _recommendationEngine = recommendationEngine;
    }

    protected override void ValidateRequest(ProjectionRequest request) => request.Validate();

    protected override void AddRequestTags(Activity? activity, ProjectionRequest request)
    {
        activity?.SetTag("investment_amount", request.InvestmentAmount);
        activity?.SetTag("currency", request.Currency);
        activity?.SetTag("time_horizon_months", request.TimeHorizonMonths);
        activity?.SetTag("risk_profile", request.RiskProfile);
        activity?.SetTag("shariah_compliant", request.ShariahCompliantOnly);
    }

    protected override void AddResultTags(Activity? activity, ProjectionResult result)
    {
        activity?.SetTag("projection_id", result.ProjectionId);
        activity?.SetTag("funds_recommended", result.RecommendedFunds.Count);
        activity?.SetTag("expected_return", result.Scenarios.Expected.TotalReturn);
    }

    protected override async Task<ProjectionResult> ExecuteCoreAsync(
        ProjectionRequest request,
        CancellationToken ct
    )
    {
        var stopwatch = Stopwatch.StartNew();

        // Step 1: Customer Context (conditional)
        var customerContext = await _customerContext.ExecuteAsync(request, ct);

        // Step 2: Parallel Analysis (Fan-out)
        var cif = request.CustomerId ?? "100000000001";
        var historicalInput = new HistoricalAnalysisInput(request, cif);

        var historicalTask = _historicalAnalyzer.ExecuteAsync(historicalInput, ct);
        var marketTask = _marketAnalyzer.ExecuteAsync(request, ct);

        await Task.WhenAll(historicalTask, marketTask);

        var historicalAnalysis = await historicalTask;
        var marketAnalysis = await marketTask;

        // Step 3: Fund Selection (depends on historical)
        var fundSelectionInput = new FundSelectionInput(request, historicalAnalysis);
        var fundSelection = await _fundSelector.ExecuteAsync(fundSelectionInput, ct);

        // Step 4: Results Aggregation (Fan-in)
        var aggregationInput = new AggregationInput(
            request,
            customerContext,
            historicalAnalysis,
            marketAnalysis,
            fundSelection
        );
        var aggregated = await _aggregator.ExecuteAsync(aggregationInput, ct);

        // Step 5: Build Scenarios
        var scenarios = await _scenarioBuilder.ExecuteAsync(aggregated, ct);

        // Step 6: Generate Recommendations
        var recommendationInput = new RecommendationInput(aggregated, scenarios);
        var result = await _recommendationEngine.ExecuteAsync(recommendationInput, ct);

        stopwatch.Stop();

        // Update metadata with execution time
        return result with
        {
            Metadata = result.Metadata with { ExecutionTimeMs = stopwatch.ElapsedMilliseconds },
        };
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;

/// <summary>
/// Profit Projection Workflow - احتساب الأرباح التقديرية
///
/// Orchestrates the complete profit projection process:
/// 1. Customer Context (conditional) - Enrich with existing customer data
/// 2. Parallel Analysis (fan-out) - Historical, Market, Fund Selection
/// 3. Results Aggregation (fan-in) - Combine parallel results
/// 4. Scenario Building - Conservative, Expected, Optimistic scenarios
/// 5. Recommendation Engine - Final recommendations and CTA
///
/// Features:
/// - Parallel execution for performance
/// - Full OpenTelemetry observability
/// - Arabic/English bilingual output
/// - Integration with SNB Capital APIs
/// </summary>
public class ProfitProjectionWorkflow
{
    private readonly CustomerContextExecutor _customerContextExecutor;
    private readonly HistoricalAnalyzer _historicalAnalyzer;
    private readonly MarketConditionsAnalyzer _marketConditionsAnalyzer;
    private readonly FundSelectionAnalyzer _fundSelectionAnalyzer;
    private readonly ResultsAggregator _resultsAggregator;
    private readonly ScenarioBuilder _scenarioBuilder;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly ILogger<ProfitProjectionWorkflow> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.Workflow",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.Workflow",
        "1.0.0"
    );
    private static readonly Counter<int> ProjectionsCounter = Meter.CreateCounter<int>(
        "projections_completed",
        "projections",
        "Number of projections completed"
    );
    private static readonly Histogram<double> ExecutionTimeHistogram =
        Meter.CreateHistogram<double>(
            "projection_execution_time_ms",
            "ms",
            "Workflow execution time in milliseconds"
        );

    public ProfitProjectionWorkflow(
        CustomerContextExecutor customerContextExecutor,
        HistoricalAnalyzer historicalAnalyzer,
        MarketConditionsAnalyzer marketConditionsAnalyzer,
        FundSelectionAnalyzer fundSelectionAnalyzer,
        ResultsAggregator resultsAggregator,
        ScenarioBuilder scenarioBuilder,
        RecommendationEngine recommendationEngine,
        ILogger<ProfitProjectionWorkflow> logger
    )
    {
        _customerContextExecutor = customerContextExecutor;
        _historicalAnalyzer = historicalAnalyzer;
        _marketConditionsAnalyzer = marketConditionsAnalyzer;
        _fundSelectionAnalyzer = fundSelectionAnalyzer;
        _resultsAggregator = resultsAggregator;
        _scenarioBuilder = scenarioBuilder;
        _recommendationEngine = recommendationEngine;
        _logger = logger;
    }

    /// <summary>
    /// Execute the complete profit projection workflow
    /// </summary>
    /// <param name="request">Projection request parameters</param>
    /// <returns>Complete projection result with scenarios and recommendations</returns>
    public async Task<ProjectionResult> ExecuteAsync(ProjectionRequest request)
    {
        using var activity = ActivitySource.StartActivity("ProfitProjectionWorkflow");
        var stopwatch = Stopwatch.StartNew();

        activity?.SetTag("investment_amount", request.InvestmentAmount);
        activity?.SetTag("currency", request.Currency);
        activity?.SetTag("time_horizon_months", request.TimeHorizonMonths);
        activity?.SetTag("risk_profile", request.RiskProfile);
        activity?.SetTag("investment_type", request.InvestmentType);
        activity?.SetTag("has_customer_id", !string.IsNullOrEmpty(request.CustomerId));
        activity?.SetTag("shariah_compliant", request.ShariahCompliantOnly);

        try
        {
            // Validate request
            ValidateRequest(request);

            // Step 1: Customer Context (conditional enrichment)
            _logger.LogDebug("Step 1: Fetching customer context");
            var customerContext = await _customerContextExecutor.ExecuteAsync(request);
            activity?.SetTag("is_existing_customer", customerContext.IsExistingCustomer);

            // Step 2: Parallel Analysis (Fan-out)
            // Execute Historical, Market, and Fund Selection analyses concurrently
            _logger.LogDebug("Step 2: Running parallel analysis");

            var cif = request.CustomerId ?? "100000000001";

            var historicalTask = _historicalAnalyzer.ExecuteAsync(request, cif);
            var marketTask = _marketConditionsAnalyzer.ExecuteAsync(request);

            // Wait for historical first as fund selection depends on it
            var historicalAnalysis = await historicalTask;
            var marketAnalysis = await marketTask;

            // Fund selection can use historical data for better matching
            var fundSelection = await _fundSelectionAnalyzer.ExecuteAsync(
                request,
                historicalAnalysis,
                cif
            );

            activity?.SetTag("funds_analyzed", historicalAnalysis.FundsAnalyzed);
            activity?.SetTag("funds_selected", fundSelection.FundsMatched);
            activity?.SetTag("market_sentiment", marketAnalysis.MarketSentiment);

            // Step 3: Results Aggregation (Fan-in)
            _logger.LogDebug("Step 3: Aggregating results");
            var aggregatedAnalysis = await _resultsAggregator.ExecuteAsync(
                request,
                customerContext,
                historicalAnalysis,
                marketAnalysis,
                fundSelection
            );

            activity?.SetTag("expected_return", aggregatedAnalysis.WeightedExpectedReturn);
            activity?.SetTag("expected_volatility", aggregatedAnalysis.ExpectedVolatility);

            // Step 4: Build Scenarios
            _logger.LogDebug("Step 4: Building scenarios");
            var scenarios = await _scenarioBuilder.ExecuteAsync(aggregatedAnalysis);

            activity?.SetTag("conservative_value", scenarios.Conservative.ProjectedValue);
            activity?.SetTag("expected_value", scenarios.Expected.ProjectedValue);
            activity?.SetTag("optimistic_value", scenarios.Optimistic.ProjectedValue);

            // Step 5: Generate Recommendations
            _logger.LogDebug("Step 5: Generating recommendations");
            stopwatch.Stop();
            var executionTimeMs = (int)stopwatch.ElapsedMilliseconds;

            var result = await _recommendationEngine.ExecuteAsync(
                aggregatedAnalysis,
                scenarios,
                executionTimeMs
            );

            // Record metrics
            ProjectionsCounter.Add(1);
            ExecutionTimeHistogram.Record(executionTimeMs);

            activity?.SetTag("projection_id", result.ProjectionId);
            activity?.SetTag("execution_time_ms", executionTimeMs);

            _logger.LogInformation(
                "Profit projection completed: {ProjectionId}, Expected value: {Value} {Currency}, Execution time: {Time}ms",
                result.ProjectionId,
                result.Scenarios.Expected.ProjectedValue,
                request.Currency,
                executionTimeMs
            );

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Profit projection workflow failed after {Time}ms",
                stopwatch.ElapsedMilliseconds
            );
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Validate the projection request
    /// </summary>
    private void ValidateRequest(ProjectionRequest request)
    {
        var errors = new List<string>();

        if (request.InvestmentAmount <= 0)
            errors.Add("Investment amount must be greater than zero");

        if (request.InvestmentAmount < 1000)
            errors.Add("Minimum investment amount is 1,000 SAR");

        if (request.InvestmentAmount > 100_000_000)
            errors.Add("Maximum investment amount is 100,000,000 SAR");

        if (request.TimeHorizonMonths < 1)
            errors.Add("Time horizon must be at least 1 month");

        if (request.TimeHorizonMonths > 360)
            errors.Add("Time horizon cannot exceed 30 years (360 months)");

        var validRiskProfiles = new[] { "Conservative", "Moderate", "Aggressive" };
        if (!validRiskProfiles.Contains(request.RiskProfile, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Risk profile must be one of: {string.Join(", ", validRiskProfiles)}");

        var validInvestmentTypes = new[] { "LumpSum", "Monthly" };
        if (
            !validInvestmentTypes.Contains(request.InvestmentType, StringComparer.OrdinalIgnoreCase)
        )
            errors.Add(
                $"Investment type must be one of: {string.Join(", ", validInvestmentTypes)}"
            );

        if (request.InvestmentType == "Monthly" && (request.MonthlyAmount ?? 0) <= 0)
            errors.Add("Monthly amount is required for Monthly investment type");

        if (errors.Any())
        {
            var errorMessage = string.Join("; ", errors);
            _logger.LogWarning("Projection request validation failed: {Errors}", errorMessage);
            throw new ArgumentException(errorMessage);
        }
    }
}

/// <summary>
/// Extension methods for registering Profit Projection Workflow services
/// </summary>
public static class ProfitProjectionWorkflowExtensions
{
    /// <summary>
    /// Add Profit Projection Workflow services to the DI container
    /// </summary>
    public static IServiceCollection AddProfitProjectionWorkflow(this IServiceCollection services)
    {
        // Register executors
        services.AddScoped<CustomerContextExecutor>();
        services.AddScoped<HistoricalAnalyzer>();
        services.AddScoped<MarketConditionsAnalyzer>();
        services.AddScoped<FundSelectionAnalyzer>();
        services.AddScoped<ResultsAggregator>();
        services.AddScoped<ScenarioBuilder>();
        services.AddScoped<RecommendationEngine>();

        // Register the workflow
        services.AddScoped<ProfitProjectionWorkflow>();

        return services;
    }
}

using System.Diagnostics;
using System.Threading.Channels;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;

/// <summary>
/// Extension methods for ProfitProjectionWorkflow to support progress streaming
/// </summary>
public static class ProfitProjectionWorkflowProgressExtensions
{
    /// <summary>
    /// Workflow step definitions for progress tracking
    /// </summary>
    public static readonly (string Id, string Name, string NameAr)[] WorkflowSteps =
    [
        ("CustomerContext", "Fetching customer portfolio data", "جاري استرجاع بيانات المحفظة"),
        (
            "HistoricalAnalysis",
            "Analyzing historical fund performance",
            "جاري تحليل أداء الصناديق التاريخي"
        ),
        ("MarketAnalysis", "Evaluating current market conditions", "جاري تقييم ظروف السوق الحالية"),
        (
            "FundSelection",
            "Selecting optimal funds for your profile",
            "جاري اختيار الصناديق المثالية لملفك"
        ),
        ("ResultsAggregation", "Aggregating analysis results", "جاري تجميع نتائج التحليل"),
        ("ScenarioBuilding", "Building projection scenarios", "جاري بناء سيناريوهات الإسقاط"),
        (
            "Recommendations",
            "Generating personalized recommendations",
            "جاري إنشاء التوصيات المخصصة"
        ),
    ];

    /// <summary>
    /// Create a progress event for a workflow step
    /// </summary>
    public static WorkflowProgressEvent CreateProgressEvent(
        int stepIndex,
        bool isCompleted,
        long? durationMs = null,
        string? details = null
    )
    {
        var step = WorkflowSteps[stepIndex];
        return new WorkflowProgressEvent
        {
            StepId = step.Id,
            StepName = step.Name,
            StepNameAr = step.NameAr,
            StepNumber = stepIndex + 1,
            TotalSteps = WorkflowSteps.Length,
            IsCompleted = isCompleted,
            DurationMs = durationMs,
            Details = details,
        };
    }
}

/// <summary>
/// Streaming version of Profit Projection Workflow with real-time progress updates
/// </summary>
public class StreamingProfitProjectionWorkflow
{
    private readonly CustomerContextExecutor _customerContextExecutor;
    private readonly HistoricalAnalyzer _historicalAnalyzer;
    private readonly MarketConditionsAnalyzer _marketConditionsAnalyzer;
    private readonly FundSelectionAnalyzer _fundSelectionAnalyzer;
    private readonly ResultsAggregator _resultsAggregator;
    private readonly ScenarioBuilder _scenarioBuilder;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly ILogger<StreamingProfitProjectionWorkflow> _logger;

    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.StreamingWorkflow",
        "1.0.0"
    );

    public StreamingProfitProjectionWorkflow(
        CustomerContextExecutor customerContextExecutor,
        HistoricalAnalyzer historicalAnalyzer,
        MarketConditionsAnalyzer marketConditionsAnalyzer,
        FundSelectionAnalyzer fundSelectionAnalyzer,
        ResultsAggregator resultsAggregator,
        ScenarioBuilder scenarioBuilder,
        RecommendationEngine recommendationEngine,
        ILogger<StreamingProfitProjectionWorkflow> logger
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
    /// Execute workflow with progress streaming via Channel
    /// </summary>
    public ChannelReader<WorkflowProgressEvent> ExecuteWithProgressAsync(
        ProjectionRequest request,
        Action<ProjectionResult> onComplete,
        Action<Exception>? onError = null
    )
    {
        var channel = Channel.CreateUnbounded<WorkflowProgressEvent>();
        _ = ExecuteInternalAsync(request, channel.Writer, onComplete, onError);
        return channel.Reader;
    }

    private async Task ExecuteInternalAsync(
        ProjectionRequest request,
        ChannelWriter<WorkflowProgressEvent> writer,
        Action<ProjectionResult> onComplete,
        Action<Exception>? onError
    )
    {
        using var activity = ActivitySource.StartActivity("StreamingProfitProjection");
        var workflowStopwatch = Stopwatch.StartNew();
        var stepStopwatch = new Stopwatch();

        try
        {
            // Step 0: Customer Context
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(0, false));

            var customerContext = await _customerContextExecutor.ExecuteAsync(request);
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(
                    0,
                    true,
                    stepStopwatch.ElapsedMilliseconds,
                    customerContext.IsExistingCustomer ? "Found existing customer" : "New customer"
                )
            );

            var cif = request.CustomerId ?? "100000000001";

            // Step 1: Historical Analysis (start)
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(1, false));
            var historicalTask = _historicalAnalyzer.ExecuteAsync(request, cif);

            // Step 2: Market Analysis (start - parallel)
            await writer.WriteAsync(CreateEvent(2, false));
            var marketTask = _marketConditionsAnalyzer.ExecuteAsync(request);

            // Wait for historical
            var historicalAnalysis = await historicalTask;
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(
                    1,
                    true,
                    stepStopwatch.ElapsedMilliseconds,
                    $"Analyzed {historicalAnalysis.FundsAnalyzed} funds"
                )
            );

            // Wait for market
            var marketAnalysis = await marketTask;
            await writer.WriteAsync(
                CreateEvent(2, true, null, $"Market: {marketAnalysis.MarketSentiment}")
            );

            // Step 3: Fund Selection
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(3, false));
            var fundSelection = await _fundSelectionAnalyzer.ExecuteAsync(
                request,
                historicalAnalysis,
                cif
            );
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(
                    3,
                    true,
                    stepStopwatch.ElapsedMilliseconds,
                    $"Selected {fundSelection.FundsMatched} funds"
                )
            );

            // Step 4: Results Aggregation
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(4, false));
            var aggregatedAnalysis = await _resultsAggregator.ExecuteAsync(
                request,
                customerContext,
                historicalAnalysis,
                marketAnalysis,
                fundSelection
            );
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(
                    4,
                    true,
                    stepStopwatch.ElapsedMilliseconds,
                    $"Expected: {aggregatedAnalysis.WeightedExpectedReturn:P1}"
                )
            );

            // Step 5: Scenario Building
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(5, false));
            var scenarios = await _scenarioBuilder.ExecuteAsync(aggregatedAnalysis);
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(5, true, stepStopwatch.ElapsedMilliseconds, "3 scenarios ready")
            );

            // Step 6: Recommendations
            stepStopwatch.Restart();
            await writer.WriteAsync(CreateEvent(6, false));
            workflowStopwatch.Stop();
            var executionTimeMs = (int)workflowStopwatch.ElapsedMilliseconds;

            var result = await _recommendationEngine.ExecuteAsync(
                aggregatedAnalysis,
                scenarios,
                executionTimeMs
            );
            stepStopwatch.Stop();
            await writer.WriteAsync(
                CreateEvent(
                    6,
                    true,
                    stepStopwatch.ElapsedMilliseconds,
                    $"Complete in {executionTimeMs}ms"
                )
            );

            onComplete(result);

            _logger.LogInformation(
                "Streaming projection completed: {Id}, Time: {Time}ms",
                result.ProjectionId,
                executionTimeMs
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming projection failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            onError?.Invoke(ex);
        }
        finally
        {
            writer.Complete();
        }
    }

    private static WorkflowProgressEvent CreateEvent(
        int stepIndex,
        bool completed,
        long? ms = null,
        string? details = null
    ) =>
        ProfitProjectionWorkflowProgressExtensions.CreateProgressEvent(
            stepIndex,
            completed,
            ms,
            details
        );
}

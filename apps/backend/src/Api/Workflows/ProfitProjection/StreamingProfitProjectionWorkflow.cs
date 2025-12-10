using System.Diagnostics;
using System.Threading.Channels;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Services;

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
public class StreamingProfitProjectionWorkflow(
    CustomerContextExecutor customerContextExecutor,
    HistoricalAnalyzer historicalAnalyzer,
    MarketConditionsAnalyzer marketConditionsAnalyzer,
    FundSelectionAnalyzer fundSelectionAnalyzer,
    ResultsAggregator resultsAggregator,
    ScenarioBuilder scenarioBuilder,
    RecommendationEngine recommendationEngine,
    WorkflowProgressNotifier progressNotifier,
    ILogger<StreamingProfitProjectionWorkflow> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.StreamingWorkflow",
        "1.0.0"
    );

    /// <summary>
    /// Execute workflow with progress streaming via Channel (legacy interface)
    /// </summary>
    public ChannelReader<WorkflowProgressEvent> ExecuteWithProgressAsync(
        ProjectionRequest request,
        Action<ProjectionResult> onComplete,
        Action<Exception>? onError = null
    )
    {
        var channel = Channel.CreateUnbounded<WorkflowProgressEvent>();
        // Note: This path doesn't use real-time SignalR push
        _ = ExecuteInternalAsync(request, channel.Writer, onComplete, onError, null);
        return channel.Reader;
    }

    /// <summary>
    /// Execute workflow with real-time SignalR progress updates
    /// </summary>
    public async Task<ProjectionResult> ExecuteWithRealTimeProgressAsync(
        ProjectionRequest request,
        string conversationId
    )
    {
        var channel = Channel.CreateUnbounded<WorkflowProgressEvent>();
        ProjectionResult? result = null;
        Exception? error = null;

        await ExecuteInternalAsync(
            request,
            channel.Writer,
            r => result = r,
            ex => error = ex,
            conversationId
        );

        if (error != null)
            throw error;

        return result
            ?? throw new InvalidOperationException(
                "Projection completed but no result was generated."
            );
    }

    private async Task ExecuteInternalAsync(
        ProjectionRequest request,
        ChannelWriter<WorkflowProgressEvent> writer,
        Action<ProjectionResult> onComplete,
        Action<Exception>? onError,
        string? conversationId
    )
    {
        using var activity = ActivitySource.StartActivity("StreamingProfitProjection");
        var workflowStopwatch = Stopwatch.StartNew();
        var stepStopwatch = new Stopwatch();

        // Helper to emit progress event with optional SignalR push
        async Task EmitProgress(
            int stepIndex,
            bool isCompleted,
            long? durationMs = null,
            string? details = null
        )
        {
            var evt = CreateEvent(stepIndex, isCompleted, durationMs, details);
            await writer.WriteAsync(evt);

            // Push directly to SignalR if conversation ID is provided
            if (!string.IsNullOrEmpty(conversationId))
            {
                await progressNotifier.NotifyProgressAsync(
                    conversationId,
                    evt.StepId,
                    evt.StepName,
                    evt.StepNameAr,
                    evt.StepNumber,
                    evt.TotalSteps,
                    evt.IsCompleted,
                    evt.DurationMs,
                    evt.Details
                );
            }
        }

        try
        {
            // Step 0: Customer Context
            stepStopwatch.Restart();
            await EmitProgress(0, false);

            var customerContext = await customerContextExecutor.ExecuteAsync(request);
            stepStopwatch.Stop();
            await EmitProgress(
                0,
                true,
                stepStopwatch.ElapsedMilliseconds,
                customerContext.IsExistingCustomer ? "Found existing customer" : "New customer"
            );

            var cif = request.CustomerId ?? "100000000001";

            // Step 1: Historical Analysis (start)
            stepStopwatch.Restart();
            await EmitProgress(1, false);
            var historicalTask = historicalAnalyzer.ExecuteAsync(request, cif);

            // Step 2: Market Analysis (start - parallel)
            await EmitProgress(2, false);
            var marketTask = marketConditionsAnalyzer.ExecuteAsync(request);

            // Wait for historical
            var historicalAnalysis = await historicalTask;
            stepStopwatch.Stop();
            await EmitProgress(
                1,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Analyzed {historicalAnalysis.FundsAnalyzed} funds"
            );

            // Wait for market
            var marketAnalysis = await marketTask;
            await EmitProgress(2, true, null, $"Market: {marketAnalysis.MarketSentiment}");

            // Step 3: Fund Selection
            stepStopwatch.Restart();
            await EmitProgress(3, false);
            var fundSelection = await fundSelectionAnalyzer.ExecuteAsync(
                request,
                historicalAnalysis,
                cif
            );
            stepStopwatch.Stop();
            await EmitProgress(
                3,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Selected {fundSelection.FundsMatched} funds"
            );

            // Step 4: Results Aggregation
            stepStopwatch.Restart();
            await EmitProgress(4, false);
            var aggregatedAnalysis = await resultsAggregator.ExecuteAsync(
                request,
                customerContext,
                historicalAnalysis,
                marketAnalysis,
                fundSelection
            );
            stepStopwatch.Stop();
            await EmitProgress(
                4,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Expected: {aggregatedAnalysis.WeightedExpectedReturn:P1}"
            );

            // Step 5: Scenario Building
            stepStopwatch.Restart();
            await EmitProgress(5, false);
            var scenarios = await scenarioBuilder.ExecuteAsync(aggregatedAnalysis);
            stepStopwatch.Stop();
            await EmitProgress(5, true, stepStopwatch.ElapsedMilliseconds, "3 scenarios ready");

            // Step 6: Recommendations
            stepStopwatch.Restart();
            await EmitProgress(6, false);
            workflowStopwatch.Stop();
            var executionTimeMs = (int)workflowStopwatch.ElapsedMilliseconds;

            var result = await recommendationEngine.ExecuteAsync(
                aggregatedAnalysis,
                scenarios,
                executionTimeMs
            );
            stepStopwatch.Stop();
            await EmitProgress(
                6,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Complete in {executionTimeMs}ms"
            );

            onComplete(result);

            logger.LogInformation(
                "Streaming projection completed: {Id}, Time: {Time}ms",
                result.ProjectionId,
                executionTimeMs
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Streaming projection failed");
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

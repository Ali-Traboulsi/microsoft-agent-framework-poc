using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgentFrameworkQuickStart.Core.Application.Common;

/// <summary>
/// Base interface for workflows
/// </summary>
public interface IWorkflow<in TRequest, TResult>
{
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken ct = default);
}

/// <summary>
/// Base class for workflows with built-in telemetry
/// </summary>
public abstract class WorkflowBase<TRequest, TResult> : IWorkflow<TRequest, TResult>
{
    protected readonly ILogger Logger;
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    private readonly Counter<int> _executionCounter;
    private readonly Histogram<double> _executionTimeHistogram;
    private readonly string _workflowName;

    protected WorkflowBase(ILogger logger)
    {
        Logger = logger;
        _workflowName = GetType().Name;

        var activitySourceName = $"InvestmentBanking.Workflow.{_workflowName}";
        _activitySource = new ActivitySource(activitySourceName, "2.0.0");
        _meter = new Meter(activitySourceName, "2.0.0");

        _executionCounter = _meter.CreateCounter<int>(
            $"{ToSnakeCase(_workflowName)}_executions",
            "executions",
            "Number of workflow executions"
        );

        _executionTimeHistogram = _meter.CreateHistogram<double>(
            $"{ToSnakeCase(_workflowName)}_duration_ms",
            "ms",
            "Workflow execution time"
        );
    }

    public async Task<TResult> ExecuteAsync(TRequest request, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity(_workflowName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Starting workflow: {Workflow}", _workflowName);
            AddRequestTags(activity, request);

            ValidateRequest(request);
            var result = await ExecuteCoreAsync(request, ct);

            stopwatch.Stop();
            _executionCounter.Add(1, new KeyValuePair<string, object?>("status", "success"));
            _executionTimeHistogram.Record(stopwatch.ElapsedMilliseconds);

            AddResultTags(activity, result);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            Logger.LogInformation(
                "Completed workflow: {Workflow} in {Duration}ms",
                _workflowName,
                stopwatch.ElapsedMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _executionCounter.Add(1, new KeyValuePair<string, object?>("status", "error"));
            _executionTimeHistogram.Record(stopwatch.ElapsedMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Logger.LogError(ex, "Workflow {Workflow} failed: {Message}", _workflowName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Core workflow logic - implement in derived classes
    /// </summary>
    protected abstract Task<TResult> ExecuteCoreAsync(TRequest request, CancellationToken ct);

    /// <summary>
    /// Validate the request before execution
    /// </summary>
    protected virtual void ValidateRequest(TRequest request) { }

    /// <summary>
    /// Add custom tags from request to activity
    /// </summary>
    protected virtual void AddRequestTags(Activity? activity, TRequest request) { }

    /// <summary>
    /// Add custom tags from result to activity
    /// </summary>
    protected virtual void AddResultTags(Activity? activity, TResult result) { }

    private static string ToSnakeCase(string text) =>
        string.Concat(text.Select((c, i) => i > 0 && char.IsUpper(c) ? $"_{c}" : c.ToString()))
            .ToLower();
}

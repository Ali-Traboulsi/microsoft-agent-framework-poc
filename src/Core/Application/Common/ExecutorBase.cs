using System.Diagnostics;

namespace AgentFrameworkQuickStart.Core.Application.Common;

/// <summary>
/// Base interface for all workflow executors
/// </summary>
public interface IExecutor<in TInput, TOutput>
{
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct = default);
}

/// <summary>
/// Base class for workflow executors with built-in telemetry and error handling
/// </summary>
public abstract class ExecutorBase<TInput, TOutput> : IExecutor<TInput, TOutput>
{
    protected readonly ILogger Logger;
    private readonly ActivitySource _activitySource;
    private readonly string _executorName;

    protected ExecutorBase(ILogger logger, string activitySourceName = "InvestmentBanking.Workflow")
    {
        Logger = logger;
        _executorName = GetType().Name;
        _activitySource = new ActivitySource($"{activitySourceName}.{_executorName}", "2.0.0");
    }

    public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken ct = default)
    {
        using var activity = _activitySource.StartActivity(_executorName);

        try
        {
            Logger.LogDebug("Starting {Executor}", _executorName);
            AddInputTags(activity, input);

            var result = await ExecuteCoreAsync(input, ct);

            AddOutputTags(activity, result);
            activity?.SetStatus(ActivityStatusCode.Ok);
            Logger.LogDebug("Completed {Executor}", _executorName);

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {Executor}: {Message}", _executorName, ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Core execution logic - implement in derived classes
    /// </summary>
    protected abstract Task<TOutput> ExecuteCoreAsync(TInput input, CancellationToken ct);

    /// <summary>
    /// Override to add custom input tags to the activity
    /// </summary>
    protected virtual void AddInputTags(Activity? activity, TInput input) { }

    /// <summary>
    /// Override to add custom output tags to the activity
    /// </summary>
    protected virtual void AddOutputTags(Activity? activity, TOutput output) { }
}

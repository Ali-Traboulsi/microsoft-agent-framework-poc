using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Workflows;
using AgentFrameworkQuickStart.Api.Workflows.FundIn;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering workflow services.
/// </summary>
public static class WorkflowExtensions
{
    /// <summary>
    /// Adds all workflow services including executors (Scoped).
    /// </summary>
    public static IServiceCollection AddWorkflows(this IServiceCollection services)
    {
        // Profit Projection Workflow Executors
        services.AddScoped<CustomerContextExecutor>();
        services.AddScoped<HistoricalAnalyzer>();
        services.AddScoped<MarketConditionsAnalyzer>();
        services.AddScoped<FundSelectionAnalyzer>();
        services.AddScoped<ResultsAggregator>();
        services.AddScoped<ScenarioBuilder>();
        services.AddScoped<RecommendationEngine>();

        // Profit Projection Workflows
        services.AddScoped<ProfitProjectionWorkflow>();
        services.AddScoped<StreamingProfitProjectionWorkflow>();

        // Fund-In Workflow Executors
        services.AddScoped<AccountsRetriever>();
        services.AddScoped<PreviewExecutor>();
        services.AddScoped<ConfirmationExecutor>();
        services.AddScoped<CommitExecutor>();

        // Workflow Progress Notifier (for real-time SignalR updates)
        services.AddScoped<WorkflowProgressNotifier>();

        // Fund-In Workflow
        services.AddScoped<StreamingFundInWorkflow>();

        // Legacy Workflows
        services.AddScoped<IWorkflow, CompleteInvestmentWorkflow>();

        return services;
    }
}

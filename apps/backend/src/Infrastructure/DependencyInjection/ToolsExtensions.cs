using AgentFrameworkQuickStart.Tools;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering tool services.
/// </summary>
public static class ToolsExtensions
{
    /// <summary>
    /// Adds all tool services (Scoped - one instance per request).
    /// </summary>
    public static IServiceCollection AddTools(this IServiceCollection services)
    {
        services.AddScoped<AccountTools>();
        services.AddScoped<PortfolioTools>();
        services.AddScoped<MutualFundTools>();
        services.AddScoped<WebSearchTools>();
        services.AddScoped<ProjectionTools>();
        services.AddScoped<SNBCapitalTools>();
        services.AddScoped<FundInTools>();
        services.AddScoped<FundInWorkflowTools>();

        return services;
    }
}

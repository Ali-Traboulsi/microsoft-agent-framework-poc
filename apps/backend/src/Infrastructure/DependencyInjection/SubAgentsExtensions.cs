using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.SubAgents;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering sub-agent services.
/// </summary>
public static class SubAgentsExtensions
{
    /// <summary>
    /// Adds all sub-agent services (Scoped - one instance per request).
    /// </summary>
    public static IServiceCollection AddSubAgents(this IServiceCollection services)
    {
        services.AddScoped<ISubAgent, PortfolioManagerSubAgent>();
        services.AddScoped<ISubAgent, InvestmentAdvisorSubAgent>();
        services.AddScoped<ISubAgent, AccountServicesSubAgent>();
        services.AddScoped<ISubAgent, ComplianceOfficerSubAgent>();
        services.AddScoped<ISubAgent, ProfitProjectionSubAgent>();
        services.AddScoped<ISubAgent, ExternalApiSubAgent>();

        return services;
    }
}

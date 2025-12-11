using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering core services.
/// </summary>
public static class CoreServicesExtensions
{
    /// <summary>
    /// Adds core services including data stores and thread managers.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Data store (Singleton - shared across all requests)
        services.AddSingleton<InvestmentDataStore>();

        // Agent thread managers (Singleton - manages conversation threads)
        services.AddSingleton<AgentThreadManager>();
        services.AddSingleton<SubAgentThreadManager>();

        // Audio transcription service (Scoped)
        services.AddHttpClient<AudioTranscriptionService>();

        return services;
    }
}

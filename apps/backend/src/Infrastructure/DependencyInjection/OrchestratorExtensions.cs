using AgentFrameworkQuickStart.Api;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering orchestrator and AI services.
/// </summary>
public static class OrchestratorExtensions
{
    /// <summary>
    /// Adds AI chat client using OpenAI.
    /// </summary>
    public static IServiceCollection AddAIChatClient(
        this IServiceCollection services,
        string openAiApiKey
    )
    {
        services.AddScoped(sp =>
        {
            var chatClient = new OpenAI.Chat.ChatClient(model: "gpt-4o", apiKey: openAiApiKey);
            return chatClient.AsIChatClient();
        });

        Console.WriteLine($"🔧 Using OpenAI with model: gpt-4o (with vision support)");

        return services;
    }

    /// <summary>
    /// Adds master orchestrator and coordination services.
    /// </summary>
    public static IServiceCollection AddOrchestration(this IServiceCollection services)
    {
        // Structured Response Handler
        services.AddScoped<StructuredResponseHandler>();

        // Master Orchestrator (includes unified processing, Swarm pattern, and helper methods)
        services.AddScoped<IMasterOrchestrator, MasterOrchestrator>();

        // Coordination Orchestrator (for sub-agent coordination protocol)
        services.AddScoped<CoordinationOrchestrator>();

        return services;
    }

    /// <summary>
    /// Adds legacy agent service for backward compatibility.
    /// </summary>
    public static IServiceCollection AddLegacyAgentService(
        this IServiceCollection services,
        string openAiApiKey
    )
    {
        services.AddScoped(sp =>
        {
            var dataStore = sp.GetRequiredService<InvestmentDataStore>();
            var accountTools = sp.GetRequiredService<AccountTools>();
            var portfolioTools = sp.GetRequiredService<PortfolioTools>();
            var fundTools = sp.GetRequiredService<MutualFundTools>();
            return new AgentService(
                dataStore,
                accountTools,
                portfolioTools,
                fundTools,
                openAiApiKey
            );
        });

        return services;
    }
}

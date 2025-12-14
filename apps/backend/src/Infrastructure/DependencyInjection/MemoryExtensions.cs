using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;
using AgentFrameworkQuickStart.Services.Memory;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering long-term memory services
/// </summary>
public static class MemoryExtensions
{
    /// <summary>
    /// Add long-term memory services to the DI container
    /// </summary>
    public static IServiceCollection AddLongTermMemory(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<IConversationSummaryRepository, ConversationSummaryRepository>();

        // Memory extraction service (uses LLM for extraction)
        services.AddScoped<IMemoryExtractionService, MemoryExtractionService>();

        // Main memory provider (AIContextProvider pattern)
        services.AddScoped<ILongTermMemoryProvider, LongTermMemoryProvider>();

        return services;
    }
}

using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Services.Intelligence;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering intelligence layer services.
/// </summary>
public static class IntelligenceExtensions
{
    /// <summary>
    /// Adds intelligence layer services (P0 + P1).
    /// Includes: FastIntentMatcher, Intent Classification, Context Store, Reasoning Engine.
    /// </summary>
    public static IServiceCollection AddIntelligenceLayer(this IServiceCollection services)
    {
        // Fast Intent Matcher: Pattern-based fast path for obvious intents
        services.AddScoped<FastIntentMatcher>();

        // Intent Classification: Semantic understanding of user requests
        services.AddScoped<IIntentClassifier, IntentClassifier>();

        // Context Store: Persistent conversation context and entity tracking
        services.AddScoped<IConversationContextStore, DatabaseConversationContextStore>();

        // Reasoning Engine: Chain-of-Thought reasoning for complex decisions
        services.AddScoped<ReasoningEngine>();

        Console.WriteLine(
            "🧠 Intelligence Layer enabled: FastIntentMatcher + Intent Classification + CoT Reasoning + Database Context Store"
        );

        return services;
    }
}

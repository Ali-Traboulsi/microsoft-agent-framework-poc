using AgentFrameworkQuickStart.Api.Hubs.Handlers;
using AgentFrameworkQuickStart.Api.Middleware;

namespace AgentFrameworkQuickStart.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering SignalR services.
/// </summary>
public static class SignalRExtensions
{
    /// <summary>
    /// Adds SignalR configuration with extended message size and timeouts.
    /// </summary>
    public static IServiceCollection AddSignalRServices(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                // Increase message size limit for multimodal content (base64-encoded files)
                // Default is 32KB, increase to 100MB to support large audio/image files
                options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100MB

                // Increase timeouts for external API calls (Render.com has cold starts)
                options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.HandshakeTimeout = TimeSpan.FromSeconds(30);
                options.EnableDetailedErrors = true;
            })
            .AddJsonProtocol(options =>
            {
                // Use camelCase for SignalR JSON serialization (matches JavaScript conventions)
                options.PayloadSerializerOptions.PropertyNamingPolicy = System
                    .Text
                    .Json
                    .JsonNamingPolicy
                    .CamelCase;
            });

        return services;
    }

    /// <summary>
    /// Adds SignalR hub handlers and delegation notifier.
    /// </summary>
    public static IServiceCollection AddSignalRHandlers(this IServiceCollection services)
    {
        // SignalR Hub Handlers
        services.AddScoped<IChatHandler, ChatStreamHandler>();
        services.AddScoped<IMultiModalChatHandler, MultiModalChatHandler>();
        services.AddScoped<IThreadedChatHandler, ThreadedChatHandler>();

        // Delegation Event Notifier (Singleton - pushes events via SignalR)
        services.AddSingleton<IDelegationEventNotifier, DelegationEventNotifier>();

        return services;
    }
}

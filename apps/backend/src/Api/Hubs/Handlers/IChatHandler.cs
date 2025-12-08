using AgentFrameworkQuickStart.Api.DTOs;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Interface for chat stream handlers
/// </summary>
public interface IChatHandler
{
    IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        string message,
        string conversationId,
        bool enableThinking,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Interface for multi-modal chat handlers
/// </summary>
public interface IMultiModalChatHandler
{
    IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        MultiModalChatRequest request,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Interface for threaded chat handlers
/// </summary>
public interface IThreadedChatHandler
{
    IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        string message,
        string? threadIdStr,
        string? conversationId,
        bool enableThinking,
        CancellationToken cancellationToken
    );
}

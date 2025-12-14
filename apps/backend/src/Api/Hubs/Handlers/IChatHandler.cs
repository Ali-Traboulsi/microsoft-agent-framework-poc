using AgentFrameworkQuickStart.Api.DTOs;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Unified interface for all chat types with thread-based persistence.
/// Consolidates text and multimodal chat with consistent memory and conversation tracking.
/// </summary>
public interface IUnifiedChatHandler
{
    /// <summary>
    /// Stream chat responses with thread persistence.
    /// Handles both text-only and multimodal requests uniformly.
    /// </summary>
    /// <param name="request">Unified request containing text and/or multimodal content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of chat responses</returns>
    IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        UnifiedThreadedChatRequest request,
        CancellationToken cancellationToken
    );
}

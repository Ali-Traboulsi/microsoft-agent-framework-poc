using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Hubs.Handlers;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Hubs;

/// <summary>
/// SignalR hub for master orchestrator with streaming support.
/// Note: For file uploads with streaming, use ChatStreamMultiModal with base64-encoded data.
/// For direct file uploads without streaming, use the HTTP endpoint /chat/multimodal/upload.
/// </summary>
public class MasterAgentHub(
    IChatHandler chatHandler,
    IMultiModalChatHandler multiModalHandler,
    IThreadedChatHandler threadedHandler
) : Hub
{
    /// <summary>
    /// Stream chat responses from the master orchestrator
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStream(
        string message,
        string conversationId,
        bool enableThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var response in chatHandler.StreamAsync(
                message,
                conversationId,
                enableThinking,
                cancellationToken
            )
        )
        {
            yield return response;
        }
    }

    /// <summary>
    /// Stream multi-modal chat responses (text, images, audio, documents)
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamMultiModal(
        MultiModalChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var response in multiModalHandler.StreamAsync(request, cancellationToken))
        {
            yield return response;
        }
    }

    /// <summary>
    /// Stream chat responses with thread persistence
    /// </summary>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamWithThread(
        string message,
        string? threadIdStr,
        string? conversationId = null,
        bool enableThinking = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (
            var response in threadedHandler.StreamAsync(
                message,
                threadIdStr,
                conversationId,
                enableThinking,
                cancellationToken
            )
        )
        {
            yield return response;
        }
    }

    /// <summary>
    /// Stream multi-modal chat with base64-encoded file data (alias for ChatStreamMultiModal)
    /// </summary>
    public IAsyncEnumerable<MasterStreamingResponse> ChatStreamMultiModalWithBase64(
        MultiModalChatRequest request,
        CancellationToken cancellationToken = default
    ) => ChatStreamMultiModal(request, cancellationToken);
}

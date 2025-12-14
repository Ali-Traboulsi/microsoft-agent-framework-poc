using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Hubs.Handlers;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Hubs;

/// <summary>
/// SignalR hub for master orchestrator with streaming support.
/// All chat requests use the unified handler with intelligent processing (Intelligence Layer).
///
/// Use ChatStreamUnified for all integrations - it handles both text and multimodal content
/// with consistent thread-based memory and persistence.
///
/// Note: For file uploads with streaming, use ChatStreamUnified with base64-encoded data.
/// For direct file uploads without streaming, use the HTTP endpoint /chat/multimodal/upload.
/// </summary>
public class MasterAgentHub(IUnifiedChatHandler unifiedHandler) : Hub
{
    /// <summary>
    /// Unified chat streaming that handles both text and multimodal content
    /// with consistent thread-based persistence and memory.
    /// </summary>
    /// <param name="request">Unified request containing text and/or multimodal content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of chat responses</returns>
    public async IAsyncEnumerable<MasterStreamingResponse> ChatStreamUnified(
        UnifiedThreadedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var response in unifiedHandler.StreamAsync(request, cancellationToken))
        {
            yield return response;
        }
    }

    /// <summary>
    /// Test method to verify SignalR workflow progress push works.
    /// Sends a test workflow progress event to all connected clients.
    /// </summary>
    public async Task TestWorkflowProgress(string conversationId)
    {
        var testResponse = new MasterStreamingResponse
        {
            Type = "StepStart",
            Content = "Test workflow step",
            StepId = "TestStep",
            StepName = "Testing SignalR Push",
            StepNameAr = "اختبار الإرسال",
            StepNumber = 1,
            TotalSteps = 1,
            StepCompleted = false,
            IsComplete = false,
        };

        await Clients.All.SendAsync("receiveWorkflowProgress", conversationId, testResponse);
    }
}

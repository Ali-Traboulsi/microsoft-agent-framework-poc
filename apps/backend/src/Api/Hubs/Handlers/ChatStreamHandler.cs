using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Handles chat streaming operations
/// All requests use intelligent streaming processing
/// </summary>
public class ChatStreamHandler(IMasterOrchestrator orchestrator) : IChatHandler
{
    public async IAsyncEnumerable<MasterStreamingResponse> StreamAsync(
        string message,
        string conversationId,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var request = new UnifiedChatRequest
        {
            ConversationId = conversationId,
            Message = message,
            EnableThinking = enableThinking,
            CancellationToken = cancellationToken,
        };

        await foreach (var chunk in orchestrator.ProcessAsync(request, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            yield return MapChunkToStreamingResponse(chunk);
        }
    }

    internal static MasterStreamingResponse MapChunkToStreamingResponse(
        UnifiedStreamingChunk chunk
    ) =>
        new()
        {
            Type = chunk.Type.ToString(),
            Content = chunk.Content,
            SubAgentName = chunk.SubAgentName,
            ToolName = chunk.ToolName,
            IsComplete = chunk.Type == StreamingChunkType.Complete,
            Metadata = chunk.Metadata,
            StepId = chunk.StepId,
            StepName = chunk.StepName,
            StepNameAr = chunk.StepNameAr,
            StepNumber = chunk.StepNumber,
            TotalSteps = chunk.TotalSteps,
            StepCompleted = chunk.Type == StreamingChunkType.StepComplete,
            StepDurationMs = chunk.StepDurationMs,
            StepDetails = chunk.StepDetails,
            ProjectionResult = chunk.FinalResult?.ProjectionResult,
        };
}

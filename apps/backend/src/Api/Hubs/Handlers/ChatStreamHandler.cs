using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;

namespace AgentFrameworkQuickStart.Api.Hubs.Handlers;

/// <summary>
/// Handles basic chat streaming operations
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
        await foreach (
            var response in orchestrator
                .ProcessRequestStreamingAsync(message, conversationId, enableThinking)
                .WithCancellation(cancellationToken)
        )
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            yield return MapToStreamingResponse(response);
        }
    }

    internal static MasterStreamingResponse MapToStreamingResponse(OrchestratorResponse response) =>
        new()
        {
            Type = response.Type.ToString(),
            Content = response.Content,
            SubAgentName = response.SubAgentName,
            ToolName = response.ToolName,
            IsComplete = response.Type == ResponseType.Complete,
            Metadata = response.Metadata,
            StepId = response.StepId,
            StepName = response.StepName,
            StepNameAr = response.StepNameAr,
            StepNumber = response.StepNumber,
            TotalSteps = response.TotalSteps,
            StepCompleted = response.Type == ResponseType.StepComplete,
            StepDurationMs = response.StepDurationMs,
            StepDetails = response.StepDetails,
            ProjectionResult = response.ProjectionResult,
        };
}

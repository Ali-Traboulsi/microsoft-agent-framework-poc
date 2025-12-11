using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Api.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

/// <summary>
/// SSE-based streaming controller.
/// Uses Server-Sent Events for proper ordered event delivery.
/// Tool/delegation events are emitted BEFORE content because they happen first.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StreamController(
    MasterOrchestratorHelper orchestrator,
    ILogger<StreamController> logger
) : ControllerBase
{
    /// <summary>
    /// Stream chat response using Server-Sent Events.
    /// Events are delivered in natural order:
    /// 1. tool_start - when a tool begins execution
    /// 2. tool_complete - when a tool finishes
    /// 3. content_delta - streaming content chunks
    /// 4. done - stream complete
    /// </summary>
    [HttpPost("chat")]
    public async Task StreamChat(
        [FromBody] StreamChatRequest request,
        CancellationToken cancellationToken
    )
    {
        // Set up SSE response
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var conversationId = request.ConversationId ?? $"conv-{DateTime.UtcNow.Ticks}";

        // Set conversation ID for middleware tracking
        DelegationEventMiddleware.CurrentConversationId = conversationId;

        try
        {
            // Create the unified request
            var unifiedRequest = new UnifiedChatRequest
            {
                Message = request.Message,
                ConversationId = conversationId,
                EnableThinking = request.EnableThinking,
            };

            // Process and stream
            await foreach (
                var chunk in orchestrator.ProcessAsync(unifiedRequest, cancellationToken)
            )
            {
                // Check for any delegation events that occurred
                while (
                    DelegationEventMiddleware.TryGetNextEvent(
                        conversationId,
                        out var delegationEvent
                    )
                )
                {
                    if (delegationEvent != null)
                    {
                        await WriteEventAsync(
                            MapDelegationEvent(delegationEvent),
                            cancellationToken
                        );
                    }
                }

                // Write content chunk
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    var eventType = chunk.Type switch
                    {
                        StreamingChunkType.Thinking => "thinking",
                        StreamingChunkType.Content => "content_delta",
                        _ => "content_delta",
                    };

                    await WriteEventAsync(
                        new StreamEvent
                        {
                            Event = eventType,
                            Data = new { content = chunk.Content },
                        },
                        cancellationToken
                    );
                }
            }

            // Drain any remaining events
            while (DelegationEventMiddleware.TryGetNextEvent(conversationId, out var finalEvent))
            {
                if (finalEvent != null)
                {
                    await WriteEventAsync(MapDelegationEvent(finalEvent), cancellationToken);
                }
            }

            // Send done event
            await WriteEventAsync(
                new StreamEvent { Event = "done", Data = new { conversationId } },
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "Stream cancelled for conversation {ConversationId}",
                conversationId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error streaming response for conversation {ConversationId}",
                conversationId
            );
            await WriteEventAsync(
                new StreamEvent { Event = "error", Data = new { error = ex.Message } },
                cancellationToken
            );
        }
        finally
        {
            DelegationEventMiddleware.CleanupConversation(conversationId);
            DelegationEventMiddleware.CurrentConversationId = null;
        }
    }

    private StreamEvent MapDelegationEvent(DelegationEvent evt)
    {
        return evt.Type switch
        {
            DelegationEventType.ToolExecutionStart => new StreamEvent
            {
                Event = "tool_start",
                Data = new { toolName = evt.ToolName, timestamp = evt.Timestamp },
            },
            DelegationEventType.ToolExecutionComplete => new StreamEvent
            {
                Event = "tool_complete",
                Data = new { toolName = evt.ToolName, timestamp = evt.Timestamp },
            },
            DelegationEventType.ToolExecutionError => new StreamEvent
            {
                Event = "tool_error",
                Data = new
                {
                    toolName = evt.ToolName,
                    error = evt.Error,
                    timestamp = evt.Timestamp,
                },
            },
            DelegationEventType.SubAgentDelegationStart => new StreamEvent
            {
                Event = "delegation_start",
                Data = new { subAgentName = evt.SubAgentName, timestamp = evt.Timestamp },
            },
            DelegationEventType.SubAgentDelegationComplete => new StreamEvent
            {
                Event = "delegation_complete",
                Data = new { subAgentName = evt.SubAgentName, timestamp = evt.Timestamp },
            },
            DelegationEventType.SubAgentDelegationError => new StreamEvent
            {
                Event = "delegation_error",
                Data = new
                {
                    subAgentName = evt.SubAgentName,
                    error = evt.Error,
                    timestamp = evt.Timestamp,
                },
            },
            DelegationEventType.WorkflowStepStart => new StreamEvent
            {
                Event = "step_start",
                Data = new
                {
                    stepId = evt.StepId,
                    stepName = evt.StepName,
                    stepNameAr = evt.StepNameAr,
                    stepNumber = evt.StepNumber,
                    totalSteps = evt.TotalSteps,
                },
            },
            DelegationEventType.WorkflowStepComplete => new StreamEvent
            {
                Event = "step_complete",
                Data = new
                {
                    stepId = evt.StepId,
                    stepName = evt.StepName,
                    stepNumber = evt.StepNumber,
                    totalSteps = evt.TotalSteps,
                    durationMs = evt.StepDurationMs,
                },
            },
            _ => new StreamEvent { Event = "unknown", Data = new { type = evt.Type.ToString() } },
        };
    }

    private async Task WriteEventAsync(StreamEvent evt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            evt.Data,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        var sseMessage = $"event: {evt.Event}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(sseMessage);

        await Response.Body.WriteAsync(bytes, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

public class StreamChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public bool EnableThinking { get; set; }
}

public class StreamEvent
{
    public string Event { get; set; } = string.Empty;
    public object Data { get; set; } = new();
}

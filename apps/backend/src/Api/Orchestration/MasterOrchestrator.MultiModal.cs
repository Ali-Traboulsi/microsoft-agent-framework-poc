using System.Diagnostics;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Multi-modal request processing methods
/// </summary>
public partial class MasterOrchestrator
{
    public async Task<OrchestratorResult> ProcessMultiModalRequestAsync(
        List<AIContent> contents,
        string conversationId,
        bool enableThinking = false
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessMultiModalRequest",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("content.count", contents.Count);
        activity?.SetTag("content.types", string.Join(",", contents.Select(c => c.GetType().Name)));
        activity?.SetTag("thinking.enabled", enableThinking);

        // TODO: Thinking mode not yet implemented for multimodal
        if (enableThinking)
        {
            _logger.LogWarning("Thinking mode requested for multimodal but not yet supported");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing multi-modal request for conversation {ConversationId} with {ContentCount} content items",
                conversationId,
                contents.Count
            );

            // Get or create thread for this conversation to maintain chat history
            var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

            // Create chat message with all content types
            var chatMessage = new ChatMessage(ChatRole.User, contents);

            // Run the agent with multi-modal content and thread for history
            var result = await _masterAgent.Value.RunAsync(chatMessage, thread);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated.";

            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation(
                "Multi-modal request completed in {Duration}ms for conversation {ConversationId}",
                sw.ElapsedMilliseconds,
                conversationId
            );

            // Capture projection result if any was generated during this request
            var projectionResult = _projectionTools.GetLastProjectionResult();
            _projectionTools.ClearLastProjectionResult();

            return new OrchestratorResult
            {
                Success = true,
                Response = responseText,
                SubAgentsUsed = new List<string>(), // Will be populated by middleware events
                TotalDurationMs = sw.ElapsedMilliseconds,
                ProjectionResult = projectionResult,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);

            _logger.LogError(
                ex,
                "Error processing multi-modal request for conversation {ConversationId}: {Error}",
                conversationId,
                ex.Message
            );

            // Clear any partial projection result on error
            _projectionTools.ClearLastProjectionResult();

            return new OrchestratorResult
            {
                Success = false,
                Response = $"Error processing multi-modal request: {ex.Message}",
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    public async IAsyncEnumerable<OrchestratorResponse> ProcessMultiModalRequestStreamingAsync(
        List<AIContent> contents,
        string conversationId,
        bool enableThinking = false
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessMultiModalRequestStreaming",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("content.count", contents.Count);
        activity?.SetTag("content.types", string.Join(",", contents.Select(c => c.GetType().Name)));
        activity?.SetTag("thinking.enabled", enableThinking);

        // TODO: Thinking mode not yet implemented for multimodal streaming
        if (enableThinking)
        {
            _logger.LogWarning(
                "Thinking mode requested for multimodal streaming but not yet supported"
            );
        }

        _logger.LogInformation(
            "Processing streaming multi-modal request for conversation {ConversationId} with {ContentCount} content items",
            conversationId,
            contents.Count
        );

        // Get or create thread for this conversation to maintain chat history
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

        var contentBuilder = new StringBuilder();
        var startTime = Stopwatch.GetTimestamp();

        // Create chat message with all content types
        var chatMessage = new ChatMessage(ChatRole.User, contents);

        // Stream the agent's response with thread for history
        await foreach (var update in _masterAgent.Value.RunStreamingAsync(chatMessage, thread))
        {
            if (update.Contents is { Count: > 0 })
            {
                foreach (var contentItem in update.Contents)
                {
                    if (contentItem is TextContent textContent)
                    {
                        contentBuilder.Append(textContent.Text);

                        yield return new OrchestratorResponse
                        {
                            Type = ResponseType.Content,
                            Content = textContent.Text,
                            Metadata = new Dictionary<string, object>
                            {
                                ["timestamp"] = DateTime.UtcNow,
                                ["isStreaming"] = true,
                            },
                        };
                    }
                }
            }
        }

        var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;

        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("duration_ms", elapsedMs);

        _logger.LogInformation(
            "Multi-modal streaming request completed in {Duration}ms for conversation {ConversationId}",
            elapsedMs,
            conversationId
        );

        // Get projection result if one was calculated during this request
        var projectionResult = _projectionTools.GetLastProjectionResult();

        yield return new OrchestratorResponse
        {
            Type = ResponseType.Complete,
            Content = contentBuilder.ToString(),
            ProjectionResult = projectionResult,
            Metadata = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow,
                ["totalDurationMs"] = elapsedMs,
                ["contentCount"] = contents.Count,
                ["hasProjectionResult"] = projectionResult != null,
            },
        };
    }
}

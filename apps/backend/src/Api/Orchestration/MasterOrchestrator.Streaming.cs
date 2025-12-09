using System.Diagnostics;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Streaming response methods
/// </summary>
public partial class MasterOrchestrator
{
    public async IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingAsync(
        string userMessage,
        string conversationId,
        bool enableThinking = false
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequestStreaming",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("message.length", userMessage.Length);
        activity?.SetTag("thinking.enabled", enableThinking);

        // Set conversation ID for middleware to track events
        DelegationEventMiddleware.CurrentConversationId = conversationId;

        // Get or create thread for this conversation to maintain chat history
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

        _logger.LogInformation(
            "Processing streaming request for conversation {ConversationId}: {Message}",
            conversationId,
            userMessage
        );

        var fullResponse = new StringBuilder();
        var isInThinkingBlock = false;
        var chunkCount = 0;
        var lastEventCheck = DateTime.UtcNow;
        var hasSeenDelegation = false;
        var hasStartedThinking = false;
        var _hasEmittedThinkingContent = false;
        try
        {
            // If thinking is enabled, emit an initial thinking message immediately
            if (enableThinking)
            {
                hasStartedThinking = true;
                isInThinkingBlock = true;
                yield return new OrchestratorResponse
                {
                    Type = ResponseType.Thinking,
                    Content = "Analyzing your request...\n",
                };
                _hasEmittedThinkingContent = true;
            }

            await foreach (var chunk in _masterAgent.Value.RunStreamingAsync(userMessage, thread))
            {
                // Check for new delegation events FIRST
                while (
                    DelegationEventMiddleware.TryGetNextEvent(
                        conversationId,
                        out var delegationEvent
                    )
                )
                {
                    hasSeenDelegation = true;

                    // If thinking is enabled and we haven't emitted thinking content yet,
                    // emit synthetic thinking based on the delegation event
                    if (enableThinking && isInThinkingBlock)
                    {
                        var thinkingContent = GenerateThinkingFromDelegation(delegationEvent);
                        if (!string.IsNullOrEmpty(thinkingContent))
                        {
                            yield return new OrchestratorResponse
                            {
                                Type = ResponseType.Thinking,
                                Content = thinkingContent,
                            };
                            _hasEmittedThinkingContent = true;
                        }
                    }

                    isInThinkingBlock = false; // End thinking when delegation starts
                    yield return ConvertDelegationEventToResponse(delegationEvent);
                }

                if (chunk.Text != null)
                {
                    fullResponse.Append(chunk.Text);
                    chunkCount++;

                    // Everything BEFORE delegation is thinking (agent's reasoning process)
                    if (!hasSeenDelegation)
                    {
                        if (!hasStartedThinking)
                        {
                            isInThinkingBlock = true;
                            hasStartedThinking = true;
                        }
                    }
                    else
                    {
                        // Everything AFTER delegation is content (final response)
                        isInThinkingBlock = false;
                    }

                    var responseType = isInThinkingBlock
                        ? ResponseType.Thinking
                        : ResponseType.Content;

                    _logger.LogInformation(
                        "Streaming chunk: Type={Type}, HasSeenDelegation={HasSeenDelegation}, ChunkPreview={Preview}",
                        responseType,
                        hasSeenDelegation,
                        chunk.Text.Length > 50 ? chunk.Text[..50] + "..." : chunk.Text
                    );

                    yield return new OrchestratorResponse
                    {
                        Type = responseType,
                        Content = chunk.Text,
                    };
                }

                // Also check for events periodically during streaming
                if ((DateTime.UtcNow - lastEventCheck).TotalMilliseconds > 100)
                {
                    while (
                        DelegationEventMiddleware.TryGetNextEvent(
                            conversationId,
                            out var delegationEvent
                        )
                    )
                    {
                        yield return ConvertDelegationEventToResponse(delegationEvent);
                    }
                    lastEventCheck = DateTime.UtcNow;
                }
            }

            // Final check for any remaining events
            while (
                DelegationEventMiddleware.TryGetNextEvent(conversationId, out var delegationEvent)
            )
            {
                yield return ConvertDelegationEventToResponse(delegationEvent);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.total_length", fullResponse.Length);
            activity?.SetTag("response.chunk_count", chunkCount);

            _logger.LogInformation(
                "Streaming completed for conversation {ConversationId}",
                conversationId
            );

            // Get projection result if one was calculated during this request
            var projectionResult = _projectionTools.GetLastProjectionResult();

            _logger.LogInformation(
                "Projection result for conversation {ConversationId}: {HasResult}, ProjectionId: {ProjectionId}",
                conversationId,
                projectionResult != null,
                projectionResult?.ProjectionId ?? "none"
            );

            yield return new OrchestratorResponse
            {
                Type = ResponseType.Complete,
                Content = "",
                ProjectionResult = projectionResult,
                Metadata = new Dictionary<string, object>
                {
                    ["traceId"] = activity?.TraceId.ToString() ?? "",
                    ["totalChunks"] = chunkCount,
                    ["responseLength"] = fullResponse.Length,
                    ["hasProjectionResult"] = projectionResult != null,
                },
            };
        }
        finally
        {
            // Cleanup
            DelegationEventMiddleware.CleanupConversation(conversationId);
            DelegationEventMiddleware.CurrentConversationId = null;
        }
    }

    /// <summary>
    /// Process a request with prior conversation history restored
    /// </summary>
    public async IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingWithHistoryAsync(
        string userMessage,
        string conversationId,
        IEnumerable<ConversationMessage> priorMessages,
        bool enableThinking = false
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequestStreamingWithHistory",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("prior_messages.count", priorMessages.Count());

        // Set conversation ID for middleware to track events
        DelegationEventMiddleware.CurrentConversationId = conversationId;

        // Get or create thread for this conversation
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

        _logger.LogInformation(
            "Processing streaming request with {PriorMessageCount} prior messages for conversation {ConversationId}",
            priorMessages.Count(),
            conversationId
        );

        // Build context message summarizing the conversation history
        var priorMessagesList = priorMessages.ToList();
        string contextualMessage;

        if (priorMessagesList.Count > 0)
        {
            var historyBuilder = new StringBuilder();
            historyBuilder.AppendLine("## Previous Conversation Context");
            historyBuilder.AppendLine(
                "The following is the previous conversation history with the user. Use this context to understand their request:\n"
            );

            foreach (var msg in priorMessagesList)
            {
                var roleLabel = msg.Role.ToLower() == "user" ? "User" : "Assistant";
                if (!string.IsNullOrEmpty(msg.SubAgentName))
                {
                    roleLabel = $"Assistant ({msg.SubAgentName})";
                }
                historyBuilder.AppendLine($"**{roleLabel}:** {msg.Content}\n");
            }

            historyBuilder.AppendLine("---\n## Current User Request");
            historyBuilder.AppendLine(userMessage);

            contextualMessage = historyBuilder.ToString();
        }
        else
        {
            contextualMessage = userMessage;
        }

        var fullResponse = new StringBuilder();
        var isInThinkingBlock = false;
        var chunkCount = 0;
        var hasSeenDelegation = false;
        var hasStartedThinking = false;
        var _hasEmittedThinkingContent = false;

        try
        {
            // If thinking is enabled, emit an initial thinking message immediately
            if (enableThinking)
            {
                hasStartedThinking = true;
                isInThinkingBlock = true;
                yield return new OrchestratorResponse
                {
                    Type = ResponseType.Thinking,
                    Content = "Analyzing your request...\n",
                };
                _hasEmittedThinkingContent = true;
            }

            await foreach (
                var chunk in _masterAgent.Value.RunStreamingAsync(contextualMessage, thread)
            )
            {
                // Check for new delegation events FIRST
                while (
                    DelegationEventMiddleware.TryGetNextEvent(
                        conversationId,
                        out var delegationEvent
                    )
                )
                {
                    hasSeenDelegation = true;

                    // If thinking is enabled and we're still in thinking block,
                    // emit synthetic thinking based on the delegation event
                    if (enableThinking && isInThinkingBlock)
                    {
                        var thinkingContent = GenerateThinkingFromDelegation(delegationEvent);
                        if (!string.IsNullOrEmpty(thinkingContent))
                        {
                            yield return new OrchestratorResponse
                            {
                                Type = ResponseType.Thinking,
                                Content = thinkingContent,
                            };
                            _hasEmittedThinkingContent = true;
                        }
                    }

                    isInThinkingBlock = false;
                    yield return ConvertDelegationEventToResponse(delegationEvent);
                }

                if (chunk.Text != null)
                {
                    fullResponse.Append(chunk.Text);
                    chunkCount++;

                    if (!hasSeenDelegation)
                    {
                        if (!hasStartedThinking)
                        {
                            isInThinkingBlock = true;
                            hasStartedThinking = true;
                        }
                    }
                    else
                    {
                        isInThinkingBlock = false;
                    }

                    var responseType = isInThinkingBlock
                        ? ResponseType.Thinking
                        : ResponseType.Content;

                    yield return new OrchestratorResponse
                    {
                        Type = responseType,
                        Content = chunk.Text,
                    };
                }
            }

            // Final check for any remaining events
            while (
                DelegationEventMiddleware.TryGetNextEvent(conversationId, out var delegationEvent)
            )
            {
                yield return ConvertDelegationEventToResponse(delegationEvent);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.total_length", fullResponse.Length);
            activity?.SetTag("response.chunk_count", chunkCount);

            _logger.LogInformation(
                "Streaming with history completed for conversation {ConversationId}",
                conversationId
            );

            var projectionResult = _projectionTools.GetLastProjectionResult();

            yield return new OrchestratorResponse
            {
                Type = ResponseType.Complete,
                Content = "",
                ProjectionResult = projectionResult,
                Metadata = new Dictionary<string, object>
                {
                    ["traceId"] = activity?.TraceId.ToString() ?? "",
                    ["totalChunks"] = chunkCount,
                    ["responseLength"] = fullResponse.Length,
                    ["hasProjectionResult"] = projectionResult != null,
                    ["priorMessagesCount"] = priorMessagesList.Count,
                },
            };
        }
        finally
        {
            DelegationEventMiddleware.CleanupConversation(conversationId);
            DelegationEventMiddleware.CurrentConversationId = null;
        }
    }
}

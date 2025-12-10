using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Unified streaming processing
/// This is the primary entry point for all requests - STREAMING ONLY
/// </summary>
public partial class MasterOrchestrator
{
    /// <summary>
    /// Process any request with streaming response.
    /// This is THE primary entry point - handles text, multi-modal, everything.
    /// Uses intelligent pipeline: intent classification, context management, smart delegation.
    /// </summary>
    public async IAsyncEnumerable<UnifiedStreamingChunk> ProcessAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessUnified",
            ActivityKind.Server
        );

        activity?.SetTag("conversation.id", request.ConversationId);
        activity?.SetTag("has_multimodal", request.HasMultiModalContent);
        activity?.SetTag("has_history", request.HasPriorMessages);
        activity?.SetTag("thinking.enabled", request.EnableThinking);

        var sw = Stopwatch.StartNew();
        var effectiveCancellation = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, request.CancellationToken)
            .Token;

        // Set conversation ID for middleware to track events
        DelegationEventMiddleware.CurrentConversationId = request.ConversationId;

        var fullResponse = new StringBuilder();
        var subAgentsUsed = new List<string>();
        string? transcription = null;
        UserIntent? classifiedIntent = null;

        try
        {
            var message = request.GetEffectiveMessage();

            _logger.LogInformation(
                "Processing streaming request for conversation {ConversationId}: {MessagePreview}",
                request.ConversationId,
                message.Length > 100 ? message[..100] + "..." : message
            );

            // Load or create context
            var context = await _contextStore.GetOrCreateContextAsync(request.ConversationId);
            await _contextStore.IncrementTurnAsync(request.ConversationId);

            // Restore prior messages if provided
            if (request.HasPriorMessages)
            {
                await RestorePriorMessagesAsync(request.ConversationId, request.PriorMessages!);
            }

            // Emit thinking chunk while classifying intent
            if (request.EnableThinking)
            {
                yield return new UnifiedStreamingChunk
                {
                    Type = StreamingChunkType.Thinking,
                    Content = "Analyzing your request...\n",
                };
            }

            // Classify intent
            var classificationSw = Stopwatch.StartNew();
            classifiedIntent = await _intentClassifier.ClassifyAsync(
                message,
                context,
                effectiveCancellation
            );
            classificationSw.Stop();

            IntentClassificationCounter.Add(
                1,
                new KeyValuePair<string, object?>(
                    "intent",
                    classifiedIntent.PrimaryIntent.ToString()
                ),
                new KeyValuePair<string, object?>(
                    "confidence",
                    classifiedIntent.Confidence > 0.8 ? "high" : "low"
                )
            );
            IntentClassificationDuration.Record(classificationSw.ElapsedMilliseconds);

            activity?.SetTag("intent.primary", classifiedIntent.PrimaryIntent.ToString());
            activity?.SetTag("intent.confidence", classifiedIntent.Confidence);
            activity?.SetTag("intent.strategy", classifiedIntent.Strategy.ToString());

            _logger.LogInformation(
                "Intent: {Intent} (confidence: {Confidence:P0}), strategy: {Strategy}",
                classifiedIntent.PrimaryIntent,
                classifiedIntent.Confidence,
                classifiedIntent.Strategy
            );

            // Emit intent classification as thinking
            if (request.EnableThinking)
            {
                yield return new UnifiedStreamingChunk
                {
                    Type = StreamingChunkType.Thinking,
                    Content =
                        $"Detected intent: {classifiedIntent.PrimaryIntent} ({classifiedIntent.Confidence:P0} confidence)\n"
                        + $"Strategy: {classifiedIntent.Strategy}\n"
                        + (
                            classifiedIntent.RequiredSubAgents.Count > 0
                                ? $"Delegating to: {string.Join(", ", classifiedIntent.RequiredSubAgents)}\n"
                                : ""
                        ),
                };
            }

            // Update context with extracted entities
            if (classifiedIntent.Entities.Count > 0)
            {
                await _contextStore.AddEntitiesAsync(
                    request.ConversationId,
                    classifiedIntent.Entities
                );
            }

            // Process based on content type - stream the response
            if (request.HasMultiModalContent)
            {
                // Extract transcription if available
                transcription = ExtractTranscriptionFromContents(request.Contents!);

                // Stream multi-modal response
                await foreach (
                    var chunk in StreamMultiModalAsync(
                        request.Contents!,
                        request.ConversationId,
                        request.EnableThinking,
                        effectiveCancellation
                    )
                )
                {
                    if (chunk.Content != null)
                        fullResponse.Append(chunk.Content);
                    if (chunk.SubAgentName != null && !subAgentsUsed.Contains(chunk.SubAgentName))
                        subAgentsUsed.Add(chunk.SubAgentName);

                    yield return chunk;
                }
            }
            else
            {
                // Stream text response using intelligent pipeline
                await foreach (
                    var chunk in StreamByStrategyAsync(
                        classifiedIntent,
                        request.ConversationId,
                        context,
                        request.EnableThinking,
                        effectiveCancellation
                    )
                )
                {
                    if (chunk.Content != null)
                        fullResponse.Append(chunk.Content);
                    if (chunk.SubAgentName != null && !subAgentsUsed.Contains(chunk.SubAgentName))
                        subAgentsUsed.Add(chunk.SubAgentName);

                    yield return chunk;
                }

                subAgentsUsed.AddRange(
                    classifiedIntent.RequiredSubAgents.Where(a => !subAgentsUsed.Contains(a))
                );
            }

            sw.Stop();

            // Record outcome in context
            if (classifiedIntent.RequiredSubAgents.Count > 0)
            {
                await _contextStore.AddSubAgentFindingAsync(
                    request.ConversationId,
                    new SubAgentFinding
                    {
                        SubAgentName = classifiedIntent.RequiredSubAgents.First(),
                        Summary = classifiedIntent.IntentSummary,
                        FoundAt = DateTime.UtcNow,
                    }
                );
            }

            // Persist conversation turn for multi-turn context awareness
            var agentName = subAgentsUsed.Count > 0 ? subAgentsUsed.First() : "MasterAgent";
            _subAgentThreadManager.AddMemory(
                request.ConversationId,
                agentName,
                message,
                fullResponse.ToString()
            );

            _logger.LogInformation(
                "Persisted conversation turn for {ConversationId}: {Agent} handled request",
                request.ConversationId,
                agentName
            );

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.length", fullResponse.Length);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            var projectionResult = _projectionTools.GetLastProjectionResult();
            _projectionTools.ClearLastProjectionResult();

            // Yield final completion chunk with full response data
            yield return new UnifiedStreamingChunk
            {
                Type = StreamingChunkType.Complete,
                Content = "",
                FinalResult = new UnifiedChatResponse
                {
                    Success = true,
                    Response = fullResponse.ToString(),
                    SubAgentsUsed = subAgentsUsed,
                    TotalDurationMs = sw.ElapsedMilliseconds,
                    ProjectionResult = projectionResult,
                    DetectedIntent = classifiedIntent.PrimaryIntent.ToString(),
                    ExtractedEntities = classifiedIntent.Entities.ToDictionary(
                        e => e.EntityType,
                        e => e.NormalizedValue ?? (object)e.RawValue
                    ),
                    ExecutionStrategy = classifiedIntent.Strategy.ToString(),
                    Transcription = transcription,
                },
                Metadata = new Dictionary<string, object>
                {
                    ["traceId"] = activity?.TraceId.ToString() ?? "",
                    ["totalDurationMs"] = sw.ElapsedMilliseconds,
                    ["responseLength"] = fullResponse.Length,
                    ["hasProjectionResult"] = projectionResult != null,
                },
            };
        }
        finally
        {
            // Cleanup
            DelegationEventMiddleware.CleanupConversation(request.ConversationId);
            DelegationEventMiddleware.CurrentConversationId = null;
        }
    }

    /// <summary>
    /// Stream response based on the determined execution strategy.
    /// Yields delegation events BEFORE content as they occur during tool execution.
    /// </summary>
    private async IAsyncEnumerable<UnifiedStreamingChunk> StreamByStrategyAsync(
        UserIntent intent,
        string conversationId,
        ConversationContext context,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Get or create thread for conversation history
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

        // Build the message with context briefing for complex requests
        var briefing = await _contextStore.GenerateSubAgentBriefingAsync(
            conversationId,
            intent.RequiredSubAgents.FirstOrDefault() ?? "MasterAgent",
            intent
        );

        var briefingString = briefing.ToPromptString();
        var enrichedMessage = !string.IsNullOrEmpty(briefingString)
            ? $"{briefingString}\n\n{intent.IntentSummary}"
            : intent.IntentSummary;

        // Clear any stale events from previous requests (shouldn't be any, but just in case)
        while (DelegationEventMiddleware.TryGetNextEvent(conversationId, out _))
        {
            // Just clearing stale events
        }

        // Use a flag to track if we've yielded first content
        var isFirstContent = true;

        // CRITICAL: Re-set conversation ID right before streaming
        // This ensures it's set even if there were async context switches
        DelegationEventMiddleware.CurrentConversationId = conversationId;

        Console.WriteLine(
            $"🚀 STARTING STREAM [{DateTime.UtcNow:HH:mm:ss.fff}]: Beginning agent streaming for {conversationId}, CurrentConversationId={DelegationEventMiddleware.CurrentConversationId}"
        );

        // Stream from the agent - tools execute during this call
        // NOTE: Delegation events are pushed directly via SignalR for real-time delivery,
        // so we don't yield them here (that would cause duplicates)
        await foreach (var chunk in _masterAgent.Value.RunStreamingAsync(enrichedMessage, thread))
        {
            // Drain events from queue to clear them (they were already sent via SignalR)
            // This prevents memory buildup in the event queue
            if (isFirstContent && chunk.Text != null)
            {
                isFirstContent = false;
                // Just clear the queue - events already sent via SignalR
                while (DelegationEventMiddleware.TryGetNextEvent(conversationId, out _))
                {
                    // Events already pushed via SignalR, just clearing queue
                }
            }

            // Clear any events that arrived between chunks
            while (DelegationEventMiddleware.TryGetNextEvent(conversationId, out _))
            {
                // Events already pushed via SignalR, just clearing queue
            }

            // Yield content only
            if (chunk.Text != null)
            {
                yield return new UnifiedStreamingChunk
                {
                    Type = StreamingChunkType.Content,
                    Content = chunk.Text,
                };
            }
        }

        // Clear any final events after streaming completes
        while (DelegationEventMiddleware.TryGetNextEvent(conversationId, out _))
        {
            // Events already pushed via SignalR, just clearing queue
        }
    }

    /// <summary>
    /// Stream multi-modal response
    /// </summary>
    private async IAsyncEnumerable<UnifiedStreamingChunk> StreamMultiModalAsync(
        List<AIContent> contents,
        string conversationId,
        bool enableThinking,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);
        var chatMessage = new ChatMessage(ChatRole.User, contents);

        await foreach (var update in _masterAgent.Value.RunStreamingAsync(chatMessage, thread))
        {
            if (update.Contents is { Count: > 0 })
            {
                foreach (var contentItem in update.Contents)
                {
                    if (contentItem is TextContent textContent)
                    {
                        yield return new UnifiedStreamingChunk
                        {
                            Type = StreamingChunkType.Content,
                            Content = textContent.Text,
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Convert delegation event to unified streaming chunk
    /// </summary>
    private static UnifiedStreamingChunk ConvertDelegationToUnifiedChunk(DelegationEvent evt)
    {
        return new UnifiedStreamingChunk
        {
            Type = evt.Type switch
            {
                DelegationEventType.SubAgentDelegationStart =>
                    StreamingChunkType.SubAgentDelegation,
                DelegationEventType.SubAgentDelegationComplete =>
                    StreamingChunkType.SubAgentComplete,
                DelegationEventType.ToolExecutionStart => StreamingChunkType.ToolExecution,
                DelegationEventType.ToolExecutionComplete => StreamingChunkType.ToolExecution,
                _ => StreamingChunkType.Content,
            },
            SubAgentName = evt.SubAgentName,
            ToolName = evt.ToolName,
            Content = evt.Result,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = evt.Type.ToString(),
                ["timestamp"] = evt.Timestamp,
            },
        };
    }

    /// <summary>
    /// Restore prior messages to context (for conversation continuity)
    /// </summary>
    private async Task RestorePriorMessagesAsync(
        string conversationId,
        IEnumerable<ConversationMessage> priorMessages
    )
    {
        foreach (var msg in priorMessages)
        {
            if (msg.Role == "assistant" && !string.IsNullOrEmpty(msg.SubAgentName))
            {
                await _contextStore.AddSubAgentFindingAsync(
                    conversationId,
                    new SubAgentFinding
                    {
                        SubAgentName = msg.SubAgentName,
                        Summary =
                            msg.Content.Length > 200 ? msg.Content[..200] + "..." : msg.Content,
                        FoundAt = DateTime.UtcNow,
                    }
                );
            }
        }
    }

    /// <summary>
    /// Extract transcription from multi-modal contents if available
    /// </summary>
    private static string? ExtractTranscriptionFromContents(List<AIContent> contents)
    {
        var textContent = contents.OfType<TextContent>().FirstOrDefault();
        if (
            textContent?.AdditionalProperties?.TryGetValue("transcription", out var transcription)
            == true
        )
        {
            return transcription?.ToString();
        }
        return null;
    }
}

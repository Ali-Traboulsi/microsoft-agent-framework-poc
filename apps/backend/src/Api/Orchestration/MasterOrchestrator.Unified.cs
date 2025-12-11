using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Domain.Reasoning;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Unified streaming processing
/// This is the primary entry point for all requests - STREAMING ONLY
/// </summary>
public partial class MasterOrchestratorHelper
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

            // Check if this looks like a follow-up answer (short message after conversation started)
            var swarmHistory = GetOrCreateSwarmHistory(request.ConversationId);
            var isLikelyFollowUp = swarmHistory.Messages.Count > 0 && message.Length < 50;
            var classificationSw = Stopwatch.StartNew();

            if (isLikelyFollowUp)
            {
                // Skip complex intent classification for likely follow-ups
                // The Swarm LLM will understand the context from conversation history
                // Set confidence to 0.95 to skip chain-of-thought reasoning (triggers at < 0.9)
                classifiedIntent = new UserIntent
                {
                    OriginalMessage = message,
                    PrimaryIntent = IntentType.Clarification,
                    Confidence = 0.95, // High confidence to skip reasoning
                    Strategy = ExecutionStrategy.SingleAgent,
                    IntentSummary = $"Follow-up response: {message}",
                    RequiredSubAgents = [],
                    Entities = [],
                };
                classificationSw.Stop();

                _logger.LogInformation(
                    "🔄 Detected likely follow-up (short message with history): '{Message}'",
                    message
                );

                activity?.SetTag("intent.source", "follow_up_detection");
            }
            else
            {
                // Try fast pattern matching first (for obvious intents)
                // Pass context to enable follow-up detection (e.g., "ACC001" after being asked for account ID)
                var fastMatch = _fastIntentMatcher.TryMatch(message, context);

                if (fastMatch != null && fastMatch.Confidence >= 0.85)
                {
                    // Fast path - use pattern-matched intent
                    classifiedIntent = fastMatch.ToUserIntent(message);
                    classificationSw.Stop();

                    _logger.LogInformation(
                        "FastIntentMatcher matched: {Intent} (confidence: {Confidence:P0}) - Reason: {Reason}",
                        classifiedIntent.PrimaryIntent,
                        classifiedIntent.Confidence,
                        fastMatch.MatchReason
                    );

                    activity?.SetTag("intent.source", "fast_match");
                }
                else
                {
                    // Fall back to LLM classification
                    classifiedIntent = await _intentClassifier.ClassifyAsync(
                        message,
                        context,
                        effectiveCancellation
                    );
                    classificationSw.Stop();

                    activity?.SetTag("intent.source", "llm_classifier");
                }
            }

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

            // Emit human-readable thinking/reasoning
            if (request.EnableThinking && !isLikelyFollowUp)
            {
                // For complex or uncertain cases, use the reasoning engine
                if (classifiedIntent.Confidence < 0.85 || classifiedIntent.IsComposite)
                {
                    var thoughtChain = await _reasoningEngine.ReasonAsync(
                        message,
                        classifiedIntent,
                        context,
                        effectiveCancellation
                    );

                    // Format reasoning in human-readable way
                    var reasoningText = FormatReasoningAsHumanReadable(thoughtChain);
                    yield return new UnifiedStreamingChunk
                    {
                        Type = StreamingChunkType.Thinking,
                        Content = reasoningText,
                    };
                }
                else
                {
                    // Simple case - just show understanding
                    var humanReadableThinking = GenerateHumanReadableThinking(classifiedIntent);
                    yield return new UnifiedStreamingChunk
                    {
                        Type = StreamingChunkType.Thinking,
                        Content = humanReadableThinking,
                    };
                }
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
                // Use Swarm pattern for ALL text requests (single or multi-agent)
                // The LLM naturally decides which sub-agents to call based on the request
                // This replaces the complex composite/single agent branching logic
                _logger.LogInformation(
                    "Swarm execution for: {Intent}, classified agents: {Agents}",
                    classifiedIntent.PrimaryIntent,
                    string.Join(", ", classifiedIntent.RequiredSubAgents)
                );

                await foreach (
                    var chunk in StreamSwarmAsync(
                        message,
                        classifiedIntent,
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

            // Detect if the response is asking for information and create pending action
            var responseText = fullResponse.ToString();
            _logger.LogDebug(
                "Analyzing response for pending actions. Response length: {Length}, First 200 chars: {Preview}",
                responseText.Length,
                responseText.Length > 200 ? responseText[..200] : responseText
            );

            var pendingAction = DetectPendingActionFromResponse(responseText, classifiedIntent);
            if (pendingAction != null)
            {
                await _contextStore.AddPendingActionAsync(request.ConversationId, pendingAction);
                _logger.LogInformation(
                    "✅ Created pending action for {ConversationId}: Description='{Action}', RequiredAgent='{Agent}', EntityType='{EntityType}'",
                    request.ConversationId,
                    pendingAction.Description,
                    pendingAction.RequiredAgent,
                    pendingAction.Parameters.GetValueOrDefault("entityType", "unknown")
                );
            }
            else
            {
                _logger.LogDebug(
                    "No pending action detected for {ConversationId}",
                    request.ConversationId
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
}

using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Unified request that can contain any type of input
/// The intelligent pipeline determines how to process based on content
/// </summary>
public class UnifiedChatRequest
{
    /// <summary>
    /// Unique identifier for this conversation
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Text message from the user (can be null if only sending files)
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Multi-modal content: images, audio, documents (optional)
    /// </summary>
    public List<AIContent>? Contents { get; init; }

    /// <summary>
    /// Prior conversation messages for context restoration (optional)
    /// If not provided, context is loaded from the context store
    /// </summary>
    public List<ConversationMessage>? PriorMessages { get; init; }

    /// <summary>
    /// Enable extended reasoning/thinking mode
    /// </summary>
    public bool EnableThinking { get; init; } = false;

    /// <summary>
    /// Cancellation token for the request
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;

    // Helper properties

    /// <summary>
    /// Whether this request has multi-modal content
    /// </summary>
    public bool HasMultiModalContent => Contents?.Count > 0;

    /// <summary>
    /// Whether this request has prior conversation history
    /// </summary>
    public bool HasPriorMessages => PriorMessages?.Count > 0;

    /// <summary>
    /// Get the effective message (from Message or extracted from Contents)
    /// </summary>
    public string GetEffectiveMessage()
    {
        if (!string.IsNullOrEmpty(Message))
            return Message;

        // Extract text from contents if no message provided
        var textContent = Contents?.OfType<TextContent>().FirstOrDefault();

        return textContent?.Text ?? string.Empty;
    }
}

/// <summary>
/// Unified response that can contain all types of output
/// The response type is determined by what the model produces
/// </summary>
public class UnifiedChatResponse
{
    /// <summary>
    /// Whether the request was processed successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The main text response
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Which sub-agents were used to process the request
    /// </summary>
    public List<string> SubAgentsUsed { get; init; } = [];

    /// <summary>
    /// Total processing time in milliseconds
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// Structured projection result (if a projection was calculated)
    /// </summary>
    public ProjectionResult? ProjectionResult { get; init; }

    /// <summary>
    /// Structured data extracted/generated (if applicable)
    /// This can contain any structured output the model produces
    /// </summary>
    public Dictionary<string, object>? StructuredData { get; init; }

    /// <summary>
    /// The detected intent that was processed
    /// </summary>
    public string? DetectedIntent { get; init; }

    /// <summary>
    /// Entities extracted from the request
    /// </summary>
    public Dictionary<string, object>? ExtractedEntities { get; init; }

    /// <summary>
    /// Execution strategy used
    /// </summary>
    public string? ExecutionStrategy { get; init; }

    /// <summary>
    /// Audio transcription if audio was processed
    /// </summary>
    public string? Transcription { get; init; }

    // Helper properties

    public bool HasProjectionResult => ProjectionResult != null;
    public bool HasStructuredData => StructuredData?.Count > 0;
    public bool HasTranscription => !string.IsNullOrEmpty(Transcription);
}

/// <summary>
/// Streaming chunk from the unified processing
/// </summary>
public class UnifiedStreamingChunk
{
    /// <summary>
    /// Type of this chunk
    /// </summary>
    public required StreamingChunkType Type { get; init; }

    /// <summary>
    /// Text content of this chunk
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Sub-agent name if this chunk is from a sub-agent
    /// </summary>
    public string? SubAgentName { get; init; }

    /// <summary>
    /// Tool being executed if applicable
    /// </summary>
    public string? ToolName { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    // Workflow progress fields
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public string? StepNameAr { get; init; }
    public int? StepNumber { get; init; }
    public int? TotalSteps { get; init; }
    public long? StepDurationMs { get; init; }
    public string? StepDetails { get; init; }

    /// <summary>
    /// Final result (included in Complete chunk)
    /// </summary>
    public UnifiedChatResponse? FinalResult { get; init; }

    /// <summary>
    /// Whether this is the final chunk
    /// </summary>
    public bool IsComplete => Type == StreamingChunkType.Complete;
}

/// <summary>
/// Types of streaming chunks
/// </summary>
public enum StreamingChunkType
{
    /// <summary>Thinking/reasoning about the request</summary>
    Thinking,

    /// <summary>Chain-of-thought reasoning step</summary>
    Reasoning,

    /// <summary>Regular content being streamed</summary>
    Content,

    /// <summary>A tool is being executed</summary>
    ToolExecution,

    /// <summary>Delegating to a sub-agent</summary>
    SubAgentDelegation,

    /// <summary>Sub-agent completed its work</summary>
    SubAgentComplete,

    /// <summary>Audio transcription result</summary>
    Transcription,

    /// <summary>A workflow step is starting</summary>
    StepStart,

    /// <summary>A workflow step completed</summary>
    StepComplete,

    /// <summary>General progress update</summary>
    Progress,

    /// <summary>Processing is complete</summary>
    Complete,

    /// <summary>An error occurred</summary>
    Error,
}

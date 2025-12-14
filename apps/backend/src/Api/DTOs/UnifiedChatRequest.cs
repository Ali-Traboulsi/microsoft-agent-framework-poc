namespace AgentFrameworkQuickStart.Api.DTOs;

/// <summary>
/// Unified request for all chat types - text, multimodal, or both.
/// All requests go through thread-based persistence for consistent memory.
/// </summary>
public record UnifiedThreadedChatRequest
{
    /// <summary>
    /// Optional text message. Can be used alone or combined with Contents.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Optional list of multimodal content items (images, audio, documents, URIs).
    /// Can be used alone or combined with Message.
    /// </summary>
    public List<ContentInput>? Contents { get; init; }

    /// <summary>
    /// Optional thread ID for continuing an existing conversation.
    /// If null/empty, a new thread will be created.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// Optional conversation ID for memory/context tracking.
    /// If not provided, uses ThreadId as the conversation ID.
    /// </summary>
    public string? ConversationId { get; init; }

    /// <summary>
    /// Enable extended reasoning mode (step-by-step thinking).
    /// </summary>
    public bool EnableThinking { get; init; } = false;

    /// <summary>
    /// Check if this request has any text content.
    /// </summary>
    public bool HasTextContent =>
        !string.IsNullOrWhiteSpace(Message)
        || Contents?.Any(c =>
            c.Type?.ToLowerInvariant() == "text" && !string.IsNullOrWhiteSpace(c.Text)
        ) == true;

    /// <summary>
    /// Check if this request has any multimodal content (non-text).
    /// </summary>
    public bool HasMultiModalContent =>
        Contents?.Any(c => c.Type?.ToLowerInvariant() != "text") == true;

    /// <summary>
    /// Check if this request has any content at all.
    /// </summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(Message) || Contents?.Count > 0;

    /// <summary>
    /// Create from a simple text message (convenience factory).
    /// </summary>
    public static UnifiedThreadedChatRequest FromText(
        string message,
        string? threadId = null,
        string? conversationId = null,
        bool enableThinking = false
    ) =>
        new()
        {
            Message = message,
            ThreadId = threadId,
            ConversationId = conversationId,
            EnableThinking = enableThinking,
        };

    /// <summary>
    /// Create from existing MultiModalChatRequest (migration helper).
    /// </summary>
    public static UnifiedThreadedChatRequest FromMultiModal(MultiModalChatRequest request) =>
        new()
        {
            Message = request.Message,
            Contents = request.Contents,
            ThreadId = request.ThreadId,
            ConversationId = request.ConversationId,
            EnableThinking = request.EnableThinking,
        };
}

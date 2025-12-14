namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a memory entry from a sub-agent interaction within a conversation.
/// This is used to share context across different sub-agents in the same conversation.
/// Note: ConversationId is a session identifier, NOT a foreign key to ChatThread.
/// </summary>
public class ConversationMemoryEntry
{
    public Guid Id { get; set; }

    /// <summary>
    /// The conversation/session ID this memory belongs to.
    /// This is an arbitrary GUID used for session tracking, not a FK to ChatThread.
    /// </summary>
    public Guid ConversationId { get; set; }

    /// <summary>
    /// Optional reference to a ChatThread (if the conversation is thread-based)
    /// </summary>
    public Guid? ThreadId { get; set; }

    /// <summary>
    /// The name of the sub-agent that handled this interaction
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// The user's request/query
    /// </summary>
    public string UserRequest { get; set; } = string.Empty;

    /// <summary>
    /// The agent's response (may be truncated for storage efficiency)
    /// </summary>
    public string AgentResponse { get; set; } = string.Empty;

    /// <summary>
    /// When this interaction occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Order within the conversation (for maintaining sequence)
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Navigation property to the parent thread (optional, for EF relationships)
    /// </summary>
    public ChatThread? Thread { get; set; }
}

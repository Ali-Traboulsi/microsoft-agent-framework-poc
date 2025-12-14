using System.Text.Json;

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a message in a chat thread
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent thread
    /// </summary>
    public Guid ThreadId { get; set; }

    /// <summary>
    /// Navigation property to the parent thread
    /// </summary>
    public ChatThread Thread { get; set; } = null!;

    /// <summary>
    /// Message role: User, Assistant, System, Tool
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// The message content (text)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Which sub-agent handled this message (if applicable)
    /// </summary>
    public string? SubAgentName { get; set; }

    /// <summary>
    /// Tool calls made during this message (JSON serialized)
    /// </summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>
    /// Additional metadata like projectionResult, workflow progress (JSON serialized)
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Multimodal attachments stored as JSON (base64 images, audio transcriptions, etc.)
    /// Allows reconstruction of full context when loading thread history
    /// </summary>
    public string? AttachmentsJson { get; set; }

    /// <summary>
    /// Order of the message in the thread
    /// </summary>
    public int SequenceNumber { get; set; }
}

/// <summary>
/// Message role enumeration
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System,
    Tool,
}

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Represents a chat conversation thread
/// </summary>
public class ChatThread
{
    public Guid Id { get; set; }

    /// <summary>
    /// Thread title, auto-generated from first message
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional summary of the conversation
    /// </summary>
    public string? Summary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the thread is archived
    /// </summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// Messages in this thread
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

using System.ComponentModel.DataAnnotations;

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Database entity for storing conversation summaries.
/// These are AI-generated summaries of past conversations for long-term memory retrieval.
/// </summary>
public class ConversationSummaryEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The original conversation/thread ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ConversationId { get; set; }

    /// <summary>
    /// User ID if available
    /// </summary>
    [MaxLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// Brief title of the conversation
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    /// <summary>
    /// AI-generated summary (2-3 sentences)
    /// </summary>
    [Required]
    public required string Summary { get; set; }

    /// <summary>
    /// JSON serialized list of topics
    /// </summary>
    public string TopicsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of entities mentioned
    /// </summary>
    public string EntitiesJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of actions performed
    /// </summary>
    public string ActionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of decisions
    /// </summary>
    public string DecisionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of pending follow-ups
    /// </summary>
    public string PendingFollowUpsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized list of sub-agents used
    /// </summary>
    public string SubAgentsUsedJson { get; set; } = "[]";

    /// <summary>
    /// Satisfaction indicator if available
    /// </summary>
    [MaxLength(50)]
    public string? SatisfactionIndicator { get; set; }

    /// <summary>
    /// JSON serialized multi-modal content references
    /// </summary>
    public string MultiModalContentJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized keywords for search
    /// </summary>
    public string KeywordsJson { get; set; } = "[]";

    /// <summary>
    /// Importance score (0-1)
    /// </summary>
    public double ImportanceScore { get; set; } = 0.5;

    /// <summary>
    /// When the conversation started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the conversation ended
    /// </summary>
    public DateTime EndedAt { get; set; }

    /// <summary>
    /// Number of turns in the conversation
    /// </summary>
    public int TurnCount { get; set; }

    /// <summary>
    /// When this summary was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional link to ChatThread
    /// </summary>
    public Guid? ThreadId { get; set; }

    public ChatThread? Thread { get; set; }
}

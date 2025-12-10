using System.ComponentModel.DataAnnotations;

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Database entity for storing conversation context
/// This is the persisted version of ConversationContext domain model
/// </summary>
public class ConversationContextEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The conversation ID this context belongs to
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ConversationId { get; set; }

    /// <summary>
    /// User ID if authenticated
    /// </summary>
    [MaxLength(100)]
    public string? UserId { get; set; }

    /// <summary>
    /// JSON serialized entities dictionary
    /// </summary>
    public string EntitiesJson { get; set; } = "{}";

    /// <summary>
    /// JSON serialized goals list
    /// </summary>
    public string GoalsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized decisions list
    /// </summary>
    public string DecisionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized sub-agent findings
    /// </summary>
    public string SubAgentFindingsJson { get; set; } = "{}";

    /// <summary>
    /// JSON serialized pending actions
    /// </summary>
    public string PendingActionsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized active constraints
    /// </summary>
    public string ActiveConstraintsJson { get; set; } = "[]";

    /// <summary>
    /// Detected user expertise level
    /// </summary>
    [MaxLength(50)]
    public string UserExpertiseLevel { get; set; } = "intermediate";

    /// <summary>
    /// Preferred language for responses
    /// </summary>
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// User's apparent risk tolerance
    /// </summary>
    [MaxLength(50)]
    public string InferredRiskTolerance { get; set; } = "moderate";

    /// <summary>
    /// Number of turns in this conversation
    /// </summary>
    public int TurnCount { get; set; }

    /// <summary>
    /// When the conversation started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional link to ChatThread
    /// </summary>
    public Guid? ThreadId { get; set; }

    public ChatThread? Thread { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace AgentFrameworkQuickStart.Models;

/// <summary>
/// Database entity for persistent user memory across conversations.
/// Stores learned preferences, frequent entities, and facts about the user.
/// </summary>
public class UserMemoryEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User identifier - can be a session ID, device ID, or authenticated user ID
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string UserId { get; set; }

    /// <summary>
    /// User's preferred language
    /// </summary>
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// Detected expertise level (Beginner, Intermediate, Advanced, Expert)
    /// </summary>
    [MaxLength(20)]
    public string ExpertiseLevel { get; set; } = "Intermediate";

    /// <summary>
    /// Inferred risk tolerance (Conservative, Moderate, Aggressive, VeryAggressive)
    /// </summary>
    [MaxLength(20)]
    public string RiskTolerance { get; set; } = "Moderate";

    /// <summary>
    /// JSON serialized frequent accounts
    /// </summary>
    public string FrequentAccountsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized frequent portfolios
    /// </summary>
    public string FrequentPortfoliosJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized frequent funds
    /// </summary>
    public string FrequentFundsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized user preferences
    /// </summary>
    public string PreferencesJson { get; set; } = "{}";

    /// <summary>
    /// JSON serialized memorized facts
    /// </summary>
    public string FactsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized investment interests
    /// </summary>
    public string InterestsJson { get; set; } = "[]";

    /// <summary>
    /// JSON serialized topic frequency map
    /// </summary>
    public string TopicFrequencyJson { get; set; } = "{}";

    /// <summary>
    /// JSON serialized communication preferences
    /// </summary>
    public string CommunicationStyleJson { get; set; } = "{}";

    /// <summary>
    /// Total number of conversations
    /// </summary>
    public int TotalConversations { get; set; }

    /// <summary>
    /// Total number of turns across all conversations
    /// </summary>
    public int TotalTurns { get; set; }

    /// <summary>
    /// When this memory was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this memory was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

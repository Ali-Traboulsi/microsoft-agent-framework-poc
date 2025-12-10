namespace AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// Represents the structured context for a conversation
/// This replaces the unstructured text-based context
/// </summary>
public class ConversationContext
{
    /// <summary>
    /// The conversation ID this context belongs to
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// User ID if authenticated
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// All entities extracted across the conversation
    /// Key is entity type, value is the most recent value
    /// </summary>
    public Dictionary<string, ExtractedEntity> Entities { get; set; } = new();

    /// <summary>
    /// User goals identified in this conversation
    /// </summary>
    public List<UserGoal> Goals { get; set; } = [];

    /// <summary>
    /// Decisions made during this conversation
    /// </summary>
    public List<DecisionRecord> Decisions { get; set; } = [];

    /// <summary>
    /// Findings from each sub-agent
    /// </summary>
    public Dictionary<string, SubAgentFinding> SubAgentFindings { get; set; } = new();

    /// <summary>
    /// Detected user expertise level
    /// </summary>
    public string UserExpertiseLevel { get; set; } = "intermediate";

    /// <summary>
    /// Preferred language for responses
    /// </summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// User's apparent risk tolerance based on conversation
    /// </summary>
    public string InferredRiskTolerance { get; set; } = "moderate";

    /// <summary>
    /// Active constraints that should be respected
    /// </summary>
    public List<string> ActiveConstraints { get; set; } = [];

    /// <summary>
    /// Pending actions that need follow-up
    /// </summary>
    public List<PendingAction> PendingActions { get; set; } = [];

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

    // Helper methods

    /// <summary>
    /// Get an entity value with fallback
    /// </summary>
    public T GetEntityValue<T>(string entityType, T defaultValue)
    {
        if (Entities.TryGetValue(entityType, out var entity) && entity.NormalizedValue is T value)
            return value;
        return defaultValue;
    }

    /// <summary>
    /// Check if an entity exists
    /// </summary>
    public bool HasEntity(string entityType) => Entities.ContainsKey(entityType);

    /// <summary>
    /// Add or update an entity
    /// </summary>
    public void SetEntity(ExtractedEntity entity)
    {
        Entities[entity.EntityType] = entity;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get the primary goal if one exists
    /// </summary>
    public UserGoal? GetPrimaryGoal() =>
        Goals.FirstOrDefault(g => g.IsPrimary && g.Status == GoalStatus.Active);

    /// <summary>
    /// Generate a summary for injection into sub-agent prompts
    /// </summary>
    public string GenerateSummary()
    {
        var parts = new List<string>();

        // Add primary goal if exists
        var primaryGoal = GetPrimaryGoal();
        if (primaryGoal != null)
            parts.Add($"User's goal: {primaryGoal.Description}");

        // Add key entities
        if (Entities.Count > 0)
        {
            var entitySummary = string.Join(
                ", ",
                Entities.Take(5).Select(e => $"{e.Key}: {e.Value.RawValue}")
            );
            parts.Add($"Known info: {entitySummary}");
        }

        // Add relevant decisions
        var recentDecisions = Decisions.OrderByDescending(d => d.MadeAt).Take(3);
        foreach (var decision in recentDecisions)
        {
            parts.Add($"Decision: {decision.Summary}");
        }

        // Add constraints
        if (ActiveConstraints.Count > 0)
        {
            parts.Add($"Constraints: {string.Join(", ", ActiveConstraints)}");
        }

        return string.Join("\n", parts);
    }
}

/// <summary>
/// Represents a user goal identified in the conversation
/// </summary>
public class UserGoal
{
    public string GoalId { get; set; } = Guid.NewGuid().ToString();
    public required string Description { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    public bool IsPrimary { get; set; }
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<string> RequiredSteps { get; set; } = [];
    public List<string> CompletedSteps { get; set; } = [];
}

public enum GoalStatus
{
    Active,
    Completed,
    Abandoned,
    Blocked,
}

/// <summary>
/// Represents a decision made during the conversation
/// </summary>
public class DecisionRecord
{
    public string DecisionId { get; set; } = Guid.NewGuid().ToString();
    public required string Summary { get; set; }
    public required string MadeBy { get; set; } // SubAgent name or "User" or "System"
    public required string Reasoning { get; set; }
    public DateTime MadeAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Context { get; set; } = new();
    public bool RequiresConfirmation { get; set; }
    public bool IsConfirmed { get; set; }
}

/// <summary>
/// Represents findings from a sub-agent
/// </summary>
public class SubAgentFinding
{
    public required string SubAgentName { get; set; }
    public required string Summary { get; set; }
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> Recommendations { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Represents an action that is pending completion
/// </summary>
public class PendingAction
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString();
    public required string Description { get; set; }
    public required string RequiredAgent { get; set; }
    public ActionPriority Priority { get; set; } = ActionPriority.Medium;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public enum ActionPriority
{
    Low,
    Medium,
    High,
    Critical,
}

namespace AgentFrameworkQuickStart.Core.Domain.Intelligence;

/// <summary>
/// Represents a fully classified user intent with all extracted information
/// </summary>
public class UserIntent
{
    /// <summary>
    /// Unique identifier for this intent classification
    /// </summary>
    public string IntentId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The original user message
    /// </summary>
    public required string OriginalMessage { get; set; }

    /// <summary>
    /// Primary intent classification
    /// </summary>
    public IntentType PrimaryIntent { get; set; }

    /// <summary>
    /// Secondary intents if the request is composite
    /// </summary>
    public List<IntentType> SecondaryIntents { get; set; } = [];

    /// <summary>
    /// Confidence score for the primary intent (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// All entities extracted from the message
    /// </summary>
    public List<ExtractedEntity> Entities { get; set; } = [];

    /// <summary>
    /// Sub-agents required to fulfill this intent
    /// </summary>
    public List<string> RequiredSubAgents { get; set; } = [];

    /// <summary>
    /// Recommended execution strategy
    /// </summary>
    public ExecutionStrategy Strategy { get; set; }

    /// <summary>
    /// Urgency level of the request
    /// </summary>
    public UrgencyLevel Urgency { get; set; } = UrgencyLevel.Medium;

    /// <summary>
    /// Detected language of the request
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Whether the user seems to be a novice or expert
    /// </summary>
    public string UserExpertiseLevel { get; set; } = "intermediate";

    /// <summary>
    /// Sentiment of the message (positive, neutral, negative, frustrated)
    /// </summary>
    public string Sentiment { get; set; } = "neutral";

    /// <summary>
    /// A brief summary of what the user wants
    /// </summary>
    public string IntentSummary { get; set; } = string.Empty;

    /// <summary>
    /// Specific request to pass to the sub-agent (refined from original message)
    /// </summary>
    public string RefinedRequest { get; set; } = string.Empty;

    /// <summary>
    /// Any ambiguities detected that might need clarification
    /// </summary>
    public List<string> Ambiguities { get; set; } = [];

    /// <summary>
    /// Suggested clarifying questions if confidence is low
    /// </summary>
    public List<string> SuggestedClarifications { get; set; } = [];

    /// <summary>
    /// Smart defaults that should be applied
    /// </summary>
    public Dictionary<string, object> AppliedDefaults { get; set; } = new();

    /// <summary>
    /// Timestamp of classification
    /// </summary>
    public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time taken to classify (for performance monitoring)
    /// </summary>
    public long ClassificationDurationMs { get; set; }

    // Helper methods

    /// <summary>
    /// Get a specific entity by type
    /// </summary>
    public ExtractedEntity? GetEntity(string entityType) =>
        Entities.FirstOrDefault(e =>
            e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>
    /// Get the normalized value of an entity, with a default fallback
    /// </summary>
    public T GetEntityValue<T>(string entityType, T defaultValue)
    {
        var entity = GetEntity(entityType);
        if (entity?.NormalizedValue is T value)
            return value;
        return defaultValue;
    }

    /// <summary>
    /// Check if a specific entity was extracted
    /// </summary>
    public bool HasEntity(string entityType) =>
        Entities.Any(e => e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Check if the intent requires multiple sub-agents
    /// </summary>
    public bool IsComposite => RequiredSubAgents.Count > 1 || SecondaryIntents.Count > 0;

    /// <summary>
    /// Check if clarification might be needed
    /// </summary>
    public bool MightNeedClarification => Confidence < 0.7 || Ambiguities.Count > 0;
}

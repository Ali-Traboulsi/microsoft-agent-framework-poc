using AgentFrameworkQuickStart.Core.Interfaces;

namespace AgentFrameworkQuickStart.Core.Domain.Memory;

/// <summary>
/// Context injection result for pre-processing before agent execution.
/// Contains relevant memories to inject into the agent's context.
/// </summary>
public class LongTermMemoryContext
{
    /// <summary>
    /// User's long-term memory (preferences, frequent entities, facts)
    /// </summary>
    public UserMemory? UserMemory { get; set; }

    /// <summary>
    /// Relevant past conversation summaries
    /// </summary>
    public List<ConversationSummary> RelevantConversations { get; set; } = [];

    /// <summary>
    /// Pre-formatted context string for injection into system prompt
    /// </summary>
    public string FormattedContext { get; set; } = string.Empty;

    /// <summary>
    /// Specific facts relevant to current query
    /// </summary>
    public List<MemorizedFact> RelevantFacts { get; set; } = [];

    /// <summary>
    /// Previously used entities that might be relevant
    /// </summary>
    public List<FrequentEntity> RelevantEntities { get; set; } = [];

    /// <summary>
    /// Pending follow-ups from previous conversations
    /// </summary>
    public List<string> PendingFollowUps { get; set; } = [];

    /// <summary>
    /// Recent conversation turns from the current session (for immediate context)
    /// </summary>
    public List<MemoryConversationTurn> RecentTurns { get; set; } = [];

    /// <summary>
    /// Whether any long-term memory was found
    /// </summary>
    public bool HasMemory =>
        UserMemory != null || RelevantConversations.Count > 0 || RecentTurns.Count > 0;

    /// <summary>
    /// Total tokens estimated for context injection
    /// </summary>
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// Result of memory extraction after agent execution
/// </summary>
public class MemoryExtractionResult
{
    /// <summary>
    /// New facts learned about the user
    /// </summary>
    public List<MemorizedFact> NewFacts { get; set; } = [];

    /// <summary>
    /// Preferences detected or updated
    /// </summary>
    public List<UserPreference> PreferencesDetected { get; set; } = [];

    /// <summary>
    /// Entities mentioned that should be tracked
    /// </summary>
    public List<ExtractedMemoryEntity> EntitiesMentioned { get; set; } = [];

    /// <summary>
    /// Topics discussed
    /// </summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>
    /// Actions performed
    /// </summary>
    public List<PerformedAction> ActionsPerformed { get; set; } = [];

    /// <summary>
    /// Any pending follow-ups identified
    /// </summary>
    public List<string> PendingFollowUps { get; set; } = [];

    /// <summary>
    /// Whether expertise level should be updated
    /// </summary>
    public ExpertiseLevel? SuggestedExpertiseLevel { get; set; }

    /// <summary>
    /// Whether risk tolerance should be updated
    /// </summary>
    public RiskTolerance? SuggestedRiskTolerance { get; set; }

    /// <summary>
    /// Multi-modal content descriptions to store
    /// </summary>
    public List<MultiModalReference> MultiModalContent { get; set; } = [];

    /// <summary>
    /// Whether conversation was significant enough to summarize
    /// </summary>
    public bool ShouldCreateSummary { get; set; }

    /// <summary>
    /// Suggested importance score for conversation
    /// </summary>
    public double ConversationImportance { get; set; } = 0.5;
}

/// <summary>
/// Settings for memory retrieval
/// </summary>
public class MemoryRetrievalSettings
{
    /// <summary>
    /// Maximum number of past conversations to retrieve
    /// </summary>
    public int MaxConversations { get; set; } = 5;

    /// <summary>
    /// Maximum number of facts to include
    /// </summary>
    public int MaxFacts { get; set; } = 10;

    /// <summary>
    /// Maximum tokens for memory context
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// How many days back to search for conversations
    /// </summary>
    public int LookbackDays { get; set; } = 30;

    /// <summary>
    /// Minimum importance score for conversations
    /// </summary>
    public double MinImportanceScore { get; set; } = 0.3;

    /// <summary>
    /// Whether to include multi-modal references
    /// </summary>
    public bool IncludeMultiModal { get; set; } = true;

    /// <summary>
    /// Maximum number of recent conversation turns to include
    /// </summary>
    public int MaxRecentTurns { get; set; } = 5;
}

/// <summary>
/// Represents a single turn in the current conversation (for memory context)
/// </summary>
public class MemoryConversationTurn
{
    public int SequenceNumber { get; set; }
    public string UserRequest { get; set; } = string.Empty;
    public string AgentResponse { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

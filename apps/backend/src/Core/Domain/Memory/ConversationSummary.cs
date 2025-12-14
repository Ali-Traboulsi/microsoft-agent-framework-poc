namespace AgentFrameworkQuickStart.Core.Domain.Memory;

/// <summary>
/// Represents a summarized conversation for long-term storage.
/// Contains extracted key information that can be retrieved for context.
/// </summary>
public class ConversationSummary
{
    /// <summary>
    /// The conversation/thread ID
    /// </summary>
    public required string ConversationId { get; set; }

    /// <summary>
    /// User ID if available
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Brief title of what the conversation was about
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// AI-generated summary of the conversation (2-3 sentences)
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// Key topics discussed
    /// </summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>
    /// Entities mentioned (account IDs, portfolio IDs, fund names, amounts)
    /// </summary>
    public List<ExtractedMemoryEntity> Entities { get; set; } = [];

    /// <summary>
    /// Actions/operations performed during the conversation
    /// </summary>
    public List<PerformedAction> ActionsPerformed { get; set; } = [];

    /// <summary>
    /// Decisions made or confirmed by user
    /// </summary>
    public List<string> Decisions { get; set; } = [];

    /// <summary>
    /// Unresolved questions or follow-ups needed
    /// </summary>
    public List<string> PendingFollowUps { get; set; } = [];

    /// <summary>
    /// Sub-agents that participated
    /// </summary>
    public List<string> SubAgentsUsed { get; set; } = [];

    /// <summary>
    /// User satisfaction indicator if available
    /// </summary>
    public string? SatisfactionIndicator { get; set; }

    /// <summary>
    /// Any multi-modal content referenced (descriptions, not raw data)
    /// </summary>
    public List<MultiModalReference> MultiModalContent { get; set; } = [];

    /// <summary>
    /// Keywords for semantic search
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Importance score (0-1) - higher means more likely to be relevant for future context
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
}

/// <summary>
/// Entity extracted for memory storage
/// </summary>
public class ExtractedMemoryEntity
{
    public required string EntityType { get; set; } // account_id, portfolio_id, fund_name, amount
    public required string Value { get; set; }
    public string? Context { get; set; } // How it was used
    public int MentionCount { get; set; } = 1;
}

/// <summary>
/// An action that was performed during the conversation
/// </summary>
public class PerformedAction
{
    public required string ActionType { get; set; } // subscription, fund-in, portfolio-creation, etc.
    public required string Description { get; set; }
    public string? SubAgentName { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool WasSuccessful { get; set; } = true;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Reference to multi-modal content (image, audio) used in conversation
/// </summary>
public class MultiModalReference
{
    public required string ContentType { get; set; } // image, audio, document
    public required string Description { get; set; } // AI-generated description of content
    public string? TranscriptionOrCaption { get; set; }
    public string? StorageReference { get; set; } // Optional path/URL to stored content
    public DateTime ReferencedAt { get; set; } = DateTime.UtcNow;
}

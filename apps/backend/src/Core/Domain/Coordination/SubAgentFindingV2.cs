namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Structured result from a sub-agent's work.
/// This is the primary output format for coordinated sub-agent execution.
/// </summary>
public record SubAgentFindingV2
{
    /// <summary>
    /// Unique identifier for this finding
    /// </summary>
    public string FindingId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Which sub-agent produced this finding
    /// </summary>
    public required string SubAgentName { get; init; }

    /// <summary>
    /// Description of the task that was performed
    /// </summary>
    public required string TaskDescription { get; init; }

    /// <summary>
    /// Status of the task execution
    /// </summary>
    public required FindingStatus Status { get; init; }

    /// <summary>
    /// Human-readable summary of the finding (for display)
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// The raw response text from the agent (for synthesis)
    /// </summary>
    public string? RawResponse { get; init; }

    // ===== STRUCTURED OUTPUTS =====

    /// <summary>
    /// Facts discovered during execution
    /// </summary>
    public List<SharedFact> DiscoveredFacts { get; init; } = [];

    /// <summary>
    /// Recommendations made by the agent
    /// </summary>
    public List<Recommendation> Recommendations { get; init; } = [];

    /// <summary>
    /// Concerns or warnings raised
    /// </summary>
    public List<Concern> Concerns { get; init; } = [];

    /// <summary>
    /// Questions this agent has for other agents
    /// </summary>
    public List<PendingQuestion> QuestionsForOtherAgents { get; init; } = [];

    // ===== EXECUTION METADATA =====

    /// <summary>
    /// When this finding was created
    /// </summary>
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// How long the execution took
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Which tools were invoked
    /// </summary>
    public List<string> ToolsUsed { get; init; } = [];

    /// <summary>
    /// Step ID if part of an execution plan
    /// </summary>
    public string? StepId { get; init; }

    /// <summary>
    /// Check if this finding has blocking concerns
    /// </summary>
    public bool HasBlockingConcerns => Concerns.Any(c => c.Blocking);

    /// <summary>
    /// Check if this finding has unanswered questions
    /// </summary>
    public bool HasPendingQuestions => QuestionsForOtherAgents.Any(q => q.Answer == null);

    /// <summary>
    /// Get high-confidence recommendations (>= 0.8)
    /// </summary>
    public IEnumerable<Recommendation> GetHighConfidenceRecommendations() =>
        Recommendations.Where(r => r.Confidence >= 0.8);
}

/// <summary>
/// Status of a sub-agent finding
/// </summary>
public enum FindingStatus
{
    /// <summary>Task completed successfully</summary>
    Success,

    /// <summary>Task partially completed, some information available</summary>
    PartialSuccess,

    /// <summary>Task needs more information to complete</summary>
    NeedsMoreInfo,

    /// <summary>Task is blocked waiting for another agent</summary>
    Blocked,

    /// <summary>Task failed with an error</summary>
    Error,
}

/// <summary>
/// A recommendation from a sub-agent
/// </summary>
public record Recommendation
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string RecommendationId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Human-readable description of the recommendation
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Type of recommendation
    /// </summary>
    public required RecommendationType Type { get; init; }

    /// <summary>
    /// Confidence in this recommendation (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; } = 0.8;

    /// <summary>
    /// Why this recommendation was made
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// IDs of facts that support this recommendation
    /// </summary>
    public List<string> SupportingFactIds { get; init; } = [];

    /// <summary>
    /// IDs of other recommendations this conflicts with
    /// </summary>
    public List<string> ConflictsWith { get; init; } = [];

    /// <summary>
    /// Priority level (1 = highest)
    /// </summary>
    public int Priority { get; init; } = 5;

    /// <summary>
    /// Which agent made this recommendation
    /// </summary>
    public string? SourceAgent { get; init; }
}

/// <summary>
/// Types of recommendations
/// </summary>
public enum RecommendationType
{
    /// <summary>Direct action to take</summary>
    Action,

    /// <summary>Warning or caution</summary>
    Caution,

    /// <summary>Informational only</summary>
    Information,

    /// <summary>Alternative option to consider</summary>
    Alternative,

    /// <summary>Prerequisite that must be met first</summary>
    Prerequisite,
}

/// <summary>
/// A concern raised by a sub-agent
/// </summary>
public record Concern
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string ConcernId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Description of the concern
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Severity level
    /// </summary>
    public required ConcernSeverity Severity { get; init; }

    /// <summary>
    /// Category of concern (e.g., "compliance", "risk", "data_quality")
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Whether this concern blocks further progress
    /// </summary>
    public bool Blocking { get; init; }

    /// <summary>
    /// Suggested resolution if available
    /// </summary>
    public string? SuggestedResolution { get; init; }

    /// <summary>
    /// IDs of recommendations affected by this concern
    /// </summary>
    public List<string> AffectedRecommendationIds { get; init; } = [];

    /// <summary>
    /// Which agent raised this concern
    /// </summary>
    public string? SourceAgent { get; init; }
}

/// <summary>
/// Severity levels for concerns
/// </summary>
public enum ConcernSeverity
{
    /// <summary>Informational only</summary>
    Info,

    /// <summary>Warning - proceed with caution</summary>
    Warning,

    /// <summary>Error - should be addressed</summary>
    Error,

    /// <summary>Critical - must be resolved before proceeding</summary>
    Critical,
}

/// <summary>
/// A question one sub-agent has for another
/// </summary>
public record PendingQuestion
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string QuestionId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Which agent is asking
    /// </summary>
    public required string FromAgent { get; init; }

    /// <summary>
    /// Which agent should answer
    /// </summary>
    public required string ToAgent { get; init; }

    /// <summary>
    /// The question being asked
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Additional context for the question
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Whether this question blocks progress
    /// </summary>
    public bool IsBlocking { get; init; }

    /// <summary>
    /// The answer if provided
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>
    /// When the question was asked
    /// </summary>
    public DateTime AskedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the question was answered
    /// </summary>
    public DateTime? AnsweredAt { get; init; }

    /// <summary>
    /// Check if this question has been answered
    /// </summary>
    public bool IsAnswered => Answer != null;
}

namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Record of a conflict between agent recommendations.
/// </summary>
public record ConflictRecord
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string ConflictId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// IDs of conflicting recommendations
    /// </summary>
    public required List<string> ConflictingRecommendationIds { get; init; }

    /// <summary>
    /// Agents involved in the conflict
    /// </summary>
    public required List<string> InvolvedAgents { get; init; }

    /// <summary>
    /// Description of the conflict
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Category of conflict
    /// </summary>
    public ConflictCategory Category { get; init; } = ConflictCategory.Recommendation;

    /// <summary>
    /// Severity of the conflict
    /// </summary>
    public ConflictSeverity Severity { get; init; } = ConflictSeverity.Medium;

    /// <summary>
    /// When the conflict was detected
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Resolution if available
    /// </summary>
    public ConflictResolution? Resolution { get; set; }

    /// <summary>
    /// Check if this conflict has been resolved
    /// </summary>
    public bool IsResolved => Resolution != null;
}

/// <summary>
/// Categories of conflicts
/// </summary>
public enum ConflictCategory
{
    /// <summary>Conflicting recommendations from different agents</summary>
    Recommendation,

    /// <summary>Conflicting facts (different values for same thing)</summary>
    Fact,

    /// <summary>Compliance vs recommendation conflict</summary>
    Compliance,

    /// <summary>Resource or capacity conflict</summary>
    Resource,
}

/// <summary>
/// Severity of conflicts
/// </summary>
public enum ConflictSeverity
{
    /// <summary>Minor conflict, can proceed with either option</summary>
    Low,

    /// <summary>Moderate conflict, should be resolved</summary>
    Medium,

    /// <summary>Serious conflict, must be resolved before proceeding</summary>
    High,

    /// <summary>Critical conflict, blocks further progress</summary>
    Critical,
}

/// <summary>
/// Resolution of a conflict
/// </summary>
public record ConflictResolution
{
    /// <summary>
    /// How the conflict was resolved
    /// </summary>
    public required ConflictResolutionMethod Method { get; init; }

    /// <summary>
    /// Who/what resolved it
    /// </summary>
    public required string ResolvedBy { get; init; }

    /// <summary>
    /// ID of the winning recommendation (if applicable)
    /// </summary>
    public string? WinningRecommendationId { get; init; }

    /// <summary>
    /// Explanation of why this resolution was chosen
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// When the conflict was resolved
    /// </summary>
    public DateTime ResolvedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// How a conflict was resolved
/// </summary>
public enum ConflictResolutionMethod
{
    /// <summary>Master agent decided</summary>
    MasterDecision,

    /// <summary>User was asked to decide</summary>
    UserDecision,

    /// <summary>Agents reached consensus</summary>
    Consensus,

    /// <summary>Priority-based (higher priority wins)</summary>
    Priority,

    /// <summary>Confidence-based (higher confidence wins)</summary>
    Confidence,

    /// <summary>Domain authority (specialist in domain wins)</summary>
    DomainAuthority,

    /// <summary>Merged into a combined recommendation</summary>
    Merged,
}

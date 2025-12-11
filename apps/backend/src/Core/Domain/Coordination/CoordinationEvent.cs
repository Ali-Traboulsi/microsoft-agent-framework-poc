namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// Event emitted during coordinated execution.
/// These events are streamed to the frontend in real-time.
/// </summary>
public record CoordinationEvent
{
    /// <summary>
    /// Type of coordination event
    /// </summary>
    public required CoordinationEventType Type { get; init; }

    /// <summary>
    /// Step ID if this event relates to a specific step
    /// </summary>
    public string? StepId { get; init; }

    /// <summary>
    /// Step number for display (1, 2, 3...)
    /// </summary>
    public int? StepNumber { get; init; }

    /// <summary>
    /// Total steps in the plan
    /// </summary>
    public int? TotalSteps { get; init; }

    /// <summary>
    /// Sub-agent name if applicable
    /// </summary>
    public string? SubAgentName { get; init; }

    /// <summary>
    /// Human-readable content for display
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Arabic content for bilingual support
    /// </summary>
    public string? ContentAr { get; init; }

    /// <summary>
    /// Discovered fact if applicable
    /// </summary>
    public SharedFact? Fact { get; init; }

    /// <summary>
    /// Full finding if step completed
    /// </summary>
    public SubAgentFindingV2? Finding { get; init; }

    /// <summary>
    /// Question if inter-agent communication
    /// </summary>
    public PendingQuestion? Question { get; init; }

    /// <summary>
    /// Conflict if detected
    /// </summary>
    public ConflictRecord? Conflict { get; init; }

    /// <summary>
    /// Duration in milliseconds if applicable
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata for the frontend
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Get an emoji for the event type (for display)
    /// </summary>
    public string GetEmoji() =>
        Type switch
        {
            CoordinationEventType.PlanCreated => "📋",
            CoordinationEventType.PlanStarted => "🚀",
            CoordinationEventType.StepStarted => "▶️",
            CoordinationEventType.StepProgress => "⏳",
            CoordinationEventType.StepCompleted => "✅",
            CoordinationEventType.StepFailed => "❌",
            CoordinationEventType.StepSkipped => "⏭️",
            CoordinationEventType.FactDiscovered => "📊",
            CoordinationEventType.FactUsed => "💡",
            CoordinationEventType.QuestionAsked => "❓",
            CoordinationEventType.QuestionAnswered => "💬",
            CoordinationEventType.RecommendationMade => "💡",
            CoordinationEventType.ConcernRaised => "⚠️",
            CoordinationEventType.ConflictDetected => "⚔️",
            CoordinationEventType.ConflictResolved => "🤝",
            CoordinationEventType.SynthesisStarted => "🔄",
            CoordinationEventType.SynthesisCompleted => "✨",
            CoordinationEventType.PlanCompleted => "🎉",
            CoordinationEventType.Error => "🔥",
            _ => "•",
        };

    /// <summary>
    /// Create a display-friendly string
    /// </summary>
    public string ToDisplayString()
    {
        var emoji = GetEmoji();
        var agent = SubAgentName != null ? $"[{SubAgentName}] " : "";
        var duration = DurationMs.HasValue ? $" ({DurationMs}ms)" : "";
        return $"{emoji} {agent}{Content}{duration}";
    }
}

/// <summary>
/// Types of coordination events for streaming
/// </summary>
public enum CoordinationEventType
{
    // ===== PLAN LIFECYCLE =====
    /// <summary>Execution plan has been created</summary>
    PlanCreated,

    /// <summary>Plan execution has started</summary>
    PlanStarted,

    /// <summary>Plan execution completed</summary>
    PlanCompleted,

    // ===== STEP LIFECYCLE =====
    /// <summary>A step has started executing</summary>
    StepStarted,

    /// <summary>Progress update from a step</summary>
    StepProgress,

    /// <summary>A step completed successfully</summary>
    StepCompleted,

    /// <summary>A step failed</summary>
    StepFailed,

    /// <summary>A step was skipped</summary>
    StepSkipped,

    // ===== FACTS =====
    /// <summary>A new fact was discovered</summary>
    FactDiscovered,

    /// <summary>An existing fact is being used</summary>
    FactUsed,

    // ===== INTER-AGENT COMMUNICATION =====
    /// <summary>An agent asked a question</summary>
    QuestionAsked,

    /// <summary>A question was answered</summary>
    QuestionAnswered,

    // ===== RECOMMENDATIONS & CONCERNS =====
    /// <summary>An agent made a recommendation</summary>
    RecommendationMade,

    /// <summary>An agent raised a concern</summary>
    ConcernRaised,

    // ===== CONFLICTS =====
    /// <summary>A conflict was detected between agents</summary>
    ConflictDetected,

    /// <summary>A conflict was resolved</summary>
    ConflictResolved,

    // ===== SYNTHESIS =====
    /// <summary>Response synthesis has started</summary>
    SynthesisStarted,

    /// <summary>Response synthesis completed</summary>
    SynthesisCompleted,

    // ===== ERRORS =====
    /// <summary>An error occurred</summary>
    Error,
}

/// <summary>
/// Factory for creating common coordination events
/// </summary>
public static class CoordinationEventFactory
{
    public static CoordinationEvent PlanCreated(ExecutionPlan plan) =>
        new()
        {
            Type = CoordinationEventType.PlanCreated,
            Content = plan.GetPlanSummary(),
            Metadata = new Dictionary<string, object>
            {
                ["planId"] = plan.PlanId,
                ["stepCount"] = plan.Steps.Count,
                ["strategy"] = plan.Strategy.ToString(),
            },
        };

    public static CoordinationEvent StepStarted(ExecutionStep step, int totalSteps) =>
        new()
        {
            Type = CoordinationEventType.StepStarted,
            StepId = step.StepId,
            StepNumber = step.Order,
            TotalSteps = totalSteps,
            SubAgentName = step.SubAgentName,
            Content = step.Task,
            ContentAr = null, // Can be localized
        };

    public static CoordinationEvent StepCompleted(ExecutionStep step, int totalSteps) =>
        new()
        {
            Type = CoordinationEventType.StepCompleted,
            StepId = step.StepId,
            StepNumber = step.Order,
            TotalSteps = totalSteps,
            SubAgentName = step.SubAgentName,
            Content = $"Completed: {step.Task}",
            DurationMs = step.DurationMs,
            Finding = step.Result,
        };

    public static CoordinationEvent FactDiscovered(SharedFact fact, string? stepId = null) =>
        new()
        {
            Type = CoordinationEventType.FactDiscovered,
            StepId = stepId,
            SubAgentName = fact.SourceAgent,
            Content = fact.ToDisplayString(),
            Fact = fact,
        };

    public static CoordinationEvent FactUsed(SharedFact fact, string byAgent) =>
        new()
        {
            Type = CoordinationEventType.FactUsed,
            SubAgentName = byAgent,
            Content = $"Using: {fact.ToDisplayString()}",
            Fact = fact,
        };

    public static CoordinationEvent QuestionAsked(PendingQuestion question) =>
        new()
        {
            Type = CoordinationEventType.QuestionAsked,
            SubAgentName = question.FromAgent,
            Content = $"Asking {question.ToAgent}: {question.Question}",
            Question = question,
        };

    public static CoordinationEvent QuestionAnswered(PendingQuestion question) =>
        new()
        {
            Type = CoordinationEventType.QuestionAnswered,
            SubAgentName = question.ToAgent,
            Content = $"Answered: {question.Answer}",
            Question = question,
        };

    public static CoordinationEvent ConcernRaised(Concern concern, string agentName) =>
        new()
        {
            Type = CoordinationEventType.ConcernRaised,
            SubAgentName = agentName,
            Content = $"[{concern.Severity}] {concern.Description}",
        };

    public static CoordinationEvent ConflictDetected(ConflictRecord conflict) =>
        new()
        {
            Type = CoordinationEventType.ConflictDetected,
            Content = conflict.Description,
            Conflict = conflict,
        };

    public static CoordinationEvent SynthesisStarted() =>
        new()
        {
            Type = CoordinationEventType.SynthesisStarted,
            Content = "Synthesizing response from agent findings...",
        };

    public static CoordinationEvent SynthesisCompleted(string response, long durationMs) =>
        new()
        {
            Type = CoordinationEventType.SynthesisCompleted,
            Content = response,
            DurationMs = durationMs,
        };

    public static CoordinationEvent Error(
        string message,
        string? stepId = null,
        string? agentName = null
    ) =>
        new()
        {
            Type = CoordinationEventType.Error,
            StepId = stepId,
            SubAgentName = agentName,
            Content = message,
        };
}

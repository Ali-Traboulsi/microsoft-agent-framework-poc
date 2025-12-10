namespace AgentFrameworkQuickStart.Core.Domain.Reasoning;

/// <summary>
/// Represents a single step in the chain-of-thought reasoning process
/// </summary>
public record ReasoningStep
{
    /// <summary>
    /// Unique identifier for this step
    /// </summary>
    public string StepId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Sequential number of this step (1, 2, 3...)
    /// </summary>
    public int StepNumber { get; init; }

    /// <summary>
    /// Type of reasoning performed in this step
    /// </summary>
    public ReasoningStepType Type { get; init; }

    /// <summary>
    /// The thought/observation at this step
    /// </summary>
    public required string Thought { get; init; }

    /// <summary>
    /// The conclusion or action derived from this thought
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Confidence in this reasoning step (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Supporting evidence for this reasoning step
    /// </summary>
    public List<string> Evidence { get; init; } = [];

    /// <summary>
    /// Duration of this reasoning step in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Timestamp when this step was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Types of reasoning steps in the chain-of-thought process
/// </summary>
public enum ReasoningStepType
{
    /// <summary>Initial understanding of the request</summary>
    Observation,

    /// <summary>Analysis of the request context</summary>
    Analysis,

    /// <summary>Hypothesis about what the user wants</summary>
    Hypothesis,

    /// <summary>Entity extraction and validation</summary>
    EntityExtraction,

    /// <summary>Constraint checking (compliance, limits, etc.)</summary>
    ConstraintCheck,

    /// <summary>Decision about which action to take</summary>
    Decision,

    /// <summary>Planning the execution steps</summary>
    Planning,

    /// <summary>Delegation reasoning (why this sub-agent)</summary>
    DelegationReasoning,

    /// <summary>Risk assessment</summary>
    RiskAssessment,

    /// <summary>Final conclusion and action</summary>
    Conclusion,
}

/// <summary>
/// Complete chain of thought for a request
/// </summary>
public class ThoughtChain
{
    /// <summary>
    /// Unique identifier for this thought chain
    /// </summary>
    public string ChainId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Conversation this chain belongs to
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// The original user request
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// All reasoning steps in order
    /// </summary>
    public List<ReasoningStep> Steps { get; init; } = [];

    /// <summary>
    /// Final decision reached
    /// </summary>
    public ReasoningDecision? FinalDecision { get; set; }

    /// <summary>
    /// Total reasoning time in milliseconds
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// When reasoning started
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When reasoning completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Add a reasoning step
    /// </summary>
    public void AddStep(ReasoningStep step)
    {
        Steps.Add(step with { StepNumber = Steps.Count + 1 });
    }

    /// <summary>
    /// Generate a human-readable summary of the thought chain
    /// </summary>
    public string GenerateSummary()
    {
        if (Steps.Count == 0)
            return "No reasoning steps recorded.";

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("🧠 **Chain of Thought:**\n");

        foreach (var step in Steps)
        {
            var emoji = step.Type switch
            {
                ReasoningStepType.Observation => "👀",
                ReasoningStepType.Analysis => "🔍",
                ReasoningStepType.Hypothesis => "💡",
                ReasoningStepType.EntityExtraction => "📋",
                ReasoningStepType.ConstraintCheck => "✅",
                ReasoningStepType.Decision => "⚖️",
                ReasoningStepType.Planning => "📝",
                ReasoningStepType.DelegationReasoning => "🤝",
                ReasoningStepType.RiskAssessment => "⚠️",
                ReasoningStepType.Conclusion => "🎯",
                _ => "•",
            };

            summary.AppendLine($"{emoji} **{step.Type}**: {step.Thought}");
            if (!string.IsNullOrEmpty(step.Action))
            {
                summary.AppendLine($"   → Action: {step.Action}");
            }
        }

        if (FinalDecision != null)
        {
            summary.AppendLine($"\n**Decision**: {FinalDecision.Action}");
            summary.AppendLine($"**Confidence**: {FinalDecision.Confidence:P0}");
        }

        return summary.ToString();
    }
}

/// <summary>
/// Final decision from the reasoning process
/// </summary>
public class ReasoningDecision
{
    /// <summary>
    /// The primary action to take
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Sub-agent(s) to delegate to (if applicable)
    /// </summary>
    public List<string> DelegateToSubAgents { get; init; } = [];

    /// <summary>
    /// The refined request to pass to sub-agents
    /// </summary>
    public string? RefinedRequest { get; init; }

    /// <summary>
    /// Confidence in this decision (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Reason for this decision
    /// </summary>
    public required string Reasoning { get; init; }

    /// <summary>
    /// Alternative actions considered
    /// </summary>
    public List<AlternativeAction> AlternativesConsidered { get; init; } = [];

    /// <summary>
    /// Any risks identified with this decision
    /// </summary>
    public List<string> IdentifiedRisks { get; init; } = [];

    /// <summary>
    /// Whether user confirmation should be requested
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Reason for requiring confirmation (if applicable)
    /// </summary>
    public string? ConfirmationReason { get; init; }
}

/// <summary>
/// An alternative action that was considered but not chosen
/// </summary>
public class AlternativeAction
{
    /// <summary>
    /// Description of the alternative
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Why this alternative was not chosen
    /// </summary>
    public required string RejectionReason { get; init; }

    /// <summary>
    /// Confidence that would have been assigned
    /// </summary>
    public double Confidence { get; init; }
}

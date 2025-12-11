namespace AgentFrameworkQuickStart.Core.Domain.Coordination;

/// <summary>
/// DAG-based execution plan for coordinated sub-agent work.
/// Supports parallel, sequential, and adaptive execution strategies.
/// </summary>
public class ExecutionPlan
{
    /// <summary>
    /// Unique identifier for this plan
    /// </summary>
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The conversation this plan belongs to
    /// </summary>
    public required string ConversationId { get; init; }

    /// <summary>
    /// Original user request that triggered this plan
    /// </summary>
    public required string UserRequest { get; init; }

    /// <summary>
    /// Intent summary for context
    /// </summary>
    public string? IntentSummary { get; init; }

    /// <summary>
    /// All steps in the plan
    /// </summary>
    public List<ExecutionStep> Steps { get; init; } = [];

    /// <summary>
    /// Execution strategy to use
    /// </summary>
    public PlanStrategy Strategy { get; init; } = PlanStrategy.Adaptive;

    /// <summary>
    /// When the plan was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total duration in milliseconds
    /// </summary>
    public long? TotalDurationMs =>
        CompletedAt.HasValue && StartedAt.HasValue
            ? (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds
            : null;

    /// <summary>
    /// Get steps that can execute now (all dependencies satisfied)
    /// </summary>
    public IEnumerable<ExecutionStep> GetExecutableSteps()
    {
        var completedStepIds = Steps
            .Where(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped)
            .Select(s => s.StepId)
            .ToHashSet();

        return Steps.Where(s =>
            s.Status == StepStatus.Pending && s.DependsOn.All(d => completedStepIds.Contains(d))
        );
    }

    /// <summary>
    /// Get steps that are currently in progress
    /// </summary>
    public IEnumerable<ExecutionStep> GetInProgressSteps() =>
        Steps.Where(s => s.Status == StepStatus.InProgress);

    /// <summary>
    /// Get steps that are blocked
    /// </summary>
    public IEnumerable<ExecutionStep> GetBlockedSteps() =>
        Steps.Where(s => s.Status == StepStatus.Blocked);

    /// <summary>
    /// Check if the plan is complete
    /// </summary>
    public bool IsComplete =>
        Steps.All(s =>
            s.Status == StepStatus.Completed
            || s.Status == StepStatus.Skipped
            || s.Status == StepStatus.Failed
        );

    /// <summary>
    /// Check if the plan succeeded (all required steps completed)
    /// </summary>
    public bool IsSuccessful =>
        Steps.Where(s => s.IsCriticalPath).All(s => s.Status == StepStatus.Completed);

    /// <summary>
    /// Get plan summary for display
    /// </summary>
    public string GetPlanSummary()
    {
        var lines = new List<string> { $"Plan: {Steps.Count} steps, {Strategy} strategy" };

        foreach (var step in Steps.OrderBy(s => s.Order))
        {
            var deps =
                step.DependsOn.Count > 0 ? $" (after: {string.Join(", ", step.DependsOn)})" : "";
            lines.Add($"  {step.Order}. [{step.SubAgentName}] {step.Task}{deps}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Mark a step as started
    /// </summary>
    public void StartStep(string stepId)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.InProgress;
            step.StartedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Mark a step as completed
    /// </summary>
    public void CompleteStep(string stepId, SubAgentFindingV2? result = null)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            step.Result = result;
            if (step.StartedAt.HasValue)
            {
                step.DurationMs = (long)(DateTime.UtcNow - step.StartedAt.Value).TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Mark a step as failed
    /// </summary>
    public void FailStep(string stepId, string errorMessage)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorMessage = errorMessage;
        }
    }
}

/// <summary>
/// Execution strategy for the plan
/// </summary>
public enum PlanStrategy
{
    /// <summary>Execute one step at a time in order</summary>
    Sequential,

    /// <summary>Execute all steps that can run in parallel</summary>
    Parallel,

    /// <summary>Dynamically determine based on dependencies</summary>
    Adaptive,

    /// <summary>Master agent reviews each step's output before proceeding</summary>
    Hierarchical,
}

/// <summary>
/// A single step in the execution plan
/// </summary>
public class ExecutionStep
{
    /// <summary>
    /// Unique identifier for this step
    /// </summary>
    public string StepId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Order in the plan (1, 2, 3...)
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// Which sub-agent executes this step
    /// </summary>
    public required string SubAgentName { get; init; }

    /// <summary>
    /// Description of the task to perform
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// Why this step is needed
    /// </summary>
    public string? Rationale { get; init; }

    // ===== DEPENDENCIES =====

    /// <summary>
    /// Step IDs that must complete before this step can run
    /// </summary>
    public List<string> DependsOn { get; init; } = [];

    /// <summary>
    /// Fact IDs that must be available before this step can run
    /// </summary>
    public List<string> RequiredFactIds { get; init; } = [];

    /// <summary>
    /// Fact categories this step needs access to
    /// </summary>
    public List<string> RequiredFactCategories { get; init; } = [];

    // ===== EXECUTION STATE =====

    /// <summary>
    /// Current status of the step
    /// </summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>
    /// When execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When execution completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Result of execution
    /// </summary>
    public SubAgentFindingV2? Result { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    // ===== EXECUTION OPTIONS =====

    /// <summary>
    /// Whether this step can run in parallel with others
    /// </summary>
    public bool CanBeParallelized { get; init; } = true;

    /// <summary>
    /// Whether this step is on the critical path (must succeed)
    /// </summary>
    public bool IsCriticalPath { get; init; } = true;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>
    /// Current retry count
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Timeout for this step in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Check if this step can be retried
    /// </summary>
    public bool CanRetry => RetryCount < MaxRetries;

    /// <summary>
    /// Create a display-friendly string
    /// </summary>
    public string ToDisplayString()
    {
        var status = Status switch
        {
            StepStatus.Pending => "⏳",
            StepStatus.InProgress => "▶️",
            StepStatus.Completed => "✅",
            StepStatus.Failed => "❌",
            StepStatus.Skipped => "⏭️",
            StepStatus.Blocked => "🚫",
            _ => "❓",
        };

        var duration = DurationMs.HasValue ? $" ({DurationMs}ms)" : "";
        return $"{status} Step {Order}: [{SubAgentName}] {Task}{duration}";
    }
}

/// <summary>
/// Status of an execution step
/// </summary>
public enum StepStatus
{
    /// <summary>Waiting to execute</summary>
    Pending,

    /// <summary>Currently executing</summary>
    InProgress,

    /// <summary>Completed successfully</summary>
    Completed,

    /// <summary>Failed with error</summary>
    Failed,

    /// <summary>Skipped (not needed or dependency failed)</summary>
    Skipped,

    /// <summary>Blocked waiting for something</summary>
    Blocked,
}

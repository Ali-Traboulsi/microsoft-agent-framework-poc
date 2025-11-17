namespace AgentFrameworkQuickStart.Api.Workflows;

/// <summary>
/// Represents a multi-step workflow coordinating multiple agents
/// </summary>
public interface IWorkflow
{
    string Name { get; }
    string Description { get; }
    Task<WorkflowResult> ExecuteAsync(Dictionary<string, object> parameters);
}

/// <summary>
/// Result from workflow execution
/// </summary>
public class WorkflowResult
{
    public bool Success { get; init; }
    public required string Summary { get; init; }
    public List<WorkflowStep> Steps { get; init; } = new();
    public long TotalDurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// A single step in a workflow
/// </summary>
public class WorkflowStep
{
    public required string Name { get; init; }
    public required string SubAgentName { get; init; }
    public bool Success { get; init; }
    public string? Result { get; init; }
    public long DurationMs { get; init; }
    public int Order { get; init; }
}

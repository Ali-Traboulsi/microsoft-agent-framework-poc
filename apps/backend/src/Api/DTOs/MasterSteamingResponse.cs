using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Api.DTOs;

/// <summary>
/// Streaming response from master agent
/// </summary>
public class MasterStreamingResponse
{
    public required string Type { get; set; }
    public string? Content { get; set; }
    public string? SubAgentName { get; set; }
    public string? ToolName { get; set; }
    public bool IsComplete { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    // Progress step fields for workflow progress tracking
    public string? StepId { get; set; }
    public string? StepName { get; set; }
    public string? StepNameAr { get; set; }
    public int? StepNumber { get; set; }
    public int? TotalSteps { get; set; }
    public bool? StepCompleted { get; set; }
    public long? StepDurationMs { get; set; }
    public string? StepDetails { get; set; }

    // Projection result (included in Complete response when projection was calculated)
    public ProjectionResult? ProjectionResult { get; set; }
    public bool HasProjectionResult => ProjectionResult != null;
}

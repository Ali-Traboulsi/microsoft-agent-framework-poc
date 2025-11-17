namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Orchestrates multiple sub-agents and coordinates their activities
/// </summary>
public interface IMasterOrchestrator
{
    /// <summary>
    /// Process a user request by analyzing intent and delegating to appropriate sub-agents
    /// </summary>
    /// <param name="userMessage">The user's request</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <returns>The orchestrated response</returns>
    Task<OrchestratorResult> ProcessRequestAsync(string userMessage, string conversationId);

    /// <summary>
    /// Process a user request with streaming response
    /// </summary>
    /// <param name="userMessage">The user's request</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <returns>Stream of orchestrator responses</returns>
    IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingAsync(
        string userMessage,
        string conversationId
    );

    /// <summary>
    /// Get information about available sub-agents
    /// </summary>
    /// <returns>List of sub-agent descriptions</returns>
    List<SubAgentInfo> GetAvailableSubAgents();
}

/// <summary>
/// Information about a sub-agent
/// </summary>
public class SubAgentInfo
{
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string[] Capabilities { get; init; }
}

/// <summary>
/// Final result from the orchestrator
/// </summary>
public class OrchestratorResult
{
    public bool Success { get; init; }
    public required string Response { get; init; }
    public List<string> SubAgentsUsed { get; init; } = new();
    public long TotalDurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Streaming response from the orchestrator
/// </summary>
public class OrchestratorResponse
{
    public ResponseType Type { get; init; }
    public string? Content { get; init; }
    public string? SubAgentName { get; init; }
    public string? ToolName { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Type of orchestrator response
/// </summary>
public enum ResponseType
{
    /// <summary>
    /// Master agent is analyzing the request and deciding on approach
    /// </summary>
    Thinking,

    /// <summary>
    /// Regular content being streamed to user
    /// </summary>
    Content,

    /// <summary>
    /// A tool is being executed
    /// </summary>
    ToolExecution,

    /// <summary>
    /// Delegating to a sub-agent
    /// </summary>
    SubAgentDelegation,

    /// <summary>
    /// Sub-agent has completed its work
    /// </summary>
    SubAgentComplete,

    /// <summary>
    /// Processing is complete
    /// </summary>
    Complete,

    /// <summary>
    /// An error occurred
    /// </summary>
    Error,
}

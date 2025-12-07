namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Response from a sub-agent after processing a request
/// </summary>
public class SubAgentResponse
{
    /// <summary>
    /// Name of the sub-agent that generated this response
    /// </summary>
    public required string SubAgentName { get; init; }

    /// <summary>
    /// Whether the request was handled successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The response text from the sub-agent
    /// </summary>
    public string? Result { get; init; }

    /// <summary>
    /// List of tools that were executed
    /// </summary>
    public List<string> ToolsUsed { get; init; } = new();

    /// <summary>
    /// Additional data or metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration of the request in milliseconds
    /// </summary>
    public long DurationMs { get; init; }
}

/// <summary>
/// Streaming chunk from a sub-agent
/// </summary>
public class SubAgentStreamChunk
{
    /// <summary>
    /// Text content of this chunk
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Tool call information if this chunk represents a tool execution
    /// </summary>
    public ToolCallInfo? ToolCall { get; init; }

    /// <summary>
    /// Whether this is the final chunk
    /// </summary>
    public bool IsComplete { get; init; }
}

/// <summary>
/// Information about a tool call
/// </summary>
public class ToolCallInfo
{
    public required string Name { get; init; }
    public string? Arguments { get; init; }
}

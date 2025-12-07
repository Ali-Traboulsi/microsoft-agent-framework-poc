namespace AgentFrameworkQuickStart.Api.Abstractions;

using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

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
    /// <param name="enableThinking">Enable extended reasoning mode (like ChatGPT o1)</param>
    /// <returns>The orchestrated response</returns>
    Task<OrchestratorResult> ProcessRequestAsync(
        string userMessage,
        string conversationId,
        bool enableThinking = false
    );

    /// <summary>
    /// Process a user request with streaming response
    /// </summary>
    /// <param name="userMessage">The user's request</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <param name="enableThinking">Enable extended reasoning mode (like ChatGPT o1)</param>
    /// <returns>Stream of orchestrator responses</returns>
    IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingAsync(
        string userMessage,
        string conversationId,
        bool enableThinking = false
    );

    /// <summary>
    /// Get information about available sub-agents
    /// </summary>
    /// <returns>List of sub-agent descriptions</returns>
    List<SubAgentInfo> GetAvailableSubAgents();

    /// <summary>
    /// Process a user request and return structured JSON response
    /// </summary>
    /// <param name="userMessage">The user's request</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <returns>Structured JSON response conforming to StructuredAgentResponse schema</returns>
    Task<StructuredOrchestratorResult> ProcessRequestStructuredAsync(
        string userMessage,
        string conversationId
    );

    /// <summary>
    /// Process a multi-modal request (text, images, audio, files)
    /// </summary>
    /// <param name="contents">List of AIContent items (text, images, audio, URIs)</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <param name="enableThinking">Enable extended reasoning mode (like ChatGPT o1)</param>
    /// <returns>The orchestrated response</returns>
    Task<OrchestratorResult> ProcessMultiModalRequestAsync(
        List<Microsoft.Extensions.AI.AIContent> contents,
        string conversationId,
        bool enableThinking = false
    );

    /// <summary>
    /// Process a multi-modal request with streaming response
    /// </summary>
    /// <param name="contents">List of AIContent items (text, images, audio, URIs)</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <param name="enableThinking">Enable extended reasoning mode (like ChatGPT o1)</param>
    /// <returns>Stream of orchestrator responses</returns>
    IAsyncEnumerable<OrchestratorResponse> ProcessMultiModalRequestStreamingAsync(
        List<Microsoft.Extensions.AI.AIContent> contents,
        string conversationId,
        bool enableThinking = false
    );

    /// <summary>
    /// Process a user request with streaming response, including prior conversation history
    /// </summary>
    /// <param name="userMessage">The user's request</param>
    /// <param name="conversationId">Unique identifier for this conversation</param>
    /// <param name="priorMessages">Prior conversation messages to restore context</param>
    /// <param name="enableThinking">Enable extended reasoning mode</param>
    /// <returns>Stream of orchestrator responses</returns>
    IAsyncEnumerable<OrchestratorResponse> ProcessRequestStreamingWithHistoryAsync(
        string userMessage,
        string conversationId,
        IEnumerable<ConversationMessage> priorMessages,
        bool enableThinking = false
    );
}

/// <summary>
/// Represents a message in conversation history
/// </summary>
public class ConversationMessage
{
    public required string Role { get; init; } // "user" or "assistant"
    public required string Content { get; init; }
    public string? SubAgentName { get; init; }
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

    /// <summary>
    /// Structured projection result if a projection was calculated
    /// This allows the API to return structured data alongside the natural language response
    /// </summary>
    public ProjectionResult? ProjectionResult { get; init; }

    /// <summary>
    /// Indicates if the response contains structured projection data
    /// </summary>
    public bool HasProjectionResult => ProjectionResult != null;
}

/// <summary>
/// Structured result from the orchestrator
/// </summary>
public class StructuredOrchestratorResult
{
    public bool Success { get; init; }
    public required StructuredAgentResponse StructuredResponse { get; init; }
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

    // Workflow progress fields (for StepStart, StepComplete, Progress types)
    public string? StepId { get; init; }
    public string? StepName { get; init; }
    public string? StepNameAr { get; init; }
    public int? StepNumber { get; init; }
    public int? TotalSteps { get; init; }
    public long? StepDurationMs { get; init; }
    public string? StepDetails { get; init; }

    // Projection result (included in Complete response when projection was calculated)
    public ProjectionResult? ProjectionResult { get; init; }
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
    /// Audio transcription result
    /// </summary>
    Transcription,

    /// <summary>
    /// Processing is complete
    /// </summary>
    Complete,

    /// <summary>
    /// An error occurred
    /// </summary>
    Error,

    /// <summary>
    /// A workflow step is starting (shows progress)
    /// </summary>
    StepStart,

    /// <summary>
    /// A workflow step has completed
    /// </summary>
    StepComplete,

    /// <summary>
    /// General progress update during workflow execution
    /// </summary>
    Progress,
}

/// <summary>
/// Progress event for workflow step tracking
/// </summary>
public record WorkflowProgressEvent
{
    /// <summary>Step identifier (e.g., "CustomerContext", "HistoricalAnalysis")</summary>
    public required string StepId { get; init; }

    /// <summary>Human-readable step name in English</summary>
    public required string StepName { get; init; }

    /// <summary>Human-readable step name in Arabic</summary>
    public required string StepNameAr { get; init; }

    /// <summary>Current step number (1-based)</summary>
    public int StepNumber { get; init; }

    /// <summary>Total number of steps in workflow</summary>
    public int TotalSteps { get; init; }

    /// <summary>Whether this step has completed</summary>
    public bool IsCompleted { get; init; }

    /// <summary>Duration of this step in milliseconds (set when completed)</summary>
    public long? DurationMs { get; init; }

    /// <summary>Optional details about what was done</summary>
    public string? Details { get; init; }
}

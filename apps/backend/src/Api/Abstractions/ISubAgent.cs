namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Represents a specialized sub-agent that handles specific domain tasks
/// </summary>
public interface ISubAgent
{
    /// <summary>
    /// Unique name of the sub-agent
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Domain or area of expertise
    /// </summary>
    string Domain { get; }

    /// <summary>
    /// List of capabilities this sub-agent provides
    /// </summary>
    string[] Capabilities { get; }

    /// <summary>
    /// Handles a request and returns a response.
    /// Uses conversation-scoped thread to maintain context within the conversation.
    /// </summary>
    /// <param name="request">The task or question to handle</param>
    /// <param name="conversationId">The conversation ID for thread management</param>
    /// <param name="context">Additional context for the request</param>
    /// <returns>Response from the sub-agent</returns>
    Task<SubAgentResponse> HandleRequestAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null
    );

    /// <summary>
    /// Handles a request with streaming response.
    /// Uses conversation-scoped thread to maintain context within the conversation.
    /// </summary>
    /// <param name="request">The task or question to handle</param>
    /// <param name="conversationId">The conversation ID for thread management</param>
    /// <param name="context">Additional context for the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of response chunks</returns>
    IAsyncEnumerable<SubAgentStreamChunk> HandleRequestStreamingAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default
    );
}

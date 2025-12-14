using AgentFrameworkQuickStart.Core.Domain.Memory;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Main interface for long-term memory operations.
/// Implements the AIContextProvider pattern - injecting context before agent runs
/// and extracting memories after agent runs.
/// </summary>
public interface ILongTermMemoryProvider
{
    /// <summary>
    /// Retrieve relevant long-term memory context before agent execution.
    /// This is called BEFORE the agent processes a message (InvokingAsync pattern).
    /// Returns context to inject into the agent's system prompt.
    /// </summary>
    Task<LongTermMemoryContext> RetrieveContextAsync(
        string userId,
        string currentMessage,
        string conversationId,
        MemoryRetrievalSettings? settings = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Extract and store memories after agent execution.
    /// This is called AFTER the agent processes a message (InvokedAsync pattern).
    /// Extracts facts, preferences, and conversation summary.
    /// </summary>
    Task ProcessAndStoreMemoriesAsync(
        string userId,
        string conversationId,
        string userMessage,
        string agentResponse,
        List<string>? subAgentsUsed = null,
        List<string>? multiModalDescriptions = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create a summary for a completed conversation.
    /// Called when a conversation ends or periodically for long conversations.
    /// </summary>
    Task CreateConversationSummaryAsync(
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Format long-term memory context for injection into system prompt.
    /// </summary>
    string FormatContextForInjection(LongTermMemoryContext context);

    /// <summary>
    /// Get pending follow-ups from previous conversations.
    /// </summary>
    Task<List<string>> GetPendingFollowUpsAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Mark a follow-up as completed.
    /// </summary>
    Task CompleteFollowUpAsync(
        string conversationId,
        string followUp,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Clear all long-term memory for a user (GDPR compliance).
    /// </summary>
    Task ClearUserMemoryAsync(string userId, CancellationToken cancellationToken = default);
}

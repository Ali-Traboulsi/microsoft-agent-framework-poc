using AgentFrameworkQuickStart.Core.Domain.Memory;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Service for extracting memorable information from conversations using LLM.
/// Handles fact extraction, preference detection, and conversation summarization.
/// </summary>
public interface IMemoryExtractionService
{
    /// <summary>
    /// Extract memorable information from a single turn of conversation.
    /// Called after each agent response.
    /// </summary>
    Task<MemoryExtractionResult> ExtractMemoriesAsync(
        string userMessage,
        string agentResponse,
        List<string> subAgentsUsed,
        List<string> multiModalDescriptions,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generate a comprehensive summary for a completed conversation.
    /// Called when conversation ends or reaches threshold.
    /// </summary>
    Task<ConversationSummary?> GenerateConversationSummaryAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Determine if a conversation is significant enough to summarize.
    /// </summary>
    Task<bool> ShouldSummarizeConversationAsync(
        string conversationId,
        int turnCount,
        CancellationToken cancellationToken = default
    );
}

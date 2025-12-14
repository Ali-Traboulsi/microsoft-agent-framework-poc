using AgentFrameworkQuickStart.Core.Domain.Memory;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Repository interface for conversation summary operations.
/// Provides CRUD and search operations for long-term conversation memory.
/// </summary>
public interface IConversationSummaryRepository
{
    /// <summary>
    /// Get summary by conversation ID
    /// </summary>
    Task<ConversationSummary?> GetByConversationIdAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Save a conversation summary
    /// </summary>
    Task SaveAsync(ConversationSummary summary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent summaries for a user
    /// </summary>
    Task<List<ConversationSummary>> GetRecentByUserAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Search summaries by keywords/topics for a user
    /// </summary>
    Task<List<ConversationSummary>> SearchAsync(
        string userId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get summaries with pending follow-ups
    /// </summary>
    Task<List<ConversationSummary>> GetWithPendingFollowUpsAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get summaries involving specific entities
    /// </summary>
    Task<List<ConversationSummary>> GetByEntityAsync(
        string userId,
        string entityType,
        string entityValue,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get summaries by topic
    /// </summary>
    Task<List<ConversationSummary>> GetByTopicAsync(
        string userId,
        string topic,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get high-importance summaries for context
    /// </summary>
    Task<List<ConversationSummary>> GetHighImportanceAsync(
        string userId,
        double minScore = 0.7,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get summaries within a time range
    /// </summary>
    Task<List<ConversationSummary>> GetByTimeRangeAsync(
        string userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a conversation summary
    /// </summary>
    Task DeleteAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old summaries (for cleanup)
    /// </summary>
    Task DeleteOldSummariesAsync(
        DateTime olderThan,
        double maxImportanceToDelete = 0.3,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get multi-modal references from past conversations
    /// </summary>
    Task<List<MultiModalReference>> GetMultiModalReferencesAsync(
        string userId,
        string contentType,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    );
}

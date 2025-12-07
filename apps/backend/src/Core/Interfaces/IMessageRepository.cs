using AgentFrameworkQuickStart.Models;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Repository interface for ChatMessage operations
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Get all messages for a thread
    /// </summary>
    Task<IEnumerable<ChatMessage>> GetByThreadIdAsync(
        Guid threadId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a message by ID
    /// </summary>
    Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a message to a thread
    /// </summary>
    Task<ChatMessage> AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple messages to a thread
    /// </summary>
    Task<IEnumerable<ChatMessage>> AddRangeAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update a message
    /// </summary>
    Task<ChatMessage> UpdateAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a message
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the next sequence number for a thread
    /// </summary>
    Task<int> GetNextSequenceNumberAsync(
        Guid threadId,
        CancellationToken cancellationToken = default
    );
}

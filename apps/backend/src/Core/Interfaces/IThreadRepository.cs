using AgentFrameworkQuickStart.Models;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Repository interface for ChatThread operations
/// </summary>
public interface IThreadRepository
{
    /// <summary>
    /// Get all threads (excluding archived by default)
    /// </summary>
    Task<IEnumerable<ChatThread>> GetAllAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a thread by ID with its messages
    /// </summary>
    Task<ChatThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a thread by ID with messages loaded
    /// </summary>
    Task<ChatThread?> GetByIdWithMessagesAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create a new thread
    /// </summary>
    Task<ChatThread> CreateAsync(ChatThread thread, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing thread
    /// </summary>
    Task<ChatThread> UpdateAsync(ChatThread thread, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a thread and all its messages
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a thread (soft delete)
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search threads by title or message content
    /// </summary>
    Task<IEnumerable<ChatThread>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default
    );
}

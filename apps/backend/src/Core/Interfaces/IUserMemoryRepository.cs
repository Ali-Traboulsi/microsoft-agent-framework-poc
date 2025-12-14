using AgentFrameworkQuickStart.Core.Domain.Memory;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Repository interface for long-term user memory operations.
/// Provides CRUD operations for persistent user preferences and learned facts.
/// </summary>
public interface IUserMemoryRepository
{
    /// <summary>
    /// Get user memory by user ID
    /// </summary>
    Task<UserMemory?> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get or create user memory for a user ID
    /// </summary>
    Task<UserMemory> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save user memory (create or update)
    /// </summary>
    Task SaveAsync(UserMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update specific facts in user memory
    /// </summary>
    Task AddFactsAsync(
        string userId,
        IEnumerable<MemorizedFact> facts,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update frequent entities (accounts, portfolios, funds)
    /// </summary>
    Task UpdateFrequentEntityAsync(
        string userId,
        string entityType,
        FrequentEntity entity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update user preferences
    /// </summary>
    Task UpdatePreferencesAsync(
        string userId,
        IEnumerable<UserPreference> preferences,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Increment conversation count for user
    /// </summary>
    Task IncrementConversationCountAsync(
        string userId,
        int turnCount,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update expertise level
    /// </summary>
    Task UpdateExpertiseLevelAsync(
        string userId,
        ExpertiseLevel level,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update risk tolerance
    /// </summary>
    Task UpdateRiskToleranceAsync(
        string userId,
        RiskTolerance tolerance,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete user memory
    /// </summary>
    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get facts relevant to a query (for context injection)
    /// </summary>
    Task<List<MemorizedFact>> GetRelevantFactsAsync(
        string userId,
        string query,
        int maxFacts = 10,
        CancellationToken cancellationToken = default
    );
}

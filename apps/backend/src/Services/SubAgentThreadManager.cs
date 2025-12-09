using System.Collections.Concurrent;
using System.Text;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using AgentFrameworkQuickStart.Models;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// Manages shared conversation memory across all sub-agents using database persistence.
/// All sub-agents within the same conversation share the same memory,
/// allowing context from one sub-agent to be available to others.
///
/// This enables scenarios like:
/// 1. User asks for Fund-In → ExternalApiServices handles it
/// 2. User asks to analyze portfolios → PortfolioManager sees the Fund-In context
/// 3. User asks for projections → ProfitProjection sees all previous context
/// </summary>
public class SubAgentThreadManager(
    IServiceScopeFactory scopeFactory,
    ILogger<SubAgentThreadManager> logger
)
{
    /// <summary>
    /// Stores per-agent threads for actual execution (each agent needs its own thread for tools).
    /// These remain in-memory as they are runtime-only constructs.
    /// </summary>
    private readonly ConcurrentDictionary<string, AgentThread> _agentThreads = new();

    /// <summary>
    /// Get or create a thread for a specific sub-agent in a conversation.
    /// The thread is unique per agent but shares conversation context.
    /// </summary>
    public AgentThread GetOrCreateThread(string conversationId, string subAgentName, AIAgent agent)
    {
        var threadKey = $"{conversationId}:{subAgentName}";

        return _agentThreads.GetOrAdd(
            threadKey,
            _ =>
            {
                logger.LogInformation(
                    "Creating new thread for sub-agent {SubAgent} in conversation {ConversationId}",
                    subAgentName,
                    conversationId
                );
                return agent.GetNewThread();
            }
        );
    }

    /// <summary>
    /// Add a memory entry to the shared conversation memory (persisted to database)
    /// </summary>
    public async Task AddMemoryAsync(
        string conversationId,
        string agentName,
        string userRequest,
        string agentResponse
    )
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            logger.LogWarning(
                "Invalid conversation ID format: {ConversationId}, skipping memory persistence",
                conversationId
            );
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get next sequence number
        var maxSequence =
            await context
                .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
                .MaxAsync(e => (int?)e.SequenceNumber) ?? 0;

        var entry = new ConversationMemoryEntry
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationGuid,
            AgentName = agentName,
            UserRequest = TruncateIfNeeded(userRequest, 2000),
            AgentResponse = TruncateIfNeeded(agentResponse, 4000),
            Timestamp = DateTime.UtcNow,
            SequenceNumber = maxSequence + 1,
        };

        await context.ConversationMemoryEntries.AddAsync(entry);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Persisted memory entry from {AgentName} to conversation {ConversationId}. Sequence: {Sequence}",
            agentName,
            conversationId,
            entry.SequenceNumber
        );
    }

    /// <summary>
    /// Synchronous wrapper for AddMemoryAsync (for compatibility with existing code)
    /// </summary>
    public void AddMemory(
        string conversationId,
        string agentName,
        string userRequest,
        string agentResponse
    )
    {
        // Fire and forget - we don't want to block the response for memory persistence
        _ = Task.Run(async () =>
        {
            try
            {
                await AddMemoryAsync(conversationId, agentName, userRequest, agentResponse);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to persist memory entry for conversation {ConversationId}",
                    conversationId
                );
            }
        });
    }

    /// <summary>
    /// Get the conversation context as a formatted string to inject into sub-agent requests
    /// </summary>
    public async Task<string> GetConversationContextAsync(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            return string.Empty;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entries = await context
                .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== PREVIOUS CONVERSATION CONTEXT ===");
            sb.AppendLine("The following interactions have occurred in this conversation:");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                sb.AppendLine(
                    $"[{entry.AgentName}] User asked: {TruncateIfNeeded(entry.UserRequest, 200)}"
                );
                sb.AppendLine(
                    $"[{entry.AgentName}] Response summary: {TruncateIfNeeded(entry.AgentResponse, 500)}"
                );
                sb.AppendLine();
            }

            sb.AppendLine("=== END OF CONTEXT ===");
            sb.AppendLine();

            return sb.ToString();
        }
        catch (Exception ex)
        {
            // Handle case where table doesn't exist yet (migration not applied)
            logger.LogWarning(
                ex,
                "Failed to get conversation context for {ConversationId}. Table may not exist yet.",
                conversationId
            );
            return string.Empty;
        }
    }

    /// <summary>
    /// Get recent conversation context (last N entries)
    /// </summary>
    public async Task<string> GetRecentContextAsync(string conversationId, int maxEntries = 5)
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            return string.Empty;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entries = await context
                .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
                .OrderByDescending(e => e.SequenceNumber)
                .Take(maxEntries)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== RECENT CONVERSATION CONTEXT ===");

            foreach (var entry in entries)
            {
                sb.AppendLine(
                    $"[{entry.AgentName}] User: {TruncateIfNeeded(entry.UserRequest, 200)}"
                );
                sb.AppendLine(
                    $"[{entry.AgentName}] Response: {TruncateIfNeeded(entry.AgentResponse, 300)}"
                );
                sb.AppendLine();
            }

            sb.AppendLine("=== END OF CONTEXT ===");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to get recent context for {ConversationId}. Table may not exist yet.",
                conversationId
            );
            return string.Empty;
        }
    }

    /// <summary>
    /// Clear all data for a specific conversation
    /// </summary>
    public async Task ClearConversationAsync(string conversationId)
    {
        if (Guid.TryParse(conversationId, out var conversationGuid))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var entries = await context
                    .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
                    .ToListAsync();

                if (entries.Count > 0)
                {
                    context.ConversationMemoryEntries.RemoveRange(entries);
                    await context.SaveChangesAsync();

                    logger.LogInformation(
                        "Removed {Count} memory entries for conversation {ConversationId}",
                        entries.Count,
                        conversationId
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to clear conversation {ConversationId}. Table may not exist yet.",
                    conversationId
                );
            }
        }

        // Clear all agent threads for this conversation
        var keysToRemove = _agentThreads
            .Keys.Where(k => k.StartsWith($"{conversationId}:"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _agentThreads.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            logger.LogInformation(
                "Removed {Count} agent threads for conversation {ConversationId}",
                keysToRemove.Count,
                conversationId
            );
        }
    }

    /// <summary>
    /// Synchronous wrapper for ClearConversationAsync
    /// </summary>
    public void ClearConversation(string conversationId)
    {
        ClearConversationAsync(conversationId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get the count of active agent threads
    /// </summary>
    public int ActiveThreadCount => _agentThreads.Count;

    /// <summary>
    /// Get the count of memory entries for a conversation
    /// </summary>
    public async Task<int> GetMemoryEntryCountAsync(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            return 0;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.ConversationMemoryEntries.CountAsync(e =>
            e.ConversationId == conversationGuid
        );
    }

    private static string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }
}

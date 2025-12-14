using System.Text;
using AgentFrameworkQuickStart.Core.Domain.Memory;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Services.Memory;

/// <summary>
/// Implements the AIContextProvider pattern for long-term memory.
/// Injects relevant memories before agent execution (InvokingAsync)
/// and extracts/stores memories after execution (InvokedAsync).
/// </summary>
public class LongTermMemoryProvider(
    IUserMemoryRepository userMemoryRepository,
    IConversationSummaryRepository conversationSummaryRepository,
    IMemoryExtractionService memoryExtractionService,
    IServiceScopeFactory scopeFactory,
    ILogger<LongTermMemoryProvider> logger
) : ILongTermMemoryProvider
{
    /// <inheritdoc/>
    public async Task<LongTermMemoryContext> RetrieveContextAsync(
        string userId,
        string currentMessage,
        string conversationId,
        MemoryRetrievalSettings? settings = null,
        CancellationToken cancellationToken = default
    )
    {
        settings ??= new MemoryRetrievalSettings();
        var context = new LongTermMemoryContext();

        try
        {
            // 1. Get user memory (preferences, frequent entities, facts)
            context.UserMemory = await userMemoryRepository.GetByUserIdAsync(
                userId,
                cancellationToken
            );

            if (context.UserMemory != null)
            {
                // Get facts relevant to current message
                context.RelevantFacts = await userMemoryRepository.GetRelevantFactsAsync(
                    userId,
                    currentMessage,
                    settings.MaxFacts,
                    cancellationToken
                );

                // Add frequently used entities that might be relevant
                context.RelevantEntities = GetRelevantEntities(context.UserMemory, currentMessage);
            }

            // 2. Search for relevant past conversations
            var lookbackDate = DateTime.UtcNow.AddDays(-settings.LookbackDays);

            // Search by keywords in current message
            var searchResults = await conversationSummaryRepository.SearchAsync(
                userId,
                currentMessage,
                settings.MaxConversations,
                cancellationToken
            );

            context.RelevantConversations = searchResults
                .Where(s => s.StartedAt >= lookbackDate)
                .Where(s => s.ImportanceScore >= settings.MinImportanceScore)
                .ToList();

            // 3. Get pending follow-ups
            var withFollowUps = await conversationSummaryRepository.GetWithPendingFollowUpsAsync(
                userId,
                cancellationToken
            );

            context.PendingFollowUps = withFollowUps
                .SelectMany(s => s.PendingFollowUps)
                .Distinct()
                .Take(5)
                .ToList();

            // 4. Load recent conversation turns from current session
            context.RecentTurns = await LoadRecentTurnsAsync(
                conversationId,
                settings.MaxRecentTurns,
                cancellationToken
            );

            // 5. Format the context
            context.FormattedContext = FormatContextForInjection(context);
            context.EstimatedTokens = EstimateTokenCount(context.FormattedContext);

            logger.LogInformation(
                "Retrieved long-term memory for user {UserId}: {FactCount} facts, {ConversationCount} relevant conversations, {TurnCount} recent turns, {FollowUpCount} follow-ups",
                userId,
                context.RelevantFacts.Count,
                context.RelevantConversations.Count,
                context.RecentTurns.Count,
                context.PendingFollowUps.Count
            );

            return context;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve long-term memory for user {UserId}", userId);
            return context; // Return empty context on error
        }
    }

    /// <inheritdoc/>
    public async Task ProcessAndStoreMemoriesAsync(
        string userId,
        string conversationId,
        string userMessage,
        string agentResponse,
        List<string>? subAgentsUsed = null,
        List<string>? multiModalDescriptions = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Extract memories using LLM
            var extraction = await memoryExtractionService.ExtractMemoriesAsync(
                userMessage,
                agentResponse,
                subAgentsUsed ?? [],
                multiModalDescriptions ?? [],
                cancellationToken
            );

            // Store new facts
            if (extraction.NewFacts.Count > 0)
            {
                foreach (var fact in extraction.NewFacts)
                {
                    fact.SourceConversationId = conversationId;
                }
                await userMemoryRepository.AddFactsAsync(
                    userId,
                    extraction.NewFacts,
                    cancellationToken
                );
            }

            // Update preferences
            if (extraction.PreferencesDetected.Count > 0)
            {
                foreach (var pref in extraction.PreferencesDetected)
                {
                    pref.LearnedFrom = conversationId;
                }
                await userMemoryRepository.UpdatePreferencesAsync(
                    userId,
                    extraction.PreferencesDetected,
                    cancellationToken
                );
            }

            // Update frequent entities
            foreach (var entity in extraction.EntitiesMentioned)
            {
                await userMemoryRepository.UpdateFrequentEntityAsync(
                    userId,
                    entity.EntityType,
                    new FrequentEntity
                    {
                        EntityId = entity.Value,
                        UsageCount = entity.MentionCount,
                        LastUsed = DateTime.UtcNow,
                    },
                    cancellationToken
                );
            }

            // Update expertise level if suggested
            if (extraction.SuggestedExpertiseLevel.HasValue)
            {
                await userMemoryRepository.UpdateExpertiseLevelAsync(
                    userId,
                    extraction.SuggestedExpertiseLevel.Value,
                    cancellationToken
                );
            }

            // Update risk tolerance if suggested
            if (extraction.SuggestedRiskTolerance.HasValue)
            {
                await userMemoryRepository.UpdateRiskToleranceAsync(
                    userId,
                    extraction.SuggestedRiskTolerance.Value,
                    cancellationToken
                );
            }

            logger.LogDebug(
                "Processed memories for conversation {ConversationId}: {FactCount} facts, {PrefCount} preferences, {EntityCount} entities",
                conversationId,
                extraction.NewFacts.Count,
                extraction.PreferencesDetected.Count,
                extraction.EntitiesMentioned.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to process and store memories for conversation {ConversationId}",
                conversationId
            );
        }
    }

    /// <inheritdoc/>
    public async Task CreateConversationSummaryAsync(
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var summary = await memoryExtractionService.GenerateConversationSummaryAsync(
                conversationId,
                userId,
                cancellationToken
            );

            if (summary != null)
            {
                await conversationSummaryRepository.SaveAsync(summary, cancellationToken);
                await userMemoryRepository.IncrementConversationCountAsync(
                    userId,
                    summary.TurnCount,
                    cancellationToken
                );

                logger.LogInformation(
                    "Created conversation summary for {ConversationId}: '{Title}' (importance: {Importance:P0})",
                    conversationId,
                    summary.Title,
                    summary.ImportanceScore
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to create conversation summary for {ConversationId}",
                conversationId
            );
        }
    }

    /// <inheritdoc/>
    public string FormatContextForInjection(LongTermMemoryContext context)
    {
        if (!context.HasMemory)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("=== LONG-TERM MEMORY CONTEXT ===");
        sb.AppendLine();

        // User profile summary
        if (context.UserMemory != null)
        {
            sb.AppendLine("## User Profile");
            sb.AppendLine($"- Expertise Level: {context.UserMemory.ExpertiseLevel}");
            sb.AppendLine($"- Risk Tolerance: {context.UserMemory.RiskTolerance}");
            sb.AppendLine($"- Total Conversations: {context.UserMemory.TotalConversations}");
            sb.AppendLine();
        }

        // Relevant facts
        if (context.RelevantFacts.Count > 0)
        {
            sb.AppendLine("## Remembered Facts About This User");
            foreach (var fact in context.RelevantFacts)
            {
                sb.AppendLine($"- [{fact.Category}] {fact.Fact}");
            }
            sb.AppendLine();
        }

        // Frequently used entities
        if (context.RelevantEntities.Count > 0)
        {
            sb.AppendLine("## Frequently Used Entities");
            foreach (var entity in context.RelevantEntities)
            {
                var displayName = !string.IsNullOrEmpty(entity.DisplayName)
                    ? $" ({entity.DisplayName})"
                    : "";
                sb.AppendLine($"- {entity.EntityId}{displayName} (used {entity.UsageCount} times)");
            }
            sb.AppendLine();
        }

        // Relevant past conversations
        if (context.RelevantConversations.Count > 0)
        {
            sb.AppendLine("## Relevant Past Conversations");
            foreach (var conv in context.RelevantConversations)
            {
                sb.AppendLine($"### {conv.Title} ({conv.StartedAt:MMM dd, yyyy})");
                sb.AppendLine($"Summary: {conv.Summary}");
                if (conv.ActionsPerformed.Count > 0)
                {
                    sb.AppendLine(
                        $"Actions: {string.Join(", ", conv.ActionsPerformed.Select(a => a.ActionType))}"
                    );
                }
                sb.AppendLine();
            }
        }

        // Pending follow-ups
        if (context.PendingFollowUps.Count > 0)
        {
            sb.AppendLine("## Pending Follow-ups From Previous Conversations");
            foreach (var followUp in context.PendingFollowUps)
            {
                sb.AppendLine($"- {followUp}");
            }
            sb.AppendLine();
        }

        // Recent conversation turns (current session history)
        if (context.RecentTurns.Count > 0)
        {
            sb.AppendLine("## Recent Conversation History (Current Session)");
            sb.AppendLine(
                "Use this context to understand what was previously discussed in this conversation:"
            );
            sb.AppendLine();
            foreach (var turn in context.RecentTurns.OrderBy(t => t.SequenceNumber))
            {
                sb.AppendLine($"### Turn {turn.SequenceNumber}");
                sb.AppendLine($"**User:** {TruncateIfNeeded(turn.UserRequest, 500)}");
                sb.AppendLine($"**Agent:** {TruncateIfNeeded(turn.AgentResponse, 1000)}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("=== END LONG-TERM MEMORY ===");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Load recent conversation turns from the database for the current conversation
    /// </summary>
    private async Task<List<MemoryConversationTurn>> LoadRecentTurnsAsync(
        string conversationId,
        int maxTurns,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
            return [];

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entries = await dbContext
                .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
                .OrderByDescending(e => e.SequenceNumber)
                .Take(maxTurns)
                .ToListAsync(cancellationToken);

            return entries
                .Select(e => new MemoryConversationTurn
                {
                    SequenceNumber = e.SequenceNumber,
                    UserRequest = e.UserRequest,
                    AgentResponse = e.AgentResponse,
                    Timestamp = e.Timestamp,
                })
                .OrderBy(t => t.SequenceNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load recent turns for conversation {ConversationId}",
                conversationId
            );
            return [];
        }
    }

    private static string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetPendingFollowUpsAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var withFollowUps = await conversationSummaryRepository.GetWithPendingFollowUpsAsync(
            userId,
            cancellationToken
        );

        return withFollowUps.SelectMany(s => s.PendingFollowUps).Distinct().ToList();
    }

    /// <inheritdoc/>
    public async Task CompleteFollowUpAsync(
        string conversationId,
        string followUp,
        CancellationToken cancellationToken = default
    )
    {
        var summary = await conversationSummaryRepository.GetByConversationIdAsync(
            conversationId,
            cancellationToken
        );

        if (summary != null && summary.PendingFollowUps.Contains(followUp))
        {
            summary.PendingFollowUps.Remove(followUp);
            await conversationSummaryRepository.SaveAsync(summary, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ClearUserMemoryAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        await userMemoryRepository.DeleteAsync(userId, cancellationToken);

        // Get all user's conversations and delete summaries
        var summaries = await conversationSummaryRepository.GetRecentByUserAsync(
            userId,
            int.MaxValue,
            cancellationToken
        );

        foreach (var summary in summaries)
        {
            await conversationSummaryRepository.DeleteAsync(
                summary.ConversationId,
                cancellationToken
            );
        }

        logger.LogInformation("Cleared all long-term memory for user {UserId}", userId);
    }

    private static List<FrequentEntity> GetRelevantEntities(UserMemory memory, string message)
    {
        var messageLower = message.ToLowerInvariant();
        var relevant = new List<FrequentEntity>();

        // Check if message mentions any frequently used entities
        foreach (var account in memory.FrequentAccounts)
        {
            if (
                messageLower.Contains(account.EntityId.ToLowerInvariant())
                || (
                    account.DisplayName != null
                    && messageLower.Contains(account.DisplayName.ToLowerInvariant())
                )
            )
            {
                relevant.Add(account);
            }
        }

        foreach (var portfolio in memory.FrequentPortfolios)
        {
            if (
                messageLower.Contains(portfolio.EntityId.ToLowerInvariant())
                || (
                    portfolio.DisplayName != null
                    && messageLower.Contains(portfolio.DisplayName.ToLowerInvariant())
                )
            )
            {
                relevant.Add(portfolio);
            }
        }

        foreach (var fund in memory.FrequentFunds)
        {
            if (
                messageLower.Contains(fund.EntityId.ToLowerInvariant())
                || (
                    fund.DisplayName != null
                    && messageLower.Contains(fund.DisplayName.ToLowerInvariant())
                )
            )
            {
                relevant.Add(fund);
            }
        }

        // If no specific matches, include top 3 most used
        if (relevant.Count == 0)
        {
            relevant.AddRange(memory.FrequentAccounts.Take(1));
            relevant.AddRange(memory.FrequentPortfolios.Take(1));
            relevant.AddRange(memory.FrequentFunds.Take(1));
        }

        return relevant.Take(5).ToList();
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimate: 4 characters per token
        return text.Length / 4;
    }
}

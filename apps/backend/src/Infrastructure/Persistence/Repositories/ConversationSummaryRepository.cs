using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Memory;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of conversation summary repository.
/// Provides persistence for AI-generated conversation summaries.
/// </summary>
public class ConversationSummaryRepository(
    AppDbContext dbContext,
    ILogger<ConversationSummaryRepository> logger
) : IConversationSummaryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ConversationSummary?> GetByConversationIdAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.ConversationSummaries.FirstOrDefaultAsync(
            s => s.ConversationId == conversationId,
            cancellationToken
        );

        return entity == null ? null : MapToSummary(entity);
    }

    public async Task SaveAsync(
        ConversationSummary summary,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.ConversationSummaries.FirstOrDefaultAsync(
            s => s.ConversationId == summary.ConversationId,
            cancellationToken
        );

        if (entity == null)
        {
            entity = new ConversationSummaryEntity
            {
                ConversationId = summary.ConversationId,
                Title = summary.Title,
                Summary = summary.Summary,
            };
            dbContext.ConversationSummaries.Add(entity);
        }

        MapToEntity(summary, entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug(
            "Saved conversation summary for {ConversationId}: {Title}",
            summary.ConversationId,
            summary.Title
        );
    }

    public async Task<List<ConversationSummary>> GetRecentByUserAsync(
        string userId,
        int count = 10,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await dbContext
            .ConversationSummaries.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.EndedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummary).ToList();
    }

    public async Task<List<ConversationSummary>> SearchAsync(
        string userId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    )
    {
        var queryLower = query.ToLowerInvariant();

        // Simple keyword search - could be enhanced with full-text search or embeddings
        var entities = await dbContext
            .ConversationSummaries.Where(s => s.UserId == userId)
            .Where(s =>
                s.Title.ToLower().Contains(queryLower)
                || s.Summary.ToLower().Contains(queryLower)
                || s.KeywordsJson.ToLower().Contains(queryLower)
                || s.TopicsJson.ToLower().Contains(queryLower)
            )
            .OrderByDescending(s => s.ImportanceScore)
            .ThenByDescending(s => s.EndedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummary).ToList();
    }

    public async Task<List<ConversationSummary>> GetWithPendingFollowUpsAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await dbContext
            .ConversationSummaries.Where(s => s.UserId == userId && s.PendingFollowUpsJson != "[]")
            .OrderByDescending(s => s.EndedAt)
            .ToListAsync(cancellationToken);

        // Filter to only those with actual follow-ups
        return entities.Select(MapToSummary).Where(s => s.PendingFollowUps.Count > 0).ToList();
    }

    public async Task<List<ConversationSummary>> GetByEntityAsync(
        string userId,
        string entityType,
        string entityValue,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    )
    {
        var entityValueLower = entityValue.ToLowerInvariant();

        var entities = await dbContext
            .ConversationSummaries.Where(s =>
                s.UserId == userId && s.EntitiesJson.ToLower().Contains(entityValueLower)
            )
            .OrderByDescending(s => s.EndedAt)
            .Take(maxResults * 2) // Get more to filter
            .ToListAsync(cancellationToken);

        // Post-filter for exact entity match
        return entities
            .Select(MapToSummary)
            .Where(s =>
                s.Entities.Any(e =>
                    e.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase)
                    && e.Value.Equals(entityValue, StringComparison.OrdinalIgnoreCase)
                )
            )
            .Take(maxResults)
            .ToList();
    }

    public async Task<List<ConversationSummary>> GetByTopicAsync(
        string userId,
        string topic,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    )
    {
        var topicLower = topic.ToLowerInvariant();

        var entities = await dbContext
            .ConversationSummaries.Where(s =>
                s.UserId == userId && s.TopicsJson.ToLower().Contains(topicLower)
            )
            .OrderByDescending(s => s.ImportanceScore)
            .ThenByDescending(s => s.EndedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummary).ToList();
    }

    public async Task<List<ConversationSummary>> GetHighImportanceAsync(
        string userId,
        double minScore = 0.7,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await dbContext
            .ConversationSummaries.Where(s => s.UserId == userId && s.ImportanceScore >= minScore)
            .OrderByDescending(s => s.ImportanceScore)
            .ThenByDescending(s => s.EndedAt)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummary).ToList();
    }

    public async Task<List<ConversationSummary>> GetByTimeRangeAsync(
        string userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await dbContext
            .ConversationSummaries.Where(s =>
                s.UserId == userId && s.StartedAt >= from && s.EndedAt <= to
            )
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSummary).ToList();
    }

    public async Task DeleteAsync(
        string conversationId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.ConversationSummaries.FirstOrDefaultAsync(
            s => s.ConversationId == conversationId,
            cancellationToken
        );

        if (entity != null)
        {
            dbContext.ConversationSummaries.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Deleted conversation summary for {ConversationId}",
                conversationId
            );
        }
    }

    public async Task DeleteOldSummariesAsync(
        DateTime olderThan,
        double maxImportanceToDelete = 0.3,
        CancellationToken cancellationToken = default
    )
    {
        var entitiesToDelete = await dbContext
            .ConversationSummaries.Where(s =>
                s.EndedAt < olderThan && s.ImportanceScore <= maxImportanceToDelete
            )
            .ToListAsync(cancellationToken);

        if (entitiesToDelete.Count > 0)
        {
            dbContext.ConversationSummaries.RemoveRange(entitiesToDelete);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Deleted {Count} old conversation summaries older than {Date}",
                entitiesToDelete.Count,
                olderThan
            );
        }
    }

    public async Task<List<MultiModalReference>> GetMultiModalReferencesAsync(
        string userId,
        string contentType,
        int maxResults = 5,
        CancellationToken cancellationToken = default
    )
    {
        var contentTypeLower = contentType.ToLowerInvariant();

        var entities = await dbContext
            .ConversationSummaries.Where(s =>
                s.UserId == userId && s.MultiModalContentJson.ToLower().Contains(contentTypeLower)
            )
            .OrderByDescending(s => s.EndedAt)
            .Take(maxResults * 2)
            .ToListAsync(cancellationToken);

        return entities
            .SelectMany(e => MapToSummary(e).MultiModalContent)
            .Where(m => m.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
    }

    private static ConversationSummary MapToSummary(ConversationSummaryEntity entity)
    {
        return new ConversationSummary
        {
            ConversationId = entity.ConversationId,
            UserId = entity.UserId,
            Title = entity.Title,
            Summary = entity.Summary,
            Topics = DeserializeOrDefault<List<string>>(entity.TopicsJson, []),
            Entities = DeserializeOrDefault<List<ExtractedMemoryEntity>>(entity.EntitiesJson, []),
            ActionsPerformed = DeserializeOrDefault<List<PerformedAction>>(entity.ActionsJson, []),
            Decisions = DeserializeOrDefault<List<string>>(entity.DecisionsJson, []),
            PendingFollowUps = DeserializeOrDefault<List<string>>(entity.PendingFollowUpsJson, []),
            SubAgentsUsed = DeserializeOrDefault<List<string>>(entity.SubAgentsUsedJson, []),
            SatisfactionIndicator = entity.SatisfactionIndicator,
            MultiModalContent = DeserializeOrDefault<List<MultiModalReference>>(
                entity.MultiModalContentJson,
                []
            ),
            Keywords = DeserializeOrDefault<List<string>>(entity.KeywordsJson, []),
            ImportanceScore = entity.ImportanceScore,
            StartedAt = entity.StartedAt,
            EndedAt = entity.EndedAt,
            TurnCount = entity.TurnCount,
            CreatedAt = entity.CreatedAt,
        };
    }

    private static void MapToEntity(ConversationSummary summary, ConversationSummaryEntity entity)
    {
        entity.UserId = summary.UserId;
        entity.Title = summary.Title;
        entity.Summary = summary.Summary;
        entity.TopicsJson = JsonSerializer.Serialize(summary.Topics, JsonOptions);
        entity.EntitiesJson = JsonSerializer.Serialize(summary.Entities, JsonOptions);
        entity.ActionsJson = JsonSerializer.Serialize(summary.ActionsPerformed, JsonOptions);
        entity.DecisionsJson = JsonSerializer.Serialize(summary.Decisions, JsonOptions);
        entity.PendingFollowUpsJson = JsonSerializer.Serialize(
            summary.PendingFollowUps,
            JsonOptions
        );
        entity.SubAgentsUsedJson = JsonSerializer.Serialize(summary.SubAgentsUsed, JsonOptions);
        entity.SatisfactionIndicator = summary.SatisfactionIndicator;
        entity.MultiModalContentJson = JsonSerializer.Serialize(
            summary.MultiModalContent,
            JsonOptions
        );
        entity.KeywordsJson = JsonSerializer.Serialize(summary.Keywords, JsonOptions);
        entity.ImportanceScore = summary.ImportanceScore;
        entity.StartedAt = summary.StartedAt;
        entity.EndedAt = summary.EndedAt;
        entity.TurnCount = summary.TurnCount;

        if (Guid.TryParse(summary.ConversationId, out var threadId))
        {
            entity.ThreadId = threadId;
        }
    }

    private static T DeserializeOrDefault<T>(string json, T defaultValue)
    {
        if (string.IsNullOrWhiteSpace(json))
            return defaultValue;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}

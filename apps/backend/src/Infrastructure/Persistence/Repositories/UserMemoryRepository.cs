using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Memory;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the user memory repository.
/// Provides persistence for long-term user preferences and facts.
/// </summary>
public class UserMemoryRepository(AppDbContext dbContext, ILogger<UserMemoryRepository> logger)
    : IUserMemoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<UserMemory?> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        return entity == null ? null : MapToMemory(entity);
    }

    public async Task<UserMemory> GetOrCreateAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        if (entity == null)
        {
            logger.LogInformation("Creating new user memory for {UserId}", userId);
            entity = new UserMemoryEntity { UserId = userId };
            dbContext.UserMemories.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapToMemory(entity);
    }

    public async Task SaveAsync(UserMemory memory, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == memory.UserId,
            cancellationToken
        );

        if (entity == null)
        {
            entity = new UserMemoryEntity { UserId = memory.UserId };
            dbContext.UserMemories.Add(entity);
        }

        MapToEntity(memory, entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug(
            "Saved user memory for {UserId}: {FactCount} facts, {PreferenceCount} preferences",
            memory.UserId,
            memory.Facts.Count,
            memory.Preferences.Count
        );
    }

    public async Task AddFactsAsync(
        string userId,
        IEnumerable<MemorizedFact> facts,
        CancellationToken cancellationToken = default
    )
    {
        var memory = await GetOrCreateAsync(userId, cancellationToken);

        foreach (var fact in facts)
        {
            // Check for duplicate or update existing
            var existing = memory.Facts.FirstOrDefault(f =>
                f.Category == fact.Category
                && f.Fact.Equals(fact.Fact, StringComparison.OrdinalIgnoreCase)
            );

            if (existing != null)
            {
                // Update confidence if higher
                if (fact.Confidence > existing.Confidence)
                {
                    existing.Confidence = fact.Confidence;
                    existing.LearnedAt = DateTime.UtcNow;
                }
            }
            else
            {
                memory.Facts.Add(fact);
            }
        }

        // Keep only top N facts per category
        memory.Facts = memory
            .Facts.GroupBy(f => f.Category)
            .SelectMany(g =>
                g.OrderByDescending(f => f.Importance).ThenByDescending(f => f.Confidence).Take(20)
            )
            .ToList();

        await SaveAsync(memory, cancellationToken);
    }

    public async Task UpdateFrequentEntityAsync(
        string userId,
        string entityType,
        FrequentEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        var memory = await GetOrCreateAsync(userId, cancellationToken);

        var list = entityType.ToLowerInvariant() switch
        {
            "account" or "account_id" => memory.FrequentAccounts,
            "portfolio" or "portfolio_id" => memory.FrequentPortfolios,
            "fund" or "fund_name" => memory.FrequentFunds,
            _ => throw new ArgumentException($"Unknown entity type: {entityType}"),
        };

        var existing = list.FirstOrDefault(e => e.EntityId == entity.EntityId);
        if (existing != null)
        {
            existing.UsageCount++;
            existing.LastUsed = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(entity.DisplayName))
                existing.DisplayName = entity.DisplayName;
        }
        else
        {
            list.Add(entity);
        }

        // Keep only top 10 most used
        var ordered = list.OrderByDescending(e => e.UsageCount).Take(10).ToList();
        list.Clear();
        list.AddRange(ordered);

        await SaveAsync(memory, cancellationToken);
    }

    public async Task UpdatePreferencesAsync(
        string userId,
        IEnumerable<UserPreference> preferences,
        CancellationToken cancellationToken = default
    )
    {
        var memory = await GetOrCreateAsync(userId, cancellationToken);

        foreach (var pref in preferences)
        {
            memory.Preferences[pref.PreferenceKey] = pref;
        }

        await SaveAsync(memory, cancellationToken);
    }

    public async Task IncrementConversationCountAsync(
        string userId,
        int turnCount,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        if (entity != null)
        {
            entity.TotalConversations++;
            entity.TotalTurns += turnCount;
            entity.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateExpertiseLevelAsync(
        string userId,
        ExpertiseLevel level,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        if (entity != null)
        {
            entity.ExpertiseLevel = level.ToString();
            entity.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateRiskToleranceAsync(
        string userId,
        RiskTolerance tolerance,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        if (entity != null)
        {
            entity.RiskTolerance = tolerance.ToString();
            entity.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.UserMemories.FirstOrDefaultAsync(
            m => m.UserId == userId,
            cancellationToken
        );

        if (entity != null)
        {
            dbContext.UserMemories.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Deleted user memory for {UserId}", userId);
        }
    }

    public async Task<List<MemorizedFact>> GetRelevantFactsAsync(
        string userId,
        string query,
        int maxFacts = 10,
        CancellationToken cancellationToken = default
    )
    {
        var memory = await GetByUserIdAsync(userId, cancellationToken);
        if (memory == null)
            return [];

        var queryLower = query.ToLowerInvariant();
        var queryWords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Simple keyword matching - could be enhanced with embeddings
        var relevantFacts = memory
            .Facts.Where(f => !f.ExpiresAt.HasValue || f.ExpiresAt > DateTime.UtcNow)
            .Select(f => new { Fact = f, Score = CalculateRelevanceScore(f, queryWords) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Fact.Importance)
            .Take(maxFacts)
            .Select(x => x.Fact)
            .ToList();

        return relevantFacts;
    }

    private static double CalculateRelevanceScore(MemorizedFact fact, string[] queryWords)
    {
        var factLower = fact.Fact.ToLowerInvariant();
        var categoryLower = fact.Category.ToLowerInvariant();

        double score = 0;
        foreach (var word in queryWords)
        {
            if (factLower.Contains(word))
                score += 1;
            if (categoryLower.Contains(word))
                score += 0.5;
        }

        return score * fact.Importance;
    }

    private static UserMemory MapToMemory(UserMemoryEntity entity)
    {
        return new UserMemory
        {
            UserId = entity.UserId,
            PreferredLanguage = entity.PreferredLanguage,
            ExpertiseLevel = Enum.TryParse<ExpertiseLevel>(entity.ExpertiseLevel, out var el)
                ? el
                : ExpertiseLevel.Intermediate,
            RiskTolerance = Enum.TryParse<RiskTolerance>(entity.RiskTolerance, out var rt)
                ? rt
                : RiskTolerance.Moderate,
            FrequentAccounts = DeserializeOrDefault<List<FrequentEntity>>(
                entity.FrequentAccountsJson,
                []
            ),
            FrequentPortfolios = DeserializeOrDefault<List<FrequentEntity>>(
                entity.FrequentPortfoliosJson,
                []
            ),
            FrequentFunds = DeserializeOrDefault<List<FrequentEntity>>(
                entity.FrequentFundsJson,
                []
            ),
            Preferences = DeserializeOrDefault<Dictionary<string, UserPreference>>(
                entity.PreferencesJson,
                new()
            ),
            Facts = DeserializeOrDefault<List<MemorizedFact>>(entity.FactsJson, []),
            Interests = DeserializeOrDefault<List<InvestmentInterest>>(entity.InterestsJson, []),
            TopicFrequency = DeserializeOrDefault<Dictionary<string, int>>(
                entity.TopicFrequencyJson,
                new()
            ),
            CommunicationStyle = DeserializeOrDefault<CommunicationPreferences>(
                entity.CommunicationStyleJson,
                new()
            ),
            TotalConversations = entity.TotalConversations,
            TotalTurns = entity.TotalTurns,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    private static void MapToEntity(UserMemory memory, UserMemoryEntity entity)
    {
        entity.PreferredLanguage = memory.PreferredLanguage;
        entity.ExpertiseLevel = memory.ExpertiseLevel.ToString();
        entity.RiskTolerance = memory.RiskTolerance.ToString();
        entity.FrequentAccountsJson = JsonSerializer.Serialize(
            memory.FrequentAccounts,
            JsonOptions
        );
        entity.FrequentPortfoliosJson = JsonSerializer.Serialize(
            memory.FrequentPortfolios,
            JsonOptions
        );
        entity.FrequentFundsJson = JsonSerializer.Serialize(memory.FrequentFunds, JsonOptions);
        entity.PreferencesJson = JsonSerializer.Serialize(memory.Preferences, JsonOptions);
        entity.FactsJson = JsonSerializer.Serialize(memory.Facts, JsonOptions);
        entity.InterestsJson = JsonSerializer.Serialize(memory.Interests, JsonOptions);
        entity.TopicFrequencyJson = JsonSerializer.Serialize(memory.TopicFrequency, JsonOptions);
        entity.CommunicationStyleJson = JsonSerializer.Serialize(
            memory.CommunicationStyle,
            JsonOptions
        );
        entity.TotalConversations = memory.TotalConversations;
        entity.TotalTurns = memory.TotalTurns;
        entity.UpdatedAt = DateTime.UtcNow;
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

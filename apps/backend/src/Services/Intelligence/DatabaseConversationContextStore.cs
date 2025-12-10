using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Services.Intelligence;

/// <summary>
/// /// Database-backed implementation of conversation context store
/// Provides persistence, horizontal scaling support, and conversation history
/// </summary>
public class DatabaseConversationContextStore(
    AppDbContext dbContext,
    ILogger<DatabaseConversationContextStore> logger
) : IConversationContextStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<ConversationContext> GetOrCreateContextAsync(string conversationId)
    {
        var entity = await dbContext.ConversationContexts.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId
        );

        if (entity == null)
        {
            logger.LogInformation(
                "Creating new conversation context for {ConversationId}",
                conversationId
            );

            entity = new ConversationContextEntity
            {
                ConversationId = conversationId,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
            };

            dbContext.ConversationContexts.Add(entity);
            await dbContext.SaveChangesAsync();
        }

        return MapToContext(entity);
    }

    public async Task<ConversationContext?> GetContextAsync(string conversationId)
    {
        var entity = await dbContext.ConversationContexts.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId
        );

        return entity == null ? null : MapToContext(entity);
    }

    public async Task SaveContextAsync(ConversationContext context)
    {
        var entity = await dbContext.ConversationContexts.FirstOrDefaultAsync(c =>
            c.ConversationId == context.ConversationId
        );

        if (entity == null)
        {
            entity = new ConversationContextEntity { ConversationId = context.ConversationId };
            dbContext.ConversationContexts.Add(entity);
        }

        // Update entity from context
        entity.UserId = context.UserId;
        entity.EntitiesJson = JsonSerializer.Serialize(context.Entities, JsonOptions);
        entity.GoalsJson = JsonSerializer.Serialize(context.Goals, JsonOptions);
        entity.DecisionsJson = JsonSerializer.Serialize(context.Decisions, JsonOptions);
        entity.SubAgentFindingsJson = JsonSerializer.Serialize(
            context.SubAgentFindings,
            JsonOptions
        );
        entity.PendingActionsJson = JsonSerializer.Serialize(context.PendingActions, JsonOptions);
        entity.ActiveConstraintsJson = JsonSerializer.Serialize(
            context.ActiveConstraints,
            JsonOptions
        );
        entity.UserExpertiseLevel = context.UserExpertiseLevel;
        entity.PreferredLanguage = context.PreferredLanguage;
        entity.InferredRiskTolerance = context.InferredRiskTolerance;
        entity.TurnCount = context.TurnCount;
        entity.StartedAt = context.StartedAt;
        entity.LastActivityAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        logger.LogDebug(
            "Saved context for {ConversationId}: {EntityCount} entities, {GoalCount} goals",
            context.ConversationId,
            context.Entities.Count,
            context.Goals.Count
        );
    }

    public async Task AddEntityAsync(string conversationId, ExtractedEntity entity)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.SetEntity(entity);
        await SaveContextAsync(context);

        logger.LogDebug(
            "Added entity {EntityType}={Value} to conversation {ConversationId}",
            entity.EntityType,
            entity.RawValue,
            conversationId
        );
    }

    public async Task AddEntitiesAsync(string conversationId, IEnumerable<ExtractedEntity> entities)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        foreach (var entity in entities)
        {
            context.SetEntity(entity);
        }
        await SaveContextAsync(context);

        logger.LogDebug(
            "Added {Count} entities to conversation {ConversationId}",
            entities.Count(),
            conversationId
        );
    }

    public async Task AddGoalAsync(string conversationId, UserGoal goal)
    {
        var context = await GetOrCreateContextAsync(conversationId);

        if (goal.IsPrimary)
        {
            foreach (var existingGoal in context.Goals.Where(g => g.IsPrimary))
            {
                existingGoal.IsPrimary = false;
            }
        }

        context.Goals.Add(goal);
        await SaveContextAsync(context);

        logger.LogInformation(
            "Added goal '{Goal}' to conversation {ConversationId}, isPrimary: {IsPrimary}",
            goal.Description,
            conversationId,
            goal.IsPrimary
        );
    }

    public async Task AddDecisionAsync(string conversationId, DecisionRecord decision)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.Decisions.Add(decision);
        await SaveContextAsync(context);

        logger.LogInformation(
            "Recorded decision '{Decision}' by {Agent} in conversation {ConversationId}",
            decision.Summary,
            decision.MadeBy,
            conversationId
        );
    }

    public async Task AddSubAgentFindingAsync(string conversationId, SubAgentFinding finding)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.SubAgentFindings[finding.SubAgentName] = finding;
        await SaveContextAsync(context);

        logger.LogDebug(
            "Added finding from {SubAgent} to conversation {ConversationId}",
            finding.SubAgentName,
            conversationId
        );
    }

    public async Task AddPendingActionAsync(string conversationId, PendingAction action)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.PendingActions.Add(action);
        await SaveContextAsync(context);

        logger.LogInformation(
            "Added pending action '{Action}' for {Agent} in conversation {ConversationId}",
            action.Description,
            action.RequiredAgent,
            conversationId
        );
    }

    public async Task CompletePendingActionAsync(string conversationId, string actionId)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        var action = context.PendingActions.FirstOrDefault(a => a.ActionId == actionId);

        if (action != null)
        {
            context.PendingActions.Remove(action);
            await SaveContextAsync(context);

            logger.LogInformation(
                "Completed pending action '{Action}' in conversation {ConversationId}",
                action.Description,
                conversationId
            );
        }
    }

    public async Task<string> GenerateContextSummaryAsync(
        string conversationId,
        int maxTokens = 500
    )
    {
        var context = await GetContextAsync(conversationId);
        return context?.GenerateSummary() ?? string.Empty;
    }

    public async Task<SubAgentBriefing> GenerateSubAgentBriefingAsync(
        string conversationId,
        string subAgentName,
        UserIntent currentIntent
    )
    {
        var context = await GetOrCreateContextAsync(conversationId);

        var briefing = new SubAgentBriefing
        {
            UserGoal = context.GetPrimaryGoal()?.Description,
            UserExpertise = context.UserExpertiseLevel,
            Language = context.PreferredLanguage,
            Constraints = context.ActiveConstraints.ToList(),
        };

        foreach (var finding in context.SubAgentFindings.Values.OrderBy(f => f.FoundAt))
        {
            briefing.PreviousAgentActions.Add($"{finding.SubAgentName}: {finding.Summary}");
        }

        var relevantEntityTypes = GetRelevantEntityTypesForAgent(subAgentName);
        foreach (var entityType in relevantEntityTypes)
        {
            if (context.Entities.TryGetValue(entityType, out var entity))
            {
                briefing.RelevantEntities[entityType] = entity.NormalizedValue ?? entity.RawValue;
            }
        }

        foreach (var entity in currentIntent.Entities)
        {
            briefing.RelevantEntities[entity.EntityType] =
                entity.NormalizedValue ?? entity.RawValue;
        }

        var relevantDecisions = context
            .Decisions.Where(d => IsDecisionRelevantForAgent(d, subAgentName))
            .OrderByDescending(d => d.MadeAt)
            .Take(3);

        foreach (var decision in relevantDecisions)
        {
            briefing.RelevantDecisions.Add(decision.Summary);
        }

        briefing.ExpectedFocus = currentIntent.IntentSummary;

        logger.LogDebug(
            "Generated briefing for {SubAgent} in conversation {ConversationId}",
            subAgentName,
            conversationId
        );

        return briefing;
    }

    public async Task ClearContextAsync(string conversationId)
    {
        var entity = await dbContext.ConversationContexts.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId
        );

        if (entity != null)
        {
            dbContext.ConversationContexts.Remove(entity);
            await dbContext.SaveChangesAsync();
        }

        logger.LogInformation("Cleared context for conversation {ConversationId}", conversationId);
    }

    public async Task IncrementTurnAsync(string conversationId)
    {
        var entity = await dbContext.ConversationContexts.FirstOrDefaultAsync(c =>
            c.ConversationId == conversationId
        );

        if (entity != null)
        {
            entity.TurnCount++;
            entity.LastActivityAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }

    private ConversationContext MapToContext(ConversationContextEntity entity)
    {
        return new ConversationContext
        {
            ConversationId = entity.ConversationId,
            UserId = entity.UserId,
            Entities = DeserializeOrDefault<Dictionary<string, ExtractedEntity>>(
                entity.EntitiesJson,
                new()
            ),
            Goals = DeserializeOrDefault<List<UserGoal>>(entity.GoalsJson, []),
            Decisions = DeserializeOrDefault<List<DecisionRecord>>(entity.DecisionsJson, []),
            SubAgentFindings = DeserializeOrDefault<Dictionary<string, SubAgentFinding>>(
                entity.SubAgentFindingsJson,
                new()
            ),
            PendingActions = DeserializeOrDefault<List<PendingAction>>(
                entity.PendingActionsJson,
                []
            ),
            ActiveConstraints = DeserializeOrDefault<List<string>>(
                entity.ActiveConstraintsJson,
                []
            ),
            UserExpertiseLevel = entity.UserExpertiseLevel,
            PreferredLanguage = entity.PreferredLanguage,
            InferredRiskTolerance = entity.InferredRiskTolerance,
            TurnCount = entity.TurnCount,
            StartedAt = entity.StartedAt,
            LastActivityAt = entity.LastActivityAt,
        };
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

    private static List<string> GetRelevantEntityTypesForAgent(string subAgentName)
    {
        return subAgentName switch
        {
            "PortfolioManager" =>
            [
                EntityTypes.PortfolioId,
                EntityTypes.AccountId,
                EntityTypes.Amount,
                EntityTypes.Strategy,
                EntityTypes.PortfolioName,
            ],
            "InvestmentAdvisor" =>
            [
                EntityTypes.Amount,
                EntityTypes.RiskLevel,
                EntityTypes.TimeHorizon,
                EntityTypes.ShariahCompliant,
                EntityTypes.FundName,
            ],
            "AccountServices" => [EntityTypes.AccountId, EntityTypes.Amount, EntityTypes.Currency],
            "ComplianceOfficer" =>
            [
                EntityTypes.AccountId,
                EntityTypes.PortfolioId,
                EntityTypes.Amount,
                EntityTypes.RiskLevel,
            ],
            "ProfitProjection" =>
            [
                EntityTypes.Amount,
                EntityTypes.TimeHorizon,
                EntityTypes.RiskLevel,
                EntityTypes.Currency,
                EntityTypes.ShariahCompliant,
            ],
            "ExternalApiServices" =>
            [
                EntityTypes.CustomerId,
                EntityTypes.AccountId,
                EntityTypes.PortfolioId,
                EntityTypes.Amount,
                EntityTypes.FundId,
                EntityTypes.Currency,
            ],
            _ => [EntityTypes.Amount, EntityTypes.AccountId, EntityTypes.PortfolioId],
        };
    }

    private static bool IsDecisionRelevantForAgent(DecisionRecord decision, string subAgentName)
    {
        if (decision.MadeBy == subAgentName)
            return true;

        if (decision.MadeBy == "ComplianceOfficer")
            return true;

        if (
            decision.MadeBy == "AccountServices"
            && (subAgentName == "PortfolioManager" || subAgentName == "ExternalApiServices")
        )
            return true;

        return false;
    }
}

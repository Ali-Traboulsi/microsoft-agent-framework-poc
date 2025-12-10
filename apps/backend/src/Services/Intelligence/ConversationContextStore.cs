using System.Collections.Concurrent;
using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Interfaces;

namespace AgentFrameworkQuickStart.Services.Intelligence;

/// <summary>
/// In-memory implementation of conversation context store
/// Fast but non-persistent - suitable for development/demo or single-instance deployments
/// </summary>
public class InMemoryConversationContextStore(ILogger<InMemoryConversationContextStore> logger)
    : IConversationContextStore
{
    private readonly ConcurrentDictionary<string, ConversationContext> _contexts = new();

    public Task<ConversationContext> GetOrCreateContextAsync(string conversationId)
    {
        var context = _contexts.GetOrAdd(
            conversationId,
            id =>
            {
                logger.LogInformation("Creating new conversation context for {ConversationId}", id);
                return new ConversationContext
                {
                    ConversationId = id,
                    StartedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                };
            }
        );

        return Task.FromResult(context);
    }

    public Task<ConversationContext?> GetContextAsync(string conversationId)
    {
        _contexts.TryGetValue(conversationId, out var context);
        return Task.FromResult(context);
    }

    public Task SaveContextAsync(ConversationContext context)
    {
        context.LastActivityAt = DateTime.UtcNow;
        _contexts[context.ConversationId] = context;

        logger.LogDebug(
            "Saved context for {ConversationId}: {EntityCount} entities, {GoalCount} goals, {DecisionCount} decisions",
            context.ConversationId,
            context.Entities.Count,
            context.Goals.Count,
            context.Decisions.Count
        );

        return Task.CompletedTask;
    }

    public async Task AddEntityAsync(string conversationId, ExtractedEntity entity)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.SetEntity(entity);

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

        logger.LogDebug(
            "Added {Count} entities to conversation {ConversationId}",
            entities.Count(),
            conversationId
        );
    }

    public async Task AddGoalAsync(string conversationId, UserGoal goal)
    {
        var context = await GetOrCreateContextAsync(conversationId);

        // If this is marked as primary, demote any existing primary goal
        if (goal.IsPrimary)
        {
            foreach (var existingGoal in context.Goals.Where(g => g.IsPrimary))
            {
                existingGoal.IsPrimary = false;
            }
        }

        context.Goals.Add(goal);
        context.LastActivityAt = DateTime.UtcNow;

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
        context.LastActivityAt = DateTime.UtcNow;

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
        context.LastActivityAt = DateTime.UtcNow;

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
        context.LastActivityAt = DateTime.UtcNow;

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
            context.LastActivityAt = DateTime.UtcNow;

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
        if (context == null)
            return string.Empty;

        return context.GenerateSummary();
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
            // In-memory store doesn't have conversation history - use DatabaseConversationContextStore for full functionality
            ConversationHistory = [],
        };

        // Add previous agent actions
        foreach (var finding in context.SubAgentFindings.Values.OrderBy(f => f.FoundAt))
        {
            briefing.PreviousAgentActions.Add($"{finding.SubAgentName}: {finding.Summary}");
        }

        // Add relevant entities based on sub-agent type
        var relevantEntityTypes = GetRelevantEntityTypesForAgent(subAgentName);
        foreach (var entityType in relevantEntityTypes)
        {
            if (context.Entities.TryGetValue(entityType, out var entity))
            {
                briefing.RelevantEntities[entityType] = entity.NormalizedValue ?? entity.RawValue;
            }
        }

        // Also add entities from the current intent
        foreach (var entity in currentIntent.Entities)
        {
            briefing.RelevantEntities[entity.EntityType] =
                entity.NormalizedValue ?? entity.RawValue;
        }

        // Add relevant decisions
        var relevantDecisions = context
            .Decisions.Where(d => IsDecisionRelevantForAgent(d, subAgentName))
            .OrderByDescending(d => d.MadeAt)
            .Take(3);

        foreach (var decision in relevantDecisions)
        {
            briefing.RelevantDecisions.Add(decision.Summary);
        }

        // Set expected focus based on intent
        briefing.ExpectedFocus = currentIntent.IntentSummary;

        logger.LogDebug(
            "Generated briefing for {SubAgent} in conversation {ConversationId}: {EntityCount} entities, {ActionCount} previous actions",
            subAgentName,
            conversationId,
            briefing.RelevantEntities.Count,
            briefing.PreviousAgentActions.Count
        );

        return briefing;
    }

    public Task ClearContextAsync(string conversationId)
    {
        _contexts.TryRemove(conversationId, out _);
        logger.LogInformation("Cleared context for conversation {ConversationId}", conversationId);
        return Task.CompletedTask;
    }

    public async Task IncrementTurnAsync(string conversationId)
    {
        var context = await GetOrCreateContextAsync(conversationId);
        context.TurnCount++;
        context.LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get entity types that are relevant for a specific sub-agent
    /// </summary>
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

    /// <summary>
    /// Check if a decision is relevant for a sub-agent
    /// </summary>
    private static bool IsDecisionRelevantForAgent(DecisionRecord decision, string subAgentName)
    {
        // All decisions from the same agent are relevant
        if (decision.MadeBy == subAgentName)
            return true;

        // Compliance decisions are relevant for most agents
        if (decision.MadeBy == "ComplianceOfficer")
            return true;

        // Account decisions are relevant for portfolio operations
        if (
            decision.MadeBy == "AccountServices"
            && (subAgentName == "PortfolioManager" || subAgentName == "ExternalApiServices")
        )
            return true;

        return false;
    }
}

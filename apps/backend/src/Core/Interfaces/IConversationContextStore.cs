using AgentFrameworkQuickStart.Core.Domain.Intelligence;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Service interface for managing structured conversation context
/// </summary>
public interface IConversationContextStore
{
    /// <summary>
    /// Get or create a context for a conversation
    /// </summary>
    Task<ConversationContext> GetOrCreateContextAsync(string conversationId);

    /// <summary>
    /// Get existing context (returns null if not found)
    /// </summary>
    Task<ConversationContext?> GetContextAsync(string conversationId);

    /// <summary>
    /// Save/update the context
    /// </summary>
    Task SaveContextAsync(ConversationContext context);

    /// <summary>
    /// Add an extracted entity to the context
    /// </summary>
    Task AddEntityAsync(string conversationId, ExtractedEntity entity);

    /// <summary>
    /// Add multiple entities at once
    /// </summary>
    Task AddEntitiesAsync(string conversationId, IEnumerable<ExtractedEntity> entities);

    /// <summary>
    /// Record a user goal
    /// </summary>
    Task AddGoalAsync(string conversationId, UserGoal goal);

    /// <summary>
    /// Record a decision made
    /// </summary>
    Task AddDecisionAsync(string conversationId, DecisionRecord decision);

    /// <summary>
    /// Record sub-agent findings
    /// </summary>
    Task AddSubAgentFindingAsync(string conversationId, SubAgentFinding finding);

    /// <summary>
    /// Add a pending action
    /// </summary>
    Task AddPendingActionAsync(string conversationId, PendingAction action);

    /// <summary>
    /// Complete a pending action
    /// </summary>
    Task CompletePendingActionAsync(string conversationId, string actionId);

    /// <summary>
    /// Generate a context summary suitable for injection into prompts
    /// </summary>
    Task<string> GenerateContextSummaryAsync(string conversationId, int maxTokens = 500);

    /// <summary>
    /// Generate a briefing for a specific sub-agent
    /// </summary>
    Task<SubAgentBriefing> GenerateSubAgentBriefingAsync(
        string conversationId,
        string subAgentName,
        UserIntent currentIntent
    );

    /// <summary>
    /// Clear/reset context for a conversation
    /// </summary>
    Task ClearContextAsync(string conversationId);

    /// <summary>
    /// Increment turn count and update last activity
    /// </summary>
    Task IncrementTurnAsync(string conversationId);
}

/// <summary>
/// Briefing provided to a sub-agent before it handles a request
/// </summary>
public class SubAgentBriefing
{
    /// <summary>
    /// The user's primary goal in this conversation
    /// </summary>
    public string? UserGoal { get; set; }

    /// <summary>
    /// What other agents have already done
    /// </summary>
    public List<string> PreviousAgentActions { get; set; } = [];

    /// <summary>
    /// Recent conversation history (user requests and agent responses)
    /// </summary>
    public List<ConversationTurn> ConversationHistory { get; set; } = [];

    /// <summary>
    /// Relevant entities for this agent
    /// </summary>
    public Dictionary<string, object> RelevantEntities { get; set; } = new();

    /// <summary>
    /// Constraints to respect
    /// </summary>
    public List<string> Constraints { get; set; } = [];

    /// <summary>
    /// Decisions already made that affect this request
    /// </summary>
    public List<string> RelevantDecisions { get; set; } = [];

    /// <summary>
    /// Expected output format or focus areas
    /// </summary>
    public string? ExpectedFocus { get; set; }

    /// <summary>
    /// User's expertise level for response calibration
    /// </summary>
    public string UserExpertise { get; set; } = "intermediate";

    /// <summary>
    /// Preferred response language
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Format as a string for injection into prompts
    /// </summary>
    public string ToPromptString()
    {
        var parts = new List<string> { "=== SESSION BRIEFING ===" };

        if (!string.IsNullOrEmpty(UserGoal))
            parts.Add($"User's Goal: {UserGoal}");

        // Add conversation history for context awareness
        if (ConversationHistory.Count > 0)
        {
            parts.Add("\n=== CONVERSATION HISTORY ===");
            parts.Add(
                "Use this history to understand user references like 'based on the recommendation', 'use that fund', etc."
            );
            foreach (var turn in ConversationHistory)
            {
                parts.Add($"\n[Turn {turn.TurnNumber}] User asked: {turn.UserRequest}");
                parts.Add(
                    $"[Turn {turn.TurnNumber}] {turn.AgentName} responded: {turn.AgentResponse}"
                );
                if (turn.OperationsPerformed.Count > 0)
                {
                    parts.Add(
                        $"[Turn {turn.TurnNumber}] Operations performed: {string.Join(", ", turn.OperationsPerformed)}"
                    );
                }
            }
            parts.Add("=== END HISTORY ===\n");
        }

        if (PreviousAgentActions.Count > 0)
            parts.Add($"Previous Actions:\n- {string.Join("\n- ", PreviousAgentActions)}");

        if (RelevantEntities.Count > 0)
        {
            var entities = string.Join(", ", RelevantEntities.Select(e => $"{e.Key}={e.Value}"));
            parts.Add($"Known Information: {entities}");
        }

        if (Constraints.Count > 0)
            parts.Add($"Constraints: {string.Join(", ", Constraints)}");

        if (RelevantDecisions.Count > 0)
            parts.Add($"Decisions Made:\n- {string.Join("\n- ", RelevantDecisions)}");

        if (!string.IsNullOrEmpty(ExpectedFocus))
            parts.Add($"Focus On: {ExpectedFocus}");

        parts.Add($"User Level: {UserExpertise}");
        parts.Add($"Language: {Language}");
        parts.Add("=== END BRIEFING ===\n");

        return string.Join("\n", parts);
    }
}

/// <summary>
/// Represents a single turn in a conversation
/// </summary>
public class ConversationTurn
{
    public int TurnNumber { get; set; }
    public required string UserRequest { get; set; }
    public required string AgentName { get; set; }
    public required string AgentResponse { get; set; }
    public List<string> OperationsPerformed { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

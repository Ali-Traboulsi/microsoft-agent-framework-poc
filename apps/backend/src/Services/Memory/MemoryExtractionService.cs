using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Memory;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Services.Memory;

/// <summary>
/// LLM-based service for extracting memorable information from conversations.
/// Uses structured output to extract facts, preferences, entities, and generate summaries.
/// </summary>
public class MemoryExtractionService(
    IChatClient chatClient,
    IServiceScopeFactory scopeFactory,
    ILogger<MemoryExtractionService> logger
) : IMemoryExtractionService
{
    private const string MemoryExtractionPrompt = """
        You are a memory extraction assistant. Analyze the conversation turn and extract valuable information to remember about the user.

        Focus on:
        1. FACTS: Personal preferences, goals, constraints, important decisions
        2. ENTITIES: Account IDs, portfolio IDs, fund names, amounts mentioned
        3. PREFERENCES: Communication style, detail level, risk preferences
        4. TOPICS: Investment topics discussed

        User Message: {0}

        Agent Response: {1}

        {2}

        Respond with a JSON object following this exact structure:
        {{
            "newFacts": [
                {{
                    "category": "investment_goal|constraint|preference|personal|decision",
                    "fact": "The extracted fact",
                    "importance": 0.1-1.0,
                    "confidence": 0.1-1.0
                }}
            ],
            "preferencesDetected": [
                {{
                    "preferenceKey": "key_name",
                    "value": "preference_value",
                    "confidence": 0.1-1.0
                }}
            ],
            "entitiesMentioned": [
                {{
                    "entityType": "account_id|portfolio_id|fund_name|amount",
                    "value": "entity_value",
                    "context": "how it was used"
                }}
            ],
            "topics": ["topic1", "topic2"],
            "actionsPerformed": [
                {{
                    "actionType": "subscription|fund_in|redemption|portfolio_creation|recommendation",
                    "description": "what was done",
                    "wasSuccessful": true
                }}
            ],
            "pendingFollowUps": ["any follow-up items mentioned"],
            "suggestedExpertiseLevel": null or "Beginner|Intermediate|Advanced|Expert",
            "suggestedRiskTolerance": null or "Conservative|Moderate|Aggressive|VeryAggressive",
            "shouldCreateSummary": true/false,
            "conversationImportance": 0.1-1.0
        }}

        Only include items you're confident about. Empty arrays are fine if nothing relevant was found.
        Set shouldCreateSummary to true if this is a significant conversation turn.
        """;

    private const string ConversationSummaryPrompt = """
        You are a conversation summarization assistant. Create a comprehensive summary of this investment banking conversation.

        Conversation History:
        {0}

        Create a JSON summary with this structure:
        {{
            "title": "Brief 5-10 word title",
            "summary": "2-3 sentence summary of what happened",
            "topics": ["key topics discussed"],
            "entities": [
                {{
                    "entityType": "account_id|portfolio_id|fund_name|amount",
                    "value": "entity_value",
                    "context": "how it was used",
                    "mentionCount": 1
                }}
            ],
            "actionsPerformed": [
                {{
                    "actionType": "subscription|fund_in|redemption|portfolio_creation|recommendation|compliance_check",
                    "description": "what was done",
                    "wasSuccessful": true
                }}
            ],
            "decisions": ["key decisions made"],
            "pendingFollowUps": ["any unresolved items or promised follow-ups"],
            "satisfactionIndicator": "positive|neutral|negative|unknown",
            "keywords": ["important keywords for future search"],
            "importanceScore": 0.1-1.0
        }}

        Set importanceScore high (0.7+) for conversations with:
        - Significant transactions (fund-in, subscriptions)
        - Important decisions made
        - Complex multi-step operations
        - Learning about user preferences

        Set importanceScore low (0.3 or less) for:
        - Simple questions answered
        - No actions taken
        - Brief clarifications
        """;

    /// <inheritdoc/>
    public async Task<MemoryExtractionResult> ExtractMemoriesAsync(
        string userMessage,
        string agentResponse,
        List<string> subAgentsUsed,
        List<string> multiModalDescriptions,
        CancellationToken cancellationToken = default
    )
    {
        var result = new MemoryExtractionResult();

        try
        {
            // Build context about multi-modal content if any
            var multiModalContext =
                multiModalDescriptions.Count > 0
                    ? $"Multi-modal content shared: {string.Join("; ", multiModalDescriptions)}"
                    : "";

            var prompt = string.Format(
                MemoryExtractionPrompt,
                userMessage,
                agentResponse,
                multiModalContext
            );

            var response = await chatClient.GetResponseAsync(
                prompt,
                new ChatOptions { Temperature = 0.1f },
                cancellationToken
            );

            var jsonResponse = ExtractJsonFromResponse(response.Text ?? "");
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var extraction = JsonSerializer.Deserialize<ExtractionResponse>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (extraction != null)
                {
                    result = MapExtractionToResult(
                        extraction,
                        subAgentsUsed,
                        multiModalDescriptions
                    );
                }
            }

            logger.LogDebug(
                "Extracted memories: {FactCount} facts, {PrefCount} preferences, {EntityCount} entities",
                result.NewFacts.Count,
                result.PreferencesDetected.Count,
                result.EntitiesMentioned.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract memories from conversation turn");
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ConversationSummary?> GenerateConversationSummaryAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Load conversation history from database
            var conversationHistory = await LoadConversationHistoryAsync(
                conversationId,
                cancellationToken
            );

            if (string.IsNullOrEmpty(conversationHistory))
            {
                logger.LogDebug(
                    "No conversation history found for {ConversationId}",
                    conversationId
                );
                return null;
            }

            var prompt = string.Format(ConversationSummaryPrompt, conversationHistory);

            var response = await chatClient.GetResponseAsync(
                prompt,
                new ChatOptions { Temperature = 0.2f },
                cancellationToken
            );

            var jsonResponse = ExtractJsonFromResponse(response.Text ?? "");
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var summaryResponse = JsonSerializer.Deserialize<SummaryResponse>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (summaryResponse != null)
                {
                    return MapSummaryResponseToSummary(summaryResponse, conversationId, userId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to generate conversation summary for {ConversationId}",
                conversationId
            );
        }

        return null;
    }

    /// <inheritdoc/>
    public Task<bool> ShouldSummarizeConversationAsync(
        string conversationId,
        int turnCount,
        CancellationToken cancellationToken = default
    )
    {
        // Simple heuristic - summarize if conversation has enough turns
        // Could be enhanced with LLM-based evaluation
        return Task.FromResult(turnCount >= 3);
    }

    private async Task<string> LoadConversationHistoryAsync(
        string conversationId,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParse(conversationId, out var conversationGuid))
            return string.Empty;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entries = await dbContext
            .ConversationMemoryEntries.Where(e => e.ConversationId == conversationGuid)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return string.Empty;

        var history = new System.Text.StringBuilder();
        foreach (var entry in entries)
        {
            history.AppendLine($"[Turn {entry.SequenceNumber}]");
            history.AppendLine($"User: {TruncateIfNeeded(entry.UserRequest, 500)}");
            history.AppendLine(
                $"Agent ({entry.AgentName}): {TruncateIfNeeded(entry.AgentResponse, 800)}"
            );
            history.AppendLine();
        }

        return history.ToString();
    }

    private static string ExtractJsonFromResponse(string response)
    {
        // Try to find JSON object in response
        var startIndex = response.IndexOf('{');
        var endIndex = response.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return response.Substring(startIndex, endIndex - startIndex + 1);
        }

        return string.Empty;
    }

    private static MemoryExtractionResult MapExtractionToResult(
        ExtractionResponse extraction,
        List<string> subAgentsUsed,
        List<string> multiModalDescriptions
    )
    {
        var result = new MemoryExtractionResult
        {
            Topics = extraction.Topics ?? [],
            PendingFollowUps = extraction.PendingFollowUps ?? [],
            ShouldCreateSummary = extraction.ShouldCreateSummary,
            ConversationImportance = extraction.ConversationImportance,
        };

        // Map facts
        if (extraction.NewFacts != null)
        {
            result.NewFacts = extraction
                .NewFacts.Select(f => new MemorizedFact
                {
                    Category = f.Category ?? "general",
                    Fact = f.Fact ?? "",
                    Importance = f.Importance,
                    Confidence = f.Confidence,
                    LearnedAt = DateTime.UtcNow,
                })
                .Where(f => !string.IsNullOrEmpty(f.Fact))
                .ToList();
        }

        // Map preferences
        if (extraction.PreferencesDetected != null)
        {
            result.PreferencesDetected = extraction
                .PreferencesDetected.Select(p => new UserPreference
                {
                    PreferenceKey = p.PreferenceKey ?? "",
                    Value = p.Value ?? "",
                    Confidence = p.Confidence,
                    LearnedAt = DateTime.UtcNow,
                })
                .Where(p => !string.IsNullOrEmpty(p.PreferenceKey))
                .ToList();
        }

        // Map entities
        if (extraction.EntitiesMentioned != null)
        {
            result.EntitiesMentioned = extraction
                .EntitiesMentioned.Select(e => new ExtractedMemoryEntity
                {
                    EntityType = e.EntityType ?? "unknown",
                    Value = e.Value ?? "",
                    Context = e.Context,
                })
                .Where(e => !string.IsNullOrEmpty(e.Value))
                .ToList();
        }

        // Map actions
        if (extraction.ActionsPerformed != null)
        {
            result.ActionsPerformed = extraction
                .ActionsPerformed.Select(a => new PerformedAction
                {
                    ActionType = a.ActionType ?? "unknown",
                    Description = a.Description ?? "",
                    WasSuccessful = a.WasSuccessful,
                    SubAgentName = subAgentsUsed.FirstOrDefault(),
                    PerformedAt = DateTime.UtcNow,
                })
                .Where(a => !string.IsNullOrEmpty(a.Description))
                .ToList();
        }

        // Map expertise/risk suggestions
        if (
            !string.IsNullOrEmpty(extraction.SuggestedExpertiseLevel)
            && Enum.TryParse<ExpertiseLevel>(extraction.SuggestedExpertiseLevel, out var el)
        )
        {
            result.SuggestedExpertiseLevel = el;
        }

        if (
            !string.IsNullOrEmpty(extraction.SuggestedRiskTolerance)
            && Enum.TryParse<RiskTolerance>(extraction.SuggestedRiskTolerance, out var rt)
        )
        {
            result.SuggestedRiskTolerance = rt;
        }

        // Add multi-modal references
        foreach (var desc in multiModalDescriptions)
        {
            result.MultiModalContent.Add(
                new MultiModalReference
                {
                    ContentType = InferContentType(desc),
                    Description = desc,
                    ReferencedAt = DateTime.UtcNow,
                }
            );
        }

        return result;
    }

    private static ConversationSummary MapSummaryResponseToSummary(
        SummaryResponse response,
        string conversationId,
        string userId
    )
    {
        var summary = new ConversationSummary
        {
            ConversationId = conversationId,
            UserId = userId,
            Title = response.Title ?? "Conversation",
            Summary = response.Summary ?? "",
            Topics = response.Topics ?? [],
            Decisions = response.Decisions ?? [],
            PendingFollowUps = response.PendingFollowUps ?? [],
            SatisfactionIndicator = response.SatisfactionIndicator,
            Keywords = response.Keywords ?? [],
            ImportanceScore = Math.Clamp(response.ImportanceScore, 0.0, 1.0),
            StartedAt = DateTime.UtcNow.AddHours(-1), // Will be updated from actual data
            EndedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        // Map entities
        if (response.Entities != null)
        {
            summary.Entities = response
                .Entities.Select(e => new ExtractedMemoryEntity
                {
                    EntityType = e.EntityType ?? "unknown",
                    Value = e.Value ?? "",
                    Context = e.Context,
                    MentionCount = e.MentionCount,
                })
                .Where(e => !string.IsNullOrEmpty(e.Value))
                .ToList();
        }

        // Map actions
        if (response.ActionsPerformed != null)
        {
            summary.ActionsPerformed = response
                .ActionsPerformed.Select(a => new PerformedAction
                {
                    ActionType = a.ActionType ?? "unknown",
                    Description = a.Description ?? "",
                    WasSuccessful = a.WasSuccessful,
                    PerformedAt = DateTime.UtcNow,
                })
                .Where(a => !string.IsNullOrEmpty(a.Description))
                .ToList();
        }

        return summary;
    }

    private static string InferContentType(string description)
    {
        var lower = description.ToLowerInvariant();
        if (
            lower.Contains("image")
            || lower.Contains("photo")
            || lower.Contains("picture")
            || lower.Contains("chart")
        )
            return "image";
        if (lower.Contains("audio") || lower.Contains("voice") || lower.Contains("recording"))
            return "audio";
        if (lower.Contains("document") || lower.Contains("pdf") || lower.Contains("file"))
            return "document";
        return "unknown";
    }

    private static string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    // DTOs for JSON deserialization
    private class ExtractionResponse
    {
        public List<FactDto>? NewFacts { get; set; }
        public List<PreferenceDto>? PreferencesDetected { get; set; }
        public List<EntityDto>? EntitiesMentioned { get; set; }
        public List<string>? Topics { get; set; }
        public List<ActionDto>? ActionsPerformed { get; set; }
        public List<string>? PendingFollowUps { get; set; }
        public string? SuggestedExpertiseLevel { get; set; }
        public string? SuggestedRiskTolerance { get; set; }
        public bool ShouldCreateSummary { get; set; }
        public double ConversationImportance { get; set; }
    }

    private class SummaryResponse
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public List<string>? Topics { get; set; }
        public List<EntityDto>? Entities { get; set; }
        public List<ActionDto>? ActionsPerformed { get; set; }
        public List<string>? Decisions { get; set; }
        public List<string>? PendingFollowUps { get; set; }
        public string? SatisfactionIndicator { get; set; }
        public List<string>? Keywords { get; set; }
        public double ImportanceScore { get; set; }
    }

    private class FactDto
    {
        public string? Category { get; set; }
        public string? Fact { get; set; }
        public double Importance { get; set; }
        public double Confidence { get; set; }
    }

    private class PreferenceDto
    {
        public string? PreferenceKey { get; set; }
        public string? Value { get; set; }
        public double Confidence { get; set; }
    }

    private class EntityDto
    {
        public string? EntityType { get; set; }
        public string? Value { get; set; }
        public string? Context { get; set; }
        public int MentionCount { get; set; } = 1;
    }

    private class ActionDto
    {
        public string? ActionType { get; set; }
        public string? Description { get; set; }
        public bool WasSuccessful { get; set; } = true;
    }
}

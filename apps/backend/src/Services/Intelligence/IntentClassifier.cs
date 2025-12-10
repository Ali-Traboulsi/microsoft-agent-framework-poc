using System.Diagnostics;
using System.Text.Json;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Interfaces;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Services.Intelligence;

/// <summary>
/// LLM-powered intent classifier that analyzes user messages
/// to extract intent, entities, and determine execution strategy
/// </summary>
public class IntentClassifier(IChatClient chatClient, ILogger<IntentClassifier> logger)
    : IIntentClassifier
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Intelligence.IntentClassifier",
        "1.0.0"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string ClassificationPrompt = """
        You are an expert intent classifier for an investment banking AI assistant.
        Analyze the user's message and extract structured information.

        ## Available Sub-Agents
        - **PortfolioManager**: Portfolio creation, analysis, holdings, rebalancing
        - **InvestmentAdvisor**: Fund recommendations, investment advice, comparisons
        - **AccountServices**: Account balance, deposits, transfers, transaction history
        - **ComplianceOfficer**: Risk assessment, regulatory compliance, verification
        - **ProfitProjection**: Investment projections, returns calculation, scenarios
        - **ExternalApiServices**: SNB Capital API, mutual funds, fund-in operations

        ## Intent Types
        - ProfitProjection: User wants to see potential returns/profits
        - InvestmentAdvice: User wants recommendations on what to invest in
        - FundRecommendation: User specifically asking about funds
        - RiskAssessment: User asking about risk levels
        - PortfolioCreation: User wants to create a new portfolio
        - PortfolioAnalysis: User wants to analyze existing portfolio
        - PortfolioRebalancing: User wants to rebalance portfolio
        - HoldingsInquiry: User asking about what they hold
        - AccountBalance: User asking about their balance
        - FundTransfer: User wants to transfer/move money
        - TransactionHistory: User asking about past transactions
        - MutualFundSearch: User searching for mutual funds
        - MutualFundDetails: User wants details about a specific fund
        - FundInOperation: User wants to add money to investment account
        - ComplianceCheck: User asking about compliance/regulations
        - GeneralInquiry: General questions about the platform
        - WebSearch: Request for current news/market info
        - Greeting: Simple hello/greeting
        - Clarification: User is clarifying a previous question
        - CompleteInvestmentWorkflow: Complex request needing multiple agents
        - Unknown: Cannot determine intent

        ## Entity Types to Extract
        - amount: Any monetary value (normalize to number, e.g., "50k" → 50000)
        - currency: Currency code (SAR, USD, etc.)
        - duration: Time period in months (e.g., "3 years" → 36)
        - risk_level: Conservative, Moderate, Aggressive
        - account_id: Account identifiers
        - portfolio_id: Portfolio identifiers
        - fund_id: Fund identifiers or names
        - customer_id: Customer/CIF numbers
        - shariah_compliant: Boolean if user mentions halal/shariah/islamic
        - portfolio_name: Name for a portfolio

        ## Execution Strategies
        - SingleAgent: One agent can handle this
        - Sequential: Multiple agents needed in order
        - Parallel: Multiple agents can work simultaneously
        - DirectResponse: Simple response, no agent needed
        - NeedsClarification: Must ask user for more info
        - Workflow: Predefined workflow should be triggered
        - Hierarchical: Master should review sub-agent work

        ## Instructions
        1. Identify the PRIMARY intent (most important)
        2. Identify any SECONDARY intents
        3. Extract ALL entities mentioned (normalize values)
        4. Determine which sub-agents are needed
        5. Choose the best execution strategy
        6. Assess user expertise (novice/intermediate/expert)
        7. Detect language (en/ar)
        8. Note any ambiguities
        9. Create a refined request for the sub-agent
        10. Apply smart defaults for missing required info

        ## Smart Defaults (for demos)
        - amount: 100000 (if investment amount needed but not specified)
        - currency: SAR
        - duration: 36 months (if time horizon needed but not specified)
        - risk_level: Moderate (if risk needed but not specified)
        - shariah_compliant: false (unless explicitly requested)

        Analyze this message and respond with valid JSON only:
        """;

    public async Task<UserIntent> ClassifyAsync(
        string userMessage,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = ActivitySource.StartActivity("ClassifyIntent");
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation(
                "Classifying intent for message: {Message}",
                userMessage.Length > 100 ? userMessage[..100] + "..." : userMessage
            );

            // Build the classification request
            var contextInfo =
                conversationContext != null
                    ? $"\n\nConversation Context:\n{conversationContext.GenerateSummary()}"
                    : "";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, ClassificationPrompt),
                new(ChatRole.User, $"User Message: {userMessage}{contextInfo}"),
            };

            // Use structured output with JSON schema
            var schema = AIJsonUtilities.CreateJsonSchema(typeof(IntentClassificationResult));
            var options = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    schema: schema,
                    schemaName: "IntentClassification",
                    schemaDescription: "Structured intent classification result"
                ),
                Temperature = 0.1f, // Low temperature for consistent classification
            };

            var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);
            var responseText = response.Text ?? "{}";

            // Parse the structured response
            var classificationResult = JsonSerializer.Deserialize<IntentClassificationResult>(
                responseText,
                JsonOptions
            );

            if (classificationResult == null)
            {
                throw new InvalidOperationException("Failed to parse classification result");
            }

            sw.Stop();

            // Convert to UserIntent
            var intent = MapToUserIntent(classificationResult, userMessage, sw.ElapsedMilliseconds);

            activity?.SetTag("intent.primary", intent.PrimaryIntent.ToString());
            activity?.SetTag("intent.confidence", intent.Confidence);
            activity?.SetTag("intent.entities_count", intent.Entities.Count);
            activity?.SetTag("intent.strategy", intent.Strategy.ToString());

            logger.LogInformation(
                "Intent classified: {Intent} with confidence {Confidence:P0}, strategy: {Strategy}, agents: [{Agents}]",
                intent.PrimaryIntent,
                intent.Confidence,
                intent.Strategy,
                string.Join(", ", intent.RequiredSubAgents)
            );

            return intent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to classify intent for message: {Message}", userMessage);
            sw.Stop();

            // Return a fallback intent
            return new UserIntent
            {
                OriginalMessage = userMessage,
                PrimaryIntent = IntentType.Unknown,
                Confidence = 0.0,
                Strategy = ExecutionStrategy.DirectResponse,
                ClassificationDurationMs = sw.ElapsedMilliseconds,
                IntentSummary = "Could not classify intent",
                RefinedRequest = userMessage,
            };
        }
    }

    public async Task<IntentType> QuickClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken = default
    )
    {
        // Quick keyword-based classification for simple routing
        var lowerMessage = userMessage.ToLowerInvariant();

        // Check for greeting patterns first
        if (IsGreeting(lowerMessage))
            return IntentType.Greeting;

        // Investment/Projection keywords
        if (
            ContainsAny(
                lowerMessage,
                "invest",
                "project",
                "return",
                "profit",
                "earn",
                "grow",
                "استثمار",
                "أرباح",
                "عوائد"
            )
        )
            return IntentType.ProfitProjection;

        // Portfolio keywords
        if (ContainsAny(lowerMessage, "portfolio", "holding", "allocation", "محفظة"))
            return IntentType.PortfolioAnalysis;

        // Account keywords
        if (ContainsAny(lowerMessage, "balance", "account", "deposit", "رصيد", "حساب"))
            return IntentType.AccountBalance;

        // Fund keywords
        if (ContainsAny(lowerMessage, "fund", "mutual", "صندوق"))
            return IntentType.MutualFundSearch;

        // Transfer keywords
        if (ContainsAny(lowerMessage, "transfer", "fund-in", "move money", "تحويل"))
            return IntentType.FundInOperation;

        // Fall back to full classification for complex cases
        var fullIntent = await ClassifyAsync(userMessage, null, cancellationToken);
        return fullIntent.PrimaryIntent;
    }

    public Task RecordOutcomeAsync(string intentId, bool wasCorrect, string? actualIntent = null)
    {
        // This would be used for learning/improving classification
        // For now, just log it
        logger.LogInformation(
            "Intent outcome recorded: {IntentId}, Correct: {WasCorrect}, Actual: {Actual}",
            intentId,
            wasCorrect,
            actualIntent ?? "N/A"
        );

        return Task.CompletedTask;
    }

    private UserIntent MapToUserIntent(
        IntentClassificationResult result,
        string originalMessage,
        long durationMs
    )
    {
        var intent = new UserIntent
        {
            OriginalMessage = originalMessage,
            PrimaryIntent = ParseIntentType(result.PrimaryIntent),
            SecondaryIntents = result.SecondaryIntents.Select(ParseIntentType).ToList(),
            Confidence = result.Confidence,
            RequiredSubAgents = result.RequiredSubAgents,
            Strategy = ParseExecutionStrategy(result.ExecutionStrategy),
            Urgency = ParseUrgencyLevel(result.Urgency),
            Language = result.Language,
            UserExpertiseLevel = result.UserExpertise,
            Sentiment = result.Sentiment,
            IntentSummary = result.IntentSummary,
            RefinedRequest = result.RefinedRequest,
            Ambiguities = result.Ambiguities,
            SuggestedClarifications = result.SuggestedClarifications,
            AppliedDefaults = result.AppliedDefaults,
            ClassificationDurationMs = durationMs,
        };

        // Convert entities
        foreach (var entityResult in result.Entities)
        {
            intent.Entities.Add(
                new ExtractedEntity
                {
                    EntityType = entityResult.Type,
                    RawValue = entityResult.RawValue,
                    NormalizedValue = entityResult.NormalizedValue,
                    Confidence = entityResult.Confidence,
                }
            );
        }

        return intent;
    }

    private static IntentType ParseIntentType(string value)
    {
        if (Enum.TryParse<IntentType>(value, ignoreCase: true, out var result))
            return result;
        return IntentType.Unknown;
    }

    private static ExecutionStrategy ParseExecutionStrategy(string value)
    {
        if (Enum.TryParse<ExecutionStrategy>(value, ignoreCase: true, out var result))
            return result;
        return ExecutionStrategy.SingleAgent;
    }

    private static UrgencyLevel ParseUrgencyLevel(string value)
    {
        if (Enum.TryParse<UrgencyLevel>(value, ignoreCase: true, out var result))
            return result;
        return UrgencyLevel.Medium;
    }

    private static bool IsGreeting(string message)
    {
        var greetings = new[]
        {
            "hello",
            "hi",
            "hey",
            "good morning",
            "good afternoon",
            "good evening",
            "مرحبا",
            "السلام",
            "أهلا",
        };
        return greetings.Any(g => message.StartsWith(g) || message == g);
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k));
    }
}

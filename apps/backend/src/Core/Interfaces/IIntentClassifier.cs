using AgentFrameworkQuickStart.Core.Domain.Intelligence;

namespace AgentFrameworkQuickStart.Core.Interfaces;

/// <summary>
/// Service interface for intelligent intent classification
/// </summary>
public interface IIntentClassifier
{
    /// <summary>
    /// Classify a user message and extract all relevant information
    /// </summary>
    /// <param name="userMessage">The raw user message</param>
    /// <param name="conversationContext">Optional context from the conversation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Fully classified user intent</returns>
    Task<UserIntent> ClassifyAsync(
        string userMessage,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Quick classification for simple routing decisions (faster, less detailed)
    /// </summary>
    Task<IntentType> QuickClassifyAsync(
        string userMessage,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validate if a classified intent matches the actual outcome
    /// (used for learning and improvement)
    /// </summary>
    Task RecordOutcomeAsync(string intentId, bool wasCorrect, string? actualIntent = null);
}

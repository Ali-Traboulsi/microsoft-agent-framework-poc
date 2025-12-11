using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Core.Domain.Intelligence;
using AgentFrameworkQuickStart.Core.Domain.Reasoning;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Abstractions;

/// <summary>
/// Helper methods for the Master Orchestrator
/// </summary>
/// <remarks>
/// This interface defines utility functions that assist the Master Orchestrator
/// in managing sub-agents, processing findings, and coordinating tasks.
/// </remarks>
public interface IMasterOrchestratorHelper
{
    /// <summary>
    /// Get a string representation of all available sub-agents and their capabilities
    /// </summary>
    /// <returns></returns>
    string GetAvailableSubAgentsAsString();

    /// <summary>
    /// Get information about all available sub-agents and their capabilities
    /// </summary>
    /// <returns></returns> <summary>
    /// </summary>
    /// <returns></returns>
    List<SubAgentInfo> GetAvailableSubAgents();

    /// <summary>
    ///  Format a ThoughtChain as human-readable text
    /// </summary>
    /// <param name="thoughtChain"></param>
    /// <returns></returns>
    string FormatReasoningAsHumanReadable(ThoughtChain thoughtChain);

    /// <summary>
    /// Stream multi-modal content asynchronously
    /// </summary>
    /// <param name="contents"></param>
    /// <param name="conversationId"></param>
    /// <param name="enableThinking"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<UnifiedStreamingChunk> StreamMultiModalAsync(
        List<AIContent> contents,
        string conversationId,
        bool enableThinking,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Restore prior messages for a given conversation
    /// </summary>
    /// <param name="conversationId"></param>
    /// <param name="priorMessages"></param>
    /// <returns></returns>
    Task RestorePriorMessagesAsync(
        string conversationId,
        IEnumerable<ConversationMessage> priorMessages
    );

    /// <summary>
    /// Extract transcription text from AIContent list
    /// </summary>
    /// <param name="contents"></param>
    /// <returns></returns> <summary>
    /// </summary>
    /// <param name="contents"></param>
    /// <returns></returns>
    string? ExtractTranscriptionFromContents(List<AIContent> contents);

    /// <summary>
    /// Detect if the response is asking for information and create a pending action.
    /// This enables context-aware follow-up detection in subsequent messages.
    /// </summary>
    /// <param name="responseText"></param>
    /// <param name="classifiedIntent"></param>
    /// <returns></returns>
    PendingAction? DetectPendingActionFromResponse(
        string responseText,
        UserIntent classifiedIntent
    );

    /// <summary>
    /// Determine the appropriate agent for a given entity type and user intent
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="intent"></param>
    /// <returns></returns>
    string DetermineAgentForEntityType(string entityType, UserIntent intent);

    /// <summary>
    /// Generate human-readable thinking steps for transparency
    /// </summary>
    /// <param name="intent"></param>
    /// <returns></returns>
    string GenerateHumanReadableThinking(UserIntent intent);

    /// <summary>
    /// Get a friendly description for a sub-agent
    /// </summary>
    /// <param name="agentName"></param>
    /// <returns></returns> <summary>
    ///
    /// </summary>
    /// <param name="agentName"></param>
    /// <returns></returns>
    string GetFriendlyAgentDescription(string agentName);

    /// <summary>
    ///  Get a friendly description for an entity
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="value"></param>
    /// <returns></returns> <summary>
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    string GetFriendlyEntityDescription(string entityType, string value);
}

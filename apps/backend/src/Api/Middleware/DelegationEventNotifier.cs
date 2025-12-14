using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Api.Middleware;

/// <summary>
/// Service that pushes delegation events directly to SignalR clients in real-time.
/// This enables events to appear BEFORE content since they're pushed immediately when tools execute.
/// </summary>
public interface IDelegationEventNotifier
{
    /// <summary>
    /// Push a delegation event to all connected clients for a conversation
    /// </summary>
    Task NotifyAsync(string conversationId, DelegationEvent delegationEvent);
}

/// <summary>
/// Implementation that uses SignalR hub context to push events
/// </summary>
public class DelegationEventNotifier(IHubContext<MasterAgentHub> hubContext)
    : IDelegationEventNotifier
{
    public async Task NotifyAsync(string conversationId, DelegationEvent delegationEvent)
    {
        // Convert to the response format the frontend expects
        var response = new MasterStreamingResponse
        {
            Type = MapEventTypeToResponseType(delegationEvent.Type),
            Content = delegationEvent.Result,
            SubAgentName = delegationEvent.SubAgentName,
            ToolName = delegationEvent.ToolName,
            StepId = delegationEvent.StepId,
            StepName =
                delegationEvent.StepName
                ?? delegationEvent.SubAgentName
                ?? delegationEvent.ToolName,
            StepNameAr = delegationEvent.StepNameAr,
            StepNumber = delegationEvent.StepNumber,
            TotalSteps = delegationEvent.TotalSteps,
            StepCompleted = delegationEvent.StepCompleted,
            StepDurationMs = delegationEvent.StepDurationMs,
            StepDetails = delegationEvent.StepDetails,
            IsComplete = false,
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = delegationEvent.Type.ToString(),
                ["timestamp"] = delegationEvent.Timestamp,
            },
        };

        // Push to all clients - they'll filter by conversationId on the frontend
        await hubContext.Clients.All.SendAsync("receiveDelegationEvent", conversationId, response);
    }

    private static string MapEventTypeToResponseType(DelegationEventType type) =>
        type switch
        {
            // Sub-agent events
            DelegationEventType.SubAgentDelegationStart => "SubAgentDelegation",
            DelegationEventType.SubAgentDelegationComplete => "SubAgentComplete",
            DelegationEventType.SubAgentDelegationError => "SubAgentError",

            // Tool events
            DelegationEventType.ToolExecutionStart => "ToolExecution",
            DelegationEventType.ToolExecutionComplete => "ToolComplete",
            DelegationEventType.ToolExecutionError => "ToolError",

            // Workflow events
            DelegationEventType.WorkflowStepStart => "StepStart",
            DelegationEventType.WorkflowStepComplete => "StepComplete",

            // Thinking/Transparency events
            DelegationEventType.ThinkingStart => "ThinkingStart",
            DelegationEventType.ThinkingProgress => "ThinkingProgress",
            DelegationEventType.ThinkingComplete => "ThinkingComplete",

            // Memory events
            DelegationEventType.MemoryRetrievalStart => "MemoryRetrievalStart",
            DelegationEventType.MemoryRetrievalComplete => "MemoryRetrievalComplete",
            DelegationEventType.MemoryStorageStart => "MemoryStorageStart",
            DelegationEventType.MemoryStorageComplete => "MemoryStorageComplete",

            // Intent/Context events
            DelegationEventType.IntentAnalysisStart => "IntentAnalysisStart",
            DelegationEventType.IntentAnalysisComplete => "IntentAnalysisComplete",
            DelegationEventType.ContextBuildingStart => "ContextBuildingStart",
            DelegationEventType.ContextBuildingComplete => "ContextBuildingComplete",
            DelegationEventType.HistoryProcessing => "HistoryProcessing",

            // Reasoning events
            DelegationEventType.ReasoningStep => "ReasoningStep",
            DelegationEventType.GeneratingResponse => "GeneratingResponse",

            _ => "Unknown",
        };
}

/// <summary>
/// Static accessor for the notifier service (used by middleware which can't use DI directly)
/// </summary>
public static class DelegationEventNotifierAccessor
{
    private static IDelegationEventNotifier? _instance;

    public static void SetInstance(IDelegationEventNotifier notifier)
    {
        _instance = notifier;
    }

    public static IDelegationEventNotifier? Instance => _instance;
}

using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Middleware;

/// <summary>
/// Middleware that intercepts function calls to emit delegation events
/// </summary>
public class DelegationEventMiddleware
{
    // Thread-safe queue to store delegation events for the current request
    private static readonly ConcurrentDictionary<
        string,
        ConcurrentQueue<DelegationEvent>
    > EventQueues = new();

    public static string? CurrentConversationId { get; set; }

    /// <summary>
    /// Function invocation middleware that tracks delegations
    /// </summary>
    public static async ValueTask<object?> FunctionInvocationMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken
    )
    {
        var functionName = context.Function.Name;
        var conversationId = CurrentConversationId ?? "unknown";

        // Check if this is a delegation function
        if (functionName == "DelegateToSubAgent" || functionName == "DelegateToMultipleSubAgents")
        {
            // Extract sub-agent name from arguments
            var subAgentName = ExtractSubAgentName(context);

            // Emit delegation start event
            EmitEvent(
                conversationId,
                new DelegationEvent
                {
                    Type = DelegationEventType.SubAgentDelegationStart,
                    SubAgentName = subAgentName,
                    FunctionName = functionName,
                    Timestamp = DateTime.UtcNow,
                }
            );

            try
            {
                // Execute the actual function
                var result = await next(context, cancellationToken);

                // Emit delegation complete event
                EmitEvent(
                    conversationId,
                    new DelegationEvent
                    {
                        Type = DelegationEventType.SubAgentDelegationComplete,
                        SubAgentName = subAgentName,
                        FunctionName = functionName,
                        Timestamp = DateTime.UtcNow,
                        Result = result?.ToString(),
                    }
                );

                return result;
            }
            catch (Exception ex)
            {
                // Emit delegation error event
                EmitEvent(
                    conversationId,
                    new DelegationEvent
                    {
                        Type = DelegationEventType.SubAgentDelegationError,
                        SubAgentName = subAgentName,
                        FunctionName = functionName,
                        Timestamp = DateTime.UtcNow,
                        Error = ex.Message,
                    }
                );

                throw;
            }
        }
        else
        {
            // For other tools, emit tool execution events
            EmitEvent(
                conversationId,
                new DelegationEvent
                {
                    Type = DelegationEventType.ToolExecutionStart,
                    ToolName = functionName,
                    Timestamp = DateTime.UtcNow,
                }
            );

            try
            {
                var result = await next(context, cancellationToken);

                EmitEvent(
                    conversationId,
                    new DelegationEvent
                    {
                        Type = DelegationEventType.ToolExecutionComplete,
                        ToolName = functionName,
                        Timestamp = DateTime.UtcNow,
                    }
                );

                return result;
            }
            catch (Exception ex)
            {
                EmitEvent(
                    conversationId,
                    new DelegationEvent
                    {
                        Type = DelegationEventType.ToolExecutionError,
                        ToolName = functionName,
                        Timestamp = DateTime.UtcNow,
                        Error = ex.Message,
                    }
                );

                throw;
            }
        }
    }

    /// <summary>
    /// Extract sub-agent name from function arguments
    /// </summary>
    private static string? ExtractSubAgentName(FunctionInvocationContext context)
    {
        // Try to get subAgentName parameter
        if (context.Arguments.TryGetValue("subAgentName", out var subAgentValue))
        {
            return subAgentValue?.ToString();
        }

        // For DelegateToMultipleSubAgents, we'll parse the JSON later
        if (
            context.Function.Name == "DelegateToMultipleSubAgents"
            && context.Arguments.TryGetValue("delegationsJson", out var jsonValue)
        )
        {
            return "MultipleAgents";
        }

        return null;
    }

    /// <summary>
    /// Emit an event to the queue
    /// </summary>
    private static void EmitEvent(string conversationId, DelegationEvent delegationEvent)
    {
        var queue = EventQueues.GetOrAdd(
            conversationId,
            _ => new ConcurrentQueue<DelegationEvent>()
        );
        queue.Enqueue(delegationEvent);
    }

    /// <summary>
    /// Emit a workflow progress event (public method for external callers)
    /// </summary>
    public static void EmitWorkflowProgressEvent(
        string conversationId,
        string stepId,
        string stepName,
        string stepNameAr,
        int stepNumber,
        int totalSteps,
        bool isCompleted,
        long? durationMs = null,
        string? details = null
    )
    {
        var eventType = isCompleted
            ? DelegationEventType.WorkflowStepComplete
            : DelegationEventType.WorkflowStepStart;

        EmitEvent(
            conversationId,
            new DelegationEvent
            {
                Type = eventType,
                StepId = stepId,
                StepName = stepName,
                StepNameAr = stepNameAr,
                StepNumber = stepNumber,
                TotalSteps = totalSteps,
                StepCompleted = isCompleted,
                StepDurationMs = durationMs,
                StepDetails = details,
                Timestamp = DateTime.UtcNow,
            }
        );
    }

    /// <summary>
    /// Get all events for a conversation and clear them
    /// </summary>
    public static IEnumerable<DelegationEvent> GetAndClearEvents(string conversationId)
    {
        if (EventQueues.TryRemove(conversationId, out var queue))
        {
            var events = new List<DelegationEvent>();
            while (queue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }
            return events;
        }

        return Enumerable.Empty<DelegationEvent>();
    }

    /// <summary>
    /// Try to dequeue a single event (non-blocking)
    /// </summary>
    public static bool TryGetNextEvent(string conversationId, out DelegationEvent? delegationEvent)
    {
        if (EventQueues.TryGetValue(conversationId, out var queue) && queue.TryDequeue(out var evt))
        {
            delegationEvent = evt;
            return true;
        }

        delegationEvent = null;
        return false;
    }

    /// <summary>
    /// Clean up old conversation queues
    /// </summary>
    public static void CleanupConversation(string conversationId)
    {
        EventQueues.TryRemove(conversationId, out _);
    }
}

/// <summary>
/// Delegation event captured by middleware
/// </summary>
public class DelegationEvent
{
    public DelegationEventType Type { get; set; }
    public string? SubAgentName { get; set; }
    public string? FunctionName { get; set; }
    public string? ToolName { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }

    // Workflow progress fields
    public string? StepId { get; set; }
    public string? StepName { get; set; }
    public string? StepNameAr { get; set; }
    public int? StepNumber { get; set; }
    public int? TotalSteps { get; set; }
    public bool? StepCompleted { get; set; }
    public long? StepDurationMs { get; set; }
    public string? StepDetails { get; set; }
}

/// <summary>
/// Types of delegation events
/// </summary>
public enum DelegationEventType
{
    SubAgentDelegationStart,
    SubAgentDelegationComplete,
    SubAgentDelegationError,
    ToolExecutionStart,
    ToolExecutionComplete,
    ToolExecutionError,
    WorkflowStepStart,
    WorkflowStepComplete,
    WorkflowProgress,
}

using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// Service for pushing real-time workflow progress events to clients via SignalR.
/// This enables live updates during long-running workflow operations.
/// </summary>
public class WorkflowProgressNotifier(
    IHubContext<MasterAgentHub> hubContext,
    ILogger<WorkflowProgressNotifier> logger
)
{
    /// <summary>
    /// Push a workflow progress event to a specific conversation
    /// Properly awaits SignalR send to ensure real-time delivery
    /// </summary>
    public async Task NotifyProgressAsync(
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
        var eventType = isCompleted ? "StepComplete" : "StepStart";

        var response = new MasterStreamingResponse
        {
            Type = eventType,
            Content = details,
            SubAgentName = null,
            ToolName = "ExecuteFundInWorkflow",
            IsComplete = false,
            Metadata = null,
            StepId = stepId,
            StepName = stepName,
            StepNameAr = stepNameAr,
            StepNumber = stepNumber,
            TotalSteps = totalSteps,
            StepCompleted = isCompleted,
            StepDurationMs = durationMs,
            StepDetails = details,
            ProjectionResult = null,
        };

        logger.LogInformation(
            "📤 Pushing workflow progress via SignalR: ConversationId={ConversationId}, Step={StepNumber}/{TotalSteps}, StepId={StepId}, Completed={Completed}",
            conversationId,
            stepNumber,
            totalSteps,
            stepId,
            isCompleted
        );

        try
        {
            // Use camelCase method name to match JavaScript SignalR client conventions
            // Await the send to ensure message is delivered before continuing
            await hubContext.Clients.All.SendAsync(
                "receiveWorkflowProgress",
                conversationId,
                response
            );

            // Small delay to ensure SignalR message is flushed and delivered
            // This prevents all messages from being buffered together when steps complete quickly
            await Task.Delay(50);

            logger.LogInformation(
                "✅ SignalR workflow progress delivered: Step {StepNumber}/{TotalSteps}, StepId={StepId}",
                stepNumber,
                totalSteps,
                stepId
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push workflow progress via SignalR");
        }
    }

    /// <summary>
    /// Push multiple progress events in sequence
    /// </summary>
    public async Task NotifyStepStartAndCompleteAsync(
        string conversationId,
        string stepId,
        string stepName,
        string stepNameAr,
        int stepNumber,
        int totalSteps,
        Func<Task> stepAction,
        string? startDetails = null
    )
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Notify step start
        await NotifyProgressAsync(
            conversationId,
            stepId,
            stepName,
            stepNameAr,
            stepNumber,
            totalSteps,
            isCompleted: false,
            details: startDetails
        );

        try
        {
            // Execute the step
            await stepAction();

            stopwatch.Stop();

            // Notify step complete
            await NotifyProgressAsync(
                conversationId,
                stepId,
                stepName,
                stepNameAr,
                stepNumber,
                totalSteps,
                isCompleted: true,
                durationMs: stopwatch.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Notify step failed
            await NotifyProgressAsync(
                conversationId,
                stepId,
                stepName,
                stepNameAr,
                stepNumber,
                totalSteps,
                isCompleted: true,
                durationMs: stopwatch.ElapsedMilliseconds,
                details: $"Error: {ex.Message}"
            );

            throw;
        }
    }
}

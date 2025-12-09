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
    /// Uses fire-and-forget pattern to ensure events are sent immediately without blocking
    /// </summary>
    public Task NotifyProgressAsync(
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
            "Pushing workflow progress via SignalR: ConversationId={ConversationId}, Step={StepNumber}/{TotalSteps}, StepId={StepId}, Completed={Completed}",
            conversationId,
            stepNumber,
            totalSteps,
            stepId,
            isCompleted
        );

        // Fire-and-forget: Queue the send operation without awaiting
        // This ensures workflow progress is pushed immediately without blocking the workflow
        _ = Task.Run(async () =>
        {
            try
            {
                await hubContext.Clients.All.SendAsync(
                    "ReceiveWorkflowProgress",
                    conversationId,
                    response
                );
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to push workflow progress via SignalR");
            }
        });

        return Task.CompletedTask;
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

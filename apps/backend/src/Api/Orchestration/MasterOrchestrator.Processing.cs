using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Api.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Master orchestrator - Request processing methods
/// </summary>
public partial class MasterOrchestrator
{
    public async Task<OrchestratorResult> ProcessRequestAsync(
        string userMessage,
        string conversationId,
        bool enableThinking = false
    )
    {
        using var activity = ActivitySource.StartActivity(
            "MasterOrchestrator.ProcessRequest",
            ActivityKind.Server
        );
        activity?.SetTag("conversation.id", conversationId);
        activity?.SetTag("message.length", userMessage.Length);
        activity?.SetTag("thinking.enabled", enableThinking);

        var sw = Stopwatch.StartNew();
        var subAgentsUsed = new List<string>();

        try
        {
            _logger.LogInformation(
                "Processing request for conversation {ConversationId}: {Message}",
                conversationId,
                userMessage
            );

            // Get or create thread for this conversation
            var thread = _threadManager.GetOrCreateThread(conversationId, _masterAgent.Value);

            string responseText;

            if (enableThinking)
            {
                // Create JSON schema from ThinkingModeResponse type
                var schema = AIJsonUtilities.CreateJsonSchema(typeof(ThinkingModeResponse));

                // Create agent with structured output using your helper method
                var thinkingAgent = CreateMasterAgentWithSchemaAndTools(schema);

                // Note: Tools need to be registered after agent creation
                // Wrap with tools and middleware
                thinkingAgent = thinkingAgent
                    .AsBuilder()
                    .Use(DelegationEventMiddleware.FunctionInvocationMiddleware)
                    .Build();

                _logger.LogInformation(
                    "Running agent with structured output (thinking mode enabled) for conversation {ConversationId}",
                    conversationId
                );

                var result = await thinkingAgent.RunAsync(userMessage, thread);

                // Deserialize the structured JSON response
                var thinkingResponse = result.Deserialize<ThinkingModeResponse>(
                    JsonSerializerOptions.Web
                );

                if (thinkingResponse != null)
                {
                    // Format response with thinking and content sections
                    var formattedResponse = new StringBuilder();
                    if (!string.IsNullOrEmpty(thinkingResponse.Thinking))
                    {
                        formattedResponse.AppendLine("## 🤔 Thinking Process\n");
                        formattedResponse.AppendLine(thinkingResponse.Thinking);
                        formattedResponse.AppendLine("\n---\n");
                    }
                    if (!string.IsNullOrEmpty(thinkingResponse.Content))
                    {
                        formattedResponse.AppendLine("## 💡 Response\n");
                        formattedResponse.AppendLine(thinkingResponse.Content);
                    }
                    responseText = formattedResponse.ToString();
                }
                else
                {
                    responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";
                }
            }
            else
            {
                // Standard mode without thinking - use thread for conversation history
                _logger.LogInformation(
                    "Running agent in standard mode for conversation {ConversationId}",
                    conversationId
                );
                var result = await _masterAgent.Value.RunAsync(userMessage, thread);
                responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";
            }

            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.length", responseText.Length);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogInformation("Request completed in {Duration}ms", sw.ElapsedMilliseconds);

            // Capture projection result if any was generated during this request
            var projectionResult = _projectionTools.GetLastProjectionResult();
            _projectionTools.ClearLastProjectionResult();

            return new OrchestratorResult
            {
                Success = true,
                Response = responseText,
                SubAgentsUsed = subAgentsUsed,
                TotalDurationMs = sw.ElapsedMilliseconds,
                ProjectionResult = projectionResult,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddTag("exception.type", ex.GetType().FullName);
            activity?.AddTag("exception.message", ex.Message);
            activity?.AddTag("exception.stacktrace", ex.StackTrace);

            _logger.LogError(
                ex,
                "Error processing request for conversation {ConversationId}: {Error}",
                conversationId,
                ex.Message
            );

            // Clear any partial projection result on error
            _projectionTools.ClearLastProjectionResult();

            return new OrchestratorResult
            {
                Success = false,
                Response = $"An error occurred: {ex.Message}",
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    public async Task<StructuredOrchestratorResult> ProcessRequestStructuredAsync(
        string userMessage,
        string conversationId
    )
    {
        return await _structuredResponseHandler.ProcessAsync(
            userMessage,
            conversationId,
            DelegateToSubAgent,
            DelegateToMultipleSubAgents
        );
    }
}

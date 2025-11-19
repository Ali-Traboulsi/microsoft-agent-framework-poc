using System.Diagnostics;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.DTOs;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.Orchestration;

/// <summary>
/// Handles structured JSON response generation for the Master Orchestrator
/// </summary>
public class StructuredResponseHandler
{
    private readonly IChatClient _chatClient;
    private readonly IEnumerable<ISubAgent> _subAgents;
    private readonly WebSearchTools _webSearchTools;
    private readonly ILogger<StructuredResponseHandler> _logger;

    public StructuredResponseHandler(
        IChatClient chatClient,
        IEnumerable<ISubAgent> subAgents,
        WebSearchTools webSearchTools,
        ILogger<StructuredResponseHandler> logger
    )
    {
        _chatClient = chatClient;
        _subAgents = subAgents;
        _webSearchTools = webSearchTools;
        _logger = logger;
    }

    /// <summary>
    /// Process request and return structured JSON response
    /// </summary>
    public async Task<StructuredOrchestratorResult> ProcessAsync(
        string userMessage,
        string conversationId,
        Func<string, string, string?, Task<string>> delegateToSubAgent,
        Func<string, Task<string>> delegateToMultipleSubAgents
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Processing structured request for conversation {ConversationId}",
                conversationId
            );

            // Create specialized structured agent
            var instructions = AgentInstructionsLoader.LoadStructuredAgentInstructions(_subAgents);

            var structuredAgent = _chatClient.CreateAIAgent(
                name: "StructuredMasterAgent",
                instructions: instructions,
                tools:
                [
                    AIFunctionFactory.Create(delegateToSubAgent),
                    AIFunctionFactory.Create(delegateToMultipleSubAgents),
                    AIFunctionFactory.Create(_webSearchTools.SearchWeb),
                ]
            );

            // Run the agent
            var result = await structuredAgent.RunAsync(userMessage);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "{}";

            // Clean and parse JSON
            var jsonResponse = CleanJsonResponse(responseText);

            _logger.LogInformation(
                "Raw JSON response (first 500 chars): {Json}",
                jsonResponse.Length > 500 ? jsonResponse.Substring(0, 500) + "..." : jsonResponse
            );

            // Fix common JSON issues
            jsonResponse = FixCommonJsonIssues(jsonResponse);

            // Parse to structured response
            var structuredResponse = JsonSerializer.Deserialize<StructuredAgentResponse>(
                jsonResponse,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = System
                        .Text
                        .Json
                        .Serialization
                        .JsonNumberHandling
                        .AllowReadingFromString,
                    DefaultIgnoreCondition = System
                        .Text
                        .Json
                        .Serialization
                        .JsonIgnoreCondition
                        .WhenWritingNull,
                }
            );

            if (structuredResponse == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize response. JSON: {jsonResponse}"
                );
            }

            sw.Stop();

            _logger.LogInformation(
                "Structured request completed in {Duration}ms",
                sw.ElapsedMilliseconds
            );

            return new StructuredOrchestratorResult
            {
                Success = true,
                StructuredResponse = structuredResponse,
                SubAgentsUsed = structuredResponse.SubAgentsUsed,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "Error processing structured request for conversation {ConversationId}",
                conversationId
            );

            return new StructuredOrchestratorResult
            {
                Success = false,
                StructuredResponse = new StructuredAgentResponse
                {
                    Summary = $"An error occurred: {ex.Message}",
                    ReferenceLinks = new List<ReferenceLink>(),
                    SubAgentsUsed = new List<string>(),
                },
                ErrorMessage = ex.Message,
                TotalDurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    /// <summary>
    /// Clean JSON response by removing markdown code blocks
    /// </summary>
    private string CleanJsonResponse(string responseText)
    {
        var jsonResponse = responseText.Trim();

        if (jsonResponse.StartsWith("```"))
        {
            var lines = jsonResponse.Split('\n');
            jsonResponse = string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
        }

        return jsonResponse.Trim();
    }

    /// <summary>
    /// Fix common JSON formatting issues
    /// </summary>
    private string FixCommonJsonIssues(string jsonResponse)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;

            // Fix webSearchesExecuted if it's a string instead of array
            if (
                root.TryGetProperty("webSearchesExecuted", out var webSearchProp)
                && webSearchProp.ValueKind == JsonValueKind.String
            )
            {
                var webSearchValue = webSearchProp.GetString();
                jsonResponse = jsonResponse.Replace(
                    $"\"webSearchesExecuted\": \"{webSearchValue}\"",
                    $"\"webSearchesExecuted\": [\"{webSearchValue}\"]"
                );
                _logger.LogWarning(
                    "Fixed webSearchesExecuted from string to array: {Value}",
                    webSearchValue
                );
            }

            // Fix subAgentsUsed if it's a string instead of array
            if (
                root.TryGetProperty("subAgentsUsed", out var subAgentsProp)
                && subAgentsProp.ValueKind == JsonValueKind.String
            )
            {
                var subAgentsValue = subAgentsProp.GetString();
                jsonResponse = jsonResponse.Replace(
                    $"\"subAgentsUsed\": \"{subAgentsValue}\"",
                    $"\"subAgentsUsed\": [\"{subAgentsValue}\"]"
                );
                _logger.LogWarning(
                    "Fixed subAgentsUsed from string to array: {Value}",
                    subAgentsValue
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pre-process JSON, will attempt direct parse");
        }

        return jsonResponse;
    }
}

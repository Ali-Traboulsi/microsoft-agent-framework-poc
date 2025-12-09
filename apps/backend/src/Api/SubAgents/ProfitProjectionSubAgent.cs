using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in profit projection calculations
/// احتساب الأرباح التقديرية
/// </summary>
public class ProfitProjectionSubAgent : ISubAgent
{
    private readonly ProjectionTools _projectionTools;
    private readonly SubAgentThreadManager _threadManager;
    private readonly ILogger<ProfitProjectionSubAgent> _logger;
    private readonly Lazy<AIAgent> _agent;

    public string Name => "ProfitProjection";
    public string Domain => "Investment Profit Projections";
    public string[] Capabilities =>
        new[]
        {
            "Calculate expected profit projections for investments",
            "Provide conservative, expected, and optimistic return scenarios",
            "Analyze fund performance and recommend suitable allocations",
            "Compare lump sum vs. monthly SIP investment strategies",
            "Calculate personalized projections for existing customers",
            "Support Shariah-compliant investment projections",
            "Provide quick estimates for investment returns",
        };

    public ProfitProjectionSubAgent(
        IChatClient chatClient,
        ProjectionTools projectionTools,
        SubAgentThreadManager threadManager,
        ILogger<ProfitProjectionSubAgent> logger
    )
    {
        _projectionTools = projectionTools;
        _threadManager = threadManager;
        _logger = logger;
        _agent = new Lazy<AIAgent>(() => CreateAgent(chatClient));
    }

    private AIAgent CreateAgent(IChatClient chatClient)
    {
        // Load instructions from external file
        var instructions = AgentInstructionsLoader.LoadProjectionSubAgentInstructions();

        return chatClient.CreateAIAgent(
            name: Name,
            instructions: instructions,
            tools:
            [
                AIFunctionFactory.Create(_projectionTools.CalculateProfitProjectionWithProgress),
                AIFunctionFactory.Create(_projectionTools.CalculateProfitProjection),
                AIFunctionFactory.Create(_projectionTools.CalculatePersonalizedProjection),
                AIFunctionFactory.Create(_projectionTools.CompareInvestmentStrategies),
                AIFunctionFactory.Create(_projectionTools.GetQuickEstimate),
            ]
        );
    }

    public async Task<SubAgentResponse> HandleRequestAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "{SubAgent} handling request for conversation {ConversationId}: {Request}",
                Name,
                conversationId,
                request
            );

            // Get shared conversation context from previous sub-agent interactions
            var conversationContext = await _threadManager.GetConversationContextAsync(
                conversationId
            );

            // Build the full request with context if available
            var fullRequest = string.IsNullOrEmpty(conversationContext)
                ? request
                : $"{conversationContext}\n\nCurrent request: {request}";

            var thread = _threadManager.GetOrCreateThread(conversationId, Name, _agent.Value);
            var result = await _agent.Value.RunAsync(fullRequest, thread);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            // Add this interaction to shared conversation memory
            _threadManager.AddMemory(conversationId, Name, request, responseText);

            sw.Stop();

            _logger.LogInformation(
                "{SubAgent} completed in {Duration}ms",
                Name,
                sw.ElapsedMilliseconds
            );

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = true,
                Result = responseText,
                ToolsUsed = new List<string>(),
                DurationMs = sw.ElapsedMilliseconds,
                Metadata = context ?? new(),
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "{SubAgent} error after {Duration}ms: {Error}",
                Name,
                sw.ElapsedMilliseconds,
                ex.Message
            );

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = false,
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds,
                ToolsUsed = new List<string>(),
                Metadata = context ?? new(),
            };
        }
    }

    public async IAsyncEnumerable<SubAgentStreamChunk> HandleRequestStreamingAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "{SubAgent} handling streaming request for conversation {ConversationId}: {Request}",
            Name,
            conversationId,
            request
        );

        // Get shared conversation context from previous sub-agent interactions
        var conversationContext = await _threadManager.GetConversationContextAsync(conversationId);

        // Build the full request with context if available
        var fullRequest = string.IsNullOrEmpty(conversationContext)
            ? request
            : $"{conversationContext}\n\nCurrent request: {request}";

        var thread = _threadManager.GetOrCreateThread(conversationId, Name, _agent.Value);
        var responseBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in _agent.Value.RunStreamingAsync(fullRequest, thread))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Text != null)
            {
                responseBuilder.Append(chunk.Text);
                yield return new SubAgentStreamChunk { Text = chunk.Text, IsComplete = false };
            }
        }

        // Add this interaction to shared conversation memory
        _threadManager.AddMemory(conversationId, Name, request, responseBuilder.ToString());

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}

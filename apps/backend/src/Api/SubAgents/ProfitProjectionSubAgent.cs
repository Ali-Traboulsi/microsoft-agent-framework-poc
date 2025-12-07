using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Orchestration;
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
    private readonly IChatClient _chatClient;
    private readonly ProjectionTools _projectionTools;
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
        ILogger<ProfitProjectionSubAgent> logger
    )
    {
        _chatClient = chatClient;
        _projectionTools = projectionTools;
        _logger = logger;
        _agent = new Lazy<AIAgent>(CreateAgent);
    }

    private AIAgent CreateAgent()
    {
        // Load instructions from external file
        var instructions = AgentInstructionsLoader.LoadProjectionSubAgentInstructions();

        return _chatClient.CreateAIAgent(
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
        Dictionary<string, object>? context = null
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("{SubAgent} handling request: {Request}", Name, request);

            var result = await _agent.Value.RunAsync(request);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

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
                ToolsUsed = new List<string>(), // Tools tracked by framework
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
        Dictionary<string, object>? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("{SubAgent} handling streaming request: {Request}", Name, request);

        await foreach (var chunk in _agent.Value.RunStreamingAsync(request))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (chunk.Text != null)
            {
                yield return new SubAgentStreamChunk { Text = chunk.Text, IsComplete = false };
            }
        }

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}

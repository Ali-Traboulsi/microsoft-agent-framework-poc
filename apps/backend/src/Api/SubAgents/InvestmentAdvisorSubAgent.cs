using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in investment advisory and recommendations
/// </summary>
public class InvestmentAdvisorSubAgent : ISubAgent
{
    private readonly MutualFundTools _fundTools;
    private readonly SubAgentThreadManager _threadManager;
    private readonly ILogger<InvestmentAdvisorSubAgent> _logger;
    private readonly Lazy<AIAgent> _agent;

    public string Name => "InvestmentAdvisor";
    public string Domain => "Investment Advisory";
    public string[] Capabilities =>
        new[]
        {
            "Recommend suitable mutual funds based on goals and risk tolerance",
            "Provide detailed fund analysis and comparisons",
            "Explain investment strategies and principles",
            "Analyze fund performance and characteristics",
            "Offer diversification recommendations",
            "Research and compare investment options",
        };

    public InvestmentAdvisorSubAgent(
        IChatClient chatClient,
        MutualFundTools fundTools,
        SubAgentThreadManager threadManager,
        ILogger<InvestmentAdvisorSubAgent> logger
    )
    {
        _fundTools = fundTools;
        _threadManager = threadManager;
        _logger = logger;
        _agent = new Lazy<AIAgent>(() => CreateAgent(chatClient));
    }

    private AIAgent CreateAgent(IChatClient chatClient)
    {
        return chatClient.CreateAIAgent(
            name: Name,
            instructions: $@"You are a knowledgeable Investment Advisor expert.

**Your Domain:** {Domain}

**Your Capabilities:**
{string.Join("\n", Capabilities.Select(c => $"- {c}"))}

**Important Guidelines:**
1. Focus on investment recommendations and fund analysis
2. Consider client risk tolerance and investment goals
3. Provide data-driven recommendations with clear reasoning
4. Explain investment concepts in accessible language
5. Compare multiple options when relevant
6. Highlight both opportunities and risks
7. If request is outside your domain, indicate that clearly

**Response Format:**
- Begin with a brief recommendation summary
- Support with fund analysis and comparisons
- Include key metrics (returns, risk levels, fees)
- End with actionable investment suggestions",
            tools:
            [
                AIFunctionFactory.Create(_fundTools.SearchFunds),
                AIFunctionFactory.Create(_fundTools.GetFundDetails),
                AIFunctionFactory.Create(_fundTools.ListAllFunds),
                AIFunctionFactory.Create(_fundTools.CompareFunds),
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

        _logger.LogInformation("{SubAgent} streaming completed", Name);

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}

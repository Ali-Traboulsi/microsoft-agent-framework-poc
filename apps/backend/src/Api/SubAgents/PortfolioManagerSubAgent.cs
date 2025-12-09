using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in portfolio management operations
/// </summary>
public class PortfolioManagerSubAgent(
    IChatClient chatClient,
    PortfolioTools portfolioTools,
    AccountTools accountTools,
    SubAgentThreadManager threadManager,
    ILogger<PortfolioManagerSubAgent> logger
) : ISubAgent
{
    // OpenTelemetry observability
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.SubAgents.PortfolioManager",
        "2.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.SubAgents.PortfolioManager",
        "2.0.0"
    );
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "subagent.requests",
        description: "Number of requests handled"
    );
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "subagent.request.duration",
        unit: "ms",
        description: "Duration of request handling"
    );

    private readonly IChatClient _chatClient = chatClient;
    private readonly PortfolioTools _portfolioTools = portfolioTools;
    private readonly AccountTools _accountTools = accountTools;
    private readonly SubAgentThreadManager _threadManager = threadManager;
    private readonly ILogger<PortfolioManagerSubAgent> _logger = logger;
    private readonly Lazy<AIAgent> _agent = new(
        CreateAgentFunc(chatClient, portfolioTools, accountTools)
    );

    public string Name => "PortfolioManager";
    public string Domain => "Portfolio Management";
    public string[] Capabilities =>
        [
            "Create and manage investment portfolios",
            "Analyze portfolio performance and returns",
            "Get portfolio allocation and composition details",
            "Execute fund purchases for portfolios",
            "Provide portfolio rebalancing recommendations",
            "Track portfolio values and changes over time",
        ];

    private static Func<AIAgent> CreateAgentFunc(
        IChatClient chatClient,
        PortfolioTools portfolioTools,
        AccountTools accountTools
    ) => () => CreateAgent(chatClient, portfolioTools, accountTools);

    private static AIAgent CreateAgent(
        IChatClient chatClient,
        PortfolioTools portfolioTools,
        AccountTools accountTools
    )
    {
        return chatClient.CreateAIAgent(
            name: "PortfolioManager",
            instructions: $@"You are a specialized Portfolio Manager expert.

**Your Domain:** Portfolio Management

**Your Capabilities:**
- Create and manage investment portfolios
- Analyze portfolio performance and returns
- Get portfolio allocation and composition details
- Execute fund purchases for portfolios
- Provide portfolio rebalancing recommendations
- Track portfolio values and changes over time

**Important Guidelines:**
1. Focus ONLY on portfolio-related requests
2. Use your tools to fulfill requests accurately
3. Provide clear, actionable recommendations
4. Include relevant metrics and data in your responses
5. Be professional and concise
6. If a request is outside your domain, clearly state that another specialist should handle it

**Response Format:**
- Start with a brief summary
- Present data clearly (use tables/lists when appropriate)
- End with actionable recommendations or next steps",
            tools:
            [
                AIFunctionFactory.Create(portfolioTools.CreatePortfolio),
                AIFunctionFactory.Create(portfolioTools.GetPortfolioDetails),
                AIFunctionFactory.Create(portfolioTools.ListPortfolios),
                AIFunctionFactory.Create(portfolioTools.GetPortfolioAllocation),
                AIFunctionFactory.Create(accountTools.FundPortfolio),
                AIFunctionFactory.Create(accountTools.GetAccountBalance),
            ]
        );
    }

    public async Task<SubAgentResponse> HandleRequestAsync(
        string request,
        string conversationId,
        Dictionary<string, object>? context = null
    )
    {
        using var activity = ActivitySource.StartActivity(
            "PortfolioManagerSubAgent.HandleRequest",
            ActivityKind.Internal
        );
        activity?.SetTag("subagent.name", Name);
        activity?.SetTag("request.length", request.Length);

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

            // Record metrics
            RequestCounter.Add(
                1,
                new KeyValuePair<string, object?>("subagent", Name),
                new KeyValuePair<string, object?>("success", true)
            );
            RequestDuration.Record(
                sw.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("subagent", Name)
            );

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("response.length", responseText.Length);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

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

            // Record error metrics
            RequestCounter.Add(
                1,
                new KeyValuePair<string, object?>("subagent", Name),
                new KeyValuePair<string, object?>("success", false)
            );

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);

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

using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in account services and management
/// </summary>
public class AccountServicesSubAgent : ISubAgent
{
    private readonly IChatClient _chatClient;
    private readonly AccountTools _accountTools;
    private readonly ILogger<AccountServicesSubAgent> _logger;
    private readonly Lazy<AIAgent> _agent;

    public string Name => "AccountServices";
    public string Domain => "Account Management";
    public string[] Capabilities =>
        new[]
        {
            "Provide account balance and status information",
            "Process deposits and fund transfers",
            "Show transaction history and details",
            "Create new investment accounts",
            "Update account information",
            "Manage account funding operations",
        };

    public AccountServicesSubAgent(
        IChatClient chatClient,
        AccountTools accountTools,
        ILogger<AccountServicesSubAgent> logger
    )
    {
        _chatClient = chatClient;
        _accountTools = accountTools;
        _logger = logger;
        _agent = new Lazy<AIAgent>(CreateAgent);
    }

    private AIAgent CreateAgent()
    {
        return _chatClient.CreateAIAgent(
            name: Name,
            instructions: $@"You are a helpful Account Services specialist.

**Your Domain:** {Domain}

**Your Capabilities:**
{string.Join("\n", Capabilities.Select(c => $"- {c}"))}

**Important Guidelines:**
1. Focus on account-related operations and inquiries
2. Verify account details before processing transactions
3. Provide clear transaction confirmations
4. Present account information in an organized format
5. Be concise and accurate
6. For requests outside your domain, indicate clearly

**Response Format:**
- Confirm account details when relevant
- Present transaction information clearly
- Include confirmation numbers or IDs
- Summarize account status when appropriate",
            tools:
            [
                AIFunctionFactory.Create(_accountTools.GetAccountBalance),
                AIFunctionFactory.Create(_accountTools.GetTransactionHistory),
                AIFunctionFactory.Create(_accountTools.DepositFunds),
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

        _logger.LogInformation("{SubAgent} streaming completed", Name);

        yield return new SubAgentStreamChunk { IsComplete = true };
    }
}

using System.Diagnostics;
using System.Runtime.CompilerServices;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Orchestration;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Api.SubAgents;

/// <summary>
/// Sub-agent specialized in external API communications.
/// Handles SNB Capital, Fund-In operations, and other external service integrations.
/// خدمات API الخارجية - SNB كابيتال والخدمات المالية
/// </summary>
public class ExternalApiSubAgent(
    IChatClient chatClient,
    SNBCapitalTools snbCapitalTools,
    FundInTools fundInTools,
    FundInWorkflowTools fundInWorkflowTools,
    ILogger<ExternalApiSubAgent> logger
) : ISubAgent
{
    private readonly Lazy<AIAgent> _agent = new(() =>
        CreateAgentInternal(chatClient, snbCapitalTools, fundInTools, fundInWorkflowTools)
    );

    public string Name => "ExternalApiServices";

    public string Domain => "External API Integration & Financial Services";

    public string[] Capabilities =>
        [
            // SNB Capital - Mutual Funds & Portfolios
            "Retrieve available mutual funds from SNB Capital",
            "Get customer portfolio information and holdings",
            "Search and filter mutual funds by criteria",
            "Get detailed mutual fund information including NAV, returns, and fees",
            "Retrieve local market stock holdings",
            // Fund-In Workflow (single-step, no OTP, uses step-up tokens)
            "Execute complete Fund-In workflow automatically (no OTP needed)",
            "Transfer money from bank account to investment portfolio",
            "Get customer bank accounts for fund transfers",
            "Get portfolios available for fund-in operations",
            // Arabic Support
            "Support Arabic language interactions for financial services",
            "Provide bilingual (Arabic/English) responses",
        ];

    private static AIAgent CreateAgentInternal(
        IChatClient chatClient,
        SNBCapitalTools snbCapitalTools,
        FundInTools fundInTools,
        FundInWorkflowTools fundInWorkflowTools
    )
    {
        var instructions = AgentInstructionsLoader.LoadExternalApiSubAgentInstructions();

        return chatClient.CreateAIAgent(
            name: "ExternalApiServices",
            instructions: instructions,
            tools:
            [
                // SNB Capital - Mutual Funds & Portfolios
                AIFunctionFactory.Create(snbCapitalTools.GetMutualFunds),
                AIFunctionFactory.Create(snbCapitalTools.GetCustomerPortfolios),
                AIFunctionFactory.Create(snbCapitalTools.GetPortfolioHoldings),
                AIFunctionFactory.Create(snbCapitalTools.GetCompleteCustomerData),
                AIFunctionFactory.Create(snbCapitalTools.SearchMutualFunds),
                AIFunctionFactory.Create(snbCapitalTools.GetMutualFundDetails),
                // Fund-In - Account Discovery (needed before workflow)
                AIFunctionFactory.Create(fundInTools.GetCustomerAccounts),
                AIFunctionFactory.Create(fundInTools.GetAccountPortfolios),
                // Fund-In Workflow (high-level orchestration)
                // Single execute method - no OTP needed, uses step-up token
                AIFunctionFactory.Create(fundInWorkflowTools.ExecuteFundInWorkflow),
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
            logger.LogInformation("{SubAgent} handling request: {Request}", Name, request);

            var result = await _agent.Value.RunAsync(request);
            var responseText = result.Messages.LastOrDefault()?.Text ?? "No response generated";

            sw.Stop();

            logger.LogInformation(
                "{SubAgent} completed in {Duration}ms",
                Name,
                sw.ElapsedMilliseconds
            );

            return new SubAgentResponse
            {
                SubAgentName = Name,
                Success = true,
                Result = responseText,
                ToolsUsed = [],
                DurationMs = sw.ElapsedMilliseconds,
                Metadata = context ?? new(),
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            logger.LogError(
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
                ToolsUsed = [],
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
        logger.LogInformation("{SubAgent} handling streaming request: {Request}", Name, request);

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

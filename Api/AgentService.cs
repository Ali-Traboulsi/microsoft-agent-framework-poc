using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentFrameworkQuickStart.Api;

public class AgentService
{
    private readonly InvestmentDataStore _dataStore;
    private readonly AccountTools _accountTools;
    private readonly PortfolioTools _portfolioTools;
    private readonly MutualFundTools _fundTools;
    private readonly IChatClient _chatClient;

    private AIAgent? _portfolioAgent;
    private AIAgent? _advisorAgent;
    private AIAgent? _accountAgent;
    private AIAgent? _complianceAgent;

    public AgentService(
        InvestmentDataStore dataStore,
        AccountTools accountTools,
        PortfolioTools portfolioTools,
        MutualFundTools fundTools,
        string apiKey
    )
    {
        _dataStore = dataStore;
        _accountTools = accountTools;
        _portfolioTools = portfolioTools;
        _fundTools = fundTools;
        _chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini").AsIChatClient();
    }

    public AIAgent GetPortfolioAgent()
    {
        return _portfolioAgent ??= _chatClient.CreateAIAgent(
            name: "PortfolioManager",
            instructions: @"You are an expert Portfolio Manager. Your responsibilities:
                - Help clients create and manage investment portfolios
                - Provide portfolio analysis and allocation insights
                - Execute fund purchases and portfolio rebalancing
                - Track portfolio performance
                
                Always be professional, clear, and provide actionable recommendations.
                Keep responses concise but informative.",
            tools:
            [
                AIFunctionFactory.Create(_portfolioTools.CreatePortfolio),
                AIFunctionFactory.Create(_portfolioTools.GetPortfolioDetails),
                AIFunctionFactory.Create(_portfolioTools.ListPortfolios),
                AIFunctionFactory.Create(_portfolioTools.GetPortfolioAllocation),
                AIFunctionFactory.Create(_accountTools.FundPortfolio),
                AIFunctionFactory.Create(_accountTools.GetAccountBalance),
            ]
        );
    }

    public AIAgent GetAdvisorAgent()
    {
        return _advisorAgent ??= _chatClient.CreateAIAgent(
            name: "InvestmentAdvisor",
            instructions: @"You are a knowledgeable Investment Advisor. Your expertise:
                - Recommend suitable mutual funds based on client goals and risk tolerance
                - Provide detailed fund analysis and comparisons
                - Explain investment strategies and diversification principles
                
                Keep recommendations clear and actionable.",
            tools:
            [
                AIFunctionFactory.Create(_fundTools.SearchFunds),
                AIFunctionFactory.Create(_fundTools.GetFundDetails),
                AIFunctionFactory.Create(_fundTools.ListAllFunds),
                AIFunctionFactory.Create(_fundTools.CompareFunds),
            ]
        );
    }

    public AIAgent GetAccountAgent()
    {
        return _accountAgent ??= _chatClient.CreateAIAgent(
            name: "AccountServices",
            instructions: @"You are a helpful Account Services specialist. Your role:
                - Provide account balance and status information
                - Process deposits and fund transfers
                - Show transaction history
                
                Be concise and verify account details.",
            tools:
            [
                AIFunctionFactory.Create(_accountTools.GetAccountBalance),
                AIFunctionFactory.Create(_accountTools.GetTransactionHistory),
                AIFunctionFactory.Create(_accountTools.DepositFunds),
            ]
        );
    }

    public AIAgent GetComplianceAgent()
    {
        return _complianceAgent ??= _chatClient.CreateAIAgent(
            name: "ComplianceOfficer",
            instructions: @"You are a Compliance Officer ensuring regulatory requirements. Your duties:
                - Verify transactions comply with investment limits
                - Check risk appropriateness for client profiles
                - Ensure proper documentation
                
                Be thorough but not obstructive.",
            tools:
            [
                AIFunctionFactory.Create(_accountTools.GetAccountBalance),
                AIFunctionFactory.Create(_portfolioTools.GetPortfolioDetails),
                AIFunctionFactory.Create(_fundTools.GetFundDetails),
            ]
        );
    }

    public AIAgent GetAgentByName(string agentName)
    {
        return agentName.ToLowerInvariant() switch
        {
            "portfolio" or "portfoliomanager" => GetPortfolioAgent(),
            "advisor" or "investmentadvisor" => GetAdvisorAgent(),
            "account" or "accountservices" => GetAccountAgent(),
            "compliance" or "complianceofficer" => GetComplianceAgent(),
            _ => GetAdvisorAgent(), // Default
        };
    }
}

using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentFrameworkQuickStart;

/// <summary>
/// Console demo application showcasing the Agent Framework capabilities.
/// To run this demo instead of the API, change the project SDK to Microsoft.NET.Sdk
/// and update the Main method call below.
/// </summary>
public class ConsoleDemo
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("   🏦 Banking Investment Agent Framework Demo");
        Console.WriteLine("   Powered by Microsoft Agent Framework");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            // ===== Setup: Initialize Data Store and Tools =====
            var dataStore = new InvestmentDataStore();
            var accountTools = new AccountTools(dataStore);
            var portfolioTools = new PortfolioTools(dataStore);
            var fundTools = new MutualFundTools(dataStore);

            // ===== Setup: Configure OpenAI Client =====
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ ERROR: OPENAI_API_KEY environment variable not set!");
                Console.WriteLine("Please set the environment variable before running the demo.");
                return;
            }

            var chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini");

            Console.WriteLine("✓ Investment data store initialized");
            Console.WriteLine("✓ Agent tools configured");
            Console.WriteLine("✓ OpenAI client ready\n");

            // ===== Create Specialized Agents =====
            Console.WriteLine("🤖 Creating specialized agents...\n");

            // 1. Portfolio Manager Agent - Handles portfolio operations
            var portfolioAgent = chatClient.CreateAIAgent(
                name: "PortfolioManager",
                instructions: @"You are an expert Portfolio Manager. Your responsibilities:
            - Help clients create and manage investment portfolios
            - Provide portfolio analysis and allocation insights
            - Execute fund purchases and portfolio rebalancing
            - Track portfolio performance
            
            Always be professional, clear, and provide actionable recommendations.
            When showing numbers, format them nicely with currency symbols and percentages.",
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
            Console.WriteLine("  ✓ Portfolio Manager Agent created");

            // 2. Investment Advisor Agent - Provides fund recommendations
            var advisorAgent = chatClient.CreateAIAgent(
                name: "InvestmentAdvisor",
                instructions: @"You are a knowledgeable Investment Advisor. Your expertise:
            - Recommend suitable mutual funds based on client goals and risk tolerance
            - Provide detailed fund analysis and comparisons
            - Explain investment strategies and diversification principles
            - Help clients understand fund performance metrics
            
            Always consider the client's risk profile and investment horizon.
            Educate clients about investment concepts in simple terms.",
                tools:
                [
                    AIFunctionFactory.Create(fundTools.SearchFunds),
                    AIFunctionFactory.Create(fundTools.GetFundDetails),
                    AIFunctionFactory.Create(fundTools.ListAllFunds),
                    AIFunctionFactory.Create(fundTools.CompareFunds),
                ]
            );
            Console.WriteLine("  ✓ Investment Advisor Agent created");

            // 3. Account Services Agent - Handles account operations
            var accountAgent = chatClient.CreateAIAgent(
                name: "AccountServices",
                instructions: @"You are a helpful Account Services specialist. Your role:
            - Provide account balance and status information
            - Process deposits and fund transfers
            - Show transaction history
            - Answer account-related questions
            
            Always verify account details and confirm transactions clearly.
            Be helpful and ensure clients understand their account status.",
                tools:
                [
                    AIFunctionFactory.Create(accountTools.GetAccountBalance),
                    AIFunctionFactory.Create(accountTools.GetTransactionHistory),
                    AIFunctionFactory.Create(accountTools.DepositFunds),
                ]
            );
            Console.WriteLine("  ✓ Account Services Agent created");

            // 4. Compliance Agent - Ensures regulatory compliance
            var complianceAgent = chatClient.CreateAIAgent(
                name: "ComplianceOfficer",
                instructions: @"You are a Compliance Officer ensuring all transactions meet regulatory requirements. Your duties:
            - Verify transactions comply with investment limits and regulations
            - Check risk appropriateness for client profiles
            - Flag any suspicious or non-compliant activities
            - Ensure proper documentation
            
            Be thorough but not obstructive. Explain compliance requirements clearly.
            Approve compliant transactions promptly.",
                tools:
                [
                    AIFunctionFactory.Create(accountTools.GetAccountBalance),
                    AIFunctionFactory.Create(portfolioTools.GetPortfolioDetails),
                    AIFunctionFactory.Create(fundTools.GetFundDetails),
                ]
            );
            Console.WriteLine("  ✓ Compliance Officer Agent created\n");

            // ===== Build Multi-Agent Workflow =====
            Console.WriteLine("🔄 Building multi-agent investment workflow...\n");

            // Sequential workflow: Advisor → Compliance → Portfolio Manager
            var investmentWorkflow = new WorkflowBuilder(advisorAgent)
                .AddEdge(advisorAgent, complianceAgent)
                .AddEdge(complianceAgent, portfolioAgent)
                .Build();

            Console.WriteLine(
                "  ✓ Investment workflow created (Advisor → Compliance → Portfolio Manager)\n"
            );

            // ===== Demo Scenarios =====
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("   📊 Running Demo Scenarios");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Scenario 1: Check Account Balance
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 1: Account Information Query");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            var accountResult = await accountAgent.RunAsync(
                "Show me the balance and details for account ACC001"
            );
            Console.WriteLine($"\n{accountResult.Text}\n");

            // Scenario 2: Search for Funds Based on Criteria
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 2: Investment Fund Research");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            var fundSearchResult = await advisorAgent.RunAsync(
                "I'm looking for low-risk funds suitable for a conservative investor. Show me the available options and compare them."
            );
            Console.WriteLine($"\n{fundSearchResult.Text}\n");

            // Scenario 3: Create Portfolio
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 3: Portfolio Creation");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            var createPortfolioResult = await portfolioAgent.RunAsync(
                "Create a new portfolio named 'Retirement Savings' for account ACC001 with a Conservative strategy"
            );
            Console.WriteLine($"\n{createPortfolioResult.Text}\n");

            // Get the created portfolio ID (simplified for demo - in production you'd parse the response)
            var portfolios = dataStore.GetPortfoliosByAccount("ACC001").ToList();
            var retirementPortfolio = portfolios.FirstOrDefault(p =>
                p.PortfolioName == "Retirement Savings"
            );

            if (retirementPortfolio != null)
            {
                // Scenario 4: Investment with Compliance Check (Workflow)
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine("Scenario 4: Complete Investment Process (Multi-Agent Workflow)");
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine("Running workflow: Advisor → Compliance → Portfolio Manager\n");

                var workflowInput =
                    $@"Client wants to invest $5000 from account ACC001 into their portfolio {retirementPortfolio.PortfolioId}. 
            They prefer low-risk bonds. Please recommend a suitable fund, verify compliance, and execute the investment.";

                await using var workflowRun = await InProcessExecution.StreamAsync(
                    investmentWorkflow,
                    new ChatMessage(ChatRole.User, workflowInput)
                );
                await workflowRun.TrySendMessageAsync(new TurnToken(emitEvents: true));

                Console.WriteLine("Workflow execution:\n");
                await foreach (var evt in workflowRun.WatchStreamAsync())
                {
                    if (
                        evt is WorkflowOutputEvent outputEvent
                        && outputEvent.Data is AgentRunResponse response
                    )
                    {
                        foreach (var message in response.Messages)
                        {
                            if (
                                message.Role == ChatRole.Assistant
                                && !string.IsNullOrWhiteSpace(message.Text)
                            )
                            {
                                Console.WriteLine($"  Agent: {message.Text}\n");
                            }
                        }
                    }
                }
            }

            // Scenario 5: Fund a Portfolio Directly
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 5: Direct Fund Investment");
            Console.WriteLine("─────────────────────────────────────────────────────────");

            if (retirementPortfolio != null)
            {
                var investmentResult = await portfolioAgent.RunAsync(
                    $"Invest $3000 from account ACC001 into portfolio {retirementPortfolio.PortfolioId} by purchasing the S&P 500 Index Fund (SP500)"
                );
                Console.WriteLine($"\n{investmentResult.Text}\n");

                // Show updated portfolio
                var portfolioDetailsResult = await portfolioAgent.RunAsync(
                    $"Show me the complete details and allocation for portfolio {retirementPortfolio.PortfolioId}"
                );
                Console.WriteLine($"\n{portfolioDetailsResult.Text}\n");
            }

            // Scenario 6: Fund Comparison and Analysis
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 6: Advanced Fund Analysis");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            var comparisonResult = await advisorAgent.RunAsync(
                "Compare the Technology Growth Fund (GTGF), S&P 500 Index Fund (SP500), and Healthcare Sector Fund (HLTHF). "
                    + "Which one offers the best balance of returns and fees for a moderate risk investor?"
            );
            Console.WriteLine($"\n{comparisonResult.Text}\n");

            // Scenario 7: Transaction History
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 7: Transaction History Review");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            var historyResult = await accountAgent.RunAsync(
                "Show me the recent transaction history for account ACC001"
            );
            Console.WriteLine($"\n{historyResult.Text}\n");

            // Scenario 8: Concurrent Agent Pattern - Multiple Agents Working Together
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 8: Concurrent Analysis (Multiple Agents)");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Running concurrent analysis by multiple agents...\n");

            // Build concurrent workflow for portfolio review
            var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(
                [advisorAgent, portfolioAgent, complianceAgent]
            );

            if (retirementPortfolio != null)
            {
                var reviewInput =
                    $"Review portfolio {retirementPortfolio.PortfolioId}. Advisor: analyze fund selection. "
                    + "Portfolio Manager: check allocation. Compliance: verify regulatory compliance.";

                await using var concurrentRun = await InProcessExecution.StreamAsync(
                    concurrentWorkflow,
                    new ChatMessage(ChatRole.User, reviewInput)
                );
                await concurrentRun.TrySendMessageAsync(new TurnToken(emitEvents: true));

                await foreach (var evt in concurrentRun.WatchStreamAsync())
                {
                    if (
                        evt is WorkflowOutputEvent outputEvent
                        && outputEvent.Data is AgentRunResponse response
                    )
                    {
                        foreach (var message in response.Messages)
                        {
                            if (
                                message.Role == ChatRole.Assistant
                                && !string.IsNullOrWhiteSpace(message.Text)
                            )
                            {
                                Console.WriteLine($"  {message.Text}\n");
                            }
                        }
                    }
                }
            }

            // Scenario 9: Portfolio Rebalancing Recommendation
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("Scenario 9: Portfolio Rebalancing Advice");
            Console.WriteLine("─────────────────────────────────────────────────────────");

            if (retirementPortfolio != null)
            {
                var rebalanceResult = await advisorAgent.RunAsync(
                    $@"Look at the current portfolio {retirementPortfolio.PortfolioId}. For a conservative retirement portfolio, 
            recommend a proper asset allocation between stocks, bonds, and other categories. 
            Suggest specific funds if rebalancing is needed."
                );
                Console.WriteLine($"\n{rebalanceResult.Text}\n");
            }

            // Final Summary
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("   ✅ Demo Complete!");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");
            Console.WriteLine("Key Features Demonstrated:");
            Console.WriteLine("  ✓ Multi-agent system with specialized roles");
            Console.WriteLine("  ✓ Function tools for banking operations");
            Console.WriteLine("  ✓ Sequential workflows (Advisor → Compliance → Manager)");
            Console.WriteLine("  ✓ Concurrent workflows (parallel agent execution)");
            Console.WriteLine("  ✓ Portfolio management and fund investment");
            Console.WriteLine("  ✓ Real-time transaction processing");
            Console.WriteLine("  ✓ Compliance checking and validation");
            Console.WriteLine("  ✓ Fund research and comparison\n");

            Console.WriteLine(
                "\n💡 TIP: You can now interact with agents individually or through workflows!"
            );
            Console.WriteLine(
                "💡 Check the code to see how tools, agents, and workflows are composed.\n"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERROR: {ex.Message}\n");

            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                Console.WriteLine("🔑 API Key Error:");
                Console.WriteLine("   - Check your API key is correct");
                Console.WriteLine("   - Get a new key from: https://platform.openai.com/api-keys");
                Console.WriteLine("   - Ensure your OpenAI account has credits");
            }
            else if (ex.Message.Contains("429") || ex.Message.Contains("rate_limit"))
            {
                Console.WriteLine("⚠️  Rate Limit Error:");
                Console.WriteLine("   - You've hit the rate limit");
                Console.WriteLine("   - Wait a few minutes and try again");
                Console.WriteLine("   - Consider upgrading your OpenAI plan");
            }
            else
            {
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}

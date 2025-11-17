using System.Diagnostics;
using System.Text;
using AgentFrameworkQuickStart.Api.Abstractions;

namespace AgentFrameworkQuickStart.Api.Workflows;

/// <summary>
/// Workflow for complete investment strategy execution
/// Coordinates Account Services, Compliance, Investment Advisor, and Portfolio Manager
/// </summary>
public class CompleteInvestmentWorkflow : IWorkflow
{
    private readonly IEnumerable<ISubAgent> _subAgents;
    private readonly ILogger<CompleteInvestmentWorkflow> _logger;
    private readonly Dictionary<string, ISubAgent> _subAgentLookup;

    public string Name => "CompleteInvestmentStrategy";
    public string Description =>
        "End-to-end investment workflow: account verification → compliance → recommendations → portfolio creation";

    public CompleteInvestmentWorkflow(
        IEnumerable<ISubAgent> subAgents,
        ILogger<CompleteInvestmentWorkflow> logger
    )
    {
        _subAgents = subAgents;
        _logger = logger;
        _subAgentLookup = subAgents.ToDictionary(sa => sa.Name, sa => sa);
    }

    public async Task<WorkflowResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var totalSw = Stopwatch.StartNew();
        var steps = new List<WorkflowStep>();
        var summary = new StringBuilder();

        _logger.LogInformation(
            "Starting {Workflow} with parameters: {Parameters}",
            Name,
            string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"))
        );

        try
        {
            // Extract parameters
            var accountId =
                parameters.GetValueOrDefault("accountId")?.ToString()
                ?? throw new ArgumentException("accountId is required");
            var amount = Convert.ToDecimal(
                parameters.GetValueOrDefault("amount")
                    ?? throw new ArgumentException("amount is required")
            );
            var riskProfile =
                parameters.GetValueOrDefault("riskProfile")?.ToString()
                ?? throw new ArgumentException("riskProfile is required");

            summary.AppendLine($"# {Name} Workflow");
            summary.AppendLine(
                $"**Account:** {accountId} | **Amount:** ${amount:N2} | **Risk Profile:** {riskProfile}\n"
            );
            summary.AppendLine("---\n");

            // Step 1: Account Verification
            var accountStep = await ExecuteStep(
                order: 1,
                name: "Account Verification",
                subAgentName: "AccountServices",
                request: $"Verify account {accountId} exists and has sufficient balance for a ${amount:N2} investment. Provide account status and current balance."
            );
            steps.Add(accountStep);
            summary.AppendLine($"## Step 1: {accountStep.Name}");
            summary.AppendLine(
                accountStep.Success ? "✅ **Status:** Verified" : "❌ **Status:** Failed"
            );
            summary.AppendLine(accountStep.Result);
            summary.AppendLine();

            if (!accountStep.Success)
            {
                return CreateResult(
                    false,
                    summary.ToString(),
                    steps,
                    totalSw,
                    "Account verification failed"
                );
            }

            // Step 2: Compliance Review
            var complianceStep = await ExecuteStep(
                order: 2,
                name: "Compliance Review",
                subAgentName: "ComplianceOfficer",
                request: $"Review proposed ${amount:N2} investment for account {accountId} with {riskProfile} risk profile. Check regulatory compliance and risk appropriateness."
            );
            steps.Add(complianceStep);
            summary.AppendLine($"## Step 2: {complianceStep.Name}");
            summary.AppendLine(
                complianceStep.Success ? "✅ **Status:** Approved" : "⚠️ **Status:** Review Needed"
            );
            summary.AppendLine(complianceStep.Result);
            summary.AppendLine();

            // Step 3: Investment Recommendations
            var advisorStep = await ExecuteStep(
                order: 3,
                name: "Investment Recommendations",
                subAgentName: "InvestmentAdvisor",
                request: $"Recommend suitable mutual funds for a ${amount:N2} investment with {riskProfile} risk profile. Provide fund names, allocation percentages, and rationale."
            );
            steps.Add(advisorStep);
            summary.AppendLine($"## Step 3: {advisorStep.Name}");
            summary.AppendLine(
                advisorStep.Success ? "✅ **Status:** Complete" : "❌ **Status:** Failed"
            );
            summary.AppendLine(advisorStep.Result);
            summary.AppendLine();

            if (!advisorStep.Success)
            {
                return CreateResult(
                    false,
                    summary.ToString(),
                    steps,
                    totalSw,
                    "Investment recommendations failed"
                );
            }

            // Step 4: Portfolio Construction
            var portfolioStep = await ExecuteStep(
                order: 4,
                name: "Portfolio Construction",
                subAgentName: "PortfolioManager",
                request: $"Create a new portfolio for account {accountId} with initial funding of ${amount:N2}. Use these recommendations: {advisorStep.Result}"
            );
            steps.Add(portfolioStep);
            summary.AppendLine($"## Step 4: {portfolioStep.Name}");
            summary.AppendLine(
                portfolioStep.Success ? "✅ **Status:** Created" : "❌ **Status:** Failed"
            );
            summary.AppendLine(portfolioStep.Result);
            summary.AppendLine();

            if (!portfolioStep.Success)
            {
                return CreateResult(
                    false,
                    summary.ToString(),
                    steps,
                    totalSw,
                    "Portfolio creation failed"
                );
            }

            // Success summary
            totalSw.Stop();
            summary.AppendLine("---\n");
            summary.AppendLine("## Workflow Summary");
            summary.AppendLine($"✅ **All steps completed successfully**");
            summary.AppendLine($"⏱️ **Total Duration:** {totalSw.ElapsedMilliseconds}ms");
            summary.AppendLine($"📊 **Steps Executed:** {steps.Count}");

            _logger.LogInformation(
                "{Workflow} completed successfully in {Duration}ms",
                Name,
                totalSw.ElapsedMilliseconds
            );

            return CreateResult(true, summary.ToString(), steps, totalSw, null);
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            _logger.LogError(ex, "{Workflow} failed: {Error}", Name, ex.Message);

            summary.AppendLine($"\n❌ **Workflow Failed:** {ex.Message}");
            return CreateResult(false, summary.ToString(), steps, totalSw, ex.Message);
        }
    }

    private async Task<WorkflowStep> ExecuteStep(
        int order,
        string name,
        string subAgentName,
        string request
    )
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (!_subAgentLookup.TryGetValue(subAgentName, out var subAgent))
            {
                throw new InvalidOperationException($"Sub-agent {subAgentName} not found");
            }

            _logger.LogInformation(
                "Executing workflow step {Order}: {Name} via {SubAgent}",
                order,
                name,
                subAgentName
            );

            var response = await subAgent.HandleRequestAsync(request);
            sw.Stop();

            return new WorkflowStep
            {
                Order = order,
                Name = name,
                SubAgentName = subAgentName,
                Success = response.Success,
                Result = response.Result,
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Workflow step {Order}: {Name} failed: {Error}",
                order,
                name,
                ex.Message
            );

            return new WorkflowStep
            {
                Order = order,
                Name = name,
                SubAgentName = subAgentName,
                Success = false,
                Result = $"Error: {ex.Message}",
                DurationMs = sw.ElapsedMilliseconds,
            };
        }
    }

    private WorkflowResult CreateResult(
        bool success,
        string summary,
        List<WorkflowStep> steps,
        Stopwatch sw,
        string? errorMessage
    )
    {
        return new WorkflowResult
        {
            Success = success,
            Summary = summary,
            Steps = steps,
            TotalDurationMs = sw.ElapsedMilliseconds,
            ErrorMessage = errorMessage,
        };
    }
}

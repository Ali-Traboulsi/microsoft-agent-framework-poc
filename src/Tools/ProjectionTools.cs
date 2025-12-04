using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for profit projection calculations that agents can invoke
/// احتساب الأرباح التقديرية
/// </summary>
public class ProjectionTools
{
    private readonly ProfitProjectionWorkflow _workflow;
    private readonly ILogger<ProjectionTools> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Tools.Projection",
        "1.0.0"
    );
    private static readonly Meter Meter = new("InvestmentBanking.Tools.Projection", "1.0.0");
    private static readonly Counter<int> ToolInvocationsCounter = Meter.CreateCounter<int>(
        "tool_invocations",
        "invocations",
        "Number of tool invocations"
    );

    public ProjectionTools(ProfitProjectionWorkflow workflow, ILogger<ProjectionTools> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    /// <summary>
    /// Calculate estimated profit projection for an investment
    /// احتساب الأرباح التقديرية للاستثمار
    /// </summary>
    /// <param name="investmentAmount">The amount to invest (e.g., 100000)</param>
    /// <param name="timeHorizonMonths">Investment time horizon in months (e.g., 36 for 3 years)</param>
    /// <param name="riskProfile">Risk profile: Conservative, Moderate, or Aggressive</param>
    /// <param name="currency">Currency code (default: SAR)</param>
    /// <param name="shariahCompliant">Whether to only include Sharia-compliant funds</param>
    /// <returns>Detailed projection with three scenarios</returns>
    [Description(
        "Calculate estimated profit projection for an investment. Shows conservative, expected, and optimistic scenarios with recommended fund allocations. Use this when a customer asks about potential returns or wants to know how much they could earn from investing."
    )]
    public async Task<string> CalculateProfitProjection(
        [Description("Investment amount in the specified currency (minimum 1000)")]
            decimal investmentAmount,
        [Description("Investment time horizon in months (e.g., 12 for 1 year, 36 for 3 years)")]
            int timeHorizonMonths,
        [Description(
            "Risk profile: Conservative (low risk), Moderate (balanced), or Aggressive (high risk)"
        )]
            string riskProfile,
        [Description("Currency code (default: SAR)")] string currency = "SAR",
        [Description("Only include Sharia-compliant funds")] bool shariahCompliant = false
    )
    {
        using var activity = ActivitySource.StartActivity("CalculateProfitProjection");
        ToolInvocationsCounter.Add(1);

        try
        {
            _logger.LogInformation(
                "Agent requested profit projection: {Amount} {Currency}, {Months} months, {Risk} profile",
                investmentAmount,
                currency,
                timeHorizonMonths,
                riskProfile
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = investmentAmount,
                Currency = currency,
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = NormalizeRiskProfile(riskProfile),
                InvestmentType = "LumpSum",
                ShariahCompliantOnly = shariahCompliant,
            };

            var result = await _workflow.ExecuteAsync(request);

            return FormatProjectionResult(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid projection request");
            return $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate profit projection");
            return $"Error calculating projection: {ex.Message}";
        }
    }

    /// <summary>
    /// Calculate profit projection for an existing customer
    /// Includes analysis of their current holdings
    /// </summary>
    /// <param name="customerId">Customer ID (CIF)</param>
    /// <param name="investmentAmount">Additional investment amount</param>
    /// <param name="timeHorizonMonths">Investment time horizon in months</param>
    /// <returns>Personalized projection with current holdings context</returns>
    [Description(
        "Calculate personalized profit projection for an existing customer. This takes into account their current portfolio and investment history. Use this when a customer with an existing account asks about potential returns."
    )]
    public async Task<string> CalculatePersonalizedProjection(
        [Description("Customer ID (CIF number like 100000000001)")] string customerId,
        [Description("Investment amount to project")] decimal investmentAmount,
        [Description("Investment time horizon in months")] int timeHorizonMonths
    )
    {
        using var activity = ActivitySource.StartActivity("CalculatePersonalizedProjection");
        ToolInvocationsCounter.Add(1);

        try
        {
            _logger.LogInformation(
                "Agent requested personalized projection for customer {CustomerId}: {Amount}, {Months} months",
                customerId,
                investmentAmount,
                timeHorizonMonths
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = investmentAmount,
                Currency = "SAR",
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = "Moderate", // Will be overridden by customer's actual profile
                InvestmentType = "LumpSum",
                CustomerId = customerId,
            };

            var result = await _workflow.ExecuteAsync(request);

            return FormatProjectionResult(result, includeCustomerContext: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate personalized projection");
            return $"Error calculating projection: {ex.Message}";
        }
    }

    /// <summary>
    /// Compare lump sum vs monthly SIP investment strategies
    /// </summary>
    /// <param name="totalAmount">Total amount to invest</param>
    /// <param name="timeHorizonMonths">Investment period in months</param>
    /// <param name="riskProfile">Risk profile</param>
    /// <returns>Comparison of both strategies with recommendations</returns>
    [Description(
        "Compare lump sum investment vs monthly SIP (Systematic Investment Plan). Use this when a customer is deciding whether to invest all at once or spread their investment over time."
    )]
    public async Task<string> CompareInvestmentStrategies(
        [Description("Total investment amount")] decimal totalAmount,
        [Description("Investment time horizon in months")] int timeHorizonMonths,
        [Description("Risk profile: Conservative, Moderate, or Aggressive")] string riskProfile
    )
    {
        using var activity = ActivitySource.StartActivity("CompareInvestmentStrategies");
        ToolInvocationsCounter.Add(1);

        try
        {
            _logger.LogInformation(
                "Agent requested strategy comparison: {Amount}, {Months} months",
                totalAmount,
                timeHorizonMonths
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = totalAmount,
                Currency = "SAR",
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = NormalizeRiskProfile(riskProfile),
                InvestmentType = "LumpSum", // Workflow will automatically compare
            };

            var result = await _workflow.ExecuteAsync(request);

            return FormatStrategyComparison(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare investment strategies");
            return $"Error comparing strategies: {ex.Message}";
        }
    }

    /// <summary>
    /// Get quick projection estimate without full workflow
    /// </summary>
    [Description(
        "Get a quick estimate of potential returns. This is faster but less detailed than the full projection."
    )]
    public string GetQuickEstimate(
        [Description("Investment amount")] decimal amount,
        [Description("Years to invest (1-10)")] int years,
        [Description("Risk level: low, medium, or high")] string riskLevel
    )
    {
        ToolInvocationsCounter.Add(1);

        // Quick estimate based on historical averages
        var annualReturn = riskLevel.ToLower() switch
        {
            "low" or "conservative" => 0.05m, // 5%
            "medium" or "moderate" => 0.08m, // 8%
            "high" or "aggressive" => 0.12m, // 12%
            _ => 0.07m, // 7% default
        };

        var futureValue = amount * (decimal)Math.Pow((double)(1 + annualReturn), years);
        var totalReturn = futureValue - amount;
        var percentReturn = (totalReturn / amount) * 100;

        return $@"## Quick Investment Estimate

**Investment Details:**
- Amount: {amount:N0} SAR
- Duration: {years} year(s)
- Risk Level: {riskLevel}

**Estimated Results (based on historical averages):**
- Projected Value: **{futureValue:N0} SAR**
- Total Return: **{totalReturn:N0} SAR** ({percentReturn:N1}%)
- Assumed Annual Return: {annualReturn * 100:N1}%

⚠️ *This is a quick estimate. For detailed projections with fund recommendations, use the full profit projection tool.*

Would you like me to run a detailed projection analysis?";
    }

    private string NormalizeRiskProfile(string input)
    {
        return input.ToLower() switch
        {
            "low" or "conservative" or "متحفظ" => "Conservative",
            "medium" or "moderate" or "balanced" or "متوازن" => "Moderate",
            "high" or "aggressive" or "جريء" => "Aggressive",
            _ => "Moderate",
        };
    }

    private string FormatProjectionResult(
        ProjectionResult result,
        bool includeCustomerContext = false
    )
    {
        var summary = result.InputSummary;
        var scenarios = result.Scenarios;

        var output =
            $@"## 📊 Profit Projection Results
### احتساب الأرباح التقديرية

**Projection ID:** {result.ProjectionId}

---

### Investment Summary
| Parameter | Value |
|-----------|-------|
| Investment Amount | {summary.Amount:N0} {summary.Currency} |
| Time Horizon | {summary.Horizon} ({summary.HorizonAr}) |
| Risk Profile | {summary.RiskProfile} ({summary.RiskProfileAr}) |
| Investment Type | {summary.InvestmentType} |
| Sharia Compliant | {(summary.ShariahCompliant ? "Yes ✓" : "No")} |

---

### 📈 Projection Scenarios

#### 🟢 Conservative Scenario ({scenarios.Conservative.Confidence}% Confidence)
- **Projected Value:** {scenarios.Conservative.ProjectedValue:N0} {summary.Currency}
- **Total Return:** {scenarios.Conservative.TotalReturn:N0} {summary.Currency}
- **Annualized Return:** {scenarios.Conservative.AnnualizedReturn:N1}%
- *{scenarios.Conservative.DescriptionAr}*

#### 🟡 Expected Scenario ({scenarios.Expected.Confidence}% Confidence)
- **Projected Value:** {scenarios.Expected.ProjectedValue:N0} {summary.Currency}
- **Total Return:** {scenarios.Expected.TotalReturn:N0} {summary.Currency}
- **Annualized Return:** {scenarios.Expected.AnnualizedReturn:N1}%
- *{scenarios.Expected.DescriptionAr}*

#### 🔵 Optimistic Scenario ({scenarios.Optimistic.Confidence}% Confidence)
- **Projected Value:** {scenarios.Optimistic.ProjectedValue:N0} {summary.Currency}
- **Total Return:** {scenarios.Optimistic.TotalReturn:N0} {summary.Currency}
- **Annualized Return:** {scenarios.Optimistic.AnnualizedReturn:N1}%
- *{scenarios.Optimistic.DescriptionAr}*

---

### 💼 Recommended Fund Allocation

| Fund | Allocation | Investment | Expected Return |
|------|------------|------------|-----------------|";

        foreach (var fund in result.RecommendedFunds.Take(5))
        {
            output +=
                $@"
| {fund.FundName} | {fund.AllocationPercent:N1}% | {fund.InvestmentAmount:N0} SAR | {fund.ExpectedReturn:N1}% |";
        }

        output +=
            $@"

---

### ⚠️ Important Notes
";
        foreach (var warning in result.RiskWarnings.Take(3))
        {
            output += $"- {warning}\n";
        }

        output +=
            $@"
---

### 🚀 Next Steps
**{result.CallToAction.PrimaryAction}** ({result.CallToAction.PrimaryActionAr})

*Projection generated at {result.Metadata.CreatedAt:yyyy-MM-dd HH:mm} UTC*
*Valid until {result.Metadata.ExpiresAt:yyyy-MM-dd}*";

        return output;
    }

    private string FormatStrategyComparison(ProjectionResult result)
    {
        var comparison = result.Scenarios.StrategyComparison;
        if (comparison == null)
        {
            return "Strategy comparison not available for this projection.";
        }

        return $@"## 📊 Investment Strategy Comparison

### Lump Sum vs Monthly SIP Analysis

---

### 💰 Lump Sum Investment
- **Total Investment:** {comparison.LumpSum.TotalInvestment:N0} SAR
- **Projected Value:** {comparison.LumpSum.ProjectedValue:N0} SAR
- **Total Return:** {comparison.LumpSum.TotalReturn:N0} SAR
- **Benefit:** {comparison.LumpSum.Benefit}
- **فائدة:** {comparison.LumpSum.BenefitAr}

---

### 📅 Monthly SIP (Systematic Investment Plan)
- **Total Investment:** {comparison.MonthlySip.TotalInvestment:N0} SAR
- **Projected Value:** {comparison.MonthlySip.ProjectedValue:N0} SAR
- **Total Return:** {comparison.MonthlySip.TotalReturn:N0} SAR
- **Benefit:** {comparison.MonthlySip.Benefit}
- **فائدة:** {comparison.MonthlySip.BenefitAr}

---

### ✅ Recommendation: **{comparison.RecommendedStrategy}**

{comparison.RecommendationRationale}

{comparison.RecommendationRationaleAr}

---

**Difference:** {Math.Abs(comparison.LumpSum.ProjectedValue - comparison.MonthlySip.ProjectedValue):N0} SAR

Would you like to proceed with one of these strategies?";
    }
}

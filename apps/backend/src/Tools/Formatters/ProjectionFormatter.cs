using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;

namespace AgentFrameworkQuickStart.Tools.Formatters;

/// <summary>
/// Formats projection results for agent responses
/// </summary>
public static class ProjectionFormatter
{
    public static string FormatResult(ProjectionResult result, bool includeCustomerContext = false)
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

{FormatScenario("🟢 Conservative", scenarios.Conservative, summary.Currency)}
{FormatScenario("🟡 Expected", scenarios.Expected, summary.Currency)}
{FormatScenario("🔵 Optimistic", scenarios.Optimistic, summary.Currency)}

---

### 💼 Recommended Fund Allocation

| Fund | Allocation | Investment | Expected Return |
|------|------------|------------|-----------------|
{FormatFunds(result.RecommendedFunds)}

---

### ⚠️ Important Notes
{FormatWarnings(result.RiskWarnings)}

---

### 🚀 Next Steps
**{result.CallToAction.PrimaryAction}** ({result.CallToAction.PrimaryActionAr})

*Projection generated at {result.Metadata.CreatedAt:yyyy-MM-dd HH:mm} UTC*
*Valid until {result.Metadata.ExpiresAt:yyyy-MM-dd}*";

        return output;
    }

    public static string FormatStrategyComparison(ProjectionResult result)
    {
        var comparison = result.Scenarios.StrategyComparison;
        if (comparison == null)
            return "Strategy comparison not available for this projection.";

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

    public static string FormatQuickEstimate(decimal amount, int years, string riskLevel)
    {
        var annualReturn = riskLevel.ToLower() switch
        {
            "low" or "conservative" => 0.05m,
            "medium" or "moderate" => 0.08m,
            "high" or "aggressive" => 0.12m,
            _ => 0.07m,
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

    private static string FormatScenario(string label, Scenario scenario, string currency)
    {
        return $@"#### {label} Scenario ({scenario.Confidence}% Confidence)
- **Projected Value:** {scenario.ProjectedValue:N0} {currency}
- **Total Return:** {scenario.TotalReturn:N0} {currency}
- **Annualized Return:** {scenario.AnnualizedReturn:N1}%
- *{scenario.DescriptionAr}*
";
    }

    private static string FormatFunds(IEnumerable<FundRecommendation> funds)
    {
        return string.Join(
            "\n",
            funds
                .Take(5)
                .Select(f =>
                    $"| {f.FundName} | {f.AllocationPercent:N1}% | {f.InvestmentAmount:N0} SAR | {f.ExpectedReturn:N1}% |"
                )
        );
    }

    private static string FormatWarnings(IEnumerable<string> warnings)
    {
        return string.Join("\n", warnings.Take(3).Select(w => $"- {w}"));
    }
}

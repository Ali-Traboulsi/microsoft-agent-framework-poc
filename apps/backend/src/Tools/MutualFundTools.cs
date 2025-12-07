using System.ComponentModel;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for mutual fund research and operations
/// </summary>
public class MutualFundTools
{
    private readonly InvestmentDataStore _dataStore;

    public MutualFundTools(InvestmentDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [Description("Search for mutual funds by various criteria")]
    public string SearchFunds(
        [Description(
            "Fund category: Equity, Bond, Balanced, MoneyMarket, Index, International, or Sector (optional)"
        )]
            string? category = null,
        [Description("Risk level: Low, Medium, High, or VeryHigh (optional)")]
            string? riskLevel = null,
        [Description("Minimum return percentage (optional)")] decimal? minReturn = null
    )
    {
        var funds = _dataStore.GetAllMutualFunds().AsEnumerable();

        if (
            !string.IsNullOrEmpty(category)
            && Enum.TryParse<FundCategory>(category, true, out var cat)
        )
            funds = funds.Where(f => f.Category == cat);

        if (
            !string.IsNullOrEmpty(riskLevel)
            && Enum.TryParse<RiskLevel>(riskLevel, true, out var risk)
        )
            funds = funds.Where(f => f.RiskLevel == risk);

        if (minReturn.HasValue)
            funds = funds.Where(f => f.OneYearReturn >= minReturn.Value);

        var fundsList = funds.ToList();
        if (!fundsList.Any())
            return "No funds found matching the criteria.";

        var result = $"Found {fundsList.Count} mutual fund(s):\n\n";
        foreach (var fund in fundsList)
        {
            result += $"• {fund.FundName} ({fund.FundSymbol})\n";
            result += $"  Category: {fund.Category} | Risk: {fund.RiskLevel}\n";
            result += $"  Current NAV: ${fund.CurrentNAV:N2}\n";
            result +=
                $"  Returns: YTD {fund.YTDReturn:N1}% | 1Yr {fund.OneYearReturn:N1}% | 3Yr {fund.ThreeYearReturn:N1}%\n";
            result +=
                $"  Expense Ratio: {fund.ExpenseRatio:N2}% | Min Investment: ${fund.MinimumInvestment:N2}\n\n";
        }

        return result;
    }

    [Description("Get detailed information about a specific mutual fund")]
    public string GetFundDetails([Description("Fund symbol or fund ID")] string fundIdentifier)
    {
        var fund =
            _dataStore.GetMutualFundBySymbol(fundIdentifier)
            ?? _dataStore.GetMutualFund(fundIdentifier);

        if (fund == null)
            return $"Error: Fund '{fundIdentifier}' not found.";

        var result = $"📊 {fund.FundName} ({fund.FundSymbol})\n";
        result += $"Fund ID: {fund.FundId}\n\n";
        result += $"Description: {fund.Description}\n\n";
        result += $"Category: {fund.Category}\n";
        result += $"Risk Level: {fund.RiskLevel}\n";
        result += $"Current NAV: ${fund.CurrentNAV:N2}\n";
        result += $"Expense Ratio: {fund.ExpenseRatio:N2}%\n";
        result += $"Minimum Investment: ${fund.MinimumInvestment:N2}\n\n";
        result += $"Performance:\n";
        result += $"  YTD Return: {fund.YTDReturn:N1}%\n";
        result += $"  1-Year Return: {fund.OneYearReturn:N1}%\n";
        result += $"  3-Year Return: {fund.ThreeYearReturn:N1}%\n";

        return result;
    }

    [Description("List all available mutual funds")]
    public string ListAllFunds()
    {
        var funds = _dataStore.GetAllMutualFunds().ToList();

        var result = $"Available Mutual Funds ({funds.Count} total):\n\n";
        foreach (var fund in funds.OrderBy(f => f.Category).ThenBy(f => f.FundName))
        {
            result += $"• {fund.FundSymbol} - {fund.FundName}\n";
            result +=
                $"  {fund.Category} | Risk: {fund.RiskLevel} | 1Yr Return: {fund.OneYearReturn:N1}%\n\n";
        }

        return result;
    }

    [Description("Compare multiple mutual funds side by side")]
    public string CompareFunds(
        [Description("Comma-separated list of fund symbols or IDs to compare")]
            string fundIdentifiers
    )
    {
        var identifiers = fundIdentifiers.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var funds = new List<MutualFund>();

        foreach (var id in identifiers)
        {
            var fund = _dataStore.GetMutualFundBySymbol(id) ?? _dataStore.GetMutualFund(id);
            if (fund != null)
                funds.Add(fund);
        }

        if (funds.Count < 2)
            return "Error: Need at least 2 valid funds to compare.";

        var result = "📊 Fund Comparison:\n\n";

        foreach (var fund in funds)
        {
            result += $"{fund.FundSymbol} - {fund.FundName}\n";
            result +=
                $"  NAV: ${fund.CurrentNAV:N2} | Category: {fund.Category} | Risk: {fund.RiskLevel}\n";
            result +=
                $"  Returns: YTD {fund.YTDReturn:N1}% | 1Yr {fund.OneYearReturn:N1}% | 3Yr {fund.ThreeYearReturn:N1}%\n";
            result +=
                $"  Expense Ratio: {fund.ExpenseRatio:N2}% | Min: ${fund.MinimumInvestment:N2}\n\n";
        }

        // Add best performer analysis
        var bestYTD = funds.OrderByDescending(f => f.YTDReturn).First();
        var lowestExpense = funds.OrderBy(f => f.ExpenseRatio).First();
        var lowestRisk = funds.OrderBy(f => f.RiskLevel).First();

        result += "Analysis:\n";
        result += $"  Best YTD Return: {bestYTD.FundSymbol} ({bestYTD.YTDReturn:N1}%)\n";
        result +=
            $"  Lowest Expense Ratio: {lowestExpense.FundSymbol} ({lowestExpense.ExpenseRatio:N2}%)\n";
        result += $"  Lowest Risk: {lowestRisk.FundSymbol} ({lowestRisk.RiskLevel})\n";

        return result;
    }
}

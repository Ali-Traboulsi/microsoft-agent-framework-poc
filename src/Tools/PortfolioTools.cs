using System.ComponentModel;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;
using Microsoft.Extensions.AI;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for portfolio management operations
/// </summary>
public class PortfolioTools
{
    private readonly InvestmentDataStore _dataStore;

    public PortfolioTools(InvestmentDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [Description("Create a new investment portfolio for a customer account")]
    public string CreatePortfolio(
        [Description("The account ID")] string accountId,
        [Description("Portfolio name")] string portfolioName,
        [Description(
            "Investment strategy: Conservative, Moderate, Aggressive, Income, Growth, or Balanced"
        )]
            string strategy
    )
    {
        var account = _dataStore.GetAccount(accountId);
        if (account == null)
            return $"Error: Account {accountId} not found.";

        if (!Enum.TryParse<PortfolioStrategy>(strategy, true, out var strategyEnum))
            return $"Error: Invalid strategy '{strategy}'. Valid values: Conservative, Moderate, Aggressive, Income, Growth, Balanced";

        var portfolio = new Portfolio
        {
            PortfolioId = $"PORT{DateTime.Now.Ticks}",
            PortfolioName = portfolioName,
            AccountId = accountId,
            CustomerId = account.CustomerId,
            CreatedDate = DateTime.Now,
            Strategy = strategyEnum,
            RiskProfile = MapStrategyToRisk(strategyEnum),
            Holdings = new List<PortfolioHolding>(),
            TotalValue = 0m,
        };

        _dataStore.AddPortfolio(portfolio);
        return $"✓ Portfolio '{portfolioName}' created successfully with ID: {portfolio.PortfolioId}";
    }

    [Description("Get details about a specific portfolio including all holdings")]
    public string GetPortfolioDetails([Description("The portfolio ID")] string portfolioId)
    {
        var portfolio = _dataStore.GetPortfolio(portfolioId);
        if (portfolio == null)
            return $"Error: Portfolio {portfolioId} not found.";

        var result = $"Portfolio: {portfolio.PortfolioName} (ID: {portfolio.PortfolioId})\n";
        result += $"Strategy: {portfolio.Strategy} | Risk Profile: {portfolio.RiskProfile}\n";
        result += $"Total Value: ${portfolio.TotalValue:N2}\n";
        result += $"Created: {portfolio.CreatedDate:yyyy-MM-dd}\n\n";

        if (portfolio.Holdings.Any())
        {
            result += "Holdings:\n";
            foreach (var holding in portfolio.Holdings)
            {
                result += $"  • {holding.FundSymbol} - {holding.FundName}\n";
                result +=
                    $"    Shares: {holding.Shares:N2} | Current Value: ${holding.CurrentValue:N2}\n";
                result +=
                    $"    Gain/Loss: ${holding.GainLoss:N2} ({holding.GainLossPercentage:N2}%)\n";
            }
        }
        else
        {
            result += "No holdings yet.\n";
        }

        return result;
    }

    [Description("List all portfolios for a specific account")]
    public string ListPortfolios([Description("The account ID")] string accountId)
    {
        var portfolios = _dataStore.GetPortfoliosByAccount(accountId).ToList();

        if (!portfolios.Any())
            return $"No portfolios found for account {accountId}.";

        var result = $"Portfolios for account {accountId}:\n\n";
        foreach (var portfolio in portfolios)
        {
            result += $"• {portfolio.PortfolioName} (ID: {portfolio.PortfolioId})\n";
            result += $"  Strategy: {portfolio.Strategy} | Value: ${portfolio.TotalValue:N2}\n";
            result += $"  Holdings: {portfolio.Holdings.Count}\n\n";
        }

        return result;
    }

    [Description("Calculate and display portfolio allocation breakdown by fund category")]
    public string GetPortfolioAllocation([Description("The portfolio ID")] string portfolioId)
    {
        var portfolio = _dataStore.GetPortfolio(portfolioId);
        if (portfolio == null)
            return $"Error: Portfolio {portfolioId} not found.";

        if (!portfolio.Holdings.Any())
            return "Portfolio has no holdings to analyze.";

        var totalValue = portfolio.Holdings.Sum(h => h.CurrentValue);
        var allocationByCategory = portfolio
            .Holdings.GroupBy(h => GetFundCategory(h.FundId))
            .Select(g => new
            {
                Category = g.Key,
                Value = g.Sum(h => h.CurrentValue),
                Percentage = (g.Sum(h => h.CurrentValue) / totalValue) * 100,
            })
            .OrderByDescending(x => x.Percentage);

        var result = $"Portfolio Allocation for {portfolio.PortfolioName}:\n\n";
        foreach (var allocation in allocationByCategory)
        {
            result +=
                $"• {allocation.Category}: {allocation.Percentage:N1}% (${allocation.Value:N2})\n";
        }

        return result;
    }

    private RiskProfile MapStrategyToRisk(PortfolioStrategy strategy) =>
        strategy switch
        {
            PortfolioStrategy.Conservative => RiskProfile.Conservative,
            PortfolioStrategy.Moderate or PortfolioStrategy.Balanced => RiskProfile.Moderate,
            PortfolioStrategy.Aggressive or PortfolioStrategy.Growth => RiskProfile.Aggressive,
            PortfolioStrategy.Income => RiskProfile.Conservative,
            _ => RiskProfile.Moderate,
        };

    private FundCategory GetFundCategory(string fundId)
    {
        var fund = _dataStore.GetMutualFund(fundId);
        return fund?.Category ?? FundCategory.Balanced;
    }
}

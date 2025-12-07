using System.Diagnostics;
using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;
using AgentFrameworkQuickStart.Infrastructure.ExternalApis;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Fetches and enriches customer context for personalized projections
/// </summary>
public sealed class CustomerContextExecutor : ExecutorBase<ProjectionRequest, CustomerContext>
{
    private readonly ISNBCapitalApi _api;

    public CustomerContextExecutor(ISNBCapitalApi api, ILogger<CustomerContextExecutor> logger)
        : base(logger)
    {
        _api = api;
    }

    protected override async Task<CustomerContext> ExecuteCoreAsync(
        ProjectionRequest input,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(input.CustomerId))
            return CustomerContext.Anonymous;

        try
        {
            var customerData = await _api.GetCustomerDataAsync(input.CustomerId);
            var holdings = ExtractHoldings(customerData);

            return new CustomerContext
            {
                CustomerId = input.CustomerId,
                IsExistingCustomer = customerData.Portfolios.Any(),
                CustomerName = $"Customer {input.CustomerId}",
                CurrentRiskProfile = DetermineRiskProfile(holdings),
                ExistingHoldings = holdings,
                CurrentPortfolioValue = customerData.TotalPortfolioValue,
                Behavior = new InvestmentBehavior
                {
                    TotalTransactions = customerData.TotalHoldingsCount,
                    AverageInvestmentSize = holdings.Any()
                        ? holdings.Average(h => h.CurrentValue)
                        : 0,
                    PreferredFundType = DeterminePreferredFundType(holdings),
                    MonthsAsCustomer = CalculateMonthsAsCustomer(customerData.Portfolios),
                    HistoricalReturns = CalculateHistoricalReturns(holdings),
                },
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Failed to fetch customer context for {CustomerId}, using anonymous",
                input.CustomerId
            );
            return CustomerContext.Anonymous;
        }
    }

    protected override void AddInputTags(Activity? activity, ProjectionRequest input) =>
        activity?.SetTag("customer_id", input.CustomerId ?? "anonymous");

    protected override void AddOutputTags(Activity? activity, CustomerContext output)
    {
        activity?.SetTag("is_existing_customer", output.IsExistingCustomer);
        activity?.SetTag("holdings_count", output.ExistingHoldings.Count);
    }

    private static List<ExistingHolding> ExtractHoldings(CustomerCompletePortfolioData data) =>
        data
            .PortfolioDetails.SelectMany(p => p.MutualFundHoldings)
            .Select(h => new ExistingHolding
            {
                FundCode = h.FundCode ?? "",
                FundName = h.FundName ?? h.FundTitle ?? "",
                Units = h.Units ?? 0,
                CurrentValue = h.CurrentValue ?? 0,
                CostBasis = (h.AverageCost ?? 0) * (h.Units ?? 0),
                UnrealizedGain = h.UnrealizedGainLoss ?? 0,
                UnrealizedGainPercent = h.UnrealizedGainLossPercent ?? 0,
            })
            .ToList();

    private static string DetermineRiskProfile(List<ExistingHolding> holdings) =>
        holdings.Count switch
        {
            0 => "Moderate",
            < 3 => "Conservative",
            < 7 => "Moderate",
            _ => "Aggressive",
        };

    private static string DeterminePreferredFundType(List<ExistingHolding> holdings)
    {
        if (!holdings.Any())
            return "Balanced";
        return holdings
            .GroupBy(h => h.FundCode.Length >= 3 ? h.FundCode[..3] : h.FundCode)
            .OrderByDescending(g => g.Sum(h => h.CurrentValue))
            .First()
            .Key;
    }

    private static int CalculateMonthsAsCustomer(List<SNBPortfolio> portfolios)
    {
        var oldest = portfolios.Where(p => p.OpenedDate.HasValue).MinBy(p => p.OpenedDate);
        return oldest?.OpenedDate.HasValue == true
            ? (int)((DateTime.UtcNow - oldest.OpenedDate.Value).TotalDays / 30)
            : 0;
    }

    private static decimal CalculateHistoricalReturns(List<ExistingHolding> holdings)
    {
        var totalCost = holdings.Sum(h => h.CostBasis);
        return totalCost == 0 ? 0 : holdings.Sum(h => h.UnrealizedGain) / totalCost * 100;
    }
}

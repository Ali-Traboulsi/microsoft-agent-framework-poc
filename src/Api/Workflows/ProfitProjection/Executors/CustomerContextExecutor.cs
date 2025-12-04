using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Enriches projection with customer context from existing data
/// If customer ID provided, fetches existing holdings and preferences
/// </summary>
public class CustomerContextExecutor
{
    private readonly SNBCapitalApiService _snbCapitalApi;
    private readonly ILogger<CustomerContextExecutor> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.CustomerContext",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.CustomerContext",
        "1.0.0"
    );
    private static readonly Counter<int> ExistingCustomerCounter = Meter.CreateCounter<int>(
        "existing_customers",
        "customers",
        "Number of existing customers identified"
    );

    public CustomerContextExecutor(
        SNBCapitalApiService snbCapitalApi,
        ILogger<CustomerContextExecutor> logger
    )
    {
        _snbCapitalApi = snbCapitalApi;
        _logger = logger;
    }

    /// <summary>
    /// Execute customer context enrichment
    /// </summary>
    public async Task<CustomerContext> ExecuteAsync(ProjectionRequest request)
    {
        using var activity = ActivitySource.StartActivity("CustomerContextEnrichment");
        activity?.SetTag("has_customer_id", !string.IsNullOrEmpty(request.CustomerId));

        try
        {
            // If no customer ID, return anonymous context
            if (string.IsNullOrEmpty(request.CustomerId))
            {
                _logger.LogInformation("No customer ID provided, returning anonymous context");
                return new CustomerContext
                {
                    IsExistingCustomer = false,
                    CurrentRiskProfile = request.RiskProfile,
                };
            }

            _logger.LogInformation(
                "Fetching context for customer {CustomerId}",
                request.CustomerId
            );

            // Use customer ID as CIF
            var cif = request.CustomerId;

            try
            {
                // Fetch customer portfolios
                var portfoliosResponse = await _snbCapitalApi.GetCustomerPortfoliosAsync(cif);

                if (portfoliosResponse.Portfolios?.Any() != true)
                {
                    _logger.LogInformation("Customer {CustomerId} has no existing portfolios", cif);
                    return new CustomerContext
                    {
                        CustomerId = cif,
                        IsExistingCustomer = false,
                        CurrentRiskProfile = request.RiskProfile,
                    };
                }

                ExistingCustomerCounter.Add(1);

                // Fetch holdings for each portfolio
                var allHoldings = new List<ExistingHolding>();
                decimal totalValue = 0m;

                foreach (var portfolio in portfoliosResponse.Portfolios)
                {
                    try
                    {
                        var mfHoldings = await _snbCapitalApi.GetMutualFundHoldingsAsync(
                            portfolio.PortfolioNumber,
                            cif
                        );

                        if (mfHoldings.Holdings != null)
                        {
                            allHoldings.AddRange(
                                mfHoldings.Holdings.Select(h => new ExistingHolding
                                {
                                    // Use new API field names: FundTitle, FundCode, Units, UnitPrice
                                    FundCode = h.FundCode ?? "UNKNOWN",
                                    FundName =
                                        h.FundTitle ?? h.FundName ?? h.FundCode ?? "Unknown Fund",
                                    Units = h.Units ?? 0,
                                    CurrentValue =
                                        (h.Units ?? 0) * (h.UnitPrice ?? h.CurrentNav ?? 0),
                                    CostBasis =
                                        (h.Units ?? 0) * (h.AverageCost ?? h.UnitPrice ?? 0),
                                    UnrealizedGain = h.UnrealizedGainLoss ?? 0,
                                    UnrealizedGainPercent = h.UnrealizedGainLossPercent ?? 0,
                                })
                            );

                            totalValue += mfHoldings.Holdings.Sum(h =>
                                (h.Units ?? 0) * (h.UnitPrice ?? h.CurrentNav ?? 0)
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to fetch holdings for portfolio {Portfolio}",
                            portfolio.PortfolioNumber
                        );
                    }
                }

                // Infer customer preferences from holdings
                var behavior = AnalyzeInvestmentBehavior(allHoldings);

                activity?.SetTag("holdings_count", allHoldings.Count);
                activity?.SetTag("total_value", totalValue);

                return new CustomerContext
                {
                    CustomerId = cif,
                    IsExistingCustomer = true,
                    CustomerName = $"Customer {cif[^4..]}", // Last 4 digits
                    CurrentRiskProfile = InferRiskProfile(allHoldings) ?? request.RiskProfile,
                    ExistingHoldings = allHoldings,
                    CurrentPortfolioValue = totalValue,
                    Behavior = behavior,
                };
            }
            catch (SNBCapitalApiException ex)
            {
                _logger.LogWarning(ex, "Failed to fetch customer data, treating as new customer");
                return new CustomerContext
                {
                    CustomerId = cif,
                    IsExistingCustomer = false,
                    CurrentRiskProfile = request.RiskProfile,
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during customer context enrichment");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private InvestmentBehavior AnalyzeInvestmentBehavior(List<ExistingHolding> holdings)
    {
        if (!holdings.Any())
        {
            return new InvestmentBehavior();
        }

        var totalValue = holdings.Sum(h => h.CurrentValue);
        var avgInvestment = holdings.Average(h => h.CostBasis);
        var totalReturn = holdings.Sum(h => h.UnrealizedGain);
        var returnPercent = totalValue > 0 ? (totalReturn / (totalValue - totalReturn)) * 100 : 0;

        // Infer preferred fund type from majority of holdings
        var preferredType =
            holdings
                .GroupBy(h => InferFundType(h.FundName))
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()
                ?.Key
            ?? "Balanced";

        return new InvestmentBehavior
        {
            TotalTransactions = holdings.Count,
            AverageInvestmentSize = Math.Round(avgInvestment, 2),
            PreferredFundType = preferredType,
            MonthsAsCustomer = 12, // Would need actual data
            HistoricalReturns = Math.Round(returnPercent, 2),
        };
    }

    private string? InferRiskProfile(List<ExistingHolding> holdings)
    {
        if (!holdings.Any())
            return null;

        var fundTypes = holdings.Select(h => InferFundType(h.FundName)).ToList();

        var equityCount = fundTypes.Count(t =>
            t.Contains("Equity", StringComparison.OrdinalIgnoreCase)
        );
        var fixedCount = fundTypes.Count(t =>
            t.Contains("Fixed", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Money", StringComparison.OrdinalIgnoreCase)
        );

        var equityRatio = (decimal)equityCount / holdings.Count;

        return equityRatio switch
        {
            > 0.6m => "Aggressive",
            > 0.3m => "Moderate",
            _ => "Conservative",
        };
    }

    private string InferFundType(string fundName)
    {
        var name = fundName.ToLower();

        if (name.Contains("equity") || name.Contains("stock") || name.Contains("growth"))
            return "Equity";
        if (name.Contains("money") || name.Contains("cash") || name.Contains("liquid"))
            return "Money Market";
        if (name.Contains("fixed") || name.Contains("bond") || name.Contains("income"))
            return "Fixed Income";
        if (name.Contains("balanced") || name.Contains("hybrid"))
            return "Balanced";

        return "Balanced";
    }
}

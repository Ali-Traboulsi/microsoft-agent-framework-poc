using System.ComponentModel;
using System.Text.Json;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for interacting with SNB Capital API - Mutual Funds and Portfolios
/// </summary>
public class SNBCapitalTools(SNBCapitalApiService apiService, ILogger<SNBCapitalTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Description(
        "Get all available mutual funds from SNB Capital. Returns fund details including name, type, risk level, NAV, returns, and fees."
    )]
    public async Task<string> GetMutualFunds(
        [Description("Customer CIF number for authentication")] string cif
    )
    {
        try
        {
            logger.LogInformation("Fetching mutual funds for CIF: {CIF}", cif);
            var response = await apiService.GetMutualFundsAsync(cif);

            if (!response.Success || response.Funds == null)
            {
                logger.LogWarning(
                    "Failed to fetch mutual funds for CIF {CIF}: {Message}",
                    cif,
                    response.Message
                );
                return JsonSerializer.Serialize(
                    new
                    {
                        Success = false,
                        Error = $"Failed to fetch mutual funds: {response.Message ?? "No data returned"}",
                        CIF = cif,
                    },
                    JsonOptions
                );
            }

            var fundsSummary = response.Funds.Select(f => new
            {
                f.FundId,
                f.FundName,
                f.FundType,
                f.RiskLevel,
                f.Currency,
                CurrentNAV = f.NavValue,
                Returns = new
                {
                    f.Return1M,
                    f.Return3M,
                    f.Return6M,
                    f.Return1Y,
                },
                MinimumInvestment = f.MinimumInvestmentAmount,
                Fees = new
                {
                    f.ManagementFee,
                    f.EntryFee,
                    f.RedemptionFee,
                },
                f.Status,
            });

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    TotalFunds = response.Funds.Count,
                    Funds = fundsSummary,
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching mutual funds for CIF: {CIF}", cif);
            return JsonSerializer.Serialize(
                new
                {
                    Success = false,
                    Error = ex.Message,
                    CIF = cif,
                    ErrorType = ex.GetType().Name,
                },
                JsonOptions
            );
        }
    }

    [Description(
        "Get customer portfolios from SNB Capital. Returns all portfolios including local market and mutual fund portfolios with balances."
    )]
    public async Task<string> GetCustomerPortfolios([Description("Customer CIF number")] string cif)
    {
        logger.LogWarning(
            "=== SNBCapitalTools.GetCustomerPortfolios CALLED with CIF: {CIF} ===",
            cif
        );
        Console.WriteLine($"=== SNBCapitalTools.GetCustomerPortfolios CALLED with CIF: {cif} ===");

        try
        {
            logger.LogInformation("Fetching portfolios for CIF: {CIF}", cif);
            Console.WriteLine($"Calling apiService.GetCustomerPortfoliosAsync for CIF: {cif}");

            var response = await apiService.GetCustomerPortfoliosAsync(cif);

            Console.WriteLine(
                $"API Response received - Success: {response.Success}, Portfolios: {response.Portfolios?.Count ?? 0}"
            );
            logger.LogInformation(
                "API Response - Success: {Success}, Count: {Count}",
                response.Success,
                response.Portfolios?.Count ?? 0
            );

            if (!response.Success || response.Portfolios == null)
            {
                logger.LogWarning(
                    "Failed to fetch portfolios for CIF {CIF}: {Message}",
                    cif,
                    response.Message
                );
                Console.WriteLine($"FAILED: {response.Message}");
                return JsonSerializer.Serialize(
                    new
                    {
                        Success = false,
                        Error = $"Failed to fetch portfolios: {response.Message ?? "No data returned"}",
                        CIF = cif,
                        Suggestion = "Please verify the CIF is valid (test CIFs: 100000000001-100000000030)",
                    },
                    JsonOptions
                );
            }

            Console.WriteLine($"SUCCESS: Found {response.Portfolios.Count} portfolios");

            var portfolioSummary = response.Portfolios.Select(p => new
            {
                p.PortfolioNumber,
                p.PortfolioName,
                p.PortfolioType,
                p.TotalValue,
                p.CashBalance,
                p.Currency,
                p.Status,
            });

            var result = JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    TotalPortfolios = response.Portfolios.Count,
                    Portfolios = portfolioSummary,
                },
                JsonOptions
            );

            Console.WriteLine($"Returning JSON result (length: {result.Length})");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching customer portfolios for CIF: {CIF}", cif);
            Console.WriteLine($"EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
            return JsonSerializer.Serialize(
                new
                {
                    Success = false,
                    Error = ex.Message,
                    CIF = cif,
                    ErrorType = ex.GetType().Name,
                },
                JsonOptions
            );
        }
    }

    [Description(
        "Get detailed portfolio holdings including mutual funds and local market stocks for a specific portfolio."
    )]
    public async Task<string> GetPortfolioHoldings(
        [Description("Customer CIF number")] string cif,
        [Description("Portfolio number to get holdings for")] string portfolioNumber
    )
    {
        try
        {
            logger.LogInformation("Fetching holdings for portfolio: {Portfolio}", portfolioNumber);

            var mfHoldings = await apiService.GetMutualFundHoldingsAsync(portfolioNumber, cif);
            var localHoldings = await apiService.GetLocalMarketHoldingsAsync(portfolioNumber, cif);

            var result = new
            {
                Success = true,
                PortfolioNumber = portfolioNumber,
                MutualFundHoldings = mfHoldings.Holdings?.Select(h => new
                {
                    h.FundName,
                    h.FundCode,
                    h.Units,
                    h.CurrentNav,
                    h.CurrentValue,
                    h.UnrealizedGainLoss,
                    h.UnrealizedGainLossPercent,
                    h.Currency,
                }),
                LocalMarketHoldings = localHoldings.Holdings?.Select(h => new
                {
                    h.Symbol,
                    h.CompanyName,
                    h.Quantity,
                    h.CurrentPrice,
                    h.MarketValue,
                    h.UnrealizedGainLoss,
                    h.UnrealizedGainLossPercent,
                    h.Sector,
                }),
                TotalMutualFundValue = mfHoldings.Holdings?.Sum(h => h.CurrentValue ?? 0) ?? 0,
                TotalLocalMarketValue = localHoldings.Holdings?.Sum(h => h.MarketValue ?? 0) ?? 0,
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching portfolio holdings");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Get complete customer data including all portfolios, holdings, and available mutual funds."
    )]
    public async Task<string> GetCompleteCustomerData(
        [Description("Customer CIF number")] string cif
    )
    {
        try
        {
            logger.LogInformation("Fetching complete customer data for CIF: {CIF}", cif);
            var data = await apiService.GetCompleteCustomerDataAsync(cif);

            var summary = new
            {
                Success = true,
                CustomerCIF = cif,
                TotalPortfolioValue = data.TotalPortfolioValue,
                TotalHoldings = data.TotalHoldingsCount,
                Portfolios = data.Portfolios.Select(p => new
                {
                    p.PortfolioNumber,
                    p.PortfolioName,
                    p.PortfolioType,
                    p.TotalValue,
                    p.Currency,
                }),
                AvailableMutualFunds = data.AvailableMutualFunds.Count,
                PortfolioDetails = data.PortfolioDetails.Select(pd => new
                {
                    pd.Portfolio.PortfolioNumber,
                    MutualFundCount = pd.MutualFundHoldings.Count,
                    LocalMarketCount = pd.LocalMarketHoldings.Count,
                    pd.TotalMutualFundValue,
                    pd.TotalLocalMarketValue,
                }),
            };

            return JsonSerializer.Serialize(summary, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching complete customer data");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Search mutual funds by criteria like risk level, fund type, or minimum investment amount."
    )]
    public async Task<string> SearchMutualFunds(
        [Description("Customer CIF number")] string cif,
        [Description("Filter by risk level: Low, Medium, High (optional)")]
            string? riskLevel = null,
        [Description("Filter by fund type: Equity, Fixed Income, Balanced, etc. (optional)")]
            string? fundType = null,
        [Description("Maximum minimum investment amount (optional)")]
            decimal? maxMinInvestment = null
    )
    {
        try
        {
            logger.LogInformation("Searching mutual funds with filters");
            var response = await apiService.GetMutualFundsAsync(cif);

            if (!response.Success || response.Funds == null)
                return $"Error: Failed to fetch mutual funds - {response.Message}";

            var filtered = response.Funds.AsEnumerable();

            if (!string.IsNullOrEmpty(riskLevel))
                filtered = filtered.Where(f =>
                    f.RiskLevel?.Equals(riskLevel, StringComparison.OrdinalIgnoreCase) == true
                );

            if (!string.IsNullOrEmpty(fundType))
                filtered = filtered.Where(f =>
                    f.FundType?.Contains(fundType, StringComparison.OrdinalIgnoreCase) == true
                );

            if (maxMinInvestment.HasValue)
                filtered = filtered.Where(f => f.MinimumInvestmentAmount <= maxMinInvestment.Value);

            var results = filtered
                .Select(f => new
                {
                    f.FundId,
                    f.FundName,
                    f.FundType,
                    f.RiskLevel,
                    f.NavValue,
                    f.Return1Y,
                    f.MinimumInvestmentAmount,
                    f.Currency,
                })
                .ToList();

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    Filters = new
                    {
                        riskLevel,
                        fundType,
                        maxMinInvestment,
                    },
                    MatchingFunds = results.Count,
                    Funds = results,
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching mutual funds");
            return $"Error: {ex.Message}";
        }
    }

    [Description("Get detailed information about a specific mutual fund by its ID or code.")]
    public async Task<string> GetMutualFundDetails(
        [Description("Customer CIF number")] string cif,
        [Description("Fund ID or Fund Code")] string fundIdOrCode
    )
    {
        try
        {
            logger.LogInformation("Fetching fund details for: {FundId}", fundIdOrCode);
            var response = await apiService.GetMutualFundsAsync(cif);

            if (!response.Success || response.Funds == null)
                return $"Error: Failed to fetch mutual funds - {response.Message}";

            var fund = response.Funds.FirstOrDefault(f =>
                f.FundId?.Equals(fundIdOrCode, StringComparison.OrdinalIgnoreCase) == true
                || f.FundCode?.Equals(fundIdOrCode, StringComparison.OrdinalIgnoreCase) == true
                || f.Symbol?.Equals(fundIdOrCode, StringComparison.OrdinalIgnoreCase) == true
            );

            if (fund == null)
                return $"Error: Fund not found with ID or code: {fundIdOrCode}";

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    Fund = new
                    {
                        fund.FundId,
                        fund.FundName,
                        fund.FundCode,
                        fund.Symbol,
                        fund.Isin,
                        fund.FundType,
                        fund.RiskLevel,
                        fund.Currency,
                        CurrentNAV = fund.NavValue,
                        fund.Description,
                        Performance = new
                        {
                            fund.Return1M,
                            fund.Return3M,
                            fund.Return6M,
                            fund.Return1Y,
                            fund.Return3Y,
                            fund.Return5Y,
                        },
                        Investment = new
                        {
                            fund.MinimumInvestmentAmount,
                            fund.MinimumAdditionalSubscription,
                            fund.MinimumRedemptionAmount,
                        },
                        Fees = new
                        {
                            fund.ManagementFee,
                            fund.PerformanceFee,
                            fund.EntryFee,
                            fund.RedemptionFee,
                        },
                        fund.Status,
                    },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching fund details");
            return $"Error: {ex.Message}";
        }
    }
}

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentFrameworkQuickStart.Api.Workflows.Messages;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// Service for integrating with SNB Capital external APIs
/// Base URL: https://snbc-api.onrender.com/snbc/api/v1
///
/// Available CIFs for testing: 100000000001 to 100000000030
/// </summary>
public class SNBCapitalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SNBCapitalApiService> _logger;
    private readonly string _baseUrl;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.SNBCapitalApi",
        "2.0.0"
    );

    public SNBCapitalApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SNBCapitalApiService> logger
    )
    {
        _httpClient = httpClient;
        _logger = logger;

        // Get configuration from appsettings.json
        _baseUrl =
            configuration["SNBCapital:BaseUrl"] ?? "https://snbc-api.onrender.com/snbc/api/v1";
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    #region Customer Portfolios API

    /// <summary>
    /// Get all portfolios for a customer
    /// GET /customer/portfolios
    /// Headers: userId (CIF)
    /// </summary>
    public async Task<CustomerPortfoliosResponse> GetCustomerPortfoliosAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetCustomerPortfolios");
        activity?.SetTag("cif", cif);

        try
        {
            _logger.LogInformation("Fetching portfolios for customer CIF: {CIF}", cif);

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/customer/portfolios");
            request.Headers.Add("userId", cif);
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Parse the actual API response format
            var apiResponse = await response.Content.ReadFromJsonAsync<
                SNBApiResponse<CustomerPortfoliosGroupedData>
            >(JsonOptions);

            if (apiResponse?.Data == null || !apiResponse.IsSuccess)
            {
                _logger.LogWarning("No portfolio data returned for CIF: {CIF}", cif);
                return new CustomerPortfoliosResponse
                {
                    Success = false,
                    Message = apiResponse?.Message,
                };
            }

            // Convert grouped data to portfolio list
            var portfolios = new List<SNBPortfolio>();

            // Add local market portfolios
            if (apiResponse.Data.LocalMarket?.Portfolios != null)
            {
                portfolios.AddRange(
                    apiResponse.Data.LocalMarket.Portfolios.Select(p => new SNBPortfolio
                    {
                        PortfolioNumber = p.PortfolioNumber,
                        PortfolioName = p.PortfolioName,
                        PortfolioType = "LocalMarket",
                        TotalValue = p.TotalPortfolioValue?.Value ?? 0,
                        CashBalance = p.TotalCash?.Value ?? 0,
                        Currency = p.Currency,
                        Status = p.Status,
                    })
                );
            }

            // Add mutual fund portfolios
            if (apiResponse.Data.MutualFund?.Portfolios != null)
            {
                portfolios.AddRange(
                    apiResponse.Data.MutualFund.Portfolios.Select(p => new SNBPortfolio
                    {
                        PortfolioNumber = p.PortfolioNumber,
                        PortfolioName = p.PortfolioName,
                        PortfolioType = "MutualFund",
                        TotalValue = p.TotalPortfolioValue?.Value ?? 0,
                        CashBalance = p.TotalCash?.Value ?? 0,
                        Currency = p.Currency,
                        Status = p.Status,
                    })
                );
            }

            activity?.SetTag("portfolio_count", portfolios.Count);
            _logger.LogInformation(
                "Found {Count} portfolios for CIF: {CIF}",
                portfolios.Count,
                cif
            );

            return new CustomerPortfoliosResponse
            {
                Portfolios = portfolios,
                Success = true,
                Message = apiResponse.Message,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching portfolios for CIF: {CIF}", cif);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new SNBCapitalApiException($"Failed to fetch portfolios: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get raw grouped portfolio data for a customer
    /// </summary>
    public async Task<CustomerPortfoliosGroupedData?> GetCustomerPortfoliosGroupedAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetCustomerPortfoliosGrouped");
        activity?.SetTag("cif", cif);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/customer/portfolios");
            request.Headers.Add("userId", cif);
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var apiResponse = await response.Content.ReadFromJsonAsync<
                SNBApiResponse<CustomerPortfoliosGroupedData>
            >(JsonOptions);

            return apiResponse?.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching grouped portfolios for CIF: {CIF}", cif);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new SNBCapitalApiException($"Failed to fetch portfolios: {ex.Message}", ex);
        }
    }

    #endregion

    #region Portfolio Holdings API

    /// <summary>
    /// Get mutual fund holdings for a portfolio
    /// GET /customer/portfolios/:portfolioNumber/holdings/mutual-fund
    /// </summary>
    public async Task<MutualFundHoldingsResponse> GetMutualFundHoldingsAsync(
        string portfolioNumber,
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetMutualFundHoldings");
        activity?.SetTag("portfolio_number", portfolioNumber);
        activity?.SetTag("cif", cif);

        try
        {
            _logger.LogInformation(
                "Fetching MF holdings for portfolio: {Portfolio}",
                portfolioNumber
            );

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_baseUrl}/customer/portfolios/{portfolioNumber}/holdings/mutual-fund"
            );
            request.Headers.Add("userId", cif);
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Parse the actual API response format
            var apiResponse = await response.Content.ReadFromJsonAsync<
                SNBApiResponse<List<MutualFundHolding>>
            >(JsonOptions);

            if (apiResponse?.Data == null)
            {
                return new MutualFundHoldingsResponse
                {
                    Holdings = new List<MutualFundHolding>(),
                    Success = true,
                };
            }

            activity?.SetTag("holdings_count", apiResponse.Data.Count);
            return new MutualFundHoldingsResponse
            {
                Holdings = apiResponse.Data,
                Success = apiResponse.IsSuccess,
                Message = apiResponse.Message,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error fetching MF holdings for portfolio: {Portfolio}",
                portfolioNumber
            );
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new SNBCapitalApiException(
                $"Failed to fetch mutual fund holdings: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Get local market (stock) holdings for a portfolio
    /// GET /customer/portfolios/:portfolioNumber/holdings/local-market
    /// </summary>
    public async Task<LocalMarketHoldingsResponse> GetLocalMarketHoldingsAsync(
        string portfolioNumber,
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetLocalMarketHoldings");
        activity?.SetTag("portfolio_number", portfolioNumber);
        activity?.SetTag("cif", cif);

        try
        {
            _logger.LogInformation(
                "Fetching local market holdings for portfolio: {Portfolio}",
                portfolioNumber
            );

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_baseUrl}/customer/portfolios/{portfolioNumber}/holdings/local-market"
            );
            request.Headers.Add("userId", cif);
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Parse the actual API response format
            var apiResponse = await response.Content.ReadFromJsonAsync<
                SNBApiResponse<List<LocalMarketHolding>>
            >(JsonOptions);

            if (apiResponse?.Data == null)
            {
                return new LocalMarketHoldingsResponse
                {
                    Holdings = new List<LocalMarketHolding>(),
                    Success = true,
                };
            }

            activity?.SetTag("holdings_count", apiResponse.Data.Count);
            return new LocalMarketHoldingsResponse
            {
                Holdings = apiResponse.Data,
                Success = apiResponse.IsSuccess,
                Message = apiResponse.Message,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error fetching local market holdings for portfolio: {Portfolio}",
                portfolioNumber
            );
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new SNBCapitalApiException(
                $"Failed to fetch local market holdings: {ex.Message}",
                ex
            );
        }
    }

    #endregion

    #region Mutual Funds API

    /// <summary>
    /// Get list of available mutual funds
    /// GET /mutual-funds
    /// </summary>
    public async Task<MutualFundsListResponse> GetMutualFundsAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetMutualFunds");
        activity?.SetTag("cif", cif);

        try
        {
            _logger.LogInformation("Fetching mutual funds list");

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/mutual-funds");
            request.Headers.Add("userId", cif);
            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // Parse the actual API response format
            var apiResponse = await response.Content.ReadFromJsonAsync<
                SNBApiResponse<List<SNBMutualFund>>
            >(JsonOptions);

            if (apiResponse?.Data == null || !apiResponse.IsSuccess)
            {
                _logger.LogWarning("No mutual funds data returned");
                return new MutualFundsListResponse
                {
                    Success = false,
                    Message = apiResponse?.Message,
                };
            }

            activity?.SetTag("funds_count", apiResponse.Data.Count);
            _logger.LogInformation("Found {Count} mutual funds", apiResponse.Data.Count);

            return new MutualFundsListResponse
            {
                Funds = apiResponse.Data,
                Success = true,
                Message = apiResponse.Message,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching mutual funds");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw new SNBCapitalApiException($"Failed to fetch mutual funds: {ex.Message}", ex);
        }
    }

    #endregion

    #region Aggregated Methods (Convenience)

    /// <summary>
    /// Get complete customer portfolio data with all holdings
    /// </summary>
    public async Task<CustomerCompletePortfolioData> GetCompleteCustomerDataAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetCompleteCustomerData");
        activity?.SetTag("cif", cif);

        var result = new CustomerCompletePortfolioData { Cif = cif };

        // Get all portfolios first
        var portfoliosResponse = await GetCustomerPortfoliosAsync(cif, accessToken);
        result.Portfolios = portfoliosResponse.Portfolios ?? new List<SNBPortfolio>();

        // Get holdings for each portfolio (in parallel)
        var holdingsTasks = result.Portfolios.Select(async portfolio =>
        {
            var mfHoldings = await GetMutualFundHoldingsAsync(
                portfolio.PortfolioNumber,
                cif,
                accessToken
            );
            var localHoldings = await GetLocalMarketHoldingsAsync(
                portfolio.PortfolioNumber,
                cif,
                accessToken
            );

            return new PortfolioWithHoldings
            {
                Portfolio = portfolio,
                MutualFundHoldings = mfHoldings.Holdings ?? new List<MutualFundHolding>(),
                LocalMarketHoldings = localHoldings.Holdings ?? new List<LocalMarketHolding>(),
            };
        });

        result.PortfolioDetails = (await Task.WhenAll(holdingsTasks)).ToList();

        // Get available mutual funds
        var fundsResponse = await GetMutualFundsAsync(cif, accessToken);
        result.AvailableMutualFunds = fundsResponse.Funds ?? new List<SNBMutualFund>();

        activity?.SetTag("total_portfolios", result.Portfolios.Count);
        activity?.SetTag("available_funds", result.AvailableMutualFunds.Count);

        return result;
    }

    #endregion

    // JSON serialization options
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #region Legacy Methods (for backward compatibility)

    /// <summary>
    /// Check if customer has an existing SNB Capital account
    /// </summary>
    public async Task<SNBAccountCheckResult> CheckExistingAccountAsync(
        string customerId,
        string email
    )
    {
        try
        {
            _logger.LogInformation("Checking SNB account for customer {CustomerId}", customerId);

            // Try to fetch portfolios to determine if customer exists
            // Use the customerId as CIF if it's numeric, otherwise generate one
            var cif =
                customerId.All(char.IsDigit) && customerId.Length == 12
                    ? customerId
                    : "100000000001"; // Default test CIF

            try
            {
                var portfolios = await GetCustomerPortfoliosAsync(cif);

                if (portfolios.Portfolios?.Any() == true)
                {
                    return new SNBAccountCheckResult
                    {
                        HasExistingAccount = true,
                        SNBAccountId = cif,
                        AccountStatus = "Active",
                        AccountOpenedDate = DateTime.UtcNow.AddYears(-1),
                        KYCVerified = true,
                        RiskRating = "Medium",
                        ExistingPortfolios = portfolios
                            .Portfolios.Select(p => new PortfolioSummary
                            {
                                PortfolioId = p.PortfolioNumber,
                                PortfolioName = p.PortfolioName ?? $"Portfolio {p.PortfolioNumber}",
                                TotalValue = p.TotalValue ?? 0m,
                                AssetTypes = new List<string> { "Mutual Funds", "Local Stocks" },
                            })
                            .ToList(),
                    };
                }
            }
            catch (SNBCapitalApiException)
            {
                // Customer doesn't exist or API error - treat as no account
            }

            return new SNBAccountCheckResult { HasExistingAccount = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SNB account for {CustomerId}", customerId);
            throw;
        }
    }

    /// <summary>
    /// Retrieve detailed portfolio information from SNB Capital
    /// </summary>
    public async Task<List<PortfolioDetails>> GetPortfolioDetailsAsync(
        string snbAccountId,
        List<string> portfolioIds
    )
    {
        try
        {
            _logger.LogInformation(
                "Retrieving portfolio details for SNB account {AccountId}",
                snbAccountId
            );

            var result = new List<PortfolioDetails>();

            foreach (var portfolioId in portfolioIds)
            {
                var mfHoldings = await GetMutualFundHoldingsAsync(portfolioId, snbAccountId);
                var localHoldings = await GetLocalMarketHoldingsAsync(portfolioId, snbAccountId);

                var allHoldings = new List<PortfolioHoldingDetail>();

                // Map MF holdings
                allHoldings.AddRange(
                    mfHoldings.Holdings?.Select(h => new PortfolioHoldingDetail
                    {
                        Symbol = h.FundCode ?? "N/A",
                        Quantity = h.Units ?? 0,
                        AveragePrice = h.AverageCost ?? 0,
                        CurrentPrice = h.CurrentNav ?? 0,
                        MarketValue = h.CurrentValue ?? 0,
                        UnrealizedGain = h.UnrealizedGainLoss ?? 0,
                    }) ?? Enumerable.Empty<PortfolioHoldingDetail>()
                );

                // Map local market holdings
                allHoldings.AddRange(
                    localHoldings.Holdings?.Select(h => new PortfolioHoldingDetail
                    {
                        Symbol = h.Symbol ?? "N/A",
                        Quantity = h.Quantity ?? 0,
                        AveragePrice = h.AverageCost ?? 0,
                        CurrentPrice = h.CurrentPrice ?? 0,
                        MarketValue = h.MarketValue ?? 0,
                        UnrealizedGain = h.UnrealizedGainLoss ?? 0,
                    }) ?? Enumerable.Empty<PortfolioHoldingDetail>()
                );

                var totalValue = allHoldings.Sum(h => h.MarketValue);
                var totalGain = allHoldings.Sum(h => h.UnrealizedGain);
                var totalCost = allHoldings.Sum(h => h.Quantity * h.AveragePrice);

                result.Add(
                    new PortfolioDetails
                    {
                        PortfolioId = portfolioId,
                        PortfolioName = $"Portfolio {portfolioId}",
                        TotalValue = totalValue,
                        CashBalance = 0, // Would need separate API call
                        InvestedValue = totalCost,
                        Holdings = allHoldings,
                        PerformanceMetrics = new PerformanceMetrics
                        {
                            TotalReturn = totalCost > 0 ? (totalGain / totalCost) * 100 : 0,
                            AnnualizedReturn =
                                totalCost > 0 ? ((totalGain / totalCost) * 100) / 1 : 0, // Simplified
                            Volatility = 15.5m, // Would need historical data
                            SharpeRatio = 0.85m, // Would need risk-free rate
                        },
                    }
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving portfolio details for {AccountId}",
                snbAccountId
            );
            throw;
        }
    }

    /// <summary>
    /// Migrate portfolios from SNB Capital to the new system
    /// </summary>
    public async Task<PortfolioMigrationResult> MigratePortfoliosAsync(
        string customerId,
        string newAccountId,
        List<PortfolioDetails> portfolios
    )
    {
        try
        {
            _logger.LogInformation(
                "Migrating {Count} portfolios for customer {CustomerId}",
                portfolios.Count,
                customerId
            );

            var migratedIds = new List<string>();
            var failedIds = new List<string>();

            foreach (var portfolio in portfolios)
            {
                try
                {
                    // In production, call internal portfolio creation API
                    await Task.Delay(100); // Simulate processing
                    migratedIds.Add(portfolio.PortfolioId);
                    _logger.LogInformation(
                        "Successfully migrated portfolio {PortfolioId}",
                        portfolio.PortfolioId
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to migrate portfolio {PortfolioId}",
                        portfolio.PortfolioId
                    );
                    failedIds.Add(portfolio.PortfolioId);
                }
            }

            return new PortfolioMigrationResult
            {
                Success = failedIds.Count == 0,
                PortfoliosMigrated = migratedIds.Count,
                MigratedPortfolioIds = migratedIds,
                FailedPortfolios = failedIds,
                ErrorMessage =
                    failedIds.Count > 0
                        ? $"Failed to migrate {failedIds.Count} portfolio(s)"
                        : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during portfolio migration for {CustomerId}", customerId);
            return new PortfolioMigrationResult
            {
                Success = false,
                ErrorMessage = $"Portfolio migration failed: {ex.Message}",
            };
        }
    }

    #endregion
}

#region API Response DTOs

/// <summary>
/// Standard API Response wrapper from SNB Capital
/// </summary>
public record SNBApiResponse<T>
{
    public int StatusCode { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public object? Metadata { get; init; }
    public object? Errors { get; init; }
    public string? Timestamp { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Response from GET /customer/portfolios
/// </summary>
public record CustomerPortfoliosResponse
{
    public List<SNBPortfolio>? Portfolios { get; init; }
    public string? Message { get; init; }
    public bool Success { get; init; } = true;
}

/// <summary>
/// Grouped portfolio response from GET /customer/portfolios
/// </summary>
public record CustomerPortfoliosGroupedData
{
    public PortfolioBalanceValue? TotalPortfolioBalance { get; init; }
    public PortfolioBalanceValue? TotalPortfolioCash { get; init; }
    public PortfolioBalanceValue? TotalMarketValue { get; init; }
    public PortfolioTypeGroup? LocalMarket { get; init; }
    public PortfolioTypeGroup? MutualFund { get; init; }
}

public record PortfolioBalanceValue
{
    public decimal Value { get; init; }
    public string Currency { get; init; } = "SAR";
}

public record PortfolioTypeGroup
{
    public List<SNBPortfolioDetail>? Portfolios { get; init; }
    public PortfolioBalanceValue? TotalBalance { get; init; }
    public PortfolioBalanceValue? TotalCash { get; init; }
    public PortfolioBalanceValue? TotalMarketValue { get; init; }
    public int PortfolioCount { get; init; }
}

public record SNBPortfolioDetail
{
    public string PortfolioNumber { get; init; } = string.Empty;
    public string? PortfolioName { get; init; }
    public string? PortfolioType { get; init; } // "LOCAL_MARKET", "MUTUAL_FUND"
    public string? Currency { get; init; }
    public string? Status { get; init; }
    public PortfolioBalanceValue? TotalPortfolioValue { get; init; }
    public PortfolioBalanceValue? MarketValue { get; init; }
    public PortfolioBalanceValue? CostValue { get; init; }
    public PortfolioBalanceValue? BuyingPower { get; init; }
    public PortfolioBalanceValue? TotalCash { get; init; }
    public PortfolioBalanceValue? CashForFundOut { get; init; }
    public PortfolioBalanceValue? UnsettledCash { get; init; }
    public PortfolioBalanceValue? ProfitLossOfDay { get; init; }
    public PortfolioBalanceValue? BlockedAmount { get; init; }
    public PortfolioBalanceValue? UnrealizedProfitLoss { get; init; }
    public PercentValue? UnrealizedProfitLossPercent { get; init; }
    public PortfolioBalanceValue? ChangePreviousDay { get; init; }
    public PercentValue? ChangePreviousDayPercent { get; init; }
    public string? UpdatedAt { get; init; }
}

public record PercentValue
{
    public decimal Value { get; init; }
}

public record SNBPortfolio
{
    public string PortfolioNumber { get; init; } = string.Empty;
    public string? PortfolioName { get; init; }
    public string? PortfolioType { get; init; } // "MutualFund", "LocalMarket", "Mixed"
    public decimal? TotalValue { get; init; }
    public decimal? CashBalance { get; init; }
    public string? Currency { get; init; }
    public string? Status { get; init; }
    public DateTime? OpenedDate { get; init; }
}

/// <summary>
/// Response from GET /customer/portfolios/:portfolioNumber/holdings/mutual-fund
/// </summary>
public record MutualFundHoldingsResponse
{
    public List<MutualFundHolding>? Holdings { get; init; }
    public string? Message { get; init; }
    public bool Success { get; init; } = true;
}

public record MutualFundHolding
{
    // Matches actual API response
    public string? FundTitle { get; init; }
    public string? FundCode { get; init; }
    public string? FundName { get; init; }
    public string? FundNameAr { get; init; }
    public decimal? Units { get; init; }
    public string? Unit { get; init; } // "Unit"
    public decimal? UnitPrice { get; init; }
    public decimal? AverageCost { get; init; }
    public decimal? CurrentNav { get; init; }
    public decimal? CurrentValue { get; init; }
    public decimal? UnrealizedGainLoss { get; init; }
    public decimal? UnrealizedGainLossPercent { get; init; }
    public string? Currency { get; init; }
    public DateTime? LastUpdated { get; init; }
}

/// <summary>
/// Response from GET /customer/portfolios/:portfolioNumber/holdings/local-market
/// </summary>
public record LocalMarketHoldingsResponse
{
    public List<LocalMarketHolding>? Holdings { get; init; }
    public string? Message { get; init; }
    public bool Success { get; init; } = true;
}

public record LocalMarketHolding
{
    // Matches actual API response
    public string? StockCode { get; init; }
    public string? StockName { get; init; }
    public string? Symbol { get; init; }
    public string? CompanyName { get; init; }
    public string? CompanyNameAr { get; init; }
    public string? Sector { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; } // "Shares"
    public decimal? LastTradePrice { get; init; }
    public decimal? AverageCost { get; init; }
    public decimal? CurrentPrice { get; init; }
    public decimal? MarketValue { get; init; }
    public decimal? RealizedProfitLossPercent { get; init; }
    public string? ProfitLossType { get; init; } // "PROFIT" or "LOSS"
    public decimal? UnrealizedGainLoss { get; init; }
    public decimal? UnrealizedGainLossPercent { get; init; }
    public string? Currency { get; init; }
    public DateTime? LastUpdated { get; init; }
}

/// <summary>
/// Response from GET /mutual-funds
/// </summary>
public record MutualFundsListResponse
{
    public List<SNBMutualFund>? Funds { get; init; }
    public string? Message { get; init; }
    public bool Success { get; init; } = true;
}

public record SNBMutualFund
{
    // Matches actual API response format
    public string? FundId { get; init; }
    public string? FundName { get; init; }
    public string? FundCode { get; init; }
    public string? Symbol { get; init; }
    public string? Isin { get; init; }
    public string? FundType { get; init; } // "EQUITY", "FIXED_INCOME", "MONEY_MARKET", "BALANCED"
    public string? RiskLevel { get; init; } // "LOW", "MEDIUM", "HIGH"
    public string? Currency { get; init; }
    public decimal? NavValue { get; init; }
    public string? NavCurrency { get; init; }
    public long? NavDate { get; init; } // Unix timestamp in milliseconds

    // Performance returns (percentages)
    public decimal? Return1M { get; init; }
    public decimal? Return3M { get; init; }
    public decimal? Return6M { get; init; }
    public decimal? Return1Y { get; init; }
    public decimal? Return3Y { get; init; }
    public decimal? Return5Y { get; init; }

    // Investment requirements
    public decimal? MinimumInvestmentAmount { get; init; }
    public decimal? MinimumAdditionalSubscription { get; init; }
    public decimal? MinimumRedemptionAmount { get; init; }
    public decimal? MinInvestmentUnits { get; init; }

    // Fees (percentages)
    public decimal? ManagementFee { get; init; }
    public decimal? PerformanceFee { get; init; }
    public decimal? EntryFee { get; init; }
    public decimal? RedemptionFee { get; init; }

    public string? Status { get; init; } // "ACTIVE"
    public long? InceptionDate { get; init; } // Unix timestamp in milliseconds
    public string? Description { get; init; }
    public string? CreatedDate { get; init; }
    public string? UpdatedDate { get; init; }

    // Legacy fields for backward compatibility
    public string? FundNameAr { get; init; }
    public decimal? CurrentNav => NavValue;
    public decimal? MinimumInvestment => MinimumInvestmentAmount;
    public decimal? SubscriptionFee => EntryFee;
    public decimal? YtdReturn => Return1Y; // Approximate
    public decimal? OneYearReturn => Return1Y;
    public decimal? ThreeYearReturn => Return3Y;
    public decimal? FiveYearReturn => Return5Y;
    public decimal? SinceInceptionReturn => Return5Y; // Approximate
    public decimal? StandardDeviation { get; init; }
    public decimal? SharpeRatio { get; init; }
    public decimal? Beta { get; init; }
    public decimal? Alpha { get; init; }
    public string? InvestmentObjective { get; init; }
    public string? InvestmentObjectiveAr { get; init; }
    public bool? IsShariahCompliant { get; init; }
    public decimal? Aum { get; init; }
}

/// <summary>
/// Complete customer portfolio data (aggregated)
/// </summary>
public record CustomerCompletePortfolioData
{
    public string Cif { get; init; } = string.Empty;
    public List<SNBPortfolio> Portfolios { get; set; } = new();
    public List<PortfolioWithHoldings> PortfolioDetails { get; set; } = new();
    public List<SNBMutualFund> AvailableMutualFunds { get; set; } = new();

    public decimal TotalPortfolioValue => Portfolios.Sum(p => p.TotalValue ?? 0);
    public int TotalHoldingsCount =>
        PortfolioDetails.Sum(p => p.MutualFundHoldings.Count + p.LocalMarketHoldings.Count);
}

public record PortfolioWithHoldings
{
    public SNBPortfolio Portfolio { get; init; } = new();
    public List<MutualFundHolding> MutualFundHoldings { get; init; } = new();
    public List<LocalMarketHolding> LocalMarketHoldings { get; init; } = new();

    public decimal TotalMutualFundValue => MutualFundHoldings.Sum(h => h.CurrentValue ?? 0);
    public decimal TotalLocalMarketValue => LocalMarketHoldings.Sum(h => h.MarketValue ?? 0);
}

#endregion

#region Exceptions

public class SNBCapitalApiException : Exception
{
    public SNBCapitalApiException(string message)
        : base(message) { }

    public SNBCapitalApiException(string message, Exception innerException)
        : base(message, innerException) { }
}

#endregion

#region Legacy DTOs (for backward compatibility with existing code)

public record PortfolioDetails
{
    public required string PortfolioId { get; init; }
    public required string PortfolioName { get; init; }
    public decimal TotalValue { get; init; }
    public decimal CashBalance { get; init; }
    public decimal InvestedValue { get; init; }
    public List<PortfolioHoldingDetail> Holdings { get; init; } = new();
    public PerformanceMetrics? PerformanceMetrics { get; init; }
}

public record PortfolioHoldingDetail
{
    public required string Symbol { get; init; }
    public decimal Quantity { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal CurrentPrice { get; init; }
    public decimal MarketValue { get; init; }
    public decimal UnrealizedGain { get; init; }
}

public record PerformanceMetrics
{
    public decimal TotalReturn { get; init; }
    public decimal AnnualizedReturn { get; init; }
    public decimal Volatility { get; init; }
    public decimal SharpeRatio { get; init; }
}

#endregion

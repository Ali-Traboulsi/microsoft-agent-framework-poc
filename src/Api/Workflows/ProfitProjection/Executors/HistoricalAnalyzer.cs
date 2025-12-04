using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Analyzes historical performance of mutual funds for projection scenarios
/// Uses SNB Capital API to fetch real fund data
/// </summary>
public class HistoricalAnalyzer
{
    private readonly SNBCapitalApiService _snbCapitalApi;
    private readonly ILogger<HistoricalAnalyzer> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.HistoricalAnalyzer",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.HistoricalAnalyzer",
        "1.0.0"
    );
    private static readonly Counter<int> FundsAnalyzedCounter = Meter.CreateCounter<int>(
        "funds_analyzed",
        "funds",
        "Number of funds analyzed"
    );

    public HistoricalAnalyzer(
        SNBCapitalApiService snbCapitalApi,
        ILogger<HistoricalAnalyzer> logger
    )
    {
        _snbCapitalApi = snbCapitalApi;
        _logger = logger;
    }

    /// <summary>
    /// Execute historical analysis for funds matching the request criteria
    /// </summary>
    public async Task<HistoricalAnalysis> ExecuteAsync(
        ProjectionRequest request,
        string? cif = null
    )
    {
        using var activity = ActivitySource.StartActivity("HistoricalAnalysis");
        activity?.SetTag("risk_profile", request.RiskProfile);
        activity?.SetTag("shariah_compliant", request.ShariahCompliantOnly);

        try
        {
            _logger.LogInformation(
                "Starting historical analysis for {RiskProfile} profile, ShariahOnly={ShariahOnly}",
                request.RiskProfile,
                request.ShariahCompliantOnly
            );

            // Use provided CIF or default test CIF
            var effectiveCif = cif ?? "100000000001";

            // Fetch available mutual funds from SNB Capital API
            var fundsResponse = await _snbCapitalApi.GetMutualFundsAsync(effectiveCif);
            var funds = fundsResponse.Funds ?? new List<SNBMutualFund>();

            _logger.LogInformation("Fetched {Count} funds from SNB Capital API", funds.Count);

            // Log Shariah-compliant fund count for debugging
            var shariahFundCount = funds.Count(f => f.IsShariahCompliant == true);
            _logger.LogInformation("Shariah-compliant funds available: {Count}", shariahFundCount);

            // Filter by risk profile and Shariah compliance if requested
            var filteredFunds = FilterFundsByRisk(
                funds,
                request.RiskProfile,
                request.ShariahCompliantOnly
            );

            // If Shariah filter resulted in no funds, log warning and try without Shariah filter
            if (!filteredFunds.Any() && request.ShariahCompliantOnly)
            {
                _logger.LogWarning(
                    "No Shariah-compliant funds found for {RiskProfile} profile. "
                        + "API may not have Shariah data. Proceeding with all funds.",
                    request.RiskProfile
                );
                filteredFunds = FilterFundsByRisk(funds, request.RiskProfile, shariahOnly: false);
            }

            // If specific fund codes requested, further filter
            if (request.TargetFundCodes?.Any() == true)
            {
                filteredFunds = filteredFunds
                    .Where(f =>
                        request.TargetFundCodes.Contains(
                            f.FundCode,
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    .ToList();
            }

            _logger.LogInformation("Analyzing {Count} funds after filtering", filteredFunds.Count);

            // Transform to historical data with calculated percentiles
            var fundPerformance = filteredFunds.Select(MapToHistoricalData).ToList();

            // Calculate risk category statistics
            var riskCategoryStats = CalculateRiskCategoryStats(fundPerformance);

            FundsAnalyzedCounter.Add(fundPerformance.Count);
            activity?.SetTag("funds_analyzed", fundPerformance.Count);

            return new HistoricalAnalysis
            {
                AnalyzedAt = DateTime.UtcNow,
                FundsAnalyzed = fundPerformance.Count,
                FundPerformance = fundPerformance,
                RiskCategoryStats = riskCategoryStats,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during historical analysis");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private List<SNBMutualFund> FilterFundsByRisk(
        List<SNBMutualFund> funds,
        string riskProfile,
        bool shariahOnly
    )
    {
        var filtered = funds.AsEnumerable();

        // Filter by Shariah compliance
        if (shariahOnly)
        {
            filtered = filtered.Where(f => f.IsShariahCompliant == true);
        }

        // Map risk profile to fund risk levels
        var acceptableRiskLevels = riskProfile.ToLower() switch
        {
            "conservative" => new[] { "Low", "Money Market" },
            "moderate" => new[] { "Low", "Medium", "Balanced" },
            "aggressive" => new[] { "Medium", "High", "Equity" },
            _ => new[] { "Low", "Medium", "High" }, // All
        };

        filtered = filtered.Where(f =>
            string.IsNullOrEmpty(f.RiskLevel)
            || acceptableRiskLevels.Any(r =>
                (f.RiskLevel?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
                || (f.FundType?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
            )
        );

        return filtered.ToList();
    }

    private FundHistoricalData MapToHistoricalData(SNBMutualFund fund)
    {
        // Use real return data from SNB Capital API
        // API provides: Return1M, Return3M, Return6M, Return1Y, Return3Y, Return5Y
        var oneYearReturn = fund.Return1Y ?? 0m;
        var threeYearReturn = fund.Return3Y ?? 0m;
        var fiveYearReturn = fund.Return5Y ?? 0m;

        // Calculate base return - prefer longer-term data for stability
        var baseReturn =
            oneYearReturn != 0 ? oneYearReturn : (fund.Return6M ?? fund.Return3M ?? 0m);

        // Estimate volatility from return spread if not provided
        // If fund has different time-horizon returns, use their variance
        var estimatedVolatility = EstimateVolatilityFromReturns(fund);

        // Estimate percentiles using normal distribution approximation
        // P10 ≈ mean - 1.28σ, P25 ≈ mean - 0.67σ, P75 ≈ mean + 0.67σ, P90 ≈ mean + 1.28σ
        var annualizedVolatility = estimatedVolatility / 100m; // Convert to decimal
        var p10 = baseReturn - (1.28m * annualizedVolatility * Math.Abs(baseReturn));
        var p25 = baseReturn - (0.67m * annualizedVolatility * Math.Abs(baseReturn));
        var p50 = baseReturn;
        var p75 = baseReturn + (0.67m * annualizedVolatility * Math.Abs(baseReturn));
        var p90 = baseReturn + (1.28m * annualizedVolatility * Math.Abs(baseReturn));

        return new FundHistoricalData
        {
            FundCode = fund.FundCode ?? fund.Symbol ?? "UNKNOWN",
            FundName = fund.FundName ?? "Unknown Fund",
            FundNameAr = fund.FundNameAr,
            FundType = fund.FundType ?? "Balanced",
            RiskLevel = fund.RiskLevel ?? "MEDIUM",
            IsShariahCompliant = fund.IsShariahCompliant ?? false,

            // Real returns from API
            YtdReturn = fund.Return1Y ?? 0,
            OneYearReturn = oneYearReturn,
            ThreeYearReturn = threeYearReturn,
            FiveYearReturn = fiveYearReturn,
            SinceInceptionReturn = fiveYearReturn, // Use 5Y as proxy

            // Risk metrics (estimated if not provided)
            StandardDeviation = estimatedVolatility,
            SharpeRatio = CalculateSharpeRatio(oneYearReturn, estimatedVolatility),
            Beta = 1m, // Assume market beta if not provided
            Alpha = 0m,
            MaxDrawdown = CalculateEstimatedMaxDrawdown(estimatedVolatility),

            // Percentile returns for scenario building
            P10Return = Math.Max(p10, -30m), // Floor at -30%
            P25Return = p25,
            P50Return = p50,
            P75Return = p75,
            P90Return = Math.Min(p90, 50m), // Cap at 50%

            // Real fees from API
            ManagementFee = fund.ManagementFee ?? 0m,
            TotalExpenseRatio = (fund.ManagementFee ?? 0m) + (fund.PerformanceFee ?? 0m) / 5,
        };
    }

    private decimal EstimateVolatilityFromReturns(SNBMutualFund fund)
    {
        // If we have returns at different time horizons, estimate volatility from spread
        var returns = new List<decimal>();
        if (fund.Return1M.HasValue)
            returns.Add(fund.Return1M.Value * 12); // Annualize
        if (fund.Return3M.HasValue)
            returns.Add(fund.Return3M.Value * 4);
        if (fund.Return6M.HasValue)
            returns.Add(fund.Return6M.Value * 2);
        if (fund.Return1Y.HasValue)
            returns.Add(fund.Return1Y.Value);

        if (returns.Count >= 2)
        {
            // Use standard deviation of annualized returns as volatility estimate
            var mean = returns.Average();
            var variance = returns.Sum(r => (r - mean) * (r - mean)) / returns.Count;
            return (decimal)Math.Sqrt((double)variance);
        }

        // Default volatility based on fund type and risk level
        return (fund.RiskLevel?.ToUpper(), fund.FundType?.ToUpper()) switch
        {
            ("LOW", _) => 5m,
            ("MEDIUM", _) => 12m,
            ("HIGH", _) => 20m,
            (_, "MONEY_MARKET") => 2m,
            (_, "FIXED_INCOME") => 5m,
            (_, "BALANCED") => 12m,
            (_, "EQUITY") => 18m,
            _ => 15m,
        };
    }

    private decimal CalculateSharpeRatio(decimal return1Y, decimal volatility)
    {
        // Assume risk-free rate of 5% (Saudi SAMA rate)
        const decimal riskFreeRate = 5m;
        if (volatility == 0)
            return 0;
        return (return1Y - riskFreeRate) / volatility;
    }

    private decimal CalculateEstimatedMaxDrawdown(decimal volatility)
    {
        // Rough estimate: max drawdown ≈ 2-3x volatility
        return Math.Min(volatility * 2.5m, 50m);
    }

    private Dictionary<string, RiskCategoryStats> CalculateRiskCategoryStats(
        List<FundHistoricalData> funds
    )
    {
        var stats = new Dictionary<string, RiskCategoryStats>();

        var riskGroups = funds.GroupBy(f => f.RiskLevel);

        foreach (var group in riskGroups)
        {
            var groupFunds = group.ToList();
            stats[group.Key] = new RiskCategoryStats
            {
                RiskCategory = group.Key,
                FundCount = groupFunds.Count,
                AverageReturn = groupFunds.Average(f => f.OneYearReturn),
                AverageVolatility = groupFunds.Average(f => f.StandardDeviation),
                AverageSharpeRatio = groupFunds.Average(f => f.SharpeRatio),
                BestReturn = groupFunds.Max(f => f.OneYearReturn),
                WorstReturn = groupFunds.Min(f => f.OneYearReturn),
            };
        }

        return stats;
    }
}

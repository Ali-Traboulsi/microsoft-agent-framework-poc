using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Selects and ranks funds based on customer's risk profile and investment goals
/// Provides recommended allocation across matched funds
/// </summary>
public class FundSelectionAnalyzer
{
    private readonly SNBCapitalApiService _snbCapitalApi;
    private readonly ILogger<FundSelectionAnalyzer> _logger;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.FundSelectionAnalyzer",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.FundSelectionAnalyzer",
        "1.0.0"
    );
    private static readonly Counter<int> FundsSelectedCounter = Meter.CreateCounter<int>(
        "funds_selected",
        "funds",
        "Number of funds selected"
    );

    public FundSelectionAnalyzer(
        SNBCapitalApiService snbCapitalApi,
        ILogger<FundSelectionAnalyzer> logger
    )
    {
        _snbCapitalApi = snbCapitalApi;
        _logger = logger;
    }

    /// <summary>
    /// Execute fund selection and ranking based on request criteria
    /// </summary>
    public async Task<FundSelectionResult> ExecuteAsync(
        ProjectionRequest request,
        HistoricalAnalysis? historicalData = null,
        string? cif = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundSelection");
        activity?.SetTag("risk_profile", request.RiskProfile);
        activity?.SetTag("investment_amount", request.InvestmentAmount);

        try
        {
            _logger.LogInformation(
                "Starting fund selection for {RiskProfile} profile with {Amount} {Currency}",
                request.RiskProfile,
                request.InvestmentAmount,
                request.Currency
            );

            var effectiveCif = cif ?? "100000000001";

            // Get funds either from historical analysis or fetch fresh
            List<FundHistoricalData> fundsToAnalyze;
            var fundNavMap = new Dictionary<string, decimal>(); // Map fundCode to NAV
            var fundMinInvestmentMap = new Dictionary<string, decimal>(); // Map to minimum investment

            if (historicalData?.FundPerformance?.Any() == true)
            {
                fundsToAnalyze = historicalData.FundPerformance;

                // Fetch NAV data from API for accurate values
                var fundsResponse = await _snbCapitalApi.GetMutualFundsAsync(effectiveCif);
                foreach (var fund in fundsResponse.Funds ?? new List<SNBMutualFund>())
                {
                    if (fund.FundCode != null)
                    {
                        fundNavMap[fund.FundCode] = fund.NavValue ?? 0;
                        fundMinInvestmentMap[fund.FundCode] = fund.MinimumInvestmentAmount ?? 0;
                    }
                }
            }
            else
            {
                // Fetch and convert from API
                var fundsResponse = await _snbCapitalApi.GetMutualFundsAsync(effectiveCif);
                fundsToAnalyze =
                    fundsResponse
                        .Funds?.Select(f =>
                        {
                            // Store NAV for later use
                            if (f.FundCode != null)
                            {
                                fundNavMap[f.FundCode] = f.NavValue ?? 0;
                                fundMinInvestmentMap[f.FundCode] = f.MinimumInvestmentAmount ?? 0;
                            }

                            return new FundHistoricalData
                            {
                                FundCode = f.FundCode ?? f.Symbol ?? "UNKNOWN",
                                FundName = f.FundName ?? "Unknown Fund",
                                FundNameAr = f.FundNameAr,
                                FundType = f.FundType ?? "Balanced",
                                RiskLevel = f.RiskLevel ?? "MEDIUM",
                                IsShariahCompliant = f.IsShariahCompliant ?? false,
                                // Use new API field names
                                OneYearReturn = f.Return1Y ?? 0,
                                ThreeYearReturn = f.Return3Y ?? 0,
                                FiveYearReturn = f.Return5Y ?? 0,
                                YtdReturn = f.Return1Y ?? 0,
                                SharpeRatio = 0.5m, // Will be calculated
                                StandardDeviation = 15m, // Will be estimated
                                ManagementFee = f.ManagementFee ?? 0,
                            };
                        })
                        .ToList() ?? new List<FundHistoricalData>();
            }

            // Score and rank funds with NAV data
            var rankedFunds = ScoreAndRankFunds(
                fundsToAnalyze,
                request,
                fundNavMap,
                fundMinInvestmentMap
            );

            // Generate recommended allocation
            var allocation = GenerateAllocation(rankedFunds, request);

            FundsSelectedCounter.Add(rankedFunds.Count);
            activity?.SetTag("funds_matched", rankedFunds.Count);

            return new FundSelectionResult
            {
                AnalyzedAt = DateTime.UtcNow,
                RiskProfileUsed = request.RiskProfile,
                FundsConsidered = fundsToAnalyze.Count,
                FundsMatched = rankedFunds.Count,
                RankedFunds = rankedFunds,
                RecommendedAllocation = allocation,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during fund selection");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private List<RankedFund> ScoreAndRankFunds(
        List<FundHistoricalData> funds,
        ProjectionRequest request,
        Dictionary<string, decimal> fundNavMap,
        Dictionary<string, decimal> fundMinInvestmentMap
    )
    {
        var scoredFunds =
            new List<(FundHistoricalData Fund, decimal Score, List<string> Reasons)>();

        foreach (var fund in funds)
        {
            var (score, reasons) = CalculateFundScore(fund, request);

            if (score > 0) // Only include funds with positive scores
            {
                scoredFunds.Add((fund, score, reasons));
            }
        }

        // If no funds matched (e.g., Shariah filter excluded all), try without Shariah filter
        if (!scoredFunds.Any() && request.ShariahCompliantOnly)
        {
            _logger.LogWarning(
                "No Shariah-compliant funds found in scoring. "
                    + "Re-scoring without Shariah requirement."
            );

            // Create a modified request without Shariah requirement
            var modifiedRequest = request with
            {
                ShariahCompliantOnly = false,
            };

            foreach (var fund in funds)
            {
                var (score, reasons) = CalculateFundScore(fund, modifiedRequest);

                if (score > 0)
                {
                    reasons.Add("Note: Shariah compliance could not be verified");
                    scoredFunds.Add((fund, score, reasons));
                }
            }
        }

        // Sort by score descending and take top funds
        var topFunds = scoredFunds
            .OrderByDescending(x => x.Score)
            .Take(10)
            .Select(
                (item, index) =>
                    new RankedFund
                    {
                        FundCode = item.Fund.FundCode,
                        FundName = item.Fund.FundName,
                        FundNameAr = item.Fund.FundNameAr,
                        FundType = item.Fund.FundType,
                        RiskLevel = item.Fund.RiskLevel,
                        Score = Math.Round(item.Score, 2),
                        Rank = index + 1,
                        MatchReasons = item.Reasons,
                        ExpectedReturn =
                            item.Fund.P50Return > 0 ? item.Fund.P50Return : item.Fund.OneYearReturn,
                        // Use real NAV from API
                        CurrentNav = fundNavMap.TryGetValue(item.Fund.FundCode, out var nav)
                            ? nav
                            : 0,
                        // Use real minimum investment from API
                        MinimumInvestment = fundMinInvestmentMap.TryGetValue(
                            item.Fund.FundCode,
                            out var min
                        )
                            ? min
                            : 0,
                    }
            )
            .ToList();

        return topFunds;
    }

    private (decimal Score, List<string> Reasons) CalculateFundScore(
        FundHistoricalData fund,
        ProjectionRequest request
    )
    {
        decimal score = 0;
        var reasons = new List<string>();

        // 1. Risk Profile Match (30 points max)
        var riskScore = CalculateRiskMatchScore(fund.RiskLevel, request.RiskProfile);
        score += riskScore;
        if (riskScore >= 25)
            reasons.Add($"Excellent risk match for {request.RiskProfile} profile");
        else if (riskScore >= 15)
            reasons.Add($"Good risk match for {request.RiskProfile} profile");

        // 2. Performance Score (25 points max)
        var perfScore = CalculatePerformanceScore(fund);
        score += perfScore;
        if (perfScore >= 20)
            reasons.Add($"Strong historical performance ({fund.OneYearReturn:F1}% 1Y return)");

        // 3. Risk-Adjusted Return - Sharpe Ratio (20 points max)
        var sharpeScore = Math.Min(fund.SharpeRatio * 20m, 20m);
        score += sharpeScore;
        if (sharpeScore >= 15)
            reasons.Add($"Excellent risk-adjusted returns (Sharpe: {fund.SharpeRatio:F2})");

        // 4. Shariah Compliance (if required) (15 points max)
        if (request.ShariahCompliantOnly)
        {
            if (fund.IsShariahCompliant)
            {
                score += 15;
                reasons.Add("Sharia-compliant investment");
            }
            else
            {
                return (0, new List<string>()); // Exclude non-compliant funds
            }
        }
        else if (fund.IsShariahCompliant)
        {
            score += 5; // Small bonus for Shariah-compliant funds
            reasons.Add("Sharia-compliant (optional)");
        }

        // 5. Time Horizon Match (10 points max)
        var horizonScore = CalculateHorizonMatchScore(fund.FundType, request.TimeHorizonMonths);
        score += horizonScore;
        if (horizonScore >= 8)
            reasons.Add($"Well-suited for {request.TimeHorizonMonths}-month horizon");

        return (score, reasons);
    }

    private decimal CalculateRiskMatchScore(string fundRisk, string profileRisk)
    {
        var riskMatrix = new Dictionary<(string, string), decimal>
        {
            // (Fund Risk, Profile Risk) => Score
            { ("Low", "Conservative"), 30m },
            { ("Low", "Moderate"), 20m },
            { ("Low", "Aggressive"), 10m },
            { ("Medium", "Conservative"), 15m },
            { ("Medium", "Moderate"), 30m },
            { ("Medium", "Aggressive"), 20m },
            { ("High", "Conservative"), 5m },
            { ("High", "Moderate"), 20m },
            { ("High", "Aggressive"), 30m },
            // Additional mappings for fund types
            { ("Money Market", "Conservative"), 30m },
            { ("Fixed Income", "Conservative"), 28m },
            { ("Balanced", "Moderate"), 30m },
            { ("Equity", "Aggressive"), 30m },
        };

        var key = (fundRisk, profileRisk);
        if (riskMatrix.TryGetValue(key, out var score))
            return score;

        // Default moderate match
        return 15m;
    }

    private decimal CalculatePerformanceScore(FundHistoricalData fund)
    {
        // Weight recent performance more
        var weightedReturn =
            (fund.YtdReturn * 0.3m)
            + (fund.OneYearReturn * 0.4m)
            + (fund.ThreeYearReturn * 0.2m)
            + (fund.FiveYearReturn * 0.1m);

        // Scale to 0-25 points
        return Math.Max(0, Math.Min(weightedReturn * 2.5m, 25m));
    }

    private decimal CalculateHorizonMatchScore(string fundType, int horizonMonths)
    {
        // Short-term (< 12 months): Money Market, Fixed Income
        // Medium-term (12-36 months): Balanced, Fixed Income
        // Long-term (> 36 months): Equity, Balanced

        return (fundType.ToLower(), horizonMonths) switch
        {
            (var t, var h) when t.Contains("money") && h <= 12 => 10m,
            (var t, var h) when t.Contains("money") && h > 12 => 5m,

            (var t, var h) when t.Contains("fixed") && h <= 24 => 10m,
            (var t, var h) when t.Contains("fixed") && h > 24 => 7m,

            (var t, var h) when t.Contains("balanced") && h >= 12 && h <= 60 => 10m,
            (var t, var h) when t.Contains("balanced") => 7m,

            (var t, var h) when t.Contains("equity") && h >= 36 => 10m,
            (var t, var h) when t.Contains("equity") && h >= 24 => 7m,
            (var t, var h) when t.Contains("equity") => 4m,

            _ => 6m, // Default moderate match
        };
    }

    private List<FundAllocation> GenerateAllocation(
        List<RankedFund> rankedFunds,
        ProjectionRequest request
    )
    {
        if (!rankedFunds.Any())
            return new List<FundAllocation>();

        // Determine allocation strategy based on risk profile
        var allocationStrategy = GetAllocationStrategy(request.RiskProfile);

        // Take top 3-5 funds for diversification
        var fundsToAllocate = rankedFunds.Take(Math.Min(5, rankedFunds.Count)).ToList();

        // Calculate weighted allocation based on scores
        var totalScore = fundsToAllocate.Sum(f => f.Score);

        var allocations = fundsToAllocate
            .Select(fund =>
            {
                var allocationPercent = (fund.Score / totalScore) * 100m;

                // Apply minimum/maximum constraints
                allocationPercent = Math.Max(10m, Math.Min(40m, allocationPercent));

                var investmentAmount = request.InvestmentAmount * (allocationPercent / 100m);

                return new FundAllocation
                {
                    FundCode = fund.FundCode,
                    FundName = fund.FundName,
                    AllocationPercent = Math.Round(allocationPercent, 1),
                    InvestmentAmount = Math.Round(investmentAmount, 2),
                    ExpectedContribution = Math.Round(
                        investmentAmount * (fund.ExpectedReturn / 100m),
                        2
                    ),
                };
            })
            .ToList();

        // Normalize to 100%
        var totalAllocation = allocations.Sum(a => a.AllocationPercent);
        if (totalAllocation != 100m && allocations.Any())
        {
            var adjustment = 100m / totalAllocation;
            allocations = allocations
                .Select(a =>
                    a with
                    {
                        AllocationPercent = Math.Round(a.AllocationPercent * adjustment, 1),
                        InvestmentAmount = Math.Round(a.InvestmentAmount * adjustment, 2),
                        ExpectedContribution = Math.Round(a.ExpectedContribution * adjustment, 2),
                    }
                )
                .ToList();
        }

        return allocations;
    }

    private (decimal Equity, decimal FixedIncome, decimal MoneyMarket) GetAllocationStrategy(
        string riskProfile
    )
    {
        return riskProfile.ToLower() switch
        {
            "conservative" => (20m, 50m, 30m),
            "moderate" => (50m, 35m, 15m),
            "aggressive" => (75m, 20m, 5m),
            _ => (40m, 40m, 20m),
        };
    }
}

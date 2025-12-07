using System.Diagnostics;
using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Input for fund selection
/// </summary>
public sealed record FundSelectionInput(
    ProjectionRequest Request,
    HistoricalAnalysis HistoricalData
);

/// <summary>
/// Selects and ranks funds based on risk profile and historical performance
/// </summary>
public sealed class FundSelectionAnalyzer : ExecutorBase<FundSelectionInput, FundSelectionResult>
{
    public FundSelectionAnalyzer(ILogger<FundSelectionAnalyzer> logger)
        : base(logger) { }

    protected override Task<FundSelectionResult> ExecuteCoreAsync(
        FundSelectionInput input,
        CancellationToken ct
    )
    {
        var request = input.Request;
        var funds = input.HistoricalData.FundPerformance;

        var rankedFunds = funds
            .Select(f => ScoreFund(f, request))
            .OrderByDescending(f => f.Score)
            .Select((f, i) => f with { Rank = i + 1 })
            .ToList();

        var topFunds = rankedFunds.Take(5).ToList();
        var allocation = CalculateAllocation(topFunds, request.InvestmentAmount);

        return Task.FromResult(
            new FundSelectionResult
            {
                RiskProfileUsed = request.NormalizedRiskProfile,
                FundsConsidered = funds.Count,
                FundsMatched = rankedFunds.Count,
                RankedFunds = rankedFunds,
                RecommendedAllocation = allocation,
            }
        );
    }

    protected override void AddOutputTags(Activity? activity, FundSelectionResult output)
    {
        activity?.SetTag("funds_matched", output.FundsMatched);
        activity?.SetTag("allocations_count", output.RecommendedAllocation.Count);
    }

    private static RankedFund ScoreFund(FundHistoricalData fund, ProjectionRequest request)
    {
        decimal score = 50;
        var reasons = new List<string>();

        // Risk matching (30 points max)
        var riskScore = CalculateRiskScore(fund, request.NormalizedRiskProfile);
        score += riskScore;
        if (riskScore > 20)
            reasons.Add($"Good risk match for {request.NormalizedRiskProfile} profile");

        // Performance (30 points max)
        var perfScore = Math.Min(30, fund.OneYearReturn);
        score += perfScore;
        if (fund.OneYearReturn > 8)
            reasons.Add($"Strong historical performance ({fund.OneYearReturn:F1}% 1Y return)");

        // Risk-adjusted returns (20 points max)
        var sharpeScore = Math.Min(20, fund.SharpeRatio * 10);
        score += sharpeScore;
        if (fund.SharpeRatio > 0.8m)
            reasons.Add($"Excellent risk-adjusted returns (Sharpe: {fund.SharpeRatio:F2})");

        // Time horizon suitability (10 points max)
        var horizonScore = CalculateHorizonScore(fund, request.TimeHorizonMonths);
        score += horizonScore;
        if (horizonScore > 5)
            reasons.Add($"Well-suited for {request.TimeHorizonMonths}-month horizon");

        return new RankedFund
        {
            FundCode = fund.FundCode,
            FundName = fund.FundName,
            FundNameAr = fund.FundNameAr,
            FundType = fund.FundType,
            RiskLevel = fund.RiskLevel,
            Score = Math.Min(100, score),
            MatchReasons = reasons,
            ExpectedReturn = fund.OneYearReturn,
            CurrentNav = 0,
            MinimumInvestment = 1000,
        };
    }

    private static decimal CalculateRiskScore(FundHistoricalData fund, string riskProfile)
    {
        var riskLevel = fund.RiskLevel.ToLower();
        return riskProfile switch
        {
            "Conservative" => riskLevel.Contains("low") ? 30
            : riskLevel.Contains("medium") ? 15
            : 5,
            "Moderate" => riskLevel.Contains("medium") || riskLevel.Contains("balanced") ? 30 : 15,
            "Aggressive" => riskLevel.Contains("high") || riskLevel.Contains("equity") ? 30 : 15,
            _ => 15,
        };
    }

    private static decimal CalculateHorizonScore(FundHistoricalData fund, int months)
    {
        // Longer horizons favor equity, shorter favor bonds/money market
        var isEquity = fund.FundType.Contains("Equity", StringComparison.OrdinalIgnoreCase);
        return months switch
        {
            < 12 => isEquity ? 3 : 10,
            < 36 => isEquity ? 7 : 8,
            _ => isEquity ? 10 : 5,
        };
    }

    private static List<FundAllocation> CalculateAllocation(
        List<RankedFund> funds,
        decimal totalAmount
    )
    {
        if (!funds.Any())
            return [];

        // Equal weight for simplicity - could be optimized
        var weight = 100m / funds.Count;
        var perFund = totalAmount / funds.Count;

        return funds
            .Select(f => new FundAllocation
            {
                FundCode = f.FundCode,
                FundName = f.FundName,
                AllocationPercent = weight,
                InvestmentAmount = perFund,
                ExpectedContribution = perFund * f.ExpectedReturn / 100,
            })
            .ToList();
    }
}

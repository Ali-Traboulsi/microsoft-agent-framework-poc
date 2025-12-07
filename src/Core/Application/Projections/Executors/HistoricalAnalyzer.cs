using System.Diagnostics;
using AgentFrameworkQuickStart.Core.Application.Common;
using AgentFrameworkQuickStart.Core.Domain.Projections;
using AgentFrameworkQuickStart.Infrastructure.ExternalApis;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Core.Application.Projections.Executors;

/// <summary>
/// Input for historical analysis
/// </summary>
public sealed record HistoricalAnalysisInput(ProjectionRequest Request, string Cif);

/// <summary>
/// Analyzes historical fund performance using SNB Capital API
/// </summary>
public sealed class HistoricalAnalyzer : ExecutorBase<HistoricalAnalysisInput, HistoricalAnalysis>
{
    private readonly ISNBCapitalApi _api;

    public HistoricalAnalyzer(ISNBCapitalApi api, ILogger<HistoricalAnalyzer> logger)
        : base(logger)
    {
        _api = api;
    }

    protected override async Task<HistoricalAnalysis> ExecuteCoreAsync(
        HistoricalAnalysisInput input,
        CancellationToken ct
    )
    {
        var request = input.Request;
        var funds = await _api.GetMutualFundsAsync(input.Cif);

        var filtered = FilterFunds(funds.Funds ?? [], request);
        var performance = filtered.Select(MapToHistoricalData).ToList();
        var stats = CalculateRiskCategoryStats(performance);

        return new HistoricalAnalysis
        {
            FundsAnalyzed = performance.Count,
            FundPerformance = performance,
            RiskCategoryStats = stats,
        };
    }

    protected override void AddInputTags(Activity? activity, HistoricalAnalysisInput input)
    {
        activity?.SetTag("risk_profile", input.Request.RiskProfile);
        activity?.SetTag("shariah_only", input.Request.ShariahCompliantOnly);
    }

    protected override void AddOutputTags(Activity? activity, HistoricalAnalysis output)
    {
        activity?.SetTag("funds_analyzed", output.FundsAnalyzed);
    }

    private List<SNBMutualFund> FilterFunds(List<SNBMutualFund> funds, ProjectionRequest request)
    {
        var filtered = funds.AsEnumerable();

        if (request.ShariahCompliantOnly)
            filtered = filtered.Where(f => f.IsShariahCompliant == true);

        var acceptableRisks = request.NormalizedRiskProfile switch
        {
            "Conservative" => new[] { "Low", "Money Market" },
            "Moderate" => new[] { "Low", "Medium", "Balanced" },
            "Aggressive" => new[] { "Medium", "High", "Equity" },
            _ => new[] { "Low", "Medium", "High" },
        };

        filtered = filtered.Where(f =>
            string.IsNullOrEmpty(f.RiskLevel)
            || acceptableRisks.Any(r =>
                (f.RiskLevel?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
                || (f.FundType?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
            )
        );

        if (request.TargetFundCodes?.Any() == true)
            filtered = filtered.Where(f =>
                request.TargetFundCodes.Contains(f.FundCode ?? "", StringComparer.OrdinalIgnoreCase)
            );

        var result = filtered.ToList();

        // Fallback: if Shariah filter resulted in no funds
        if (!result.Any() && request.ShariahCompliantOnly)
        {
            Logger.LogWarning("No Shariah-compliant funds found, using all matching funds");
            return FilterFunds(funds, request with { ShariahCompliantOnly = false });
        }

        return result;
    }

    private static FundHistoricalData MapToHistoricalData(SNBMutualFund fund)
    {
        var baseReturn = fund.Return1Y ?? fund.Return6M ?? fund.Return3M ?? 0m;
        var volatility = EstimateVolatility(fund);

        return new FundHistoricalData
        {
            FundCode = fund.FundCode ?? fund.Symbol ?? "UNKNOWN",
            FundName = fund.FundName ?? "Unknown Fund",
            FundNameAr = fund.FundNameAr,
            FundType = fund.FundType ?? "Balanced",
            RiskLevel = fund.RiskLevel ?? "MEDIUM",
            IsShariahCompliant = fund.IsShariahCompliant ?? false,
            YtdReturn = fund.YtdReturn ?? fund.Return1Y ?? 0,
            OneYearReturn = fund.Return1Y ?? 0,
            ThreeYearReturn = fund.Return3Y ?? 0,
            FiveYearReturn = fund.Return5Y ?? 0,
            SinceInceptionReturn = fund.SinceInceptionReturn ?? fund.Return5Y ?? 0,
            StandardDeviation = volatility,
            SharpeRatio = CalculateSharpeRatio(baseReturn, volatility),
            Beta = 1.0m,
            Alpha = 0,
            MaxDrawdown = volatility * 2,
            P10Return = baseReturn - (1.28m * volatility * 0.01m * Math.Abs(baseReturn)),
            P25Return = baseReturn - (0.67m * volatility * 0.01m * Math.Abs(baseReturn)),
            P50Return = baseReturn,
            P75Return = baseReturn + (0.67m * volatility * 0.01m * Math.Abs(baseReturn)),
            P90Return = baseReturn + (1.28m * volatility * 0.01m * Math.Abs(baseReturn)),
            ManagementFee = fund.ManagementFee ?? 1.5m,
            TotalExpenseRatio = (fund.ManagementFee ?? 1.5m) + 0.5m, // Estimate TER from management fee
        };
    }

    private static decimal EstimateVolatility(SNBMutualFund fund)
    {
        var returns = new[] { fund.Return1M, fund.Return3M, fund.Return6M, fund.Return1Y }
            .Where(r => r.HasValue && r != 0)
            .Select(r => r!.Value)
            .ToList();

        if (returns.Count < 2)
            return 15m;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / returns.Count;
        return (decimal)Math.Sqrt((double)variance) * 2;
    }

    private static decimal CalculateSharpeRatio(decimal returns, decimal volatility)
    {
        const decimal riskFreeRate = 3.0m;
        return volatility > 0 ? (returns - riskFreeRate) / volatility : 0;
    }

    private static Dictionary<string, RiskCategoryStats> CalculateRiskCategoryStats(
        List<FundHistoricalData> funds
    )
    {
        return funds
            .GroupBy(f => f.RiskLevel)
            .ToDictionary(
                g => g.Key,
                g => new RiskCategoryStats
                {
                    RiskCategory = g.Key,
                    FundCount = g.Count(),
                    AverageReturn = g.Average(f => f.OneYearReturn),
                    AverageVolatility = g.Average(f => f.StandardDeviation),
                    AverageSharpeRatio = g.Average(f => f.SharpeRatio),
                    BestReturn = g.Max(f => f.OneYearReturn),
                    WorstReturn = g.Min(f => f.OneYearReturn),
                }
            );
    }
}

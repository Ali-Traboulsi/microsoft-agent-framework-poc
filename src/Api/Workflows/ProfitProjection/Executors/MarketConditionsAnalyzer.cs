using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Tools;

namespace AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Executors;

/// <summary>
/// Analyzes current market conditions using real-time web search data
/// No dummy data - all information from live sources
/// </summary>
public class MarketConditionsAnalyzer
{
    private readonly WebSearchTools _webSearchTools;
    private readonly ILogger<MarketConditionsAnalyzer> _logger;

    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.ProfitProjection.MarketConditionsAnalyzer",
        "1.0.0"
    );
    private static readonly Meter Meter = new(
        "InvestmentBanking.ProfitProjection.MarketConditionsAnalyzer",
        "1.0.0"
    );
    private static readonly Counter<int> AnalysisCounter = Meter.CreateCounter<int>(
        "market_analysis_count",
        "analyses",
        "Number of market analyses performed"
    );

    public MarketConditionsAnalyzer(
        WebSearchTools webSearchTools,
        ILogger<MarketConditionsAnalyzer> logger
    )
    {
        _webSearchTools = webSearchTools;
        _logger = logger;
    }

    /// <summary>
    /// Execute market conditions analysis using real-time web search
    /// </summary>
    public async Task<MarketAnalysis> ExecuteAsync(ProjectionRequest request)
    {
        using var activity = ActivitySource.StartActivity("MarketConditionsAnalysis");
        activity?.SetTag("time_horizon_months", request.TimeHorizonMonths);

        try
        {
            _logger.LogInformation("Starting market conditions analysis with web search");

            // Fetch real market data via web search in parallel
            var economicIndicatorsTask = FetchEconomicIndicatorsAsync();
            var sectorOutlooksTask = FetchSectorOutlooksAsync();
            var oilPriceTask = FetchOilPriceAsync();

            await Task.WhenAll(economicIndicatorsTask, sectorOutlooksTask, oilPriceTask);

            var economicIndicators = await economicIndicatorsTask;
            var sectorOutlooks = await sectorOutlooksTask;
            var oilPrice = await oilPriceTask;

            // Update oil price in economic indicators
            economicIndicators = economicIndicators with
            {
                OilPrice = oilPrice,
            };

            // Calculate market sentiment and adjustment factors
            var (sentiment, conditionScore) = CalculateMarketSentiment(
                economicIndicators,
                sectorOutlooks
            );
            var returnAdjustment = CalculateReturnAdjustmentFactor(
                conditionScore,
                request.TimeHorizonMonths
            );
            var riskAdjustment = CalculateRiskAdjustmentFactor(conditionScore);

            AnalysisCounter.Add(1);

            activity?.SetTag("market_sentiment", sentiment);
            activity?.SetTag("return_adjustment", returnAdjustment);
            activity?.SetTag("data_source", "web_search");

            return new MarketAnalysis
            {
                AnalyzedAt = DateTime.UtcNow,
                MarketSentiment = sentiment,
                MarketConditionScore = conditionScore,
                SectorOutlooks = sectorOutlooks,
                EconomicIndicators = economicIndicators,
                ReturnAdjustmentFactor = returnAdjustment,
                RiskAdjustmentFactor = riskAdjustment,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during market conditions analysis");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task<EconomicIndicators> FetchEconomicIndicatorsAsync()
    {
        try
        {
            var searchQuery =
                "Saudi Arabia GDP growth rate inflation interest rate 2024 2025 economy";
            var searchResult = await _webSearchTools.SearchWeb(searchQuery, 5);

            _logger.LogInformation("Fetched economic indicators from web search");
            return ParseEconomicIndicators(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch economic indicators from web");
            return new EconomicIndicators
            {
                GdpGrowth = 0m,
                InflationRate = 0m,
                InterestRate = 0m,
                OilPrice = 0m,
                CurrencyOutlook = "Stable",
            };
        }
    }

    private async Task<List<SectorOutlook>> FetchSectorOutlooksAsync()
    {
        try
        {
            var searchQuery =
                "Tadawul Saudi stock market sector performance outlook 2024 2025 banking energy real estate";
            var searchResult = await _webSearchTools.SearchWeb(searchQuery, 5);

            _logger.LogInformation("Fetched sector outlooks from web search");
            return ParseSectorOutlooks(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sector outlooks from web");
            return new List<SectorOutlook>();
        }
    }

    private async Task<decimal> FetchOilPriceAsync()
    {
        try
        {
            var searchQuery = "Brent crude oil price today USD per barrel";
            var searchResult = await _webSearchTools.SearchWeb(searchQuery, 3);

            _logger.LogInformation("Fetched oil price from web search");
            return ParseOilPrice(searchResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch oil price from web");
            return 0m;
        }
    }

    private EconomicIndicators ParseEconomicIndicators(string searchResult)
    {
        decimal gdpGrowth = ExtractPercentageValue(
            searchResult,
            new[] { "GDP growth", "economic growth", "GDP" }
        );
        decimal inflationRate = ExtractPercentageValue(searchResult, new[] { "inflation", "CPI" });
        decimal interestRate = ExtractPercentageValue(
            searchResult,
            new[] { "interest rate", "SAMA rate", "repo rate" }
        );

        string currencyOutlook = "Stable";
        if (
            searchResult.Contains("devaluation", StringComparison.OrdinalIgnoreCase)
            || searchResult.Contains("pressure", StringComparison.OrdinalIgnoreCase)
        )
        {
            currencyOutlook = "Cautious";
        }

        return new EconomicIndicators
        {
            GdpGrowth = gdpGrowth,
            InflationRate = inflationRate,
            InterestRate = interestRate,
            OilPrice = 0m,
            CurrencyOutlook = currencyOutlook,
        };
    }

    private List<SectorOutlook> ParseSectorOutlooks(string searchResult)
    {
        var outlooks = new List<SectorOutlook>();
        var searchLower = searchResult.ToLower();

        var sectors = new[]
        {
            (
                "Banking & Financial Services",
                "البنوك والخدمات المالية",
                new[] { "bank", "financial", "finance" }
            ),
            (
                "Energy & Petrochemicals",
                "الطاقة والبتروكيماويات",
                new[] { "oil", "energy", "petrochemical", "aramco" }
            ),
            (
                "Real Estate & Construction",
                "العقارات والتشييد",
                new[] { "real estate", "construction", "property", "neom" }
            ),
            (
                "Technology & Telecom",
                "التقنية والاتصالات",
                new[] { "technology", "tech", "telecom", "digital" }
            ),
            ("Healthcare", "الرعاية الصحية", new[] { "healthcare", "hospital", "medical" }),
            ("Consumer & Retail", "المستهلك والتجزئة", new[] { "consumer", "retail", "shopping" }),
        };

        foreach (var (name, nameAr, keywords) in sectors)
        {
            var outlook = DetermineSectorOutlook(searchLower, keywords);
            outlooks.Add(
                new SectorOutlook
                {
                    SectorName = name,
                    SectorNameAr = nameAr,
                    Outlook = outlook.Outlook,
                    ExpectedReturnAdjustment = outlook.Adjustment,
                    Rationale = outlook.Rationale,
                }
            );
        }

        return outlooks;
    }

    private (string Outlook, decimal Adjustment, string Rationale) DetermineSectorOutlook(
        string searchContent,
        string[] keywords
    )
    {
        int positiveSignals = 0;
        int negativeSignals = 0;

        var positiveWords = new[]
        {
            "growth",
            "bullish",
            "outperform",
            "increase",
            "strong",
            "positive",
            "record",
            "high",
        };
        var negativeWords = new[]
        {
            "decline",
            "bearish",
            "underperform",
            "decrease",
            "weak",
            "negative",
            "low",
            "concern",
        };

        foreach (var keyword in keywords)
        {
            var index = searchContent.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var start = Math.Max(0, index - 100);
                var end = Math.Min(searchContent.Length, index + keyword.Length + 100);
                var context = searchContent[start..end];

                positiveSignals += positiveWords.Count(w =>
                    context.Contains(w, StringComparison.OrdinalIgnoreCase)
                );
                negativeSignals += negativeWords.Count(w =>
                    context.Contains(w, StringComparison.OrdinalIgnoreCase)
                );
            }
        }

        if (positiveSignals > negativeSignals + 2)
        {
            return ("Bullish", 1.5m, "Positive market signals from recent news");
        }
        else if (negativeSignals > positiveSignals + 2)
        {
            return ("Bearish", -1.0m, "Negative market signals from recent news");
        }
        else
        {
            return ("Neutral", 0m, "Mixed or neutral signals from market data");
        }
    }

    private decimal ParseOilPrice(string searchResult)
    {
        var patterns = new[]
        {
            @"\$(\d+\.?\d*)",
            @"(\d+\.?\d*)\s*(?:USD|dollars?|per barrel)",
            @"Brent[^0-9]*(\d+\.?\d*)",
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                searchResult,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var price))
            {
                if (price > 20 && price < 200)
                {
                    return price;
                }
            }
        }

        return 0m;
    }

    private decimal ExtractPercentageValue(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var searchArea = text.Substring(index, Math.Min(100, text.Length - index));
                var match = System.Text.RegularExpressions.Regex.Match(
                    searchArea,
                    @"(-?\d+\.?\d*)\s*%"
                );

                if (match.Success && decimal.TryParse(match.Groups[1].Value, out var value))
                {
                    return value;
                }
            }
        }

        return 0m;
    }

    private (string Sentiment, decimal Score) CalculateMarketSentiment(
        EconomicIndicators indicators,
        List<SectorOutlook> sectors
    )
    {
        decimal score = 0;
        int dataPoints = 0;

        if (indicators.GdpGrowth != 0)
        {
            score += Math.Clamp(indicators.GdpGrowth / 10m, -0.3m, 0.3m);
            dataPoints++;
        }

        if (indicators.InflationRate != 0)
        {
            score -= Math.Clamp((indicators.InflationRate - 3m) / 10m, -0.2m, 0.2m);
            dataPoints++;
        }

        if (indicators.OilPrice > 0)
        {
            score += Math.Clamp((indicators.OilPrice - 60m) / 100m, -0.2m, 0.2m);
            dataPoints++;
        }

        if (sectors.Any())
        {
            var bullishSectors = sectors.Count(s => s.Outlook == "Bullish");
            var bearishSectors = sectors.Count(s => s.Outlook == "Bearish");
            score += Math.Clamp((bullishSectors - bearishSectors) * 0.05m, -0.3m, 0.3m);
            dataPoints++;
        }

        if (dataPoints == 0)
        {
            return ("Neutral", 0m);
        }

        score = Math.Clamp(score, -1m, 1m);

        string sentiment = score switch
        {
            > 0.3m => "Bullish",
            < -0.3m => "Bearish",
            _ => "Neutral",
        };

        return (sentiment, Math.Round(score, 2));
    }

    private decimal CalculateReturnAdjustmentFactor(decimal conditionScore, int timeHorizonMonths)
    {
        var baseAdjustment = 1m + (conditionScore * 0.1m);

        if (timeHorizonMonths > 36)
        {
            baseAdjustment = 1m + ((baseAdjustment - 1m) * 0.5m);
        }

        return Math.Round(baseAdjustment, 3);
    }

    private decimal CalculateRiskAdjustmentFactor(decimal conditionScore)
    {
        return conditionScore switch
        {
            < -0.3m => 1.2m,
            > 0.3m => 0.95m,
            _ => 1.0m,
        };
    }
}

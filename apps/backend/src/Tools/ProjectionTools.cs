using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Tools.Formatters;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for profit projection calculations that agents can invoke
/// احتساب الأرباح التقديرية
/// </summary>
public class ProjectionTools
{
    private readonly ProfitProjectionWorkflow _workflow;
    private readonly StreamingProfitProjectionWorkflow _streamingWorkflow;
    private readonly WorkflowProgressNotifier _progressNotifier;
    private readonly ILogger<ProjectionTools> _logger;

    // Store the last projection result for structured access
    private ProjectionResult? _lastProjectionResult;

    // OpenTelemetry instrumentation
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Tools.Projection",
        "1.0.0"
    );
    private static readonly Meter Meter = new("InvestmentBanking.Tools.Projection", "1.0.0");
    private static readonly Counter<int> ToolInvocationsCounter = Meter.CreateCounter<int>(
        "tool_invocations",
        "invocations",
        "Number of tool invocations"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public ProjectionTools(
        ProfitProjectionWorkflow workflow,
        StreamingProfitProjectionWorkflow streamingWorkflow,
        WorkflowProgressNotifier progressNotifier,
        ILogger<ProjectionTools> logger
    )
    {
        _workflow = workflow;
        _streamingWorkflow = streamingWorkflow;
        _progressNotifier = progressNotifier;
        _logger = logger;
    }

    /// <summary>
    /// Get the last projection result as a structured object
    /// </summary>
    public ProjectionResult? GetLastProjectionResult() => _lastProjectionResult;

    /// <summary>
    /// Clear the last projection result (should be called after reading it)
    /// </summary>
    public void ClearLastProjectionResult() => _lastProjectionResult = null;

    /// <summary>
    /// Execute projection and return the raw ProjectionResult object
    /// </summary>
    public async Task<ProjectionResult> ExecuteProjectionAsync(ProjectionRequest request)
    {
        var result = await _workflow.ExecuteAsync(request);
        _lastProjectionResult = result;
        return result;
    }

    /// <summary>
    /// Calculate estimated profit projection for an investment and return structured JSON
    /// احتساب الأرباح التقديرية للاستثمار
    /// </summary>
    [Description(
        "Calculate profit projection and return structured JSON result. Use this for programmatic access to projection data. Returns complete projection with scenarios, fund recommendations, and metadata."
    )]
    public async Task<string> CalculateProfitProjectionStructured(
        [Description("Investment amount in the specified currency (minimum 1000)")]
            decimal investmentAmount,
        [Description("Investment time horizon in months (e.g., 12 for 1 year, 36 for 3 years)")]
            int timeHorizonMonths,
        [Description(
            "Risk profile: Conservative (low risk), Moderate (balanced), or Aggressive (high risk)"
        )]
            string riskProfile,
        [Description("Currency code (default: SAR)")] string currency = "SAR",
        [Description("Only include Sharia-compliant funds")] bool shariahCompliant = false,
        [Description("Customer ID for personalized projection (optional)")]
            string? customerId = null
    )
    {
        using var activity = ActivitySource.StartActivity("CalculateProfitProjectionStructured");
        ToolInvocationsCounter.Add(1);

        // Get conversation ID from middleware context for progress streaming
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Agent requested structured profit projection: {Amount} {Currency}, {Months} months, {Risk} profile",
                investmentAmount,
                currency,
                timeHorizonMonths,
                riskProfile
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = investmentAmount,
                Currency = currency,
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = NormalizeRiskProfile(riskProfile),
                InvestmentType = "LumpSum",
                ShariahCompliantOnly = shariahCompliant,
                CustomerId = customerId,
            };

            // Use new real-time streaming method that pushes directly to SignalR
            var result = await _streamingWorkflow.ExecuteWithRealTimeProgressAsync(
                request,
                conversationId
            );
            _lastProjectionResult = result;

            // Return as JSON string for the agent to process
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid projection request");
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate profit projection");
            return JsonSerializer.Serialize(
                new { error = $"Error calculating projection: {ex.Message}" },
                JsonOptions
            );
        }
    }

    /// <summary>
    /// Calculate estimated profit projection for an investment with real-time progress streaming
    /// احتساب الأرباح التقديرية للاستثمار
    /// </summary>
    /// <param name="investmentAmount">The amount to invest (e.g., 100000)</param>
    /// <param name="timeHorizonMonths">Investment time horizon in months (e.g., 36 for 3 years)</param>
    /// <param name="riskProfile">Risk profile: Conservative, Moderate, or Aggressive</param>
    /// <param name="currency">Currency code (default: SAR)</param>
    /// <param name="shariahCompliant">Whether to only include Sharia-compliant funds</param>
    /// <returns>Detailed projection with three scenarios</returns>
    [Description(
        "PRIMARY TOOL: Calculate estimated profit projection for an investment with real-time progress updates. Shows conservative, expected, and optimistic scenarios with recommended fund allocations. Progress steps are displayed in the UI as they complete. Use this for ALL investment projection requests."
    )]
    public async Task<string> CalculateProfitProjection(
        [Description("Investment amount in the specified currency (minimum 1000)")]
            decimal investmentAmount,
        [Description("Investment time horizon in months (e.g., 12 for 1 year, 36 for 3 years)")]
            int timeHorizonMonths,
        [Description(
            "Risk profile: Conservative (low risk), Moderate (balanced), or Aggressive (high risk)"
        )]
            string riskProfile,
        [Description("Currency code (default: SAR)")] string currency = "SAR",
        [Description("Only include Sharia-compliant funds")] bool shariahCompliant = false
    )
    {
        using var activity = ActivitySource.StartActivity("CalculateProfitProjection");
        ToolInvocationsCounter.Add(1);

        // Get conversation ID from middleware context for progress streaming
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Agent requested profit projection with real-time progress: {Amount} {Currency}, {Months} months, {Risk} profile",
                investmentAmount,
                currency,
                timeHorizonMonths,
                riskProfile
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = investmentAmount,
                Currency = currency,
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = NormalizeRiskProfile(riskProfile),
                InvestmentType = "LumpSum",
                ShariahCompliantOnly = shariahCompliant,
            };

            // Use new real-time streaming method that pushes directly to SignalR
            var result = await _streamingWorkflow.ExecuteWithRealTimeProgressAsync(
                request,
                conversationId
            );
            _lastProjectionResult = result;

            // Return the formatted result
            return FormatProjectionResult(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid projection request");
            return $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate profit projection");
            return $"Error calculating projection: {ex.Message}";
        }
    }

    /// <summary>
    /// Calculate profit projection for an existing customer
    /// Includes analysis of their current holdings
    /// </summary>
    /// <param name="customerId">Customer ID (CIF)</param>
    /// <param name="investmentAmount">Additional investment amount</param>
    /// <param name="timeHorizonMonths">Investment time horizon in months</param>
    /// <returns>Personalized projection with current holdings context</returns>
    [Description(
        "Calculate personalized profit projection for an existing customer. This takes into account their current portfolio and investment history. Use this when a customer with an existing account asks about potential returns."
    )]
    public async Task<string> CalculatePersonalizedProjection(
        [Description("Customer ID (CIF number like 100000000001)")] string customerId,
        [Description("Investment amount to project")] decimal investmentAmount,
        [Description("Investment time horizon in months")] int timeHorizonMonths
    )
    {
        using var activity = ActivitySource.StartActivity("CalculatePersonalizedProjection");
        ToolInvocationsCounter.Add(1);

        // Get conversation ID from middleware context for progress streaming
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Agent requested personalized projection for customer {CustomerId}: {Amount}, {Months} months",
                customerId,
                investmentAmount,
                timeHorizonMonths
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = investmentAmount,
                Currency = "SAR",
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = "Moderate", // Will be overridden by customer's actual profile
                InvestmentType = "LumpSum",
                CustomerId = customerId,
            };

            // Use new real-time streaming method that pushes directly to SignalR
            var result = await _streamingWorkflow.ExecuteWithRealTimeProgressAsync(
                request,
                conversationId
            );
            _lastProjectionResult = result;

            // Return the formatted result
            return FormatProjectionResult(result, includeCustomerContext: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate personalized projection");
            return $"Error calculating projection: {ex.Message}";
        }
    }

    /// <summary>
    /// Compare lump sum vs monthly SIP investment strategies
    /// </summary>
    /// <param name="totalAmount">Total amount to invest</param>
    /// <param name="timeHorizonMonths">Investment period in months</param>
    /// <param name="riskProfile">Risk profile</param>
    /// <returns>Comparison of both strategies with recommendations</returns>
    [Description(
        "Compare lump sum investment vs monthly SIP (Systematic Investment Plan). Use this when a customer is deciding whether to invest all at once or spread their investment over time."
    )]
    public async Task<string> CompareInvestmentStrategies(
        [Description("Total investment amount")] decimal totalAmount,
        [Description("Investment time horizon in months")] int timeHorizonMonths,
        [Description("Risk profile: Conservative, Moderate, or Aggressive")] string riskProfile
    )
    {
        using var activity = ActivitySource.StartActivity("CompareInvestmentStrategies");
        ToolInvocationsCounter.Add(1);

        // Get conversation ID from middleware context for progress streaming
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Agent requested strategy comparison: {Amount}, {Months} months",
                totalAmount,
                timeHorizonMonths
            );

            var request = new ProjectionRequest
            {
                InvestmentAmount = totalAmount,
                Currency = "SAR",
                TimeHorizonMonths = timeHorizonMonths,
                RiskProfile = NormalizeRiskProfile(riskProfile),
                InvestmentType = "LumpSum", // Workflow will automatically compare
            };

            // Use new real-time streaming method that pushes directly to SignalR
            var result = await _streamingWorkflow.ExecuteWithRealTimeProgressAsync(
                request,
                conversationId
            );
            _lastProjectionResult = result;

            // Return the formatted result
            return FormatStrategyComparison(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare investment strategies");
            return $"Error comparing strategies: {ex.Message}";
        }
    }

    /// <summary>
    /// Get quick projection estimate without full workflow
    /// </summary>
    [Description(
        "Get a quick estimate of potential returns. Faster but less detailed than full projection."
    )]
    public string GetQuickEstimate(
        [Description("Investment amount")] decimal amount,
        [Description("Years to invest (1-10)")] int years,
        [Description("Risk level: low, medium, or high")] string riskLevel
    )
    {
        ToolInvocationsCounter.Add(1);
        return ProjectionFormatter.FormatQuickEstimate(amount, years, riskLevel);
    }

    private string NormalizeRiskProfile(string input)
    {
        return input.ToLower() switch
        {
            "low" or "conservative" or "متحفظ" => "Conservative",
            "medium" or "moderate" or "balanced" or "متوازن" => "Moderate",
            "high" or "aggressive" or "جريء" => "Aggressive",
            _ => "Moderate",
        };
    }

    private string FormatProjectionResult(
        ProjectionResult result,
        bool includeCustomerContext = false
    ) => ProjectionFormatter.FormatResult(result, includeCustomerContext);

    private string FormatStrategyComparison(ProjectionResult result) =>
        ProjectionFormatter.FormatStrategyComparison(result);
}

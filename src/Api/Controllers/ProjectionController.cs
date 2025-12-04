using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection;
using AgentFrameworkQuickStart.Api.Workflows.ProfitProjection.Messages;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

/// <summary>
/// API Controller for Profit Projection operations
/// احتساب الأرباح التقديرية
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectionController : ControllerBase
{
    private readonly ProfitProjectionWorkflow _workflow;
    private readonly ILogger<ProjectionController> _logger;

    public ProjectionController(
        ProfitProjectionWorkflow workflow,
        ILogger<ProjectionController> logger
    )
    {
        _workflow = workflow;
        _logger = logger;
    }

    /// <summary>
    /// Calculate profit projection for an investment
    /// </summary>
    /// <param name="request">Projection request parameters</param>
    /// <returns>Complete projection result with scenarios and recommendations</returns>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(ProjectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProjectionResult>> CalculateProjection(
        [FromBody] ProjectionRequestDto request
    )
    {
        try
        {
            _logger.LogInformation(
                "Projection request received: {Amount} {Currency}, {Months} months, {Risk} risk",
                request.InvestmentAmount,
                request.Currency,
                request.TimeHorizonMonths,
                request.RiskProfile
            );

            var projectionRequest = new ProjectionRequest
            {
                InvestmentAmount = request.InvestmentAmount,
                Currency = request.Currency ?? "SAR",
                TimeHorizonMonths = request.TimeHorizonMonths,
                RiskProfile = request.RiskProfile,
                InvestmentType = request.InvestmentType ?? "LumpSum",
                MonthlyAmount = request.MonthlyAmount,
                CustomerId = request.CustomerId,
                TargetFundCodes = request.TargetFundCodes,
                ShariahCompliantOnly = request.ShariahCompliantOnly ?? false,
            };

            var result = await _workflow.ExecuteAsync(projectionRequest);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid projection request");
            return BadRequest(
                new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating projection");
            return StatusCode(
                500,
                new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail =
                        "An error occurred while calculating the projection. Please try again.",
                    Status = StatusCodes.Status500InternalServerError,
                }
            );
        }
    }

    /// <summary>
    /// Calculate personalized projection for an existing customer
    /// </summary>
    [HttpPost("customer/{customerId}")]
    [ProducesResponseType(typeof(ProjectionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectionResult>> CalculateCustomerProjection(
        string customerId,
        [FromBody] CustomerProjectionRequestDto request
    )
    {
        try
        {
            _logger.LogInformation(
                "Customer projection request: {CustomerId}, {Amount}, {Months} months",
                customerId,
                request.InvestmentAmount,
                request.TimeHorizonMonths
            );

            var projectionRequest = new ProjectionRequest
            {
                InvestmentAmount = request.InvestmentAmount,
                Currency = request.Currency ?? "SAR",
                TimeHorizonMonths = request.TimeHorizonMonths,
                RiskProfile = request.RiskProfile ?? "Moderate",
                InvestmentType = request.InvestmentType ?? "LumpSum",
                CustomerId = customerId,
                ShariahCompliantOnly = request.ShariahCompliantOnly ?? false,
            };

            var result = await _workflow.ExecuteAsync(projectionRequest);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating customer projection");
            return StatusCode(
                500,
                new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while calculating the projection.",
                    Status = StatusCodes.Status500InternalServerError,
                }
            );
        }
    }

    /// <summary>
    /// Compare investment strategies (Lump Sum vs SIP)
    /// </summary>
    [HttpPost("compare-strategies")]
    [ProducesResponseType(typeof(StrategyComparisonResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StrategyComparisonResponse>> CompareStrategies(
        [FromBody] StrategyComparisonRequestDto request
    )
    {
        try
        {
            _logger.LogInformation(
                "Strategy comparison request: {Amount}, {Months} months",
                request.TotalAmount,
                request.TimeHorizonMonths
            );

            var projectionRequest = new ProjectionRequest
            {
                InvestmentAmount = request.TotalAmount,
                Currency = request.Currency ?? "SAR",
                TimeHorizonMonths = request.TimeHorizonMonths,
                RiskProfile = request.RiskProfile ?? "Moderate",
                InvestmentType = "LumpSum",
                ShariahCompliantOnly = request.ShariahCompliantOnly ?? false,
            };

            var result = await _workflow.ExecuteAsync(projectionRequest);

            var comparison = result.Scenarios.StrategyComparison;
            if (comparison == null)
            {
                return Ok(
                    new StrategyComparisonResponse
                    {
                        Success = false,
                        Message = "Strategy comparison not available",
                    }
                );
            }

            return Ok(
                new StrategyComparisonResponse
                {
                    Success = true,
                    ProjectionId = result.ProjectionId,
                    LumpSum = comparison.LumpSum,
                    MonthlySip = comparison.MonthlySip,
                    RecommendedStrategy = comparison.RecommendedStrategy,
                    RecommendationRationale = comparison.RecommendationRationale,
                    RecommendationRationaleAr = comparison.RecommendationRationaleAr,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing strategies");
            return StatusCode(
                500,
                new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while comparing strategies.",
                    Status = StatusCodes.Status500InternalServerError,
                }
            );
        }
    }

    /// <summary>
    /// Get quick estimate without full workflow
    /// </summary>
    [HttpGet("quick-estimate")]
    [ProducesResponseType(typeof(QuickEstimateResponse), StatusCodes.Status200OK)]
    public ActionResult<QuickEstimateResponse> GetQuickEstimate(
        [FromQuery] decimal amount,
        [FromQuery] int years,
        [FromQuery] string riskLevel = "moderate"
    )
    {
        if (amount <= 0)
            return BadRequest(new ProblemDetails { Detail = "Amount must be greater than zero" });

        if (years < 1 || years > 30)
            return BadRequest(new ProblemDetails { Detail = "Years must be between 1 and 30" });

        var annualReturn = riskLevel.ToLower() switch
        {
            "low" or "conservative" => 0.05m,
            "medium" or "moderate" => 0.08m,
            "high" or "aggressive" => 0.12m,
            _ => 0.07m,
        };

        var futureValue = amount * (decimal)Math.Pow((double)(1 + annualReturn), years);
        var totalReturn = futureValue - amount;

        return Ok(
            new QuickEstimateResponse
            {
                InvestmentAmount = amount,
                Years = years,
                RiskLevel = riskLevel,
                AssumedAnnualReturn = annualReturn * 100,
                ProjectedValue = Math.Round(futureValue, 2),
                TotalReturn = Math.Round(totalReturn, 2),
                PercentageReturn = Math.Round((totalReturn / amount) * 100, 2),
                Disclaimer =
                    "This is a quick estimate based on historical averages. Actual returns may vary.",
            }
        );
    }
}

#region Request/Response DTOs

public record ProjectionRequestDto
{
    public decimal InvestmentAmount { get; init; }
    public string? Currency { get; init; } = "SAR";
    public int TimeHorizonMonths { get; init; }
    public required string RiskProfile { get; init; }
    public string? InvestmentType { get; init; } = "LumpSum";
    public decimal? MonthlyAmount { get; init; }
    public string? CustomerId { get; init; }
    public List<string>? TargetFundCodes { get; init; }
    public bool? ShariahCompliantOnly { get; init; } = false;
}

public record CustomerProjectionRequestDto
{
    public decimal InvestmentAmount { get; init; }
    public string? Currency { get; init; } = "SAR";
    public int TimeHorizonMonths { get; init; }
    public string? RiskProfile { get; init; }
    public string? InvestmentType { get; init; }
    public bool? ShariahCompliantOnly { get; init; }
}

public record StrategyComparisonRequestDto
{
    public decimal TotalAmount { get; init; }
    public int TimeHorizonMonths { get; init; }
    public string? RiskProfile { get; init; }
    public string? Currency { get; init; }
    public bool? ShariahCompliantOnly { get; init; }
}

public record StrategyComparisonResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ProjectionId { get; init; }
    public StrategyResult? LumpSum { get; init; }
    public StrategyResult? MonthlySip { get; init; }
    public string? RecommendedStrategy { get; init; }
    public string? RecommendationRationale { get; init; }
    public string? RecommendationRationaleAr { get; init; }
}

public record QuickEstimateResponse
{
    public decimal InvestmentAmount { get; init; }
    public int Years { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public decimal AssumedAnnualReturn { get; init; }
    public decimal ProjectedValue { get; init; }
    public decimal TotalReturn { get; init; }
    public decimal PercentageReturn { get; init; }
    public string Disclaimer { get; init; } = string.Empty;
}

#endregion

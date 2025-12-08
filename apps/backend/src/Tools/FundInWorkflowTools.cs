using System.ComponentModel;
using System.Text.Json;
using AgentFrameworkQuickStart.Api.Workflows.FundIn;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for invoking the Fund-In Workflow
/// Provides a high-level interface for the complete fund transfer process
/// </summary>
public class FundInWorkflowTools(FundInWorkflow fundInWorkflow, ILogger<FundInWorkflowTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Cache for workflow states awaiting OTP
    private static readonly Dictionary<string, FundInWorkflowState> PendingWorkflows = new();

    [Description(
        "Execute the complete Fund-In workflow to transfer money from a bank account to an investment portfolio. "
            + "This is a multi-step process: Preview → Start → OTP Verification → Commit. "
            + "Use this when you have all required information including the OTP code."
    )]
    public async Task<string> ExecuteFundInWorkflow(
        [Description("Customer CIF number (default: 100000000005)")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("OTP code for verification")] string otp,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID for specific mutual fund (optional)")] string? fundId = null,
        [Description("Transaction notes (optional)")] string? notes = null
    )
    {
        try
        {
            logger.LogInformation(
                "Executing Fund-In workflow: {Amount} {Currency} from {Account} to {Portfolio}",
                amount,
                currency,
                sourceAccountId,
                targetPortfolioNumber
            );

            var request = new FundInWorkflowRequest
            {
                Cif = cif,
                SourceAccountId = sourceAccountId,
                TargetPortfolioNumber = targetPortfolioNumber,
                Amount = amount,
                Currency = currency,
                FundId = fundId,
                Notes = notes,
            };

            var result = await fundInWorkflow.ExecuteAsync(request, otp);

            return JsonSerializer.Serialize(
                new
                {
                    result.Success,
                    result.WorkflowId,
                    Status = result.Status.ToString(),
                    result.TransactionId,
                    result.ReferenceNumber,
                    result.Amount,
                    result.Currency,
                    result.Fees,
                    result.TotalAmount,
                    result.Units,
                    result.NavAtPurchase,
                    result.SourceAccountId,
                    result.TargetPortfolioNumber,
                    result.FundName,
                    DurationMs = result.Duration.TotalMilliseconds,
                    result.SummaryEn,
                    result.SummaryAr,
                    Error = result.Success
                        ? null
                        : new
                        {
                            result.ErrorMessage,
                            result.ErrorCode,
                            FailedStep = result.FailedAtStep?.ToString(),
                        },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fund-In workflow execution failed");
            return JsonSerializer.Serialize(
                new { Success = false, ErrorMessage = ex.Message },
                JsonOptions
            );
        }
    }

    [Description(
        "Start a Fund-In workflow and trigger OTP. Use this as the first step when the customer "
            + "wants to transfer money. Returns a workflow ID to continue after OTP is received."
    )]
    public async Task<string> StartFundInWorkflow(
        [Description("Customer CIF number (default: 100000000005)")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID for specific mutual fund (optional)")] string? fundId = null,
        [Description("Transaction notes (optional)")] string? notes = null
    )
    {
        try
        {
            logger.LogInformation(
                "Starting Fund-In workflow: {Amount} {Currency}",
                amount,
                currency
            );

            var request = new FundInWorkflowRequest
            {
                Cif = cif,
                SourceAccountId = sourceAccountId,
                TargetPortfolioNumber = targetPortfolioNumber,
                Amount = amount,
                Currency = currency,
                FundId = fundId,
                Notes = notes,
            };

            var state = await fundInWorkflow.ExecuteUpToOtpAsync(request);

            // Cache the state for completion
            if (state.Status == FundInWorkflowStatus.AwaitingOtp)
            {
                PendingWorkflows[state.WorkflowId] = state;
            }

            return JsonSerializer.Serialize(
                new
                {
                    state.WorkflowId,
                    Status = state.Status.ToString(),
                    Success = state.Status == FundInWorkflowStatus.AwaitingOtp,
                    Preview = state.Preview != null
                        ? new
                        {
                            state.Preview.Amount,
                            state.Preview.Currency,
                            state.Preview.Fees,
                            state.Preview.TotalAmount,
                            state.Preview.FundName,
                            state.Preview.EstimatedUnits,
                            state.Preview.CurrentNav,
                        }
                        : null,
                    OtpInfo = state.Start != null
                        ? new
                        {
                            state.Start.TransactionId,
                            state.Start.OtpSentTo,
                            state.Start.OtpExpirySeconds,
                        }
                        : null,
                    NextStep = state.Status == FundInWorkflowStatus.AwaitingOtp
                        ? "Customer needs to provide the OTP. Use CompleteFundInWorkflow with the workflowId and OTP."
                        : null,
                    Error = state.Status == FundInWorkflowStatus.Failed
                        ? new { state.ErrorMessage, FailedStep = state.FailedAtStep?.ToString() }
                        : null,
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Start Fund-In workflow failed");
            return JsonSerializer.Serialize(
                new { Success = false, ErrorMessage = ex.Message },
                JsonOptions
            );
        }
    }

    [Description(
        "Complete a pending Fund-In workflow after the customer provides the OTP code. "
            + "Use the workflowId from StartFundInWorkflow."
    )]
    public async Task<string> CompleteFundInWorkflow(
        [Description("Workflow ID from StartFundInWorkflow")] string workflowId,
        [Description("OTP code provided by the customer")] string otp
    )
    {
        try
        {
            logger.LogInformation("Completing Fund-In workflow {WorkflowId} with OTP", workflowId);

            // Retrieve cached state
            if (!PendingWorkflows.TryGetValue(workflowId, out var state))
            {
                return JsonSerializer.Serialize(
                    new
                    {
                        Success = false,
                        ErrorMessage = $"Workflow {workflowId} not found. It may have expired or been completed.",
                    },
                    JsonOptions
                );
            }

            // Remove from cache (one-time use)
            PendingWorkflows.Remove(workflowId);

            var result = await fundInWorkflow.CompleteAfterOtpAsync(state, otp);

            return JsonSerializer.Serialize(
                new
                {
                    result.Success,
                    result.WorkflowId,
                    Status = result.Status.ToString(),
                    result.TransactionId,
                    result.ReferenceNumber,
                    result.Amount,
                    result.Currency,
                    result.Fees,
                    result.TotalAmount,
                    result.Units,
                    result.NavAtPurchase,
                    result.SourceAccountId,
                    result.TargetPortfolioNumber,
                    result.FundName,
                    DurationMs = result.Duration.TotalMilliseconds,
                    result.SummaryEn,
                    result.SummaryAr,
                    Error = result.Success
                        ? null
                        : new
                        {
                            result.ErrorMessage,
                            result.ErrorCode,
                            FailedStep = result.FailedAtStep?.ToString(),
                        },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Complete Fund-In workflow failed");
            return JsonSerializer.Serialize(
                new { Success = false, ErrorMessage = ex.Message },
                JsonOptions
            );
        }
    }

    [Description("Get the status of pending Fund-In workflows awaiting OTP.")]
    public string GetPendingWorkflows()
    {
        try
        {
            var pending = PendingWorkflows
                .Values.Select(s => new
                {
                    s.WorkflowId,
                    Status = s.Status.ToString(),
                    s.Request.Amount,
                    s.Request.Currency,
                    s.Request.SourceAccountId,
                    s.Request.TargetPortfolioNumber,
                    TransactionId = s.Start?.TransactionId,
                    OtpSentTo = s.Start?.OtpSentTo,
                    StartedAt = s.StartedAt,
                })
                .ToList();

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    PendingCount = pending.Count,
                    Workflows = pending,
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Get pending workflows failed");
            return JsonSerializer.Serialize(
                new { Success = false, ErrorMessage = ex.Message },
                JsonOptions
            );
        }
    }
}

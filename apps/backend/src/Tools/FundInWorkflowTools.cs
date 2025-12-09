using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using AgentFrameworkQuickStart.Api.Middleware;
using AgentFrameworkQuickStart.Api.Workflows.FundIn;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for invoking the Fund-In Workflow with real-time progress streaming
/// Provides a high-level interface for the complete fund transfer process
/// Events are pushed directly via SignalR for real-time updates
///
/// Simplified flow (no OTP):
/// 1. Initialization
/// 2. Accounts & Preview (get transactionId)
/// 3. Confirmation (step-up token → ReadyToCommit)
/// 4. Commit (idempotency key → success)
/// </summary>
public class FundInWorkflowTools(
    StreamingFundInWorkflow streamingWorkflow,
    ILogger<FundInWorkflowTools> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Tools",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Tools", "1.0.0");

    private static readonly Counter<int> ToolInvocationsCounter = Meter.CreateCounter<int>(
        "fundin_tool_invocations",
        "invocations",
        "Number of Fund-In tool invocations"
    );

    // Store last result for retrieval
    private FundInWorkflowResult? _lastResult;

    [Description(
        """
            Execute the complete Fund-In workflow with real-time progress updates to transfer money 
            from a bank account to an investment portfolio. This is a 4-step process:
            1. Initialization - validate request
            2. Accounts & Preview - retrieve accounts, calculate fees, get transaction ID
            3. Confirmation - authorize with step-up token
            4. Commit - finalize transaction

            Shows step-by-step progress in the UI. Authorization is handled automatically 
            using step-up tokens (no OTP required).
            """
    )]
    public async Task<string> ExecuteFundInWorkflow(
        [Description("Customer CIF number (default: 100000000005)")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID for specific mutual fund (optional)")] string? fundId = null,
        [Description("Transaction notes (optional)")] string? notes = null
    )
    {
        using var activity = ActivitySource.StartActivity("ExecuteFundInWorkflow");
        ToolInvocationsCounter.Add(1);

        // Get conversation ID from middleware context for progress streaming
        var conversationId =
            DelegationEventMiddleware.CurrentConversationId ?? Guid.NewGuid().ToString();

        try
        {
            logger.LogInformation(
                "Executing streaming Fund-In workflow for conversation {ConversationId}: {Amount} {Currency}",
                conversationId,
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

            FundInWorkflowResult? result = null;
            Exception? error = null;

            var channelReader = streamingWorkflow.ExecuteAsync(
                request,
                null, // accessToken - step-up token is generated internally
                conversationId, // Pass conversation ID for SignalR progress updates
                onComplete: r =>
                {
                    result = r;
                    _lastResult = r;
                },
                onError: ex => error = ex
            );

            // Wait for channel to complete (progress is now pushed via SignalR in real-time)
            await foreach (var _ in channelReader.ReadAllAsync())
            {
                // Events are now pushed directly via SignalR in the workflow
                // This just awaits completion
            }

            if (error != null)
            {
                logger.LogError(error, "Streaming Fund-In workflow failed");
                return FormatErrorResult(error.Message);
            }

            if (result == null)
            {
                return FormatErrorResult("Workflow completed but no result was generated");
            }

            if (!result.Success)
            {
                logger.LogWarning(
                    "Fund-In workflow failed: {TransactionId}, Error: {Error}",
                    result.TransactionId,
                    result.ErrorMessage
                );
                return FormatErrorResult(result.ErrorMessage ?? "Unknown error");
            }

            logger.LogInformation(
                "Streaming Fund-In workflow completed: TransactionId={TransactionId}, Ref={RefNum}",
                result.TransactionId,
                result.ReferenceNumber
            );

            return FormatSuccessResult(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fund-In workflow execution failed");
            return FormatErrorResult(ex.Message);
        }
    }

    /// <summary>
    /// Get the last workflow result (for retrieval after streaming)
    /// </summary>
    public FundInWorkflowResult? GetLastResult() => _lastResult;

    #region Formatting Helpers

    private static string FormatSuccessResult(FundInWorkflowResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## ✅ Fund-In Transaction Completed Successfully\n");
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|-------|-------|");
        sb.AppendLine($"| Reference Number | {result.ReferenceNumber} |");
        sb.AppendLine($"| Transaction ID | {result.TransactionId} |");
        sb.AppendLine($"| Amount | {result.Amount:N2} {result.Currency} |");
        sb.AppendLine($"| Fees | {result.Fees:N2} {result.Currency} |");
        sb.AppendLine($"| Total Amount | {result.TotalAmount:N2} {result.Currency} |");

        if (result.Units > 0)
            sb.AppendLine($"| Units Purchased | {result.Units:N4} |");
        if (result.NavAtPurchase > 0)
            sb.AppendLine($"| NAV at Purchase | {result.NavAtPurchase:N4} |");
        if (!string.IsNullOrEmpty(result.FundName))
            sb.AppendLine($"| Fund Name | {result.FundName} |");

        sb.AppendLine($"| Source Account | {result.SourceAccountId} |");
        sb.AppendLine($"| Target Portfolio | {result.TargetPortfolioNumber} |");
        sb.AppendLine($"| Duration | {result.Duration.TotalMilliseconds:N0}ms |");

        sb.AppendLine();
        sb.AppendLine($"**English Summary**: {result.SummaryEn}");
        sb.AppendLine();
        sb.AppendLine($"**Arabic Summary**: {result.SummaryAr}");

        return sb.ToString();
    }

    private static string FormatErrorResult(string errorMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## ❌ Fund-In Transaction Failed\n");
        sb.AppendLine($"**Error**: {errorMessage}");
        return sb.ToString();
    }

    #endregion
}

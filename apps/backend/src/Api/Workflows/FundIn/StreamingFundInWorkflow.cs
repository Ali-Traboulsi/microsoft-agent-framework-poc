using System.Diagnostics;
using System.Threading.Channels;
using AgentFrameworkQuickStart.Api.Abstractions;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn;

/// <summary>
/// Step definitions for Fund-In workflow progress tracking (simplified 4-step flow)
/// تعريفات خطوات تتبع تقدم تحويل الأموال (4 خطوات مبسطة)
///
/// Flow: Initialization → Accounts/Preview → Confirmation (step-up token) → Commit
/// </summary>
public static class FundInWorkflowStepDefinitions
{
    /// <summary>
    /// Workflow step definitions with IDs, English names, and Arabic names
    /// </summary>
    public static readonly (string Id, string Name, string NameAr)[] Steps =
    [
        ("Initialization", "Initializing Fund-In request", "جاري تهيئة طلب التحويل"),
        (
            "AccountsAndPreview",
            "Retrieving accounts and calculating fees",
            "جاري استرجاع الحسابات وحساب الرسوم"
        ),
        (
            "Confirmation",
            "Confirming transaction with step-up token",
            "جاري تأكيد المعاملة برمز التصعيد"
        ),
        ("Commit", "Finalizing transaction", "جاري إتمام المعاملة"),
    ];

    /// <summary>
    /// Total number of steps in the workflow
    /// </summary>
    public static int TotalSteps => Steps.Length;

    /// <summary>
    /// Create a progress event for a specific workflow step
    /// </summary>
    public static WorkflowProgressEvent CreateProgressEvent(
        int stepIndex,
        bool isCompleted,
        long? durationMs = null,
        string? details = null
    )
    {
        if (stepIndex < 0 || stepIndex >= Steps.Length)
            throw new ArgumentOutOfRangeException(nameof(stepIndex));

        var step = Steps[stepIndex];
        return new WorkflowProgressEvent
        {
            StepId = step.Id,
            StepName = step.Name,
            StepNameAr = step.NameAr,
            StepNumber = stepIndex + 1,
            TotalSteps = TotalSteps,
            IsCompleted = isCompleted,
            DurationMs = durationMs,
            Details = details,
        };
    }

    /// <summary>
    /// Get step index by step ID
    /// </summary>
    public static int GetStepIndex(string stepId) => Array.FindIndex(Steps, s => s.Id == stepId);
}

/// <summary>
/// Streaming version of Fund-In Workflow with real-time progress updates
/// Provides ChannelReader-based progress streaming for UI updates via SignalR
///
/// Simplified 4-step flow:
/// 1. Initialization - validate request
/// 2. Accounts & Preview - get accounts, calculate fees, get transactionId
/// 3. Confirmation - confirm with step-up token, get "ReadyToCommit" status
/// 4. Commit - finalize transaction with idempotency key
/// </summary>
public class StreamingFundInWorkflow(
    AccountsRetriever accountsRetriever,
    PreviewExecutor previewExecutor,
    ConfirmationExecutor confirmationExecutor,
    CommitExecutor commitExecutor,
    WorkflowProgressNotifier progressNotifier,
    ILogger<StreamingFundInWorkflow> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.StreamingWorkflow",
        "1.0.0"
    );

    /// <summary>
    /// Execute complete workflow with progress streaming via Channel
    /// No OTP required - uses step-up token for authorization
    /// </summary>
    public ChannelReader<WorkflowProgressEvent> ExecuteAsync(
        FundInWorkflowRequest request,
        string? accessToken,
        string conversationId,
        Action<FundInWorkflowResult> onComplete,
        Action<Exception>? onError = null
    )
    {
        var channel = Channel.CreateUnbounded<WorkflowProgressEvent>();
        _ = ExecuteInternalAsync(
            request,
            accessToken,
            conversationId,
            channel.Writer,
            onComplete,
            onError
        );
        return channel.Reader;
    }

    private async Task ExecuteInternalAsync(
        FundInWorkflowRequest request,
        string? accessToken,
        string conversationId,
        ChannelWriter<WorkflowProgressEvent> writer,
        Action<FundInWorkflowResult> onComplete,
        Action<Exception>? onError
    )
    {
        using var activity = ActivitySource.StartActivity("StreamingFundIn");
        var stepStopwatch = new Stopwatch();
        var transactionId = Guid.NewGuid().ToString();
        var startedAt = DateTime.UtcNow;

        activity?.SetTag("transaction_id", transactionId);
        activity?.SetTag("amount", request.Amount);
        activity?.SetTag("currency", request.Currency);
        activity?.SetTag("cif", request.Cif);

        // Helper to emit progress via SignalR AND channel
        async Task EmitProgress(
            int stepIndex,
            bool isCompleted,
            long? durationMs = null,
            string? details = null
        )
        {
            var progressEvent = FundInWorkflowStepDefinitions.CreateProgressEvent(
                stepIndex,
                isCompleted,
                durationMs,
                details
            );

            // Write to channel for internal tracking
            await writer.WriteAsync(progressEvent);

            // Push directly to SignalR for real-time updates
            await progressNotifier.NotifyProgressAsync(
                conversationId,
                progressEvent.StepId,
                progressEvent.StepName,
                progressEvent.StepNameAr,
                progressEvent.StepNumber,
                progressEvent.TotalSteps,
                progressEvent.IsCompleted,
                progressEvent.DurationMs,
                progressEvent.Details
            );
        }

        try
        {
            // Step 0: Initialization
            stepStopwatch.Restart();
            await EmitProgress(0, false);
            await Task.Delay(50); // Brief pause for UI to render
            stepStopwatch.Stop();
            await EmitProgress(
                0,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Transaction {transactionId[..8]} initialized"
            );

            // Step 1: Accounts Retrieval & Preview
            stepStopwatch.Restart();
            await EmitProgress(1, false);

            var accountsContext = await accountsRetriever.ExecuteAsync(request, accessToken);

            if (!accountsContext.AccountFound)
            {
                throw new InvalidOperationException(
                    $"Account '{request.SourceAccountId}' not found. Available: {string.Join(", ", accountsContext.Accounts.Select(a => a.AccountId))}"
                );
            }

            if (!accountsContext.PortfolioFound)
            {
                throw new InvalidOperationException(
                    $"Portfolio '{request.TargetPortfolioNumber}' not found. Available: {string.Join(", ", accountsContext.Portfolios.Select(p => p.PortfolioNumber))}"
                );
            }

            var previewResult = await previewExecutor.ExecuteAsync(
                request,
                accountsContext,
                accessToken
            );

            if (!previewResult.Success || string.IsNullOrEmpty(previewResult.TransactionId))
            {
                throw new InvalidOperationException(
                    $"Preview failed: {previewResult.ErrorMessage}"
                );
            }

            // Use transactionId from preview response
            transactionId = previewResult.TransactionId;

            stepStopwatch.Stop();
            await EmitProgress(
                1,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Accounts verified. Fees: {previewResult.Fees:N2} {request.Currency}, Est. Units: {previewResult.EstimatedUnits:N4}"
            );

            // Step 2: Confirmation (with step-up token)
            stepStopwatch.Restart();
            await EmitProgress(2, false);

            // Generate step-up token for confirmation
            var stepUpToken = confirmationExecutor.GenerateStepUpToken();

            var confirmationResult = await confirmationExecutor.ExecuteAsync(
                request.Cif,
                transactionId,
                stepUpToken
            );

            if (!confirmationResult.Success || !confirmationResult.IsReadyToCommit)
            {
                throw new InvalidOperationException(
                    $"Confirmation failed: {confirmationResult.ErrorMessage}"
                );
            }

            stepStopwatch.Stop();
            await EmitProgress(
                2,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Transaction {transactionId[..8]} confirmed, status: {confirmationResult.Status}"
            );

            // Step 3: Commit (with step-up token and idempotency key)
            stepStopwatch.Restart();
            await EmitProgress(3, false);

            // Use same step-up token for commit
            var commitResult = await commitExecutor.ExecuteAsync(
                request.Cif,
                transactionId,
                stepUpToken
            );

            if (!commitResult.Success)
            {
                throw new InvalidOperationException($"Commit failed: {commitResult.ErrorMessage}");
            }

            stepStopwatch.Stop();
            var completedAt = DateTime.UtcNow;

            await EmitProgress(
                3,
                true,
                stepStopwatch.ElapsedMilliseconds,
                $"Transaction committed: {commitResult.ReferenceNumber}"
            );

            // Create successful result
            var result = new FundInWorkflowResult
            {
                Success = true,
                WorkflowId = transactionId, // Use transactionId as workflowId for consistency
                Status = FundInWorkflowStatus.Completed,
                TransactionId = transactionId,
                ReferenceNumber = commitResult.ReferenceNumber,
                Amount = commitResult.Amount,
                Currency = commitResult.Currency,
                Fees = previewResult.Fees,
                TotalAmount = previewResult.TotalAmount,
                Units = commitResult.Units,
                NavAtPurchase = commitResult.NavAtPurchase,
                SourceAccountId = request.SourceAccountId,
                TargetPortfolioNumber = request.TargetPortfolioNumber,
                FundName = previewResult.FundName,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                SummaryEn =
                    $"Successfully transferred {commitResult.Amount:N2} {commitResult.Currency} to portfolio {request.TargetPortfolioNumber}. Reference: {commitResult.ReferenceNumber}",
                SummaryAr =
                    $"تم تحويل {commitResult.Amount:N2} {commitResult.Currency} بنجاح إلى المحفظة {request.TargetPortfolioNumber}. المرجع: {commitResult.ReferenceNumber}",
            };

            onComplete(result);
            logger.LogInformation(
                "Streaming Fund-In workflow completed: TransactionId={TransactionId}, Ref={RefNum}",
                transactionId,
                commitResult.ReferenceNumber
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Streaming Fund-In workflow failed: TransactionId={TransactionId}",
                transactionId
            );

            var errorResult = new FundInWorkflowResult
            {
                Success = false,
                WorkflowId = transactionId,
                Status = FundInWorkflowStatus.Failed,
                TransactionId = transactionId,
                Amount = request.Amount,
                Currency = request.Currency,
                SourceAccountId = request.SourceAccountId,
                TargetPortfolioNumber = request.TargetPortfolioNumber,
                StartedAt = startedAt,
                ErrorMessage = ex.Message,
                SummaryEn = $"Fund-In failed: {ex.Message}",
                SummaryAr = $"فشل التحويل: {ex.Message}",
            };

            onComplete(errorResult);
            onError?.Invoke(ex);
        }
        finally
        {
            writer.Complete();
        }
    }
}

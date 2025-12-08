using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn;

/// <summary>
/// Fund-In Workflow - تحويل الأموال للاستثمار
///
/// Orchestrates the complete Fund-In process:
/// 1. Accounts Retrieval - Fetch customer accounts and portfolios
/// 2. Preview - Show fees and estimated units
/// 3. Start - Initiate transaction, trigger OTP
/// 4. OTP Verification - Verify customer identity
/// 5. Commit - Finalize transaction with step-up token
///
/// Features:
/// - Sequential workflow with clear edges
/// - Full OpenTelemetry observability
/// - Automatic step-up token generation
/// - Bilingual output (Arabic/English)
/// </summary>
public class FundInWorkflow(
    AccountsRetriever accountsRetriever,
    PreviewExecutor previewExecutor,
    StartExecutor startExecutor,
    OtpVerificationExecutor otpVerificationExecutor,
    CommitExecutor commitExecutor,
    ILogger<FundInWorkflow> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Workflow",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Workflow", "1.0.0");

    private static readonly Counter<int> WorkflowsCompletedCounter = Meter.CreateCounter<int>(
        "fundin_workflows_completed",
        "workflows",
        "Number of Fund-In workflows completed successfully"
    );

    private static readonly Counter<int> WorkflowsFailedCounter = Meter.CreateCounter<int>(
        "fundin_workflows_failed",
        "workflows",
        "Number of Fund-In workflows that failed"
    );

    private static readonly Histogram<double> WorkflowDurationHistogram =
        Meter.CreateHistogram<double>(
            "fundin_workflow_duration_ms",
            "ms",
            "Fund-In workflow duration in milliseconds"
        );

    /// <summary>
    /// Execute the complete Fund-In workflow (requires OTP)
    /// Use ExecuteUpToOtp + CompleteAfterOtp for interactive flows
    /// </summary>
    public async Task<FundInWorkflowResult> ExecuteAsync(
        FundInWorkflowRequest request,
        string otp,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInWorkflow");
        var sw = Stopwatch.StartNew();
        var workflowId = Guid.NewGuid().ToString();

        activity?.SetTag("workflow_id", workflowId);
        activity?.SetTag("cif", request.Cif);
        activity?.SetTag("amount", request.Amount);
        activity?.SetTag("currency", request.Currency);

        try
        {
            logger.LogInformation(
                "Starting Fund-In workflow {WorkflowId}: {Amount} {Currency}",
                workflowId,
                request.Amount,
                request.Currency
            );

            // Step 1: Fetch accounts
            var accountsContext = await accountsRetriever.ExecuteAsync(request, accessToken);

            if (!accountsContext.HasSufficientFunds)
            {
                return CreateFailedResult(
                    workflowId,
                    request,
                    FundInWorkflowStep.AccountsRetrieval,
                    $"Insufficient funds. Required: {request.Amount:N2} {request.Currency}, "
                        + $"Available: {accountsContext.SelectedAccountBalance:N2} {request.Currency}",
                    sw.Elapsed
                );
            }

            // Step 2: Preview
            var preview = await previewExecutor.ExecuteAsync(request, accountsContext, accessToken);

            if (!preview.Success)
            {
                return CreateFailedResult(
                    workflowId,
                    request,
                    FundInWorkflowStep.Preview,
                    preview.ErrorMessage ?? "Preview failed",
                    sw.Elapsed
                );
            }

            // Step 3: Start
            var start = await startExecutor.ExecuteAsync(request, preview, accessToken);

            if (!start.Success)
            {
                return CreateFailedResult(
                    workflowId,
                    request,
                    FundInWorkflowStep.Start,
                    start.ErrorMessage ?? "Start failed",
                    sw.Elapsed
                );
            }

            // Step 4: Verify OTP
            var otpResult = await otpVerificationExecutor.ExecuteAsync(
                request.Cif,
                start.TransactionId!,
                otp,
                accessToken
            );

            if (!otpResult.Success || !otpResult.IsVerified)
            {
                return CreateFailedResult(
                    workflowId,
                    request,
                    FundInWorkflowStep.OtpVerification,
                    otpResult.ErrorMessage ?? "OTP verification failed",
                    sw.Elapsed
                );
            }

            // Step 5: Generate step-up token and commit
            var stepUpToken = commitExecutor.GenerateStepUpToken();

            var commit = await commitExecutor.ExecuteAsync(
                request.Cif,
                start.TransactionId!,
                stepUpToken
            );

            if (!commit.Success)
            {
                return CreateFailedResult(
                    workflowId,
                    request,
                    FundInWorkflowStep.Commit,
                    commit.ErrorMessage ?? "Commit failed",
                    sw.Elapsed
                );
            }

            sw.Stop();
            WorkflowsCompletedCounter.Add(1);
            WorkflowDurationHistogram.Record(sw.ElapsedMilliseconds);

            logger.LogInformation(
                "Fund-In workflow {WorkflowId} completed in {Duration}ms. Reference: {Reference}",
                workflowId,
                sw.ElapsedMilliseconds,
                commit.ReferenceNumber
            );

            return new FundInWorkflowResult
            {
                Success = true,
                WorkflowId = workflowId,
                Status = FundInWorkflowStatus.Completed,
                TransactionId = commit.TransactionId,
                ReferenceNumber = commit.ReferenceNumber,
                Amount = commit.Amount,
                Currency = commit.Currency,
                Fees = preview.Fees,
                TotalAmount = preview.TotalAmount,
                Units = commit.Units,
                NavAtPurchase = commit.NavAtPurchase,
                SourceAccountId = request.SourceAccountId,
                TargetPortfolioNumber = request.TargetPortfolioNumber,
                FundName = preview.FundName,
                StartedAt = request.RequestedAt,
                CompletedAt = DateTime.UtcNow,
                SummaryEn =
                    $"Successfully transferred {commit.Amount:N2} {commit.Currency} "
                    + $"to portfolio {request.TargetPortfolioNumber}. "
                    + $"Reference: {commit.ReferenceNumber}",
                SummaryAr =
                    $"تم تحويل {commit.Amount:N2} {commit.Currency} بنجاح "
                    + $"إلى المحفظة {request.TargetPortfolioNumber}. "
                    + $"المرجع: {commit.ReferenceNumber}",
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            WorkflowsFailedCounter.Add(1);
            WorkflowDurationHistogram.Record(sw.ElapsedMilliseconds);

            logger.LogError(ex, "Fund-In workflow {WorkflowId} failed", workflowId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return CreateFailedResult(
                workflowId,
                request,
                FundInWorkflowStep.AccountsRetrieval,
                ex.Message,
                sw.Elapsed
            );
        }
    }

    /// <summary>
    /// Execute workflow up to OTP (Preview → Start)
    /// Returns state to continue after OTP is received
    /// </summary>
    public async Task<FundInWorkflowState> ExecuteUpToOtpAsync(
        FundInWorkflowRequest request,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInWorkflow.UpToOtp");
        var workflowId = Guid.NewGuid().ToString();

        activity?.SetTag("workflow_id", workflowId);
        activity?.SetTag("cif", request.Cif);
        activity?.SetTag("amount", request.Amount);

        try
        {
            logger.LogInformation(
                "Starting Fund-In workflow (up to OTP) {WorkflowId}: {Amount} {Currency}",
                workflowId,
                request.Amount,
                request.Currency
            );

            // Step 1: Fetch accounts
            var accountsContext = await accountsRetriever.ExecuteAsync(request, accessToken);

            if (!accountsContext.HasSufficientFunds)
            {
                return new FundInWorkflowState
                {
                    WorkflowId = workflowId,
                    Request = request,
                    Status = FundInWorkflowStatus.Failed,
                    AccountsContext = accountsContext,
                    FailedAtStep = FundInWorkflowStep.AccountsRetrieval,
                    ErrorMessage = "Insufficient funds",
                };
            }

            // Step 2: Preview
            var preview = await previewExecutor.ExecuteAsync(request, accountsContext, accessToken);

            if (!preview.Success)
            {
                return new FundInWorkflowState
                {
                    WorkflowId = workflowId,
                    Request = request,
                    Status = FundInWorkflowStatus.Failed,
                    AccountsContext = accountsContext,
                    Preview = preview,
                    FailedAtStep = FundInWorkflowStep.Preview,
                    ErrorMessage = preview.ErrorMessage,
                };
            }

            // Step 3: Start (triggers OTP)
            var start = await startExecutor.ExecuteAsync(request, preview, accessToken);

            if (!start.Success)
            {
                return new FundInWorkflowState
                {
                    WorkflowId = workflowId,
                    Request = request,
                    Status = FundInWorkflowStatus.Failed,
                    AccountsContext = accountsContext,
                    Preview = preview,
                    Start = start,
                    FailedAtStep = FundInWorkflowStep.Start,
                    ErrorMessage = start.ErrorMessage,
                };
            }

            logger.LogInformation(
                "Workflow {WorkflowId} awaiting OTP. Transaction: {TransactionId}",
                workflowId,
                start.TransactionId
            );

            return new FundInWorkflowState
            {
                WorkflowId = workflowId,
                Request = request,
                Status = FundInWorkflowStatus.AwaitingOtp,
                AccountsContext = accountsContext,
                Preview = preview,
                Start = start,
                AccessToken = accessToken,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fund-In workflow (up to OTP) {WorkflowId} failed", workflowId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return new FundInWorkflowState
            {
                WorkflowId = workflowId,
                Request = request,
                Status = FundInWorkflowStatus.Failed,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <summary>
    /// Complete workflow after OTP verification (Verify → Commit)
    /// </summary>
    public async Task<FundInWorkflowResult> CompleteAfterOtpAsync(
        FundInWorkflowState state,
        string otp
    )
    {
        using var activity = ActivitySource.StartActivity("FundInWorkflow.AfterOtp");
        var sw = Stopwatch.StartNew();

        activity?.SetTag("workflow_id", state.WorkflowId);
        activity?.SetTag("transaction_id", state.Start?.TransactionId);

        try
        {
            if (state.Status != FundInWorkflowStatus.AwaitingOtp)
            {
                return CreateFailedResult(
                    state.WorkflowId,
                    state.Request,
                    FundInWorkflowStep.OtpVerification,
                    $"Invalid workflow state: {state.Status}",
                    sw.Elapsed
                );
            }

            logger.LogInformation(
                "Completing Fund-In workflow {WorkflowId} after OTP",
                state.WorkflowId
            );

            // Step 4: Verify OTP
            var otpResult = await otpVerificationExecutor.ExecuteAsync(
                state.Request.Cif,
                state.Start!.TransactionId!,
                otp,
                state.AccessToken
            );

            if (!otpResult.Success || !otpResult.IsVerified)
            {
                return CreateFailedResult(
                    state.WorkflowId,
                    state.Request,
                    FundInWorkflowStep.OtpVerification,
                    otpResult.ErrorMessage ?? "OTP verification failed",
                    sw.Elapsed
                );
            }

            // Step 5: Generate step-up token
            var stepUpToken = commitExecutor.GenerateStepUpToken();

            // Step 6: Commit
            var commit = await commitExecutor.ExecuteAsync(
                state.Request.Cif,
                state.Start.TransactionId!,
                stepUpToken
            );

            if (!commit.Success)
            {
                return CreateFailedResult(
                    state.WorkflowId,
                    state.Request,
                    FundInWorkflowStep.Commit,
                    commit.ErrorMessage ?? "Commit failed",
                    sw.Elapsed
                );
            }

            sw.Stop();
            WorkflowsCompletedCounter.Add(1);
            WorkflowDurationHistogram.Record(sw.ElapsedMilliseconds);

            logger.LogInformation(
                "Fund-In workflow {WorkflowId} completed. Reference: {Reference}",
                state.WorkflowId,
                commit.ReferenceNumber
            );

            return new FundInWorkflowResult
            {
                Success = true,
                WorkflowId = state.WorkflowId,
                Status = FundInWorkflowStatus.Completed,
                TransactionId = commit.TransactionId,
                ReferenceNumber = commit.ReferenceNumber,
                Amount = commit.Amount,
                Currency = commit.Currency,
                Fees = state.Preview?.Fees ?? 0,
                TotalAmount = state.Preview?.TotalAmount ?? commit.Amount,
                Units = commit.Units,
                NavAtPurchase = commit.NavAtPurchase,
                SourceAccountId = state.Request.SourceAccountId,
                TargetPortfolioNumber = state.Request.TargetPortfolioNumber,
                FundName = state.Preview?.FundName,
                StartedAt = state.StartedAt,
                CompletedAt = DateTime.UtcNow,
                SummaryEn =
                    $"Successfully transferred {commit.Amount:N2} {commit.Currency} "
                    + $"to portfolio {state.Request.TargetPortfolioNumber}. "
                    + $"Reference: {commit.ReferenceNumber}",
                SummaryAr =
                    $"تم تحويل {commit.Amount:N2} {commit.Currency} بنجاح "
                    + $"إلى المحفظة {state.Request.TargetPortfolioNumber}. "
                    + $"المرجع: {commit.ReferenceNumber}",
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            WorkflowsFailedCounter.Add(1);

            logger.LogError(
                ex,
                "Fund-In workflow {WorkflowId} completion failed",
                state.WorkflowId
            );
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            return CreateFailedResult(
                state.WorkflowId,
                state.Request,
                FundInWorkflowStep.Commit,
                ex.Message,
                sw.Elapsed
            );
        }
    }

    private static FundInWorkflowResult CreateFailedResult(
        string workflowId,
        FundInWorkflowRequest request,
        FundInWorkflowStep failedStep,
        string errorMessage,
        TimeSpan duration
    )
    {
        return new FundInWorkflowResult
        {
            Success = false,
            WorkflowId = workflowId,
            Status = FundInWorkflowStatus.Failed,
            Amount = request.Amount,
            Currency = request.Currency,
            SourceAccountId = request.SourceAccountId,
            TargetPortfolioNumber = request.TargetPortfolioNumber,
            StartedAt = request.RequestedAt,
            CompletedAt = DateTime.UtcNow,
            FailedAtStep = failedStep,
            ErrorMessage = errorMessage,
            SummaryEn = $"Fund transfer failed at {failedStep}: {errorMessage}",
            SummaryAr = $"فشل التحويل في خطوة {failedStep}: {errorMessage}",
        };
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Starts the Fund-In transaction and triggers OTP
/// Step 3: Transaction Start
/// </summary>
public class StartExecutor(FundInService fundInService, ILogger<StartExecutor> logger)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Start",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Start", "1.0.0");

    private static readonly Counter<int> TransactionsStartedCounter = Meter.CreateCounter<int>(
        "fundin_transactions_started",
        "transactions",
        "Number of Fund-In transactions started"
    );

    /// <summary>
    /// Start Fund-In transaction (sends OTP)
    /// </summary>
    public async Task<StartResult> ExecuteAsync(
        FundInWorkflowRequest request,
        PreviewResult preview,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInStart");
        activity?.SetTag("cif", request.Cif);
        activity?.SetTag("amount", request.Amount);
        activity?.SetTag("preview_transaction_id", preview.TransactionId);

        try
        {
            logger.LogInformation(
                "Starting Fund-In transaction: {Amount} {Currency}",
                request.Amount,
                request.Currency
            );

            var startRequest = new FundInStartRequest
            {
                SourceAccountId = request.SourceAccountId,
                TargetPortfolioNumber = request.TargetPortfolioNumber,
                Amount = request.Amount,
                Currency = request.Currency,
                FundId = request.FundId,
                Notes = request.Notes,
            };

            var response = await fundInService.StartFundInAsync(
                request.Cif,
                startRequest,
                accessToken
            );

            if (!response.Success)
            {
                logger.LogWarning(
                    "Start failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new StartResult
                {
                    Success = false,
                    Status = "Failed",
                    ErrorMessage = response.Message ?? "Failed to start transaction",
                };
            }

            var data = response.Data!;

            TransactionsStartedCounter.Add(1);

            activity?.SetTag("transaction_id", data.TransactionId);
            activity?.SetTag("otp_sent_to", data.OtpSentTo);
            activity?.SetTag("otp_expiry_seconds", data.OtpExpirySeconds);

            logger.LogInformation(
                "Transaction started: TransactionId={TransactionId}, OTP sent to {OtpSentTo}",
                data.TransactionId,
                data.OtpSentTo
            );

            return new StartResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                Status = data.Status ?? "Pending",
                OtpSentTo = data.OtpSentTo,
                OtpExpirySeconds = data.OtpExpirySeconds ?? 180,
                CreatedAt = data.CreatedAt,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Start execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

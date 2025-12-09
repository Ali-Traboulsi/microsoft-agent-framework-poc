using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Confirms the Fund-In transaction using step-up token
/// Step 3: Transaction Confirmation
///
/// Flow:
/// 1. Preview returns transactionId
/// 2. Confirmation sends transactionId + step-up token header
/// 3. Returns "ReadyToCommit" status when authorized
/// </summary>
public class ConfirmationExecutor(
    FundInService fundInService,
    TestTokenService testTokenService,
    ILogger<ConfirmationExecutor> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Confirmation",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Confirmation", "1.0.0");

    private static readonly Counter<int> TransactionsConfirmedCounter = Meter.CreateCounter<int>(
        "fundin_transactions_confirmed",
        "transactions",
        "Number of Fund-In transactions confirmed with step-up token"
    );

    private static readonly Counter<int> ConfirmationFailedCounter = Meter.CreateCounter<int>(
        "fundin_confirmation_failed",
        "failures",
        "Number of failed Fund-In confirmations"
    );

    /// <summary>
    /// Generate step-up token for confirmation authorization
    /// </summary>
    public string GenerateStepUpToken()
    {
        logger.LogInformation("Generating step-up token for Fund-In confirmation");
        return testTokenService.GenerateTestToken(includeStepUpScope: true, expiryHours: 1);
    }

    /// <summary>
    /// Confirm Fund-In transaction with step-up token
    /// </summary>
    /// <param name="cif">Customer identification</param>
    /// <param name="transactionId">Transaction ID from preview</param>
    /// <param name="stepUpToken">Step-up token for authorization</param>
    /// <returns>Confirmation result with IsReadyToCommit flag</returns>
    public async Task<ConfirmationResult> ExecuteAsync(
        string cif,
        string transactionId,
        string stepUpToken
    )
    {
        using var activity = ActivitySource.StartActivity("FundInConfirmation");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);
        activity?.SetTag("has_step_up_token", !string.IsNullOrEmpty(stepUpToken));

        try
        {
            logger.LogInformation("Confirming Fund-In transaction: {TransactionId}", transactionId);

            // Request only contains TransactionId
            // Step-up token is passed in the Authorization header
            var confirmRequest = new FundInConfirmStartRequest { TransactionId = transactionId };

            var response = await fundInService.ConfirmStartFundInAsync(
                cif,
                confirmRequest,
                stepUpToken
            );

            if (!response.Success)
            {
                ConfirmationFailedCounter.Add(1);

                logger.LogWarning(
                    "Confirmation failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new ConfirmationResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    Status = "Failed",
                    IsReadyToCommit = false,
                    ErrorMessage = response.Message ?? "Failed to confirm transaction",
                };
            }

            var data = response.Data!;

            // Check if the response indicates ready to commit
            if (!data.IsReadyToCommit)
            {
                ConfirmationFailedCounter.Add(1);

                logger.LogWarning(
                    "Transaction not ready to commit: {TransactionId}, Status: {Status}",
                    transactionId,
                    data.Status
                );

                return new ConfirmationResult
                {
                    Success = false,
                    TransactionId = data.TransactionId,
                    Status = data.Status ?? "Pending",
                    IsReadyToCommit = false,
                    ErrorMessage =
                        "Transaction is not ready to commit. Step-up token may be invalid.",
                };
            }

            TransactionsConfirmedCounter.Add(1);

            activity?.SetTag("status", data.Status);
            activity?.SetTag("is_ready_to_commit", data.IsReadyToCommit);

            logger.LogInformation(
                "Transaction confirmed: TransactionId={TransactionId}, Status={Status}, ReadyToCommit={Ready}",
                data.TransactionId,
                data.Status,
                data.IsReadyToCommit
            );

            return new ConfirmationResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                Status = data.Status ?? "ReadyToCommit",
                IsReadyToCommit = true,
                ConfirmedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            ConfirmationFailedCounter.Add(1);
            logger.LogError(ex, "Confirmation execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

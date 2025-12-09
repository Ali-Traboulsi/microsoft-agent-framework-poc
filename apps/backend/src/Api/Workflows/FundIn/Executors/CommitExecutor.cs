using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Commits/finalizes the Fund-In transaction
/// Step 5: Transaction Commit
/// </summary>
public class CommitExecutor(
    FundInService fundInService,
    TestTokenService testTokenService,
    ILogger<CommitExecutor> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.Commit",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.Commit", "1.0.0");

    private static readonly Counter<int> CommitsSuccessCounter = Meter.CreateCounter<int>(
        "fundin_commits_success",
        "commits",
        "Number of successful Fund-In commits"
    );

    private static readonly Counter<int> CommitsFailedCounter = Meter.CreateCounter<int>(
        "fundin_commits_failed",
        "failures",
        "Number of failed Fund-In commits"
    );

    private static readonly Histogram<double> CommitAmountHistogram = Meter.CreateHistogram<double>(
        "fundin_commit_amount",
        "SAR",
        "Committed transaction amounts"
    );

    /// <summary>
    /// Generate step-up token for commit authorization
    /// </summary>
    public string GenerateStepUpToken()
    {
        logger.LogInformation("Generating step-up token for Fund-In commit");
        return testTokenService.GenerateTestToken(includeStepUpScope: true, expiryHours: 1);
    }

    /// <summary>
    /// Commit Fund-In transaction
    /// </summary>
    public async Task<CommitResult> ExecuteAsync(
        string cif,
        string transactionId,
        string stepUpToken
    )
    {
        // Generate idempotency key for this commit
        var idempotencyKey = Guid.NewGuid().ToString();

        using var activity = ActivitySource.StartActivity("FundInCommit");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);
        activity?.SetTag("idempotency_key", idempotencyKey);
        activity?.SetTag("has_step_up_token", !string.IsNullOrEmpty(stepUpToken));

        try
        {
            logger.LogInformation(
                "Committing Fund-In transaction: {TransactionId}, IdempotencyKey: {IdempotencyKey}",
                transactionId,
                idempotencyKey
            );

            var commitRequest = new FundInCommitRequest
            {
                TransactionId = transactionId,
                IdempotencyKey = idempotencyKey,
            };

            var response = await fundInService.CommitFundInAsync(cif, commitRequest, stepUpToken);

            if (!response.Success)
            {
                CommitsFailedCounter.Add(1);

                logger.LogWarning(
                    "Commit failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new CommitResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    Status = "Failed",
                    ErrorMessage = response.Message ?? "Failed to commit transaction",
                };
            }

            var data = response.Data!;

            CommitsSuccessCounter.Add(1);
            CommitAmountHistogram.Record((double)data.DebitAmount);

            activity?.SetTag("reference_number", data.PaymentReferenceId);
            activity?.SetTag("amount", data.DebitAmount);
            activity?.SetTag("debit_currency", data.DebitCurrency);

            logger.LogInformation(
                "Transaction committed: Reference={Reference}, Amount={Amount} {Currency}",
                data.PaymentReferenceId,
                data.DebitAmount,
                data.DebitCurrency
            );

            return new CommitResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                ReferenceNumber = data.PaymentReferenceId,
                Status = data.Status ?? "Completed",
                Amount = data.DebitAmount,
                Currency = data.DebitCurrency ?? "SAR",
                // Units and NavAtPurchase are not provided in commit response
                // These would come from a separate holdings API call
                Units = null,
                NavAtPurchase = null,
                CompletedAt = data.CompletedAt ?? DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            CommitsFailedCounter.Add(1);
            logger.LogError(ex, "Commit execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

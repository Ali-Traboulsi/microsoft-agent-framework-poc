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
        using var activity = ActivitySource.StartActivity("FundInCommit");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);
        activity?.SetTag("has_step_up_token", !string.IsNullOrEmpty(stepUpToken));

        try
        {
            logger.LogInformation("Committing Fund-In transaction: {TransactionId}", transactionId);

            var commitRequest = new FundInCommitRequest { TransactionId = transactionId };

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
            if (data.Amount.HasValue)
            {
                CommitAmountHistogram.Record((double)data.Amount.Value);
            }

            activity?.SetTag("reference_number", data.ReferenceNumber);
            activity?.SetTag("amount", data.Amount);
            activity?.SetTag("units", data.Units);

            logger.LogInformation(
                "Transaction committed: Reference={Reference}, Amount={Amount}, Units={Units}",
                data.ReferenceNumber,
                data.Amount,
                data.Units
            );

            return new CommitResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                ReferenceNumber = data.ReferenceNumber,
                Status = data.Status ?? "Completed",
                Amount = data.Amount ?? 0,
                Currency = data.Currency ?? "SAR",
                Units = data.Units,
                NavAtPurchase = data.NavAtPurchase,
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

using System.ComponentModel;
using System.Text.Json;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for Fund-In operations with SNB Capital
/// Provides low-level API access for account discovery and transaction management
///
/// For complete fund-in workflow, use FundInWorkflowTools.ExecuteFundInWorkflow instead.
/// </summary>
public class FundInTools(
    FundInService fundInService,
    TestTokenService testTokenService,
    ILogger<FundInTools> logger
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Description(
        "Get customer bank accounts available for fund-in transfers. Returns account details including balance and currency."
    )]
    public async Task<string> GetCustomerAccounts([Description("Customer CIF number")] string cif)
    {
        try
        {
            logger.LogInformation("Fetching customer accounts for CIF: {CIF}", cif);
            var response = await fundInService.GetCustomerAccountsAsync(cif);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to fetch accounts"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    TotalAccounts = response.Data?.TotalCount ?? 0,
                    Accounts = response.Data?.CurrentAccounts?.Select(a => new
                    {
                        a.AccountId,
                        HolderName = a.AccountHolderNameInEnglish,
                        a.AccountBalance,
                        Currency = a.CurrencyCode,
                        AccountType = a.AccountTypeDescriptionEnglish,
                    }),
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching customer accounts");
            return $"Error: {ex.Message}";
        }
    }

    [Description("Get portfolios linked to customer accounts for fund-in operations.")]
    public async Task<string> GetAccountPortfolios([Description("Customer CIF number")] string cif)
    {
        try
        {
            logger.LogInformation("Fetching portfolios for CIF: {CIF}", cif);
            var response = await fundInService.GetCustomerAccountPortfoliosAsync(cif);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to fetch portfolios"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    TotalPortfolios = response.Data?.Count ?? 0,
                    Portfolios = response.Data?.Select(p => new
                    {
                        p.PortfolioNumber,
                        p.PortfolioName,
                        p.PortfolioType,
                        p.Currency,
                        p.Status,
                    }),
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching portfolios");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Preview a fund-in transaction to see fees and estimated units before confirming. Returns transaction preview with fees, estimated units, and transaction ID."
    )]
    public async Task<string> PreviewFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID if investing in a specific mutual fund (optional)")]
            string? fundId = null,
        [Description("Optional notes for the transaction")] string? notes = null,
        [Description("Access token for authorization (optional)")] string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation(
                "Previewing fund-in: {Amount} {Currency} from {Account} to {Portfolio}",
                amount,
                currency,
                sourceAccountId,
                targetPortfolioNumber
            );

            var request = new FundInPreviewRequest
            {
                SourceAccountId = sourceAccountId,
                TargetPortfolioNumber = targetPortfolioNumber,
                Amount = amount,
            };

            var response = await fundInService.PreviewFundInAsync(cif, request, accessToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to preview fund-in"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    Preview = new
                    {
                        response.Data?.TransactionId,
                        response.Data?.Amount,
                        response.Data?.Currency,
                        response.Data?.Fees,
                        response.Data?.TotalAmount,
                    },
                    NextStep = "Use ConfirmFundIn with transactionId to proceed",
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing fund-in");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Confirm a fund-in transaction after preview. Uses step-up token for authorization. Returns ReadyToCommit status when successful."
    )]
    public async Task<string> ConfirmFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID from PreviewFundIn")] string transactionId,
        [Description("Access token for authorization (optional - step-up token will be generated)")]
            string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Confirming fund-in transaction: {TransactionId}", transactionId);

            // Generate step-up token if not provided
            var stepUpToken =
                accessToken
                ?? testTokenService.GenerateTestToken(includeStepUpScope: true, expiryHours: 1);

            var request = new FundInConfirmStartRequest { TransactionId = transactionId };

            var response = await fundInService.ConfirmStartFundInAsync(cif, request, stepUpToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to confirm fund-in"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    Confirmation = new
                    {
                        response.Data?.Otp,
                        response.Data?.Status,
                        response.Data?.IsReadyToCommit,
                    },
                    NextStep = response.Data?.IsReadyToCommit == true
                        ? "Use CommitFundIn with transactionId to complete the transaction"
                        : "Transaction not ready to commit - check status",
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming fund-in");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Commit and finalize a fund-in transaction. This completes the money transfer. "
            + "REQUIRES confirmation first (ConfirmFundIn must return IsReadyToCommit=true)."
    )]
    public async Task<string> CommitFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID from PreviewFundIn/ConfirmFundIn")] string transactionId,
        [Description("Access token for authorization (optional - step-up token will be generated)")]
            string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Committing fund-in transaction: {TransactionId}", transactionId);

            // Generate step-up token if not provided
            var stepUpToken =
                accessToken
                ?? testTokenService.GenerateTestToken(includeStepUpScope: true, expiryHours: 1);

            // Generate idempotency key for this commit
            var idempotencyKey = Guid.NewGuid().ToString();

            var request = new FundInCommitRequest
            {
                TransactionId = transactionId,
                IdempotencyKey = idempotencyKey,
            };

            var response = await fundInService.CommitFundInAsync(cif, request, stepUpToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to commit fund-in"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    CompletedTransaction = new
                    {
                        response.Data?.TransactionId,
                        ReferenceNumber = response.Data?.PaymentReferenceId,
                        response.Data?.Status,
                        Amount = response.Data?.DebitAmount,
                        Currency = response.Data?.DebitCurrency,
                        response.Data?.CompletedAt,
                    },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error committing fund-in");
            return $"Error: {ex.Message}";
        }
    }

    [Description("Get the current status of a fund-in transaction.")]
    public async Task<string> GetFundInStatus(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID")] string transactionId,
        [Description("Access token for authorization (optional)")] string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Getting status for transaction: {TransactionId}", transactionId);

            var response = await fundInService.GetTransactionStatusAsync(
                cif,
                transactionId,
                accessToken
            );

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to get status"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    Status = new
                    {
                        response.Data?.TransactionId,
                        response.Data?.Status,
                        response.Data?.Amount,
                        response.Data?.Currency,
                        response.Data?.CreatedAt,
                        response.Data?.CompletedAt,
                    },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting fund-in status");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Generate a test access token for development/testing. Use includeStepUp=true for commit operations."
    )]
    public string GenerateTestToken(
        [Description("Include step-up scope (required for CommitFundIn)")]
            bool includeStepUp = false,
        [Description("Token expiry in hours (default: 24)")] int expiryHours = 24
    )
    {
        try
        {
            logger.LogInformation(
                "Generating test token. StepUp: {StepUp}, Expiry: {Expiry}h",
                includeStepUp,
                expiryHours
            );

            var token = testTokenService.GenerateTestToken(includeStepUp, expiryHours);

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    Token = token,
                    IncludesStepUpScope = includeStepUp,
                    ExpiresIn = $"{expiryHours} hours",
                    Note = includeStepUp
                        ? "This token can be used for CommitFundIn"
                        : "This token can be used for preview/confirm operations",
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating test token");
            return $"Error: {ex.Message}";
        }
    }
}

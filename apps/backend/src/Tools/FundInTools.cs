using System.ComponentModel;
using System.Text.Json;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for Fund-In operations with SNB Capital
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
            logger.LogInformation("Fetching account portfolios for CIF: {CIF}", cif);
            var response = await fundInService.GetCustomerAccountPortfoliosAsync(cif);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to fetch portfolios"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
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
            logger.LogError(ex, "Error fetching account portfolios");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Preview a fund-in transaction before confirmation. Shows fees, estimated units, and total amount. Use this before starting a fund-in."
    )]
    public async Task<string> PreviewFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID if investing in a specific mutual fund (optional)")]
            string? fundId = null,
        [Description(
            "Access token for authorization (optional - use GenerateTestToken to create one)"
        )]
            string? accessToken = null
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
                Currency = currency,
                FundId = fundId,
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
                        response.Data?.SourceAccountId,
                        response.Data?.TargetPortfolioNumber,
                        response.Data?.FundName,
                        response.Data?.EstimatedUnits,
                        response.Data?.CurrentNav,
                        response.Data?.ExpiresAt,
                    },
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
        "Start a fund-in transaction. This will send an OTP to the customer for verification. Call PreviewFundIn first to see transaction details."
    )]
    public async Task<string> StartFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Source bank account ID")] string sourceAccountId,
        [Description("Target portfolio number")] string targetPortfolioNumber,
        [Description("Amount to transfer")] decimal amount,
        [Description("Currency (default: SAR)")] string currency = "SAR",
        [Description("Fund ID if investing in a specific mutual fund (optional)")]
            string? fundId = null,
        [Description("Optional notes for the transaction")] string? notes = null,
        [Description(
            "Access token for authorization (optional - use GenerateTestToken to create one)"
        )]
            string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation(
                "Starting fund-in: {Amount} {Currency} from {Account} to {Portfolio}",
                amount,
                currency,
                sourceAccountId,
                targetPortfolioNumber
            );

            var request = new FundInStartRequest
            {
                SourceAccountId = sourceAccountId,
                TargetPortfolioNumber = targetPortfolioNumber,
                Amount = amount,
                Currency = currency,
                FundId = fundId,
                Notes = notes,
            };

            var response = await fundInService.StartFundInAsync(cif, request, accessToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to start fund-in"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    Transaction = new
                    {
                        response.Data?.TransactionId,
                        response.Data?.Status,
                        response.Data?.OtpSentTo,
                        response.Data?.OtpExpirySeconds,
                        response.Data?.CreatedAt,
                    },
                    NextStep = "Customer needs to verify OTP using VerifyFundInOtp",
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting fund-in");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Verify OTP for a pending fund-in transaction. Call this after the customer receives and provides their OTP."
    )]
    public async Task<string> VerifyFundInOtp(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID from StartFundIn")] string transactionId,
        [Description("OTP code provided by customer")] string otp,
        [Description(
            "Access token for authorization (optional - use GenerateTestToken to create one)"
        )]
            string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Verifying OTP for transaction: {TransactionId}", transactionId);

            var request = new FundInVerifyOtpRequest { TransactionId = transactionId, Otp = otp };

            var response = await fundInService.VerifyOtpAsync(cif, request, accessToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "OTP verification failed"} (Code: {response.ErrorCode})";

            var result = new
            {
                response.Success,
                response.Message,
                Verification = new
                {
                    response.Data?.TransactionId,
                    response.Data?.Status,
                    response.Data?.IsVerified,
                    response.Data?.RemainingAttempts,
                },
                NextStep = response.Data?.IsVerified == true
                    ? "OTP verified. Call CommitFundIn to complete the transaction"
                    : "OTP not verified. Check remaining attempts or resend OTP",
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying OTP");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Resend OTP for a pending fund-in transaction if the customer didn't receive it or it expired."
    )]
    public async Task<string> ResendFundInOtp(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID from StartFundIn")] string transactionId,
        [Description("Access token for authorization (optional)")] string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Resending OTP for transaction: {TransactionId}", transactionId);

            var request = new FundInResendOtpRequest { TransactionId = transactionId };

            var response = await fundInService.ResendOtpAsync(cif, request, accessToken);

            if (!response.Success)
                return $"Error: {response.Message ?? "Failed to resend OTP"} (Code: {response.ErrorCode})";

            return JsonSerializer.Serialize(
                new
                {
                    response.Success,
                    response.Message,
                    OtpResent = new
                    {
                        response.Data?.TransactionId,
                        response.Data?.OtpSentTo,
                        response.Data?.OtpExpirySeconds,
                        response.Data?.ResendCount,
                        response.Data?.MaxResendAttempts,
                    },
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resending OTP");
            return $"Error: {ex.Message}";
        }
    }

    [Description(
        "Commit and finalize a fund-in transaction after OTP verification. This completes the money transfer. REQUIRES a step-up access token with bb-su:snbc-fund-in scope."
    )]
    public async Task<string> CommitFundIn(
        [Description("Customer CIF number")] string cif,
        [Description("Transaction ID from StartFundIn")] string transactionId,
        [Description(
            "Access token with step-up scope (required - use GenerateTestToken with includeStepUp=true)"
        )]
            string? accessToken = null
    )
    {
        try
        {
            logger.LogInformation("Committing fund-in transaction: {TransactionId}", transactionId);

            var request = new FundInCommitRequest { TransactionId = transactionId };

            var response = await fundInService.CommitFundInAsync(cif, request, accessToken);

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
                        response.Data?.ReferenceNumber,
                        response.Data?.Status,
                        response.Data?.Amount,
                        response.Data?.Currency,
                        response.Data?.Units,
                        response.Data?.NavAtPurchase,
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
                    TransactionStatus = new
                    {
                        response.Data?.TransactionId,
                        response.Data?.Status,
                        response.Data?.StatusDescription,
                        response.Data?.Amount,
                        response.Data?.Currency,
                        response.Data?.SourceAccountId,
                        response.Data?.TargetPortfolioNumber,
                        response.Data?.FundId,
                        response.Data?.CreatedAt,
                        response.Data?.UpdatedAt,
                        response.Data?.CompletedAt,
                        response.Data?.ReferenceNumber,
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

    #region Token Generation

    [Description(
        "Generate a test JWT token for Fund-In operations. FOR DEMO/TESTING ONLY. "
            + "Use includeStepUp=true when you need to call CommitFundIn (requires bb-su:snbc-fund-in scope)."
    )]
    public string GenerateTestToken(
        [Description("Include step-up scope (bb-su:snbc-fund-in) required for CommitFundIn")]
            bool includeStepUp = false,
        [Description("Token validity in hours (default: 24)")] int expiryHours = 24
    )
    {
        try
        {
            logger.LogInformation(
                "Generating test token - StepUp: {StepUp}, ExpiryHours: {Hours}",
                includeStepUp,
                expiryHours
            );

            var token = testTokenService.GenerateTestToken(includeStepUp, expiryHours);

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    Message = includeStepUp
                        ? "Generated token with step-up scope for Fund-In commit operations"
                        : "Generated standard token without step-up scope",
                    Token = token,
                    HasStepUpScope = includeStepUp,
                    ExpiresInHours = expiryHours,
                    Usage = includeStepUp
                        ? "Use this token for CommitFundIn operations"
                        : "Use this token for Preview/Start operations. Generate with includeStepUp=true for CommitFundIn",
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

    [Description("Decode and inspect a JWT token to see its claims and validate it.")]
    public string DecodeToken([Description("The JWT token to decode")] string token)
    {
        try
        {
            logger.LogInformation("Decoding token");

            var tokenInfo = testTokenService.DecodeToken(token);

            if (tokenInfo == null)
                return "Error: Failed to decode token - invalid format";

            return JsonSerializer.Serialize(
                new
                {
                    Success = true,
                    Token = new
                    {
                        tokenInfo.Subject,
                        tokenInfo.Issuer,
                        Expiry = tokenInfo.Expiry.ToString("o"),
                        tokenInfo.IsExpired,
                        tokenInfo.HasStepUpScope,
                        ClaimCount = tokenInfo.Claims.Count,
                    },
                    Warning = tokenInfo.IsExpired ? "Token has expired" : null,
                },
                JsonOptions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error decoding token");
            return $"Error: {ex.Message}";
        }
    }

    #endregion
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Handles OTP verification for Fund-In
/// Step 4: OTP Verification
/// </summary>
public class OtpVerificationExecutor(
    FundInService fundInService,
    ILogger<OtpVerificationExecutor> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.OtpVerification",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.FundIn.OtpVerification", "1.0.0");

    private static readonly Counter<int> OtpVerifiedCounter = Meter.CreateCounter<int>(
        "fundin_otp_verified",
        "verifications",
        "Number of successful OTP verifications"
    );

    private static readonly Counter<int> OtpFailedCounter = Meter.CreateCounter<int>(
        "fundin_otp_failed",
        "failures",
        "Number of failed OTP verifications"
    );

    /// <summary>
    /// Verify OTP for Fund-In transaction
    /// </summary>
    public async Task<OtpVerificationResult> ExecuteAsync(
        string cif,
        string transactionId,
        string otp,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInOtpVerification");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);

        try
        {
            logger.LogInformation("Verifying OTP for transaction: {TransactionId}", transactionId);

            var verifyRequest = new FundInVerifyOtpRequest
            {
                TransactionId = transactionId,
                Otp = otp,
            };

            var response = await fundInService.VerifyOtpAsync(cif, verifyRequest, accessToken);

            if (!response.Success)
            {
                OtpFailedCounter.Add(1);

                logger.LogWarning(
                    "OTP verification failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new OtpVerificationResult
                {
                    Success = false,
                    IsVerified = false,
                    TransactionId = transactionId,
                    Status = "Failed",
                    RemainingAttempts = response.Data?.RemainingAttempts ?? 0,
                    ErrorMessage = response.Message ?? "OTP verification failed",
                };
            }

            var data = response.Data!;

            if (data.IsVerified)
            {
                OtpVerifiedCounter.Add(1);
                logger.LogInformation(
                    "OTP verified successfully for transaction: {TransactionId}",
                    transactionId
                );
            }
            else
            {
                OtpFailedCounter.Add(1);
                logger.LogWarning(
                    "OTP not verified. Remaining attempts: {Remaining}",
                    data.RemainingAttempts
                );
            }

            activity?.SetTag("is_verified", data.IsVerified);
            activity?.SetTag("remaining_attempts", data.RemainingAttempts);

            return new OtpVerificationResult
            {
                Success = true,
                IsVerified = data.IsVerified,
                TransactionId = data.TransactionId,
                Status = data.Status ?? (data.IsVerified ? "Verified" : "Pending"),
                RemainingAttempts = data.RemainingAttempts ?? 0,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OTP verification execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Resend OTP for Fund-In transaction
    /// </summary>
    public async Task<StartResult> ResendOtpAsync(
        string cif,
        string transactionId,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("FundInResendOtp");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);

        try
        {
            logger.LogInformation("Resending OTP for transaction: {TransactionId}", transactionId);

            var resendRequest = new FundInResendOtpRequest { TransactionId = transactionId };

            var response = await fundInService.ResendOtpAsync(cif, resendRequest, accessToken);

            if (!response.Success)
            {
                logger.LogWarning(
                    "Resend OTP failed: {Message} (Code: {Code})",
                    response.Message,
                    response.ErrorCode
                );

                return new StartResult
                {
                    Success = false,
                    TransactionId = transactionId,
                    Status = "Failed",
                    ErrorMessage = response.Message ?? "Failed to resend OTP",
                };
            }

            var data = response.Data!;

            logger.LogInformation(
                "OTP resent to {OtpSentTo}. Resend count: {ResendCount}/{MaxAttempts}",
                data.OtpSentTo,
                data.ResendCount,
                data.MaxResendAttempts
            );

            return new StartResult
            {
                Success = true,
                TransactionId = data.TransactionId,
                Status = "OtpResent",
                OtpSentTo = data.OtpSentTo,
                OtpExpirySeconds = data.OtpExpirySeconds ?? 180,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Resend OTP execution failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

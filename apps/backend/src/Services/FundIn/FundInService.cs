using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentFrameworkQuickStart.Services.FundIn;

/// <summary>
/// Service for Fund-In operations with SNB Capital API
/// </summary>
public class FundInService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FundInService> logger
)
{
    private readonly string _baseUrl =
        configuration["SNBCapital:BaseUrl"] ?? "https://snbc-api.onrender.com/snbc/api/v1";

    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundInService",
        "1.0.0"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #region Customer Accounts

    /// <summary>
    /// Get customer bank accounts available for fund-in
    /// GET /customer/accounts
    /// </summary>
    public async Task<CustomerAccountsResponse> GetCustomerAccountsAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetCustomerAccounts");
        activity?.SetTag("cif", cif);

        try
        {
            logger.LogInformation("Fetching customer accounts for CIF: {CIF}", cif);

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/customer/accounts");
            request.Headers.Add("userId", cif);
            AddAuthHeader(request, accessToken);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new CustomerAccountsResponse
                {
                    Success = false,
                    Message = $"Failed to fetch accounts: {response.StatusCode}",
                    ErrorCode = response.StatusCode.ToString(),
                };
            }

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<CustomerAccountsData>>(
                content,
                JsonOptions
            );

            return new CustomerAccountsResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching customer accounts for CIF: {CIF}", cif);
            return new CustomerAccountsResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Get portfolios associated with customer accounts
    /// GET /customer/accounts/portfolios
    /// </summary>
    public async Task<CustomerAccountPortfoliosResponse> GetCustomerAccountPortfoliosAsync(
        string cif,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetCustomerAccountPortfolios");
        activity?.SetTag("cif", cif);

        try
        {
            logger.LogInformation("Fetching account portfolios for CIF: {CIF}", cif);

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_baseUrl}/customer/accounts/portfolios"
            );
            request.Headers.Add("userId", cif);
            AddAuthHeader(request, accessToken);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new CustomerAccountPortfoliosResponse
                {
                    Success = false,
                    Message = $"Failed to fetch portfolios: {response.StatusCode}",
                    ErrorCode = response.StatusCode.ToString(),
                };
            }

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<List<AccountPortfolio>>>(
                content,
                JsonOptions
            );

            return new CustomerAccountPortfoliosResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
            };
        }
        catch (Exception ex)
        {
            return new CustomerAccountPortfoliosResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    #endregion

    #region Fund-In Operations

    /// <summary>
    /// Preview a fund-in transaction before confirmation
    /// POST /fundin/confirm/preview
    /// </summary>
    public async Task<FundInPreviewResponse> PreviewFundInAsync(
        string cif,
        FundInPreviewRequest request,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("PreviewFundIn");
        activity?.SetTag("cif", cif);
        activity?.SetTag("amount", request.Amount);

        try
        {
            logger.LogInformation(
                "Previewing fund-in for CIF: {CIF}, Amount: {Amount} {Currency}",
                cif,
                request.Amount,
                request.Currency
            );

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/fundin/confirm/preview"
            )
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            httpRequest.Headers.Add("userId", cif);
            AddAuthHeader(httpRequest, accessToken);

            var response = await httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            // Log raw response for debugging
            logger.LogDebug(
                "Fund-In Preview API Response - Status: {StatusCode}, Content: {Content}",
                response.StatusCode,
                content
            );

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInPreviewData>>(
                content,
                JsonOptions
            );

            // Build detailed error message
            string? errorMessage = null;
            if (!(apiResponse?.IsSuccess ?? false))
            {
                errorMessage = apiResponse?.Message ?? "Unknown error";
                if (!response.IsSuccessStatusCode)
                {
                    errorMessage =
                        $"{errorMessage} (HTTP {(int)response.StatusCode}: {response.StatusCode})";
                }
                logger.LogWarning(
                    "Fund-In Preview failed: {Error}. Raw response: {Content}",
                    errorMessage,
                    content
                );
            }

            return new FundInPreviewResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = errorMessage ?? apiResponse?.Message,
                Data = apiResponse?.Data,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing fund-in for CIF: {CIF}", cif);
            return new FundInPreviewResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Confirm/Start a fund-in transaction (requires step-up token)
    /// POST /fundin/confirm/start
    /// Returns ReadyToCommit when token has required scope
    /// </summary>
    public async Task<FundInConfirmStartResponse> ConfirmStartFundInAsync(
        string cif,
        FundInConfirmStartRequest request,
        string stepUpToken
    )
    {
        using var activity = ActivitySource.StartActivity("ConfirmStartFundIn");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", request.TransactionId);

        try
        {
            logger.LogInformation(
                "Confirming fund-in for CIF: {CIF}, TransactionId: {TransactionId}",
                cif,
                request.TransactionId
            );

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/fundin/confirm/start"
            )
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            httpRequest.Headers.Add("userId", cif);
            AddAuthHeader(httpRequest, stepUpToken);

            var response = await httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            logger.LogDebug(
                "Confirm Start API Response - Status: {StatusCode}, Content: {Content}",
                response.StatusCode,
                content
            );

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInConfirmStartData>>(
                content,
                JsonOptions
            );

            var isReadyToCommit =
                apiResponse?.Data?.Status == "ReadyToCommit"
                || apiResponse?.Data?.IsReadyToCommit == true
                || (apiResponse?.IsSuccess == true);

            return new FundInConfirmStartResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data =
                    apiResponse?.Data != null
                        ? apiResponse.Data with
                        {
                            IsReadyToCommit = isReadyToCommit,
                        }
                        : null,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming fund-in for CIF: {CIF}", cif);
            return new FundInConfirmStartResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Verify OTP for fund-in transaction
    /// POST /fundin/verify-otp
    /// </summary>
    public async Task<FundInVerifyOtpResponse> VerifyOtpAsync(
        string cif,
        FundInVerifyOtpRequest request,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("VerifyFundInOtp");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", request.TransactionId);

        try
        {
            logger.LogInformation(
                "Verifying OTP for transaction: {TransactionId}",
                request.TransactionId
            );

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/fundin/verify-otp"
            )
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            httpRequest.Headers.Add("userId", cif);
            AddAuthHeader(httpRequest, accessToken);

            var response = await httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInVerifyData>>(
                content,
                JsonOptions
            );

            return new FundInVerifyOtpResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error verifying OTP for transaction: {TransactionId}",
                request.TransactionId
            );
            return new FundInVerifyOtpResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Resend OTP for fund-in transaction
    /// POST /fundin/resend-otp
    /// </summary>
    public async Task<FundInResendOtpResponse> ResendOtpAsync(
        string cif,
        FundInResendOtpRequest request,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("ResendFundInOtp");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", request.TransactionId);

        try
        {
            logger.LogInformation(
                "Resending OTP for transaction: {TransactionId}",
                request.TransactionId
            );

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/fundin/resend-otp"
            )
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            httpRequest.Headers.Add("userId", cif);
            AddAuthHeader(httpRequest, accessToken);

            var response = await httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInResendData>>(
                content,
                JsonOptions
            );

            return new FundInResendOtpResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error resending OTP for transaction: {TransactionId}",
                request.TransactionId
            );
            return new FundInResendOtpResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Commit/finalize the fund-in transaction
    /// POST /fundin/commit
    /// Requires transactionId and idempotency key in request body
    /// </summary>
    public async Task<FundInCommitResponse> CommitFundInAsync(
        string cif,
        FundInCommitRequest request,
        string stepUpToken
    )
    {
        using var activity = ActivitySource.StartActivity("CommitFundIn");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", request.TransactionId);
        activity?.SetTag("idempotency_key", request.IdempotencyKey);

        try
        {
            logger.LogInformation(
                "Committing fund-in transaction: {TransactionId} with idempotency key: {IdempotencyKey}",
                request.TransactionId,
                request.IdempotencyKey
            );

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/fundin/commit")
            {
                Content = JsonContent.Create(request, options: JsonOptions),
            };
            httpRequest.Headers.Add("userId", cif);
            AddAuthHeader(httpRequest, stepUpToken);

            var response = await httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            logger.LogDebug(
                "Commit API Response - Status: {StatusCode}, Content: {Content}",
                response.StatusCode,
                content
            );

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInCommitData>>(
                content,
                JsonOptions
            );

            return new FundInCommitResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error committing fund-in transaction: {TransactionId}",
                request.TransactionId
            );
            return new FundInCommitResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    /// <summary>
    /// Get status of a fund-in transaction
    /// GET /fundin/{transactionId}/status
    /// </summary>
    public async Task<FundInStatusResponse> GetTransactionStatusAsync(
        string cif,
        string transactionId,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("GetFundInStatus");
        activity?.SetTag("cif", cif);
        activity?.SetTag("transaction_id", transactionId);

        try
        {
            logger.LogInformation("Getting status for transaction: {TransactionId}", transactionId);

            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_baseUrl}/fundin/{transactionId}/status"
            );
            request.Headers.Add("userId", cif);
            AddAuthHeader(request, accessToken);

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var apiResponse = JsonSerializer.Deserialize<SNBApiResponse<FundInStatusData>>(
                content,
                JsonOptions
            );

            return new FundInStatusResponse
            {
                Success = apiResponse?.IsSuccess ?? false,
                Message = apiResponse?.Message,
                Data = apiResponse?.Data,
                ErrorCode = !response.IsSuccessStatusCode ? response.StatusCode.ToString() : null,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error getting status for transaction: {TransactionId}",
                transactionId
            );
            return new FundInStatusResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION",
            };
        }
    }

    #endregion

    private static void AddAuthHeader(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
    }

    // Reusing the SNBApiResponse from SNBCapitalApiService
    private record SNBApiResponse<T>
    {
        public int StatusCode { get; init; }
        public string? Message { get; init; }
        public bool IsSuccess { get; init; }
        public T? Data { get; init; }
    }
}

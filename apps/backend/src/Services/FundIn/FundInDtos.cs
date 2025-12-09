using System.Text.Json.Serialization;

namespace AgentFrameworkQuickStart.Services.FundIn;

#region Fund-In Request DTOs

/// <summary>
/// Request to preview a fund-in transaction
/// API expects: fromAccountId, toPortfolioId, amount, currency, fundId
/// </summary>
public record FundInPreviewRequest
{
    [JsonPropertyName("fromAccountId")]
    public required string SourceAccountId { get; init; }

    [JsonPropertyName("toPortfolioId")]
    public required string TargetPortfolioNumber { get; init; }

    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }
}

/// <summary>
/// Request to confirm/start a fund-in transaction (after preview)
/// Requires transactionId from preview response
/// API expects: { "fundInTxnId": "..." }
/// </summary>
public record FundInConfirmStartRequest
{
    [JsonPropertyName("fundInTxnId")]
    public required string TransactionId { get; init; }
}

/// <summary>
/// Request to verify OTP for fund-in
/// </summary>
public record FundInVerifyOtpRequest
{
    public required string TransactionId { get; init; }
    public required string Otp { get; init; }
}

/// <summary>
/// Request to resend OTP
/// </summary>
public record FundInResendOtpRequest
{
    public required string TransactionId { get; init; }
}

/// <summary>
/// Request to commit/finalize a fund-in transaction
/// Requires transactionId and idempotency key
/// API expects: { "fundInTxnId": "...", "idempotencyKey": "..." }
/// </summary>
public record FundInCommitRequest
{
    [JsonPropertyName("fundInTxnId")]
    public required string TransactionId { get; init; }

    [JsonPropertyName("idempotencyKey")]
    public required string IdempotencyKey { get; init; }
}

#endregion

#region Fund-In Response DTOs

/// <summary>
/// Preview response showing transaction details before confirmation
/// </summary>
public record FundInPreviewResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInPreviewData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Preview data matching actual API response structure
/// API returns: txnId, sessionId, summary (nested), fx, warnings
/// </summary>
public record FundInPreviewData
{
    [JsonPropertyName("txnId")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("summary")]
    public FundInPreviewSummary? Summary { get; init; }

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; init; }

    // Computed properties for backward compatibility
    public decimal Amount => Summary?.Amount?.Value ?? 0;
    public string? Currency => Summary?.Amount?.Currency;
    public decimal Fees => Summary?.Fee?.Value ?? 0;
    public decimal Vat => Summary?.Vat?.Value ?? 0;
    public decimal TotalAmount => Summary?.TotalDebit?.Value ?? 0;
    public string? TargetPortfolioNumber => Summary?.ToPortfolio?.Id;
    public string? TargetPortfolioName => Summary?.ToPortfolio?.Name;
    public string? SourceAccountMasked => Summary?.FromAccountMasked;
}

public record FundInPreviewSummary
{
    [JsonPropertyName("fromAccountMasked")]
    public string? FromAccountMasked { get; init; }

    [JsonPropertyName("toPortfolio")]
    public FundInPortfolioInfo? ToPortfolio { get; init; }

    [JsonPropertyName("amount")]
    public MoneyValue? Amount { get; init; }

    [JsonPropertyName("fee")]
    public MoneyValue? Fee { get; init; }

    [JsonPropertyName("vat")]
    public MoneyValue? Vat { get; init; }

    [JsonPropertyName("totalDebit")]
    public MoneyValue? TotalDebit { get; init; }

    [JsonPropertyName("rate")]
    public decimal? Rate { get; init; }

    [JsonPropertyName("amountAccountCcy")]
    public MoneyValue? AmountAccountCcy { get; init; }

    [JsonPropertyName("amountPortfolioCcy")]
    public MoneyValue? AmountPortfolioCcy { get; init; }
}

public record FundInPortfolioInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public record MoneyValue
{
    [JsonPropertyName("value")]
    public decimal Value { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

/// <summary>
/// Response after confirming/starting fund-in (ReadyToCommit)
/// API returns: { "data": { "status": "ReadyToCommit", "otp": null } }
/// </summary>
public record FundInConfirmStartResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInConfirmStartData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record FundInConfirmStartData
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("otp")]
    public string? Otp { get; init; }

    // Computed property - ready to commit if status is "ReadyToCommit"
    public bool IsReadyToCommit => Status == "ReadyToCommit";
}

/// <summary>
/// Response after OTP verification
/// </summary>
public record FundInVerifyOtpResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInVerifyData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record FundInVerifyData
{
    public string? TransactionId { get; init; }
    public string? Status { get; init; }
    public bool IsVerified { get; init; }
    public int? RemainingAttempts { get; init; }
}

/// <summary>
/// Response after resending OTP
/// </summary>
public record FundInResendOtpResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInResendData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record FundInResendData
{
    public string? TransactionId { get; init; }
    public string? OtpSentTo { get; init; }
    public int? OtpExpirySeconds { get; init; }
    public int? ResendCount { get; init; }
    public int? MaxResendAttempts { get; init; }
}

/// <summary>
/// Response after committing the transaction
/// </summary>
public record FundInCommitResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInCommitData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record FundInCommitData
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? PaymentReferenceId { get; init; }
    public string? FxReferenceId { get; init; }
    public decimal DebitAmount { get; init; }
    public string? DebitCurrency { get; init; }
    public decimal CreditAmount { get; init; }
    public string? CreditCurrency { get; init; }
    public decimal? ExchangeRate { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Status { get; init; }
    public string? Message { get; init; }
    public bool PartialSuccess { get; init; }
    public string? WarningMessage { get; init; }
}

/// <summary>
/// Transaction status response
/// </summary>
public record FundInStatusResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public FundInStatusData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record FundInStatusData
{
    public string? TransactionId { get; init; }
    public string? Status { get; init; }
    public string? StatusDescription { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? SourceAccountId { get; init; }
    public string? TargetPortfolioNumber { get; init; }
    public string? FundId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ReferenceNumber { get; init; }
}

#endregion

#region Customer Accounts DTOs

/// <summary>
/// Response for customer accounts
/// </summary>
public record CustomerAccountsResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public CustomerAccountsData? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record CustomerAccountsData
{
    public List<CustomerAccount>? CurrentAccounts { get; init; }
    public int TotalCount { get; init; }
    public string? Cif { get; init; }
}

public record CustomerAccount
{
    public string? AccountId { get; init; }
    public string? AccountHolderNameInEnglish { get; init; }
    public string? AccountHolderNameInArabic { get; init; }
    public decimal AccountBalance { get; init; }
    public string? CurrencyCode { get; init; }
    public string? AccountTypeCode { get; init; }
    public string? AccountTypeDescriptionEnglish { get; init; }
    public string? AccountTypeDescriptionArabic { get; init; }
    public string? LanguageCode { get; init; }
}

/// <summary>
/// Response for customer account portfolios
/// </summary>
public record CustomerAccountPortfoliosResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<AccountPortfolio>? Data { get; init; }
    public string? ErrorCode { get; init; }
}

public record AccountPortfolio
{
    public string? PortfolioNumber { get; init; }
    public string? PortfolioName { get; init; }
    public string? PortfolioType { get; init; }
    public string? Currency { get; init; }
    public string? Status { get; init; }
}

#endregion

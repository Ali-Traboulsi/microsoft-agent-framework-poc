namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;

#region Progress Reporting

/// <summary>
/// Delegate for reporting workflow progress
/// </summary>
public delegate void ProgressCallback(WorkflowProgressUpdate update);

/// <summary>
/// Progress update during workflow execution
/// </summary>
public record WorkflowProgressUpdate
{
    public required string Step { get; init; }
    public required string Message { get; init; }
    public string? MessageAr { get; init; }
    public ProgressStatus Status { get; init; } = ProgressStatus.InProgress;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object>? Details { get; init; }
}

public enum ProgressStatus
{
    Starting,
    InProgress,
    Completed,
    Failed,
}

#endregion

#region Workflow Request

/// <summary>
/// Initial request for Fund-In workflow
/// طلب تحويل الأموال للاستثمار
/// </summary>
public record FundInWorkflowRequest
{
    /// <summary>Customer CIF number</summary>
    public required string Cif { get; init; }

    /// <summary>Source bank account ID</summary>
    public required string SourceAccountId { get; init; }

    /// <summary>Target portfolio number</summary>
    public required string TargetPortfolioNumber { get; init; }

    /// <summary>Amount to transfer</summary>
    public required decimal Amount { get; init; }

    /// <summary>Currency code (default: SAR)</summary>
    public string Currency { get; init; } = "SAR";

    /// <summary>Optional fund ID for specific mutual fund investment</summary>
    public string? FundId { get; init; }

    /// <summary>Optional notes for the transaction</summary>
    public string? Notes { get; init; }

    /// <summary>Request timestamp</summary>
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

#endregion

#region Workflow State

/// <summary>
/// Current state of the Fund-In workflow
/// Tracks progress through each step
/// </summary>
public record FundInWorkflowState
{
    public string WorkflowId { get; init; } = Guid.NewGuid().ToString();
    public FundInWorkflowRequest Request { get; init; } = null!;
    public FundInWorkflowStatus Status { get; init; } = FundInWorkflowStatus.NotStarted;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }

    // Step Results
    public AccountsContext? AccountsContext { get; init; }
    public PreviewResult? Preview { get; init; }
    public ConfirmationResult? Confirmation { get; init; }
    public CommitResult? Commit { get; init; }

    // Token for authentication
    public string? AccessToken { get; init; }
    public string? StepUpToken { get; init; }

    // Error tracking
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public FundInWorkflowStep? FailedAtStep { get; init; }
}

/// <summary>
/// Workflow status enum
/// </summary>
public enum FundInWorkflowStatus
{
    NotStarted,
    FetchingAccounts,
    Previewing,
    Confirming,
    Committing,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// Workflow step identifiers
/// </summary>
public enum FundInWorkflowStep
{
    AccountsRetrieval,
    Preview,
    Confirmation,
    Commit,
}

#endregion

#region Step Results

/// <summary>
/// Customer accounts and portfolios context
/// </summary>
public record AccountsContext
{
    public List<AccountInfo> Accounts { get; init; } = [];
    public List<PortfolioInfo> Portfolios { get; init; } = [];
    public string? SelectedAccountId { get; init; }
    public string? SelectedPortfolioNumber { get; init; }
    public decimal SelectedAccountBalance { get; init; }
    public bool HasSufficientFunds { get; init; }
    public bool AccountFound { get; init; }
    public bool PortfolioFound { get; init; }
}

public record AccountInfo
{
    public required string AccountId { get; init; }
    public string? HolderName { get; init; }
    public decimal Balance { get; init; }
    public string Currency { get; init; } = "SAR";
    public string? AccountType { get; init; }
}

public record PortfolioInfo
{
    public required string PortfolioNumber { get; init; }
    public string? PortfolioName { get; init; }
    public string? PortfolioType { get; init; }
    public string Currency { get; init; } = "SAR";
    public string? Status { get; init; }
}

/// <summary>
/// Preview transaction result
/// </summary>
public record PreviewResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "SAR";
    public decimal Fees { get; init; }
    public decimal TotalAmount { get; init; }
    public string? FundName { get; init; }
    public decimal? EstimatedUnits { get; init; }
    public decimal? CurrentNav { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Confirmation result (after step-up token validation)
/// Returns IsReadyToCommit when step-up token is valid
/// </summary>
public record ConfirmationResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string Status { get; init; } = "Pending";
    public bool IsReadyToCommit { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Commit transaction result
/// </summary>
public record CommitResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? ReferenceNumber { get; init; }
    public string Status { get; init; } = "Pending";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "SAR";
    public decimal? Units { get; init; }
    public decimal? NavAtPurchase { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

#endregion

#region Workflow Result

/// <summary>
/// Final workflow result
/// </summary>
public record FundInWorkflowResult
{
    public bool Success { get; init; }
    public string WorkflowId { get; init; } = "";
    public FundInWorkflowStatus Status { get; init; }

    // Transaction details
    public string? TransactionId { get; init; }
    public string? ReferenceNumber { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "SAR";
    public decimal Fees { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal? Units { get; init; }
    public decimal? NavAtPurchase { get; init; }

    // Source and target
    public string? SourceAccountId { get; init; }
    public string? TargetPortfolioNumber { get; init; }
    public string? FundName { get; init; }

    // Timestamps
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;

    // Error info
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public FundInWorkflowStep? FailedAtStep { get; init; }

    // Summary message (bilingual)
    public string? SummaryEn { get; init; }
    public string? SummaryAr { get; init; }
}

#endregion

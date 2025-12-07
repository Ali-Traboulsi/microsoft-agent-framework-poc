namespace AgentFrameworkQuickStart.Api.Workflows.Messages;

/// <summary>
/// Initial onboarding request with customer information
/// </summary>
public record OnboardingRequest
{
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Country { get; init; }
    public decimal InitialDepositAmount { get; init; }
    public string? ExistingSNBAccountId { get; init; } // For existing SNB customers
    public List<DocumentUpload> Documents { get; init; } = new();
}

public record DocumentUpload
{
    public required string DocumentType { get; init; } // "ID", "ProofOfAddress", "TaxForm"
    public required string FileName { get; init; }
    public required string Base64Content { get; init; }
}

/// <summary>
/// Customer information collected during onboarding
/// </summary>
public record CustomerInfo
{
    public required string CustomerId { get; init; }
    public required string CustomerName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Country { get; init; }
    public decimal InitialDepositAmount { get; init; }
    public string? ExistingSNBAccountId { get; init; }
    public List<DocumentUpload> Documents { get; init; } = new();
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result from external SNB account check
/// </summary>
public record SNBAccountCheckResult
{
    public bool HasExistingAccount { get; init; }
    public string? SNBAccountId { get; init; }
    public string? AccountStatus { get; init; } // "Active", "Suspended", "Closed"
    public DateTime? AccountOpenedDate { get; init; }
    public bool KYCVerified { get; init; }
    public string? RiskRating { get; init; } // "Low", "Medium", "High"
    public List<PortfolioSummary> ExistingPortfolios { get; init; } = new();
}

public record PortfolioSummary
{
    public required string PortfolioId { get; init; }
    public required string PortfolioName { get; init; }
    public decimal TotalValue { get; init; }
    public List<string> AssetTypes { get; init; } = new();
}

/// <summary>
/// Compliance check results from parallel execution
/// </summary>
public record ComplianceCheckResult
{
    public required string CheckType { get; init; } // "KYC", "AML", "Sanctions"
    public bool Passed { get; init; }
    public int RiskScore { get; init; } // 0-100
    public List<string> Flags { get; init; } = new();
    public List<string> WarningMessages { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Aggregated risk assessment from all compliance checks
/// </summary>
public record RiskAssessment
{
    public int OverallRiskScore { get; init; } // 0-100
    public string RiskLevel { get; init; } = "Low"; // "Low", "Medium", "High", "Critical"
    public bool RequiresManualReview { get; init; }
    public bool RequiresEnhancedDueDiligence { get; init; }
    public List<ComplianceCheckResult> ComplianceResults { get; init; } = new();
    public List<string> RecommendedActions { get; init; } = new();
    public DateTime AssessedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Human review decision for high-risk cases
/// </summary>
public record ManualReviewDecision
{
    public bool Approved { get; init; }
    public string? ReviewerId { get; init; }
    public string? ReviewerName { get; init; }
    public string? ReviewNotes { get; init; }
    public List<string> AdditionalDocumentsRequired { get; init; } = new();
    public DateTime ReviewedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Account creation result
/// </summary>
public record AccountCreationResult
{
    public bool Success { get; init; }
    public string? NewAccountId { get; init; }
    public string? AccountNumber { get; init; }
    public string AccountType { get; init; } = "Standard";
    public List<string> EnabledServices { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Portfolio migration result
/// </summary>
public record PortfolioMigrationResult
{
    public bool Success { get; init; }
    public int PortfoliosMigrated { get; init; }
    public List<string> MigratedPortfolioIds { get; init; } = new();
    public List<string> FailedPortfolios { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Final onboarding result
/// </summary>
public record OnboardingResult
{
    public bool Success { get; init; }
    public required string CustomerId { get; init; }
    public string? NewAccountId { get; init; }
    public string? AccountNumber { get; init; }
    public RiskAssessment? RiskAssessment { get; init; }
    public bool RequiredManualReview { get; init; }
    public ManualReviewDecision? ReviewDecision { get; init; }
    public SNBAccountCheckResult? SNBAccountCheck { get; init; }
    public PortfolioMigrationResult? PortfolioMigration { get; init; }
    public List<string> NextSteps { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public TimeSpan TotalProcessingTime { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

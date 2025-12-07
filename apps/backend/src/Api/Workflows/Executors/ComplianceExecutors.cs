using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.Messages;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Api.Workflows.Executors;

/// <summary>
/// Customer information collection and validation executor
/// </summary>
public class CustomerInfoExecutor
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Workflows.CustomerInfo",
        "1.0.0"
    );

    private static readonly Meter Meter = new("InvestmentBanking.Workflows.CustomerInfo", "1.0.0");
    private static readonly Counter<int> ValidationCounter = Meter.CreateCounter<int>(
        "customer_info_validations"
    );

    private readonly ILogger<CustomerInfoExecutor> _logger;

    public CustomerInfoExecutor(ILogger<CustomerInfoExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<CustomerInfo> ExecuteAsync(
        OnboardingRequest request,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("CollectCustomerInfo");
        activity?.SetTag("customer.id", request.CustomerId);

        _logger.LogInformation(
            "Collecting customer information for {CustomerId}",
            request.CustomerId
        );

        // Validate required fields
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            validationErrors.Add("Customer name is required");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            validationErrors.Add("Valid email is required");

        if (string.IsNullOrWhiteSpace(request.Phone))
            validationErrors.Add("Phone number is required");

        if (request.InitialDepositAmount < 1000)
            validationErrors.Add("Minimum initial deposit is $1,000");

        if (validationErrors.Any())
        {
            ValidationCounter.Add(1, new KeyValuePair<string, object?>("result", "failed"));
            throw new InvalidOperationException(
                $"Validation failed: {string.Join(", ", validationErrors)}"
            );
        }

        ValidationCounter.Add(1, new KeyValuePair<string, object?>("result", "success"));

        await Task.Delay(200, cancellationToken); // Simulate processing

        return new CustomerInfo
        {
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            Email = request.Email,
            Phone = request.Phone,
            Country = request.Country,
            InitialDepositAmount = request.InitialDepositAmount,
            ExistingSNBAccountId = request.ExistingSNBAccountId,
            Documents = request.Documents,
            RequestedAt = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// SNB Capital account check executor using external API
/// </summary>
public class SNBAccountCheckExecutor
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Workflows.SNBAccountCheck",
        "1.0.0"
    );

    private static readonly Meter Meter = new(
        "InvestmentBanking.Workflows.SNBAccountCheck",
        "1.0.0"
    );
    private static readonly Counter<int> CheckCounter = Meter.CreateCounter<int>(
        "snb_account_checks"
    );

    private readonly SNBCapitalApiService _snbService;
    private readonly ILogger<SNBAccountCheckExecutor> _logger;

    public SNBAccountCheckExecutor(
        SNBCapitalApiService snbService,
        ILogger<SNBAccountCheckExecutor> logger
    )
    {
        _snbService = snbService;
        _logger = logger;
    }

    public async Task<SNBAccountCheckResult> ExecuteAsync(
        CustomerInfo input,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("CheckSNBAccount");
        activity?.SetTag("customer.id", input.CustomerId);
        activity?.SetTag("customer.email", input.Email);

        _logger.LogInformation("Checking SNB Capital account for {CustomerId}", input.CustomerId);

        try
        {
            var result = await _snbService.CheckExistingAccountAsync(input.CustomerId, input.Email);

            CheckCounter.Add(
                1,
                new KeyValuePair<string, object?>("has_account", result.HasExistingAccount)
            );

            activity?.SetTag("snb.has_account", result.HasExistingAccount);
            activity?.SetTag("snb.kyc_verified", result.KYCVerified);
            activity?.SetTag("snb.risk_rating", result.RiskRating);

            _logger.LogInformation(
                "SNB account check complete: HasAccount={HasAccount}, KYC={KYC}",
                result.HasExistingAccount,
                result.KYCVerified
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SNB account");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Return negative result on error - don't block onboarding
            return new SNBAccountCheckResult { HasExistingAccount = false };
        }
    }
}

/// <summary>
/// KYC (Know Your Customer) compliance check executor
/// </summary>
public class KYCExecutor
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Workflows.KYC",
        "1.0.0"
    );

    private readonly ILogger<KYCExecutor> _logger;

    public KYCExecutor(ILogger<KYCExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<ComplianceCheckResult> ExecuteAsync(
        CustomerInfo input,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("PerformKYC");
        activity?.SetTag("customer.id", input.CustomerId);

        _logger.LogInformation("Performing KYC check for {CustomerId}", input.CustomerId);

        // Simulate KYC processing
        await Task.Delay(1500, cancellationToken);

        // Mock KYC logic - check documents and information
        var flags = new List<string>();
        var warnings = new List<string>();
        var riskScore = 20; // Base score

        // Document checks
        if (!input.Documents.Any(d => d.DocumentType == "ID"))
        {
            flags.Add("Missing government-issued ID");
            riskScore += 30;
        }

        if (!input.Documents.Any(d => d.DocumentType == "ProofOfAddress"))
        {
            warnings.Add("No proof of address provided");
            riskScore += 10;
        }

        // High-risk countries
        var highRiskCountries = new[] { "North Korea", "Iran", "Syria" };
        if (highRiskCountries.Contains(input.Country))
        {
            flags.Add($"High-risk country: {input.Country}");
            riskScore += 40;
        }

        var passed = riskScore < 50;

        activity?.SetTag("kyc.passed", passed);
        activity?.SetTag("kyc.risk_score", riskScore);

        _logger.LogInformation(
            "KYC check complete: Passed={Passed}, RiskScore={Score}",
            passed,
            riskScore
        );

        return new ComplianceCheckResult
        {
            CheckType = "KYC",
            Passed = passed,
            RiskScore = riskScore,
            Flags = flags,
            WarningMessages = warnings,
            Metadata = new Dictionary<string, object>
            {
                ["documents_provided"] = input.Documents.Count,
                ["country"] = input.Country,
            },
        };
    }
}

/// <summary>
/// AML (Anti-Money Laundering) compliance check executor
/// </summary>
public class AMLExecutor
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Workflows.AML",
        "1.0.0"
    );

    private readonly ILogger<AMLExecutor> _logger;

    public AMLExecutor(ILogger<AMLExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<ComplianceCheckResult> ExecuteAsync(
        CustomerInfo input,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("PerformAML");
        activity?.SetTag("customer.id", input.CustomerId);

        _logger.LogInformation("Performing AML check for {CustomerId}", input.CustomerId);

        // Simulate AML screening
        await Task.Delay(2000, cancellationToken);

        var flags = new List<string>();
        var warnings = new List<string>();
        var riskScore = 15;

        // Large initial deposit check
        if (input.InitialDepositAmount > 100000)
        {
            warnings.Add("Large initial deposit detected");
            riskScore += 20;
        }

        // Suspicious patterns (mock logic)
        var nameHash = Math.Abs(input.CustomerName.GetHashCode());
        if (nameHash % 10 == 0)
        {
            flags.Add("Customer name matches suspicious patterns database");
            riskScore += 35;
        }

        var passed = riskScore < 50;

        activity?.SetTag("aml.passed", passed);
        activity?.SetTag("aml.risk_score", riskScore);

        _logger.LogInformation(
            "AML check complete: Passed={Passed}, RiskScore={Score}",
            passed,
            riskScore
        );

        return new ComplianceCheckResult
        {
            CheckType = "AML",
            Passed = passed,
            RiskScore = riskScore,
            Flags = flags,
            WarningMessages = warnings,
            Metadata = new Dictionary<string, object>
            {
                ["initial_deposit"] = input.InitialDepositAmount,
                ["screening_database"] = "WorldCheck",
            },
        };
    }
}

/// <summary>
/// Sanctions screening executor
/// </summary>
public class SanctionsExecutor
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.Workflows.Sanctions",
        "1.0.0"
    );

    private readonly ILogger<SanctionsExecutor> _logger;

    public SanctionsExecutor(ILogger<SanctionsExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<ComplianceCheckResult> ExecuteAsync(
        CustomerInfo input,
        CancellationToken cancellationToken
    )
    {
        using var activity = ActivitySource.StartActivity("ScreenSanctions");
        activity?.SetTag("customer.id", input.CustomerId);

        _logger.LogInformation("Performing sanctions screening for {CustomerId}", input.CustomerId);

        // Simulate sanctions screening
        await Task.Delay(1000, cancellationToken);

        var flags = new List<string>();
        var riskScore = 10;

        // Mock sanctions check - check against OFAC, UN, EU lists
        var sanctionedCountries = new[] { "North Korea", "Iran", "Syria", "Cuba" };
        if (sanctionedCountries.Contains(input.Country))
        {
            flags.Add($"Customer from sanctioned country: {input.Country}");
            riskScore = 100; // Auto-fail
        }

        // Name screening (mock)
        var sanctionedNames = new[] { "Blocked", "Sanctioned", "Prohibited" };
        if (
            sanctionedNames.Any(s =>
                input.CustomerName.Contains(s, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            flags.Add("Name match on sanctions list");
            riskScore = 100;
        }

        var passed = riskScore < 50;

        activity?.SetTag("sanctions.passed", passed);
        activity?.SetTag("sanctions.risk_score", riskScore);

        _logger.LogInformation(
            "Sanctions check complete: Passed={Passed}, RiskScore={Score}",
            passed,
            riskScore
        );

        return new ComplianceCheckResult
        {
            CheckType = "Sanctions",
            Passed = passed,
            RiskScore = riskScore,
            Flags = flags,
            WarningMessages = new List<string>(),
            Metadata = new Dictionary<string, object>
            {
                ["screening_lists"] = new[] { "OFAC", "UN", "EU" },
                ["country"] = input.Country,
            },
        };
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Retrieves customer accounts and portfolios for Fund-In
/// Step 1: Account Context Retrieval
/// Uses FundInService for accounts and SNBCapitalApiService for portfolios
/// </summary>
public class AccountsRetriever(
    FundInService fundInService,
    SNBCapitalApiService snbCapitalApiService,
    ILogger<AccountsRetriever> logger
)
{
    private static readonly ActivitySource ActivitySource = new(
        "InvestmentBanking.FundIn.AccountsRetriever",
        "1.0.0"
    );

    private static readonly Meter Meter = new(
        "InvestmentBanking.FundIn.AccountsRetriever",
        "1.0.0"
    );

    private static readonly Counter<int> AccountsRetrievedCounter = Meter.CreateCounter<int>(
        "fundin_accounts_retrieved",
        "accounts",
        "Number of account retrieval operations"
    );

    /// <summary>
    /// Fetch customer accounts and portfolios
    /// </summary>
    public async Task<AccountsContext> ExecuteAsync(
        FundInWorkflowRequest request,
        string? accessToken = null
    )
    {
        using var activity = ActivitySource.StartActivity("AccountsRetrieval");
        activity?.SetTag("cif", request.Cif);
        activity?.SetTag("source_account", request.SourceAccountId);
        activity?.SetTag("target_portfolio", request.TargetPortfolioNumber);

        try
        {
            logger.LogInformation("Fetching accounts and portfolios for CIF: {Cif}", request.Cif);

            // Fetch accounts from FundInService and portfolios from SNBCapitalApiService in parallel
            // Using SNBCapitalApiService for portfolios because it uses the correct endpoint
            var accountsTask = fundInService.GetCustomerAccountsAsync(request.Cif, accessToken);
            var portfoliosTask = snbCapitalApiService.GetCustomerPortfoliosAsync(
                request.Cif,
                accessToken
            );

            await Task.WhenAll(accountsTask, portfoliosTask);

            var accountsResponse = await accountsTask;
            var portfoliosResponse = await portfoliosTask;

            // Log raw API response for debugging
            logger.LogDebug(
                "Portfolios API response - Success: {Success}, Message: {Message}, PortfolioCount: {Count}",
                portfoliosResponse.Success,
                portfoliosResponse.Message,
                portfoliosResponse.Portfolios?.Count ?? 0
            );

            // Map accounts
            var accounts =
                accountsResponse
                    .Data?.CurrentAccounts?.Select(a => new AccountInfo
                    {
                        AccountId = a.AccountId ?? "",
                        HolderName = a.AccountHolderNameInEnglish,
                        Balance = a.AccountBalance,
                        Currency = a.CurrencyCode ?? "SAR",
                        AccountType = a.AccountTypeDescriptionEnglish,
                    })
                    .ToList() ?? [];

            // Map portfolios from SNBCapitalApiService response
            var portfolios =
                portfoliosResponse
                    .Portfolios?.Select(p => new PortfolioInfo
                    {
                        PortfolioNumber = p.PortfolioNumber ?? "",
                        PortfolioName = p.PortfolioName,
                        PortfolioType = p.PortfolioType,
                        Currency = p.Currency ?? "SAR",
                        Status = p.Status,
                    })
                    .ToList() ?? [];

            // Find selected account - try exact match first, then partial match
            var selectedAccount = accounts.FirstOrDefault(a =>
                a.AccountId == request.SourceAccountId
            );

            // If not found, try partial/contains match for flexibility
            selectedAccount ??= accounts.FirstOrDefault(a =>
                a.AccountId.Contains(request.SourceAccountId)
                || request.SourceAccountId.Contains(a.AccountId)
            );

            // Log available accounts for debugging if not found
            if (selectedAccount == null)
            {
                var availableAccounts = string.Join(
                    ", ",
                    accounts.Select(a => $"{a.AccountId} ({a.Balance:N2} {a.Currency})")
                );
                logger.LogWarning(
                    "Account {RequestedAccount} not found. Available accounts: {AvailableAccounts}",
                    request.SourceAccountId,
                    availableAccounts
                );
            }

            // Also validate portfolio exists - try exact match first
            var selectedPortfolio = portfolios.FirstOrDefault(p =>
                p.PortfolioNumber == request.TargetPortfolioNumber
            );

            // If not found, try partial/contains match for flexibility
            selectedPortfolio ??= portfolios.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.PortfolioNumber)
                && (
                    p.PortfolioNumber.Contains(request.TargetPortfolioNumber)
                    || request.TargetPortfolioNumber.Contains(p.PortfolioNumber)
                )
            );

            // If still not found, try trimmed comparison (remove leading/trailing whitespace)
            selectedPortfolio ??= portfolios.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.PortfolioNumber)
                && p.PortfolioNumber.Trim() == request.TargetPortfolioNumber.Trim()
            );

            if (selectedPortfolio == null)
            {
                var availablePortfolios = string.Join(
                    ", ",
                    portfolios.Select(p => $"'{p.PortfolioNumber}'")
                );
                logger.LogWarning(
                    "Portfolio '{RequestedPortfolio}' not found. Available portfolios: [{AvailablePortfolios}]",
                    request.TargetPortfolioNumber,
                    availablePortfolios
                );

                // Log portfolio details for debugging
                foreach (var p in portfolios)
                {
                    logger.LogDebug(
                        "Portfolio: Number='{Number}', Name='{Name}', Type='{Type}', Status='{Status}'",
                        p.PortfolioNumber,
                        p.PortfolioName,
                        p.PortfolioType,
                        p.Status
                    );
                }
            }

            var hasSufficientFunds = selectedAccount?.Balance >= request.Amount;
            var accountFound = selectedAccount != null;
            var portfolioFound = selectedPortfolio != null;

            activity?.SetTag("accounts_found", accounts.Count);
            activity?.SetTag("portfolios_found", portfolios.Count);
            activity?.SetTag("account_found", accountFound);
            activity?.SetTag("portfolio_found", portfolioFound);
            activity?.SetTag("has_sufficient_funds", hasSufficientFunds);

            AccountsRetrievedCounter.Add(1);

            logger.LogInformation(
                "Found {AccountCount} accounts and {PortfolioCount} portfolios. "
                    + "Account found: {AccountFound}, Portfolio found: {PortfolioFound}, Sufficient funds: {HasFunds}",
                accounts.Count,
                portfolios.Count,
                accountFound,
                portfolioFound,
                hasSufficientFunds
            );

            return new AccountsContext
            {
                Accounts = accounts,
                Portfolios = portfolios,
                SelectedAccountId = selectedAccount?.AccountId ?? request.SourceAccountId,
                SelectedPortfolioNumber =
                    selectedPortfolio?.PortfolioNumber ?? request.TargetPortfolioNumber,
                SelectedAccountBalance = selectedAccount?.Balance ?? 0,
                HasSufficientFunds = accountFound && portfolioFound && hasSufficientFunds,
                AccountFound = accountFound,
                PortfolioFound = portfolioFound,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve accounts for CIF: {Cif}", request.Cif);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentFrameworkQuickStart.Api.Workflows.FundIn.Messages;
using AgentFrameworkQuickStart.Services.FundIn;

namespace AgentFrameworkQuickStart.Api.Workflows.FundIn.Executors;

/// <summary>
/// Retrieves customer accounts and portfolios for Fund-In
/// Step 1: Account Context Retrieval
/// </summary>
public class AccountsRetriever(FundInService fundInService, ILogger<AccountsRetriever> logger)
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

            // Fetch accounts and portfolios in parallel
            var accountsTask = fundInService.GetCustomerAccountsAsync(request.Cif, accessToken);
            var portfoliosTask = fundInService.GetCustomerAccountPortfoliosAsync(
                request.Cif,
                accessToken
            );

            await Task.WhenAll(accountsTask, portfoliosTask);

            var accountsResponse = await accountsTask;
            var portfoliosResponse = await portfoliosTask;

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

            // Map portfolios
            var portfolios =
                portfoliosResponse
                    .Data?.Select(p => new PortfolioInfo
                    {
                        PortfolioNumber = p.PortfolioNumber ?? "",
                        PortfolioName = p.PortfolioName,
                        PortfolioType = p.PortfolioType,
                        Currency = p.Currency ?? "SAR",
                        Status = p.Status,
                    })
                    .ToList() ?? [];

            // Find selected account
            var selectedAccount = accounts.FirstOrDefault(a =>
                a.AccountId == request.SourceAccountId
            );

            var hasSufficientFunds = selectedAccount?.Balance >= request.Amount;

            activity?.SetTag("accounts_found", accounts.Count);
            activity?.SetTag("portfolios_found", portfolios.Count);
            activity?.SetTag("has_sufficient_funds", hasSufficientFunds);

            AccountsRetrievedCounter.Add(1);

            logger.LogInformation(
                "Found {AccountCount} accounts and {PortfolioCount} portfolios. "
                    + "Sufficient funds: {HasFunds}",
                accounts.Count,
                portfolios.Count,
                hasSufficientFunds
            );

            return new AccountsContext
            {
                Accounts = accounts,
                Portfolios = portfolios,
                SelectedAccountId = request.SourceAccountId,
                SelectedPortfolioNumber = request.TargetPortfolioNumber,
                SelectedAccountBalance = selectedAccount?.Balance ?? 0,
                HasSufficientFunds = hasSufficientFunds,
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

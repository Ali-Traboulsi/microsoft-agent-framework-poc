using System.ComponentModel;
using AgentFrameworkQuickStart.Models;
using AgentFrameworkQuickStart.Services;

namespace AgentFrameworkQuickStart.Tools;

/// <summary>
/// Tools for account and transaction operations
/// </summary>
public class AccountTools
{
    private readonly InvestmentDataStore _dataStore;

    public AccountTools(InvestmentDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [Description("Get account balance and details")]
    public string GetAccountBalance([Description("The account ID")] string accountId)
    {
        var account = _dataStore.GetAccount(accountId);
        if (account == null)
            return $"Error: Account {accountId} not found.";

        var result = $"Account Information:\n";
        result += $"Account ID: {account.AccountId}\n";
        result += $"Customer: {account.CustomerName} (ID: {account.CustomerId})\n";
        result += $"Available Balance: ${account.Balance:N2} {account.Currency}\n";
        result += $"Status: {account.Status}\n";
        result += $"Account opened: {account.CreatedDate:yyyy-MM-dd}\n";

        return result;
    }

    [Description("Fund a portfolio by transferring money from account balance")]
    public string FundPortfolio(
        [Description("The account ID")] string accountId,
        [Description("The portfolio ID")] string portfolioId,
        [Description("The fund symbol or ID to purchase")] string fundIdentifier,
        [Description("Amount to invest in USD")] decimal amount
    )
    {
        // Validate account
        var account = _dataStore.GetAccount(accountId);
        if (account == null)
            return $"Error: Account {accountId} not found.";

        if (account.Balance < amount)
            return $"Error: Insufficient funds. Available balance: ${account.Balance:N2}, Required: ${amount:N2}";

        // Validate portfolio
        var portfolio = _dataStore.GetPortfolio(portfolioId);
        if (portfolio == null)
            return $"Error: Portfolio {portfolioId} not found.";

        if (portfolio.AccountId != accountId)
            return "Error: Portfolio does not belong to this account.";

        // Validate fund
        var fund =
            _dataStore.GetMutualFundBySymbol(fundIdentifier)
            ?? _dataStore.GetMutualFund(fundIdentifier);
        if (fund == null)
            return $"Error: Fund '{fundIdentifier}' not found.";

        if (amount < fund.MinimumInvestment)
            return $"Error: Amount ${amount:N2} is below minimum investment of ${fund.MinimumInvestment:N2} for {fund.FundSymbol}";

        // Calculate shares
        var shares = amount / fund.CurrentNAV;

        // Create or update holding
        var existingHolding = portfolio.Holdings.FirstOrDefault(h => h.FundId == fund.FundId);

        if (existingHolding != null)
        {
            // Update existing holding
            var totalShares = existingHolding.Shares + shares;
            var totalCost = (existingHolding.Shares * existingHolding.AverageCost) + amount;
            existingHolding.Shares = totalShares;
            existingHolding.AverageCost = totalCost / totalShares;
            existingHolding.CurrentValue = totalShares * fund.CurrentNAV;
            existingHolding.GainLoss = existingHolding.CurrentValue - totalCost;
            existingHolding.GainLossPercentage = (existingHolding.GainLoss / totalCost) * 100;
        }
        else
        {
            // Create new holding
            var holding = new PortfolioHolding
            {
                HoldingId = $"HOLD{DateTime.Now.Ticks}",
                PortfolioId = portfolioId,
                FundId = fund.FundId,
                FundSymbol = fund.FundSymbol,
                FundName = fund.FundName,
                Shares = shares,
                AverageCost = fund.CurrentNAV,
                CurrentValue = amount,
                GainLoss = 0,
                GainLossPercentage = 0,
                PurchaseDate = DateTime.Now,
            };
            portfolio.Holdings.Add(holding);
        }

        // Update portfolio value
        portfolio.TotalValue = portfolio.Holdings.Sum(h => h.CurrentValue);
        _dataStore.UpdatePortfolio(portfolio);

        // Update account balance
        account.Balance -= amount;
        _dataStore.UpdateAccount(account);

        // Record transaction
        var transaction = new Transaction
        {
            TransactionId = $"TXN{DateTime.Now.Ticks}",
            AccountId = accountId,
            PortfolioId = portfolioId,
            FundId = fund.FundId,
            Type = TransactionType.Buy,
            Amount = amount,
            Shares = shares,
            PricePerShare = fund.CurrentNAV,
            TransactionDate = DateTime.Now,
            Status = TransactionStatus.Completed,
            Description =
                $"Purchased {shares:N4} shares of {fund.FundSymbol} at ${fund.CurrentNAV:N2}/share",
        };
        _dataStore.AddTransaction(transaction);

        var result = $"✓ Investment successful!\n";
        result += $"Purchased {shares:N4} shares of {fund.FundSymbol} ({fund.FundName})\n";
        result += $"Amount: ${amount:N2} at ${fund.CurrentNAV:N2} per share\n";
        result += $"Transaction ID: {transaction.TransactionId}\n";
        result += $"New account balance: ${account.Balance:N2}\n";
        result += $"Portfolio value: ${portfolio.TotalValue:N2}\n";

        return result;
    }

    [Description("Get transaction history for an account")]
    public string GetTransactionHistory(
        [Description("The account ID")] string accountId,
        [Description("Number of recent transactions to show (default 10)")] int limit = 10
    )
    {
        var transactions = _dataStore.GetTransactionsByAccount(accountId).Take(limit).ToList();

        if (!transactions.Any())
            return $"No transactions found for account {accountId}.";

        var result = $"Transaction History for {accountId} (Last {transactions.Count}):\n\n";

        foreach (var txn in transactions)
        {
            result += $"• {txn.TransactionDate:yyyy-MM-dd HH:mm} - {txn.Type}\n";
            result += $"  {txn.Description}\n";
            result += $"  Amount: ${txn.Amount:N2} | Status: {txn.Status}\n";
            result += $"  Transaction ID: {txn.TransactionId}\n\n";
        }

        return result;
    }

    [Description("Deposit money into account")]
    public string DepositFunds(
        [Description("The account ID")] string accountId,
        [Description("Amount to deposit")] decimal amount
    )
    {
        if (amount <= 0)
            return "Error: Deposit amount must be positive.";

        var account = _dataStore.GetAccount(accountId);
        if (account == null)
            return $"Error: Account {accountId} not found.";

        account.Balance += amount;
        _dataStore.UpdateAccount(account);

        var transaction = new Transaction
        {
            TransactionId = $"TXN{DateTime.Now.Ticks}",
            AccountId = accountId,
            Type = TransactionType.Deposit,
            Amount = amount,
            TransactionDate = DateTime.Now,
            Status = TransactionStatus.Completed,
            Description = $"Deposit of ${amount:N2}",
        };
        _dataStore.AddTransaction(transaction);

        return $"✓ Deposited ${amount:N2} successfully. New balance: ${account.Balance:N2}";
    }
}

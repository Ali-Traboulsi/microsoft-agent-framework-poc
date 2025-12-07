using AgentFrameworkQuickStart.Models;

namespace AgentFrameworkQuickStart.Services;

/// <summary>
/// In-memory data store for the banking investment application
/// </summary>
public class InvestmentDataStore
{
    private readonly Dictionary<string, Account> _accounts = new();
    private readonly Dictionary<string, Portfolio> _portfolios = new();
    private readonly Dictionary<string, MutualFund> _mutualFunds = new();
    private readonly List<Transaction> _transactions = new();

    public InvestmentDataStore()
    {
        SeedData();
    }

    // Account operations
    public Account? GetAccount(string accountId) => _accounts.GetValueOrDefault(accountId);

    public IEnumerable<Account> GetAllAccounts() => _accounts.Values;

    public void AddAccount(Account account) => _accounts[account.AccountId] = account;

    public void UpdateAccount(Account account) => _accounts[account.AccountId] = account;

    // Portfolio operations
    public Portfolio? GetPortfolio(string portfolioId) =>
        _portfolios.GetValueOrDefault(portfolioId);

    public IEnumerable<Portfolio> GetAllPortfolios() => _portfolios.Values;

    public IEnumerable<Portfolio> GetPortfoliosByAccount(string accountId) =>
        _portfolios.Values.Where(p => p.AccountId == accountId);

    public void AddPortfolio(Portfolio portfolio) => _portfolios[portfolio.PortfolioId] = portfolio;

    public void UpdatePortfolio(Portfolio portfolio) =>
        _portfolios[portfolio.PortfolioId] = portfolio;

    // Mutual Fund operations
    public MutualFund? GetMutualFund(string fundId) => _mutualFunds.GetValueOrDefault(fundId);

    public MutualFund? GetMutualFundBySymbol(string symbol) =>
        _mutualFunds.Values.FirstOrDefault(f =>
            f.FundSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
        );

    public IEnumerable<MutualFund> GetAllMutualFunds() => _mutualFunds.Values;

    public IEnumerable<MutualFund> GetMutualFundsByCategory(FundCategory category) =>
        _mutualFunds.Values.Where(f => f.Category == category);

    public IEnumerable<MutualFund> GetMutualFundsByRisk(RiskLevel risk) =>
        _mutualFunds.Values.Where(f => f.RiskLevel == risk);

    // Transaction operations
    public void AddTransaction(Transaction transaction) => _transactions.Add(transaction);

    public IEnumerable<Transaction> GetTransactionsByAccount(string accountId) =>
        _transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.TransactionDate);

    public IEnumerable<Transaction> GetTransactionsByPortfolio(string portfolioId) =>
        _transactions
            .Where(t => t.PortfolioId == portfolioId)
            .OrderByDescending(t => t.TransactionDate);

    public void SeedData()
    {
        // Seed accounts
        _accounts["ACC001"] = new Account
        {
            AccountId = "ACC001",
            CustomerId = "CUST001",
            CustomerName = "John Smith",
            Balance = 50000m,
            Currency = "USD",
            CreatedDate = DateTime.Now.AddYears(-2),
            Status = AccountStatus.Active,
        };

        _accounts["ACC002"] = new Account
        {
            AccountId = "ACC002",
            CustomerId = "CUST002",
            CustomerName = "Sarah Johnson",
            Balance = 100000m,
            Currency = "USD",
            CreatedDate = DateTime.Now.AddYears(-1),
            Status = AccountStatus.Active,
        };

        // Seed mutual funds
        _mutualFunds["FUND001"] = new MutualFund
        {
            FundId = "FUND001",
            FundName = "Global Technology Growth Fund",
            FundSymbol = "GTGF",
            Category = FundCategory.Equity,
            CurrentNAV = 125.50m,
            ExpenseRatio = 0.75m,
            MinimumInvestment = 1000m,
            RiskLevel = RiskLevel.High,
            Description =
                "Invests primarily in global technology companies with high growth potential",
            YTDReturn = 18.5m,
            OneYearReturn = 22.3m,
            ThreeYearReturn = 45.8m,
        };

        _mutualFunds["FUND002"] = new MutualFund
        {
            FundId = "FUND002",
            FundName = "US Bond Income Fund",
            FundSymbol = "USBIF",
            Category = FundCategory.Bond,
            CurrentNAV = 10.75m,
            ExpenseRatio = 0.45m,
            MinimumInvestment = 500m,
            RiskLevel = RiskLevel.Low,
            Description = "Conservative bond fund focused on stable income generation",
            YTDReturn = 3.2m,
            OneYearReturn = 4.1m,
            ThreeYearReturn = 12.5m,
        };

        _mutualFunds["FUND003"] = new MutualFund
        {
            FundId = "FUND003",
            FundName = "Balanced Growth & Income Fund",
            FundSymbol = "BGIF",
            Category = FundCategory.Balanced,
            CurrentNAV = 55.20m,
            ExpenseRatio = 0.65m,
            MinimumInvestment = 1000m,
            RiskLevel = RiskLevel.Medium,
            Description = "Balanced portfolio of stocks and bonds for growth and income",
            YTDReturn = 10.5m,
            OneYearReturn = 12.7m,
            ThreeYearReturn = 28.3m,
        };

        _mutualFunds["FUND004"] = new MutualFund
        {
            FundId = "FUND004",
            FundName = "S&P 500 Index Fund",
            FundSymbol = "SP500",
            Category = FundCategory.Index,
            CurrentNAV = 420.75m,
            ExpenseRatio = 0.05m,
            MinimumInvestment = 500m,
            RiskLevel = RiskLevel.Medium,
            Description = "Tracks the S&P 500 index with ultra-low fees",
            YTDReturn = 15.2m,
            OneYearReturn = 18.9m,
            ThreeYearReturn = 38.6m,
        };

        _mutualFunds["FUND005"] = new MutualFund
        {
            FundId = "FUND005",
            FundName = "Emerging Markets Equity Fund",
            FundSymbol = "EMEF",
            Category = FundCategory.International,
            CurrentNAV = 32.40m,
            ExpenseRatio = 1.15m,
            MinimumInvestment = 2000m,
            RiskLevel = RiskLevel.VeryHigh,
            Description = "High-risk, high-reward emerging markets exposure",
            YTDReturn = 25.8m,
            OneYearReturn = 30.2m,
            ThreeYearReturn = 52.1m,
        };

        _mutualFunds["FUND006"] = new MutualFund
        {
            FundId = "FUND006",
            FundName = "Healthcare Sector Fund",
            FundSymbol = "HLTHF",
            Category = FundCategory.Sector,
            CurrentNAV = 88.90m,
            ExpenseRatio = 0.85m,
            MinimumInvestment = 1500m,
            RiskLevel = RiskLevel.High,
            Description = "Focused investment in healthcare and biotech sectors",
            YTDReturn = 14.3m,
            OneYearReturn = 16.8m,
            ThreeYearReturn = 42.5m,
        };
        _portfolios["PORT001"] = new Portfolio
        {
            PortfolioId = "PORT001",
            AccountId = "ACC001",
            PortfolioName = "John's Retirement Fund",
            Strategy = PortfolioStrategy.Balanced,
            CreatedDate = DateTime.Now.AddYears(-2),
            Holdings = new List<PortfolioHolding>(),
        };
        _portfolios["PORT002"] = new Portfolio
        {
            PortfolioId = "PORT002",
            AccountId = "ACC002",
            PortfolioName = "Sarah's Growth Portfolio",
            Strategy = PortfolioStrategy.Aggressive,
            CreatedDate = DateTime.Now.AddYears(-1),
            Holdings = new List<PortfolioHolding>(),
        };
    }
}

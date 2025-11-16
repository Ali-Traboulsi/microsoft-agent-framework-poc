# 📚 Code Examples & Patterns

This guide shows key patterns and code snippets from the project for learning and reference.

## Table of Contents
1. [Creating Agents](#creating-agents)
2. [Building Tools](#building-tools)
3. [Workflows](#workflows)
4. [Data Models](#data-models)
5. [Advanced Patterns](#advanced-patterns)

---

## Creating Agents

### Basic Agent

```csharp
var agent = chatClient.CreateAIAgent(
    name: "HelperBot",
    instructions: "You are a helpful assistant."
);

// Use the agent
var response = await agent.RunAsync("Hello!");
Console.WriteLine(response.Text);
```

### Agent with Tools

```csharp
var agent = chatClient.CreateAIAgent(
    name: "PortfolioManager",
    instructions: @"You are a portfolio manager. 
        Help clients manage their investments.",
    tools: [
        AIFunctionFactory.Create(portfolioTools.CreatePortfolio),
        AIFunctionFactory.Create(portfolioTools.GetPortfolioDetails),
        AIFunctionFactory.Create(portfolioTools.ListPortfolios)
    ]
);
```

### Agent with Multiple Tool Sources

```csharp
var agent = chatClient.CreateAIAgent(
    name: "InvestmentAdvisor",
    instructions: "Provide investment advice",
    tools: [
        // Portfolio tools
        AIFunctionFactory.Create(portfolioTools.GetDetails),
        
        // Fund tools
        AIFunctionFactory.Create(fundTools.SearchFunds),
        AIFunctionFactory.Create(fundTools.CompareFunds),
        
        // Account tools
        AIFunctionFactory.Create(accountTools.GetBalance)
    ]
);
```

---

## Building Tools

### Simple Tool

```csharp
[Description("Get the current time")]
public string GetCurrentTime()
{
    return DateTime.Now.ToString("HH:mm:ss");
}

// Register with agent
AIFunctionFactory.Create(GetCurrentTime)
```

### Tool with Parameters

```csharp
[Description("Search for mutual funds by category")]
public string SearchFunds(
    [Description("Fund category: Equity, Bond, Balanced, etc.")] 
    string category)
{
    var funds = _dataStore.GetMutualFundsByCategory(category);
    return FormatFunds(funds);
}
```

### Tool with Optional Parameters

```csharp
[Description("Get transaction history")]
public string GetTransactionHistory(
    [Description("The account ID")] 
    string accountId,
    [Description("Number of transactions (default 10)")] 
    int limit = 10)
{
    var transactions = _dataStore
        .GetTransactionsByAccount(accountId)
        .Take(limit);
    return FormatTransactions(transactions);
}
```

### Complex Tool with Validation

```csharp
[Description("Fund a portfolio by purchasing mutual fund shares")]
public string FundPortfolio(
    [Description("The account ID")] string accountId,
    [Description("The portfolio ID")] string portfolioId,
    [Description("The fund symbol")] string fundSymbol,
    [Description("Amount to invest")] decimal amount)
{
    // 1. Validate account
    var account = _dataStore.GetAccount(accountId);
    if (account == null)
        return $"Error: Account {accountId} not found.";
    
    // 2. Check balance
    if (account.Balance < amount)
        return $"Error: Insufficient funds. Available: ${account.Balance:N2}";
    
    // 3. Validate portfolio
    var portfolio = _dataStore.GetPortfolio(portfolioId);
    if (portfolio == null)
        return $"Error: Portfolio {portfolioId} not found.";
    
    if (portfolio.AccountId != accountId)
        return "Error: Portfolio doesn't belong to this account.";
    
    // 4. Validate fund
    var fund = _dataStore.GetMutualFundBySymbol(fundSymbol);
    if (fund == null)
        return $"Error: Fund {fundSymbol} not found.";
    
    if (amount < fund.MinimumInvestment)
        return $"Error: Below minimum investment of ${fund.MinimumInvestment:N2}";
    
    // 5. Execute transaction
    var shares = amount / fund.CurrentNAV;
    
    // 6. Update holdings
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
        PurchaseDate = DateTime.Now
    };
    
    portfolio.Holdings.Add(holding);
    portfolio.TotalValue += amount;
    _dataStore.UpdatePortfolio(portfolio);
    
    // 7. Update account
    account.Balance -= amount;
    _dataStore.UpdateAccount(account);
    
    // 8. Record transaction
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
        Description = $"Purchased {shares:N4} shares"
    };
    _dataStore.AddTransaction(transaction);
    
    return $"✓ Successfully invested ${amount:N2} in {fund.FundSymbol}";
}
```

---

## Workflows

### Sequential Workflow

```csharp
// Build workflow
var workflow = new WorkflowBuilder(agent1)
    .AddEdge(agent1, agent2)
    .AddEdge(agent2, agent3)
    .Build();

// Execute workflow
await using var run = await InProcessExecution.StreamAsync(
    workflow, 
    new ChatMessage(ChatRole.User, "User input")
);

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// Watch events
await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        Console.WriteLine($"Output: {outputEvent.Data}");
    }
}
```

### Concurrent Workflow

```csharp
// All agents run in parallel
var concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent([
    advisorAgent,
    portfolioAgent,
    complianceAgent
]);

await using var run = await InProcessExecution.StreamAsync(
    concurrentWorkflow,
    new ChatMessage(ChatRole.User, "Analyze portfolio PORT123")
);

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent 
        && outputEvent.Data is AgentRunResponse response)
    {
        foreach (var message in response.Messages)
        {
            Console.WriteLine($"Agent: {message.Text}");
        }
    }
}
```

### Conditional Workflow (Pattern)

```csharp
// Build workflow with conditions
var workflow = new WorkflowBuilder(analyzerAgent)
    .AddEdge(
        analyzerAgent, 
        highRiskAgent,
        condition: result => IsHighRisk(result)
    )
    .AddEdge(
        analyzerAgent,
        lowRiskAgent,
        condition: result => !IsHighRisk(result)
    )
    .Build();

bool IsHighRisk(object? result)
{
    // Your logic here
    return result?.ToString()?.Contains("high risk") ?? false;
}
```

### Sub-Workflow Pattern

```csharp
// Create a reusable sub-workflow
var subWorkflow = new WorkflowBuilder(step1)
    .AddEdge(step1, step2)
    .AddEdge(step2, step3)
    .WithOutputFrom(step3)
    .Build();

// Use it as an executor in parent workflow
var subWorkflowExecutor = subWorkflow.BindAsExecutor("SubWorkflowName");

var mainWorkflow = new WorkflowBuilder(mainAgent)
    .AddEdge(mainAgent, subWorkflowExecutor)
    .AddEdge(subWorkflowExecutor, finalAgent)
    .Build();
```

---

## Data Models

### Simple Entity

```csharp
public class Account
{
    public string AccountId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public AccountStatus Status { get; set; }
}

public enum AccountStatus
{
    Active,
    Suspended,
    Closed
}
```

### Entity with Relationships

```csharp
public class Portfolio
{
    public string PortfolioId { get; set; } = string.Empty;
    public string PortfolioName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    
    // Related entities
    public List<PortfolioHolding> Holdings { get; set; } = new();
    
    // Computed properties
    public decimal TotalValue => Holdings.Sum(h => h.CurrentValue);
}

public class PortfolioHolding
{
    public string HoldingId { get; set; } = string.Empty;
    public string PortfolioId { get; set; } = string.Empty;
    public string FundId { get; set; } = string.Empty;
    public decimal Shares { get; set; }
    public decimal CurrentValue { get; set; }
}
```

### Rich Domain Model

```csharp
public class MutualFund
{
    public string FundId { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string FundSymbol { get; set; } = string.Empty;
    
    // Categories and risk
    public FundCategory Category { get; set; }
    public RiskLevel RiskLevel { get; set; }
    
    // Financial data
    public decimal CurrentNAV { get; set; }
    public decimal ExpenseRatio { get; set; }
    public decimal MinimumInvestment { get; set; }
    
    // Performance metrics
    public decimal YTDReturn { get; set; }
    public decimal OneYearReturn { get; set; }
    public decimal ThreeYearReturn { get; set; }
    
    // Metadata
    public string Description { get; set; } = string.Empty;
}
```

---

## Advanced Patterns

### Agent Streaming

```csharp
Console.Write("Agent: ");

await foreach (var update in agent.RunStreamingAsync(query))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.Write(update.Text);
    }
}

Console.WriteLine();
```

### Error Handling

```csharp
try
{
    var result = await agent.RunAsync(query);
    Console.WriteLine(result.Text);
}
catch (Exception ex) when (ex.Message.Contains("rate_limit"))
{
    Console.WriteLine("Rate limit hit. Please wait.");
    await Task.Delay(TimeSpan.FromSeconds(60));
    // Retry logic
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    // Log error
}
```

### Tool with Dependency Injection

```csharp
public class PortfolioTools
{
    private readonly InvestmentDataStore _dataStore;
    private readonly ILogger<PortfolioTools> _logger;
    
    public PortfolioTools(
        InvestmentDataStore dataStore,
        ILogger<PortfolioTools> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }
    
    [Description("Create portfolio")]
    public string CreatePortfolio(string name)
    {
        _logger.LogInformation("Creating portfolio: {Name}", name);
        
        // Create portfolio
        var portfolio = new Portfolio { PortfolioName = name };
        _dataStore.AddPortfolio(portfolio);
        
        _logger.LogInformation("Portfolio created: {Id}", portfolio.PortfolioId);
        return $"Created portfolio {portfolio.PortfolioId}";
    }
}
```

### Data Store Pattern

```csharp
public class InvestmentDataStore
{
    private readonly Dictionary<string, Account> _accounts = new();
    private readonly Dictionary<string, Portfolio> _portfolios = new();
    private readonly List<Transaction> _transactions = new();
    
    public Account? GetAccount(string id) => 
        _accounts.GetValueOrDefault(id);
    
    public void AddAccount(Account account) => 
        _accounts[account.AccountId] = account;
    
    public void UpdateAccount(Account account) => 
        _accounts[account.AccountId] = account;
    
    public IEnumerable<Account> GetAllAccounts() => 
        _accounts.Values;
    
    // Seed data in constructor
    public InvestmentDataStore()
    {
        SeedData();
    }
    
    private void SeedData()
    {
        _accounts["ACC001"] = new Account
        {
            AccountId = "ACC001",
            CustomerName = "John Smith",
            Balance = 50000m
        };
    }
}
```

### Formatted Output

```csharp
public string GetPortfolioDetails(string portfolioId)
{
    var portfolio = _dataStore.GetPortfolio(portfolioId);
    if (portfolio == null)
        return $"Error: Portfolio {portfolioId} not found.";
    
    // Build formatted string
    var result = new StringBuilder();
    result.AppendLine($"Portfolio: {portfolio.PortfolioName}");
    result.AppendLine($"ID: {portfolio.PortfolioId}");
    result.AppendLine($"Total Value: ${portfolio.TotalValue:N2}");
    result.AppendLine();
    
    if (portfolio.Holdings.Any())
    {
        result.AppendLine("Holdings:");
        foreach (var holding in portfolio.Holdings)
        {
            result.AppendLine($"  • {holding.FundSymbol} - {holding.FundName}");
            result.AppendLine($"    Shares: {holding.Shares:N2}");
            result.AppendLine($"    Value: ${holding.CurrentValue:N2}");
            result.AppendLine($"    Gain/Loss: {holding.GainLossPercentage:N2}%");
        }
    }
    else
    {
        result.AppendLine("No holdings yet.");
    }
    
    return result.ToString();
}
```

### Enum Parsing

```csharp
[Description("Create portfolio with strategy")]
public string CreatePortfolio(
    string accountId,
    string name,
    string strategy)
{
    // Parse enum safely
    if (!Enum.TryParse<PortfolioStrategy>(strategy, true, out var strategyEnum))
    {
        var validValues = string.Join(", ", Enum.GetNames<PortfolioStrategy>());
        return $"Error: Invalid strategy. Valid values: {validValues}";
    }
    
    // Use parsed enum
    var portfolio = new Portfolio
    {
        PortfolioName = name,
        Strategy = strategyEnum
    };
    
    return $"Created {strategyEnum} portfolio";
}
```

### Aggregation and Grouping

```csharp
[Description("Get portfolio allocation by category")]
public string GetAllocation(string portfolioId)
{
    var portfolio = _dataStore.GetPortfolio(portfolioId);
    if (portfolio == null) return "Portfolio not found";
    
    var totalValue = portfolio.Holdings.Sum(h => h.CurrentValue);
    
    // Group by category and calculate percentages
    var allocation = portfolio.Holdings
        .GroupBy(h => GetFundCategory(h.FundId))
        .Select(g => new
        {
            Category = g.Key,
            Value = g.Sum(h => h.CurrentValue),
            Percentage = (g.Sum(h => h.CurrentValue) / totalValue) * 100
        })
        .OrderByDescending(x => x.Percentage);
    
    var result = new StringBuilder();
    result.AppendLine("Portfolio Allocation:");
    
    foreach (var item in allocation)
    {
        result.AppendLine(
            $"  {item.Category}: {item.Percentage:N1}% (${item.Value:N2})"
        );
    }
    
    return result.ToString();
}
```

---

## Testing Patterns

### Unit Test Example

```csharp
[Fact]
public void CreatePortfolio_ValidInput_ReturnsSuccess()
{
    // Arrange
    var dataStore = new InvestmentDataStore();
    var tools = new PortfolioTools(dataStore);
    
    // Act
    var result = tools.CreatePortfolio(
        accountId: "ACC001",
        portfolioName: "Test Portfolio",
        strategy: "Conservative"
    );
    
    // Assert
    Assert.Contains("created successfully", result);
}

[Fact]
public void FundPortfolio_InsufficientBalance_ReturnsError()
{
    // Arrange
    var dataStore = new InvestmentDataStore();
    var tools = new AccountTools(dataStore);
    
    // Act
    var result = tools.FundPortfolio(
        accountId: "ACC001",
        portfolioId: "PORT001",
        fundSymbol: "GTGF",
        amount: 1000000m // More than available
    );
    
    // Assert
    Assert.Contains("Insufficient funds", result);
}
```

---

## Best Practices

### 1. Tool Naming
```csharp
// ✅ Good - Clear, action-oriented
CreatePortfolio, GetAccountBalance, SearchFunds

// ❌ Bad - Vague or technical
DoStuff, Process, Handle
```

### 2. Error Messages
```csharp
// ✅ Good - Specific and helpful
return $"Error: Account {accountId} not found. Please verify the account ID.";

// ❌ Bad - Generic
return "Error occurred";
```

### 3. Parameter Descriptions
```csharp
// ✅ Good - Clear expectations
[Description("The account ID (e.g., ACC001)")]
string accountId

// ❌ Bad - No context
[Description("ID")]
string id
```

### 4. Response Formatting
```csharp
// ✅ Good - Structured, readable
var result = $@"
Portfolio: {name}
Balance: ${balance:N2}
Status: {status}
";

// ❌ Bad - Hard to read
var result = name + balance + status;
```

---

**These patterns form the foundation of effective Agent Framework applications!**

# 🚀 Quick Start Guide

## Run the Demo in 3 Steps

### Step 1: Set Your API Key

Option A - Environment Variable (Recommended):
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

Option B - Edit Program.cs:
```csharp
// Line 21 in Program.cs
var apiKey = "sk-your-key-here";
```

### Step 2: Build the Project

```bash
dotnet build
```

### Step 3: Run the Demo

```bash
dotnet run
```

## 📺 What You'll See

The demo runs 9 scenarios automatically:

1. ✅ **Account Balance Check** - View account details
2. ✅ **Fund Research** - Search low-risk funds
3. ✅ **Portfolio Creation** - Create "Retirement Savings" portfolio
4. ✅ **Investment Workflow** - Multi-agent investment with compliance
5. ✅ **Direct Investment** - Purchase S&P 500 fund
6. ✅ **Fund Comparison** - Compare 3 different funds
7. ✅ **Transaction History** - View recent transactions
8. ✅ **Concurrent Analysis** - Multiple agents analyze portfolio
9. ✅ **Rebalancing Advice** - Get portfolio optimization tips

## 🎮 Interactive Mode

To interact with agents directly, modify `Program.cs`:

```csharp
// Add this at the end of Main()
while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();
    if (input == "exit") break;
    
    // Choose which agent to use
    var response = await portfolioAgent.RunAsync(input);
    Console.WriteLine($"Agent: {response.Text}");
}
```

## 💬 Example Queries

Try asking the agents:

**Portfolio Manager**
- "Create a growth portfolio for account ACC001"
- "Show me portfolio details for PORT123"
- "What's the allocation of my portfolio?"

**Investment Advisor**
- "Find high-performing equity funds"
- "Compare GTGF and SP500 funds"
- "Recommend funds for a moderate risk investor"

**Account Services**
- "What's my account balance?"
- "Deposit $10000 into account ACC001"
- "Show my last 5 transactions"

**Compliance Officer**
- "Check if this investment meets requirements"
- "Verify portfolio compliance for ACC001"

## 🔧 Customization

### Add Your Own Mutual Fund

Edit `InvestmentDataStore.cs`:

```csharp
_mutualFunds["FUND007"] = new MutualFund
{
    FundId = "FUND007",
    FundName = "Your Fund Name",
    FundSymbol = "SYMBOL",
    Category = FundCategory.Equity,
    CurrentNAV = 100.00m,
    ExpenseRatio = 0.50m,
    MinimumInvestment = 1000m,
    RiskLevel = RiskLevel.Medium,
    Description = "Your fund description",
    YTDReturn = 10.0m,
    OneYearReturn = 12.0m,
    ThreeYearReturn = 30.0m
};
```

### Add Custom Tool

Create a new method in any Tool class:

```csharp
[Description("Your tool description")]
public string YourToolName(
    [Description("Parameter description")] string param)
{
    // Your logic here
    return "Result";
}
```

Then add it to an agent:

```csharp
tools: [
    AIFunctionFactory.Create(portfolioTools.YourToolName),
    // ... other tools
]
```

### Create Custom Agent

```csharp
var customAgent = chatClient.CreateAIAgent(
    name: "CustomAgent",
    instructions: "Your custom instructions",
    tools: [/* your tools */]
);
```

## 📊 Sample Data

The demo includes:

**Accounts:**
- ACC001 - John Smith ($50,000 balance)
- ACC002 - Sarah Johnson ($100,000 balance)

**Funds:**
- GTGF - Technology (High Risk, 22.3% 1yr return)
- USBIF - Bonds (Low Risk, 4.1% 1yr return)
- BGIF - Balanced (Medium Risk, 12.7% 1yr return)
- SP500 - Index (Medium Risk, 18.9% 1yr return)
- EMEF - Emerging Markets (Very High Risk, 30.2% 1yr return)
- HLTHF - Healthcare (High Risk, 16.8% 1yr return)

## 🐛 Troubleshooting

### Error: "API Key not found"
- Set the `OPENAI_API_KEY` environment variable
- Or hardcode it in Program.cs (line 21)

### Error: "Rate limit exceeded"
- Wait a few minutes
- Consider upgrading your OpenAI plan
- The demo makes multiple API calls

### Error: "Package not found"
- Run `dotnet restore`
- Ensure you have .NET 9.0 SDK installed

### Build Errors
- Clean and rebuild: `dotnet clean && dotnet build`
- Check that all files are present in the project

## 🎯 Understanding the Code

### Key Files

1. **Program.cs** (355 lines)
   - Main entry point
   - Agent initialization
   - Workflow building
   - Demo scenarios

2. **Models/** (5 files)
   - Domain entities
   - Enums for types

3. **Services/InvestmentDataStore.cs**
   - Data layer
   - Seed data
   - CRUD operations

4. **Tools/** (3 files)
   - Agent function tools
   - Business logic
   - 14 tool methods total

### Flow

```
User Request
    ↓
AI Agent (with instructions)
    ↓
Function Tools (calls C# methods)
    ↓
Data Store (CRUD operations)
    ↓
Domain Models (entities)
    ↓
Response to User
```

## 📖 Learn More

- Read `README.md` for full documentation
- Check `PROJECT_SUMMARY.md` for technical details
- Explore the code - it's well-commented!

## ⚡ Pro Tips

1. **Use streaming** for better UX:
   ```csharp
   await foreach (var update in agent.RunStreamingAsync(query))
       Console.Write(update.Text);
   ```

2. **Chain workflows** for complex processes:
   ```csharp
   var workflow = new WorkflowBuilder(agent1)
       .AddEdge(agent1, agent2)
       .AddEdge(agent2, agent3)
       .Build();
   ```

3. **Handle errors gracefully**:
   ```csharp
   try {
       var result = await agent.RunAsync(query);
   } catch (Exception ex) {
       // Handle specific errors
   }
   ```

4. **Use concurrent workflows** for parallel tasks:
   ```csharp
   var workflow = AgentWorkflowBuilder.BuildConcurrent(agents);
   ```

---

**Ready to explore? Run `dotnet run` and watch the magic! 🎉**

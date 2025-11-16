# 🏦 Banking Investment Agent Framework Demo

A comprehensive demonstration of Microsoft's Agent Framework applied to a banking and investment management scenario, showcasing multi-agent systems, workflows, and tool integration.

## 🎯 Overview

This project implements a complete banking investment platform using the Microsoft Agent Framework, featuring:

- **Multi-Agent System**: Specialized agents for different banking roles
- **Function Tools**: Rich set of banking operations (portfolios, funds, accounts)
- **Workflows**: Sequential and concurrent agent orchestration
- **Domain Models**: Complete banking entities (accounts, portfolios, mutual funds, transactions)

## 🏗️ Architecture

### Agents

1. **Portfolio Manager Agent**
   - Creates and manages investment portfolios
   - Executes fund purchases
   - Provides portfolio analysis and allocation insights
   - Tracks portfolio performance

2. **Investment Advisor Agent**
   - Recommends suitable mutual funds
   - Provides detailed fund analysis and comparisons
   - Explains investment strategies
   - Helps with fund selection based on risk tolerance

3. **Account Services Agent**
   - Manages account balances and deposits
   - Processes fund transfers
   - Shows transaction history
   - Handles account-related queries

4. **Compliance Officer Agent**
   - Verifies regulatory compliance
   - Checks risk appropriateness
   - Reviews transaction validity
   - Ensures proper documentation

### Tools

**Portfolio Tools**
- `CreatePortfolio`: Create new investment portfolios
- `GetPortfolioDetails`: View portfolio holdings and performance
- `ListPortfolios`: List all portfolios for an account
- `GetPortfolioAllocation`: Analyze portfolio allocation by category

**Mutual Fund Tools**
- `SearchFunds`: Search funds by category, risk level, or returns
- `GetFundDetails`: Get detailed fund information
- `ListAllFunds`: View all available funds
- `CompareFunds`: Compare multiple funds side-by-side

**Account Tools**
- `GetAccountBalance`: Check account balance and details
- `FundPortfolio`: Transfer funds from account to portfolio
- `GetTransactionHistory`: View transaction history
- `DepositFunds`: Deposit money into account

### Workflows

**Sequential Workflow**
```
Advisor → Compliance → Portfolio Manager
```
Used for complete investment processes with compliance checks.

**Concurrent Workflow**
```
      ┌─ Advisor
      ├─ Portfolio Manager
      └─ Compliance
```
Used for parallel analysis and portfolio reviews.

## 📊 Domain Models

- **Account**: Customer accounts with balances
- **Portfolio**: Investment portfolios with holdings
- **MutualFund**: Available mutual funds with performance metrics
- **PortfolioHolding**: Fund positions within portfolios
- **Transaction**: Financial transactions with full audit trail

## 🚀 Getting Started

### Prerequisites

- .NET 9.0 SDK
- OpenAI API key (or Azure OpenAI credentials)

### Setup

1. **Clone the repository**

2. **Set your OpenAI API key**
   ```bash
   # Option 1: Environment variable
   export OPENAI_API_KEY="your-api-key-here"
   
   # Option 2: Edit Program.cs line 21
   ```

3. **Restore packages**
   ```bash
   dotnet restore
   ```

4. **Run the demo**
   ```bash
   dotnet run
   ```

## 📝 Demo Scenarios

The application demonstrates 9 comprehensive scenarios:

1. **Account Information Query** - Check account balance and details
2. **Investment Fund Research** - Search and analyze available funds
3. **Portfolio Creation** - Create a new investment portfolio
4. **Complete Investment Process** - Multi-agent workflow with compliance
5. **Direct Fund Investment** - Purchase funds into portfolio
6. **Advanced Fund Analysis** - Compare and analyze multiple funds
7. **Transaction History Review** - View account transaction history
8. **Concurrent Analysis** - Multiple agents analyzing portfolio simultaneously
9. **Portfolio Rebalancing Advice** - Get recommendations for portfolio optimization

## 🔑 Key Features

### ✅ Multi-Agent Collaboration
- Specialized agents working together
- Sequential and concurrent workflows
- Agent-to-agent communication

### ✅ Function Tools Integration
- Rich set of banking operations
- Type-safe function calling
- Automatic parameter validation

### ✅ Workflow Orchestration
- Visual workflow building with `WorkflowBuilder`
- Sequential execution for compliance processes
- Concurrent execution for parallel analysis

### ✅ Real Banking Operations
- Portfolio creation and management
- Fund research and investment
- Transaction processing
- Compliance checking

### ✅ Streaming Support
- Real-time agent responses
- Workflow event streaming
- Progress tracking

## 📚 Code Structure

```
AgentFrameworkQuickStart/
├── Models/                    # Domain models
│   ├── Account.cs
│   ├── Portfolio.cs
│   ├── MutualFund.cs
│   ├── PortfolioHolding.cs
│   └── Transaction.cs
├── Services/                  # Business logic
│   └── InvestmentDataStore.cs
├── Tools/                     # Agent function tools
│   ├── AccountTools.cs
│   ├── PortfolioTools.cs
│   └── MutualFundTools.cs
└── Program.cs                 # Main demo application
```

## 🎓 Learning Resources

### Microsoft Agent Framework
- [Documentation](https://learn.microsoft.com/agent-framework/)
- [GitHub Repository](https://github.com/microsoft/agent-framework)
- [Quick Start Guide](https://learn.microsoft.com/agent-framework/tutorials/quick-start)

### Key Concepts Demonstrated

1. **Agent Creation**
   ```csharp
   var agent = chatClient.CreateAIAgent(
       name: "AgentName",
       instructions: "System prompt here",
       tools: [AIFunctionFactory.Create(method)]
   );
   ```

2. **Workflow Building**
   ```csharp
   var workflow = new WorkflowBuilder(agent1)
       .AddEdge(agent1, agent2)
       .AddEdge(agent2, agent3)
       .Build();
   ```

3. **Function Tools**
   ```csharp
   [Description("Tool description")]
   public string MethodName(
       [Description("Param description")] string param)
   {
       // Implementation
   }
   ```

## 💡 Extension Ideas

This demo can be extended with:

- **Risk Analysis Agent**: Advanced portfolio risk assessment
- **Market Data Integration**: Real-time market data and pricing
- **Reporting Agent**: Generate investment reports and statements
- **Customer Service Bot**: Handle customer inquiries
- **Robo-Advisor**: Automated investment recommendations
- **Tax Optimization**: Tax-loss harvesting and optimization strategies
- **Persistent Storage**: Database integration for data persistence
- **Authentication**: User authentication and authorization
- **API Integration**: External financial data APIs

## 🔐 Security Notes

- API keys are for demo purposes only
- In production, use secure credential management
- Implement proper authentication and authorization
- Use Azure Key Vault or similar for secrets
- Validate all user inputs
- Implement proper audit logging

## 📄 License

This is a demonstration project for educational purposes.

## 🤝 Contributing

Feel free to fork and extend this demo with additional features!

## 📞 Support

For issues with the Microsoft Agent Framework:
- [GitHub Issues](https://github.com/microsoft/agent-framework/issues)
- [Discord Community](https://discord.gg/b5zjErwbQM)

---

**Built with ❤️ using Microsoft Agent Framework**

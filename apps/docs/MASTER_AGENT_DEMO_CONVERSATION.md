# 🎯 Master Agent Demo Conversation

This document provides a complete multi-turn conversation that showcases **all** the Master Agent's capabilities in a logical, natural flow. Use this script to demonstrate the full power of the agent framework.

---

## 📋 Features Demonstrated

| Turn | Feature | Sub-Agent/Tool | Description |
|------|---------|----------------|-------------|
| 1 | Web Search | `SearchWeb` | Real-time market data retrieval |
| 2 | Account Services | `AccountServices` | Account balance inquiry |
| 3 | Investment Advisor | `InvestmentAdvisor` | Fund search and recommendations |
| 4 | Fund Comparison | `InvestmentAdvisor` | Compare multiple funds |
| 5 | Profit Projection | `ProfitProjection` | Calculate investment projections |
| 6 | Portfolio Manager | `PortfolioManager` | Create and fund portfolio |
| 7 | Compliance Check | `ComplianceOfficer` | Risk and compliance verification |
| 8 | Complete Workflow | `CompleteInvestmentWorkflow` | End-to-end orchestrated workflow |
| 9 | Multi-Agent Composite | Multiple Sub-Agents | Complex query touching multiple domains |
| 10 | Multi-Modal (Optional) | Image/Audio analysis | Document or audio analysis |

---

## 🎬 The Demo Script

### **Scenario Background**
You are demonstrating the system to a prospect. The story: A new client (John Smith - ACC001) wants to invest $25,000 in their portfolio. Walk through the complete investment journey.

---

### **Turn 1: Web Search - Real-Time Market Context** 🌐

**User:**
> "What are the latest trends in the global technology sector and how is the US stock market performing today?"

**Expected Behavior:**
- Master Agent invokes `SearchWeb` tool
- Retrieves real-time data from Bing Search API
- Provides current market context

**What to Highlight:**
- 🌐 Web search delegation indicator appears
- Real-time data retrieval (not cached)
- Natural language synthesis of search results

---

### **Turn 2: Account Services - Check Account Status** 💰

**User:**
> "I'd like to start investing. Can you check the balance and status of account ACC001?"

**Expected Behavior:**
- Routes to `AccountServices` sub-agent
- Calls `GetAccountBalance` tool
- Returns: John Smith, $50,000 balance, Active status

**What to Highlight:**
- Sub-agent delegation (AccountServices)
- Tool invocation visible in delegation events
- Clean response format with account details

---

### **Turn 3: Investment Advisor - Fund Discovery** 📊

**User:**
> "I'm interested in technology and growth investments with moderate to high risk. What funds do you recommend?"

**Expected Behavior:**
- Routes to `InvestmentAdvisor` sub-agent
- Calls `SearchFunds` tool with risk/category filters
- Returns matching funds (FUND001 - Global Technology Growth, FUND004 - S&P 500 Index, etc.)

**What to Highlight:**
- Different sub-agent handling the request
- Intelligent fund filtering based on criteria
- Data-driven recommendations with metrics

---

### **Turn 4: Investment Advisor - Fund Comparison** 📈

**User:**
> "Can you compare FUND001 (Global Technology Growth Fund) and FUND004 (S&P 500 Index Fund) side by side?"

**Expected Behavior:**
- Continues with `InvestmentAdvisor` sub-agent
- Calls `CompareFunds` tool
- Returns comparison table with NAV, returns, expense ratios, risk levels

**What to Highlight:**
- Same sub-agent maintains context
- Structured comparison output
- Key metrics: YTD Return, 1-Year Return, Expense Ratio, Risk Level

**Expected Data Points:**
| Metric | FUND001 (Tech Growth) | FUND004 (S&P 500) |
|--------|----------------------|-------------------|
| NAV | $125.50 | $420.75 |
| YTD Return | 18.5% | 15.2% |
| 1Y Return | 22.3% | 18.9% |
| Expense Ratio | 0.75% | 0.05% |
| Risk Level | High | Medium |

---

### **Turn 5: Profit Projection - Investment Scenarios** 💵

**User:**
> "If I invest $25,000 for 24 months with a moderate risk profile, what kind of returns can I expect? Show me different scenarios."

**Expected Behavior:**
- Routes to `ProfitProjection` sub-agent
- Calls `CalculateProfitProjection` tool with streaming progress
- Returns conservative, expected, and optimistic scenarios
- Shows recommended fund allocations

**What to Highlight:**
- Workflow progress indicators (multiple steps)
- Three scenario projections (Conservative/Expected/Optimistic)
- Fund recommendations based on risk profile
- Structured JSON data with visualization support

**Expected Output Structure:**
```
Investment: $25,000 | Duration: 24 months | Risk: Moderate

📊 Projected Returns:
- Conservative: $27,500 (10% total, ~5% annual)
- Expected: $30,250 (21% total, ~10% annual)  
- Optimistic: $33,750 (35% total, ~16% annual)

🎯 Recommended Allocation:
- 40% Balanced Growth & Income Fund (BGIF)
- 35% S&P 500 Index Fund (SP500)
- 25% Global Technology Growth Fund (GTGF)
```

---

### **Turn 6: Portfolio Manager - Create & Fund Portfolio** 📁

**User:**
> "That looks great! Please create a new portfolio called 'Tech Growth Portfolio' with a Growth strategy for account ACC001, and fund it with $25,000."

**Expected Behavior:**
- Routes to `PortfolioManager` sub-agent
- Calls `CreatePortfolio` tool → New portfolio ID (PORT...)
- Calls `FundPortfolio` tool → Transfer funds from account
- Confirms portfolio creation and funding

**What to Highlight:**
- Multiple tool calls in sequence
- Account balance deduction ($50,000 → $25,000)
- Portfolio ID generation
- Transaction confirmation

---

### **Turn 7: Compliance Officer - Risk Verification** ✅

**User:**
> "Before we proceed, can you verify that this investment is compliant with regulations and appropriate for the account's risk profile?"

**Expected Behavior:**
- Routes to `ComplianceOfficer` sub-agent
- Reviews portfolio allocation and account details
- Calls `GetAccountBalance`, `GetPortfolioDetails` tools
- Returns compliance status (✅ Compliant / ⚠️ Review Needed)

**What to Highlight:**
- Different specialist handling compliance
- Risk assessment capabilities
- Regulatory compliance verification
- Clear status indicators

---

### **Turn 8: Complete Investment Workflow - End-to-End Orchestration** 🔄

**User:**
> "I'd like to make another investment. Run a complete investment workflow for account ACC002 with $15,000 and aggressive risk profile."

**Expected Behavior:**
- Triggers `CompleteInvestmentWorkflow`
- Orchestrates 4 sub-agents in sequence:
  1. **AccountServices** → Verify account, check balance
  2. **ComplianceOfficer** → Risk assessment, regulatory check
  3. **InvestmentAdvisor** → Fund recommendations
  4. **PortfolioManager** → Portfolio creation, fund purchases
- Shows workflow progress with step-by-step updates

**What to Highlight:**
- Workflow progress panel with steps
- Sequential agent coordination
- Step completion times
- Unified final summary

**Expected Workflow Steps:**
```
Step 1/4: Account Verification ✅ (1.2s)
Step 2/4: Compliance Check ✅ (0.8s)
Step 3/4: Investment Analysis ✅ (2.1s)
Step 4/4: Portfolio Execution ✅ (1.5s)

📊 Workflow Complete in 5.6s
```

---

### **Turn 9: Multi-Agent Composite Query - Complex Request** 🎯

**User:**
> "Give me a complete picture: What's the current balance for both accounts ACC001 and ACC002, list all their portfolios with allocations, and provide a compliance summary for each."

**Expected Behavior:**
- Master Agent coordinates multiple sub-agents
- `AccountServices` → Get balances for both accounts
- `PortfolioManager` → List portfolios and allocations
- `ComplianceOfficer` → Compliance summary
- Synthesizes unified response

**What to Highlight:**
- Multiple sub-agent delegations in single request
- Information aggregation across domains
- Coherent consolidated response

---

### **Turn 10: Multi-Modal Request (Optional)** 📄🎤

#### Option A: Image Analysis (Document Review)

**User:** 
Upload an image of a financial statement or investment document, then ask:
> "Please analyze this financial document and summarize the key investment information."

**Expected Behavior:**
- Multi-modal processing activates
- Image is analyzed for text/content
- Agent provides summary and insights

#### Option B: Audio Transcription

**User:**
Upload an audio file with investment instructions, then ask:
> "Transcribe this audio and process any investment requests mentioned."

**Expected Behavior:**
- Audio transcription service activates
- Transcript appears as a transcription message
- Agent processes the extracted instructions

**What to Highlight:**
- Multi-modal message persistence (now saved in thread history!)
- Audio transcription capability
- Image/document understanding
- Seamless integration with other agent capabilities

---

## 🔧 Technical Features Summary

### Sub-Agents Triggered:
- ✅ **AccountServices** - Balance inquiries, account status
- ✅ **InvestmentAdvisor** - Fund search, comparison, recommendations
- ✅ **PortfolioManager** - Portfolio creation, funding, allocation
- ✅ **ComplianceOfficer** - Risk assessment, regulatory compliance
- ✅ **ProfitProjection** - Investment projections with scenarios

### Tools Invoked:
- ✅ `SearchWeb` - Real-time web search
- ✅ `GetAccountBalance` - Account information
- ✅ `SearchFunds` - Fund discovery
- ✅ `GetFundDetails` - Fund information
- ✅ `CompareFunds` - Side-by-side comparison
- ✅ `CalculateProfitProjection` - Projection calculations
- ✅ `CreatePortfolio` - Portfolio creation
- ✅ `FundPortfolio` - Transfer and fund
- ✅ `ListPortfolios` - Portfolio listing
- ✅ `GetPortfolioDetails` - Portfolio information
- ✅ `GetPortfolioAllocation` - Allocation breakdown

### Special Features:
- ✅ **Web Search** - Real-time Bing Search API integration
- ✅ **Workflow Orchestration** - CompleteInvestmentWorkflow
- ✅ **Streaming Responses** - Real-time token streaming
- ✅ **Delegation Events** - Visible sub-agent handoffs
- ✅ **Workflow Progress** - Step-by-step progress tracking
- ✅ **Multi-Modal** - Image and audio processing
- ✅ **Thread Persistence** - Conversation history saved

---

## 📊 Sample Account Data

For reference, here are the test accounts available:

| Account ID | Customer | Balance | Status |
|------------|----------|---------|--------|
| ACC001 | John Smith | $50,000 | Active |
| ACC002 | Sarah Johnson | $100,000 | Active |

### Available Funds:

| Fund ID | Name | Category | Risk | YTD Return |
|---------|------|----------|------|------------|
| FUND001 | Global Technology Growth Fund | Equity | High | 18.5% |
| FUND002 | US Bond Income Fund | Bond | Low | 3.2% |
| FUND003 | Balanced Growth & Income Fund | Balanced | Medium | 10.5% |
| FUND004 | S&P 500 Index Fund | Index | Medium | 15.2% |
| FUND005 | Emerging Markets Equity Fund | International | Very High | 25.8% |
| FUND006 | Healthcare Sector Fund | Sector | High | 14.3% |

### Existing Portfolios:

| Portfolio ID | Account | Name | Strategy |
|--------------|---------|------|----------|
| PORT001 | ACC001 | John's Retirement Fund | Balanced |
| PORT002 | ACC002 | Sarah's Growth Portfolio | Aggressive |

---

## 🎤 Presentation Tips

1. **Start with context**: Explain you're demonstrating an AI-powered investment platform
2. **Highlight tool calls**: Point out the delegation events as they appear
3. **Emphasize real-time**: The streaming responses and workflow progress show real-time processing
4. **Show the threads**: Navigate to conversation history to show persistence
5. **Multi-agent value**: Explain how each specialist focuses on their domain
6. **Enterprise ready**: Mention observability with OpenTelemetry

---

## 🚀 Quick Start Commands

Copy-paste these prompts in order for a complete demo:

```
1. What are the latest trends in the global technology sector and how is the US stock market performing today?

2. Can you check the balance and status of account ACC001?

3. I'm interested in technology and growth investments with moderate to high risk. What funds do you recommend?

4. Compare FUND001 and FUND004 side by side.

5. If I invest $25,000 for 24 months with a moderate risk profile, what returns can I expect? Show me different scenarios.

6. Create a new portfolio called 'Tech Growth Portfolio' with Growth strategy for account ACC001, and fund it with $25,000.

7. Verify that this investment is compliant with regulations and appropriate for the account's risk profile.

8. Run a complete investment workflow for account ACC002 with $15,000 and aggressive risk profile.

9. Give me a complete picture: What's the current balance for both accounts ACC001 and ACC002, list all their portfolios with allocations, and provide a compliance summary for each.
```

---

*Last Updated: December 2024*

# 📊 Profit Projection Workflow - Implementation Plan
## احتساب الأرباح التقديرية

**Priority:** ⭐⭐⭐⭐⭐ (Highest)  
**Estimated Effort:** 3-4 days  
**Business Impact:** 40%+ increase in fund subscription conversion

---

## 🎯 Overview

This workflow calculates estimated investment profits based on user input (amount, time horizon, risk profile) using parallel analysis, scenario building, and personalized recommendations.

### User Story (Arabic Context)
> "يطلب احتساب ارباحه التقديرية في حال استثمر المبلغ الفلاني"
> 
> Customer asks: "If I invest 100,000 SAR for 3 years with moderate risk, what can I expect?"

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    PROFIT PROJECTION WORKFLOW                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   INPUT: ProjectionRequest                                              │
│   {                                                                     │
│     InvestmentAmount: 100000,                                           │
│     Currency: "SAR",                                                    │
│     TimeHorizonMonths: 36,                                              │
│     RiskProfile: "Moderate",                                            │
│     InvestmentType: "LumpSum" | "Monthly",                              │
│     CustomerId: "ACC001" (optional)                                     │
│   }                                                                     │
│                                                                         │
│   ┌───────────────────────────────────────────────────────────────-──┐  │
│   │  STEP 1: CUSTOMER CONTEXT (Conditional)                          │  │
│   │  ────────────────────────────────────────────────────────        │  │
│   │  IF CustomerId provided:                                         │  │
│   │    → Fetch existing holdings                                     │  │
│   │    → Get risk profile from account                               │  │
│   │    → Consider tax situation                                      │  │
│   │  ELSE:                                                           │  │
│   │    → Use provided parameters only                                │  │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               ↓                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  STEP 2: PARALLEL ANALYSIS (Fan-out)                            |   │
│   │  ═══════════════════════════════════════════════════════════════|   │
│   │                                                                 |   │
│   │  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐    │   │
│   │  │  Historical     │ │  Market         │ │  Fund Selection │    │   │
│   │  │  Performance    │ │  Conditions     │ │  Analyzer       │    │   │
│   │  │  Analyzer       │ │  Analyzer       │ │                 │    │   │
│   │  ├─────────────────┤ ├─────────────────┤ ├─────────────────┤    │   │
│   │  │ • 1Y, 3Y, 5Y    │ │ • Bull/Bear     │ │ • Filter by     │    │   │
│   │  │   returns       │ │   indicators    │ │   risk profile  │    │   │
│   │  │ • Volatility    │ │ • Sector trends │ │ • Rank by       │    │   │
│   │  │ • Sharpe Ratio  │ │ • Economic      │ │   performance   │    │   │
│   │  │ • Max Drawdown  │ │   outlook       │ │ • Match to goal │    │   │
│   │  └─────────────────┘ └─────────────────┘ └─────────────────┘    │   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               ↓                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  STEP 3: RESULTS AGGREGATOR (Fan-in)                                │
│   │  ─────────────────────────────────────────────────────────────      │
│   │  Combine all parallel analysis results into unified data model      │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               ↓                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  STEP 4: SCENARIO BUILDER                                           │
│   │  ═══════════════════════════════════════════════════════════════    │
│   │                                                                     │
│   │  ┌─────────────────────────────────────────────────────────────┐│   │
│   │  │  CONSERVATIVE SCENARIO (80% Confidence)                      │   │
│   │  │  • Uses lower quartile of historical returns                ││   │
│   │  │  • Assumes higher volatility                                ││   │
│   │  │  • "Expect at least X even in bad markets"                  ││   │
│   │  └─────────────────────────────────────────────────────────────┘│   │
│   │                                                                 │   │
│   │  ┌─────────────────────────────────────────────────────────────┐│   │
│   │  │  EXPECTED SCENARIO (50% Confidence)                          │   │
│   │  │  • Uses median historical returns                           ││   │
│   │  │  • Adjusts for current market conditions                    ││   │
│   │  │  • "Most likely outcome based on data"                      ││   │
│   │  └─────────────────────────────────────────────────────────────┘│   │
│   │                                                                     │
│   │  ┌─────────────────────────────────────────────────────────────┐│   │
│   │  │  OPTIMISTIC SCENARIO (20% Confidence)                        │   │
│   │  │  • Uses upper quartile returns                              ││   │
│   │  │  • Assumes favorable conditions                             ││   │
│   │  │  • "Best case if markets perform well"                      ││   │
│   │  └─────────────────────────────────────────────────────────────┘│   │
│   │                                                                 │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               ↓                                         │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  STEP 5: RECOMMENDATION ENGINE                                  │   │
│   │  ─────────────────────────────────────────────────────────────  │   │
│   │  • Optimal Fund Mix for Target Return                           │   │
│   │  • Monthly SIP vs Lump Sum Comparison                           │   │
│   │  • Alternative Scenarios (higher/lower risk)                    │   │
│   │  • Action Items & Call-to-Action                                │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                               ↓                                         │
│   OUTPUT: ProjectionResult                                              │
│   {                                                                     │
│     Scenarios: [Conservative, Expected, Optimistic],                    │
│     RecommendedFunds: [...],                                            │
│     Comparison: { LumpSum: X, Monthly: Y },                             │
│     RiskWarnings: [...],                                                │
│     CallToAction: "Subscribe Now" button                                │
│   }                                                                     │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📁 File Structure

```
src/
├── Api/
│   └── Workflows/
│       └── ProfitProjection/
│           ├── ProfitProjectionWorkflow.cs      # Main workflow orchestrator
│           ├── Messages/
│           │   └── ProjectionMessages.cs        # DTOs/Message types
│           └── Executors/
│               ├── CustomerContextExecutor.cs   # Fetch customer data
│               ├── HistoricalAnalyzer.cs        # Historical performance
│               ├── MarketConditionsAnalyzer.cs  # Current market analysis
│               ├── FundSelectionAnalyzer.cs     # Fund matching
│               ├── ResultsAggregator.cs         # Combine parallel results
│               ├── ScenarioBuilder.cs           # Build 3 scenarios
│               └── RecommendationEngine.cs      # Final recommendations
├── Tools/
│   └── ProjectionTools.cs                       # Agent-callable tools
└── Services/
    └── MarketDataService.cs                     # Market data provider
```

---

## 📋 Implementation Tasks

### Phase 1: Message Types (Day 1 - Morning)

**Task 1.1: Create Projection Message Types**

File: `src/Api/Workflows/ProfitProjection/Messages/ProjectionMessages.cs`

```csharp
// Records for workflow messages
- ProjectionRequest      // Input from user
- CustomerContext        // Optional customer data
- HistoricalAnalysis     // Historical performance data
- MarketAnalysis         // Current market conditions
- FundMatch              // Matched funds
- AggregatedAnalysis     // Combined parallel results
- Scenario               // Single scenario (Conservative/Expected/Optimistic)
- ScenarioSet            // All 3 scenarios
- FundRecommendation     // Individual fund recommendation
- ProjectionResult       // Final output
```

**Acceptance Criteria:**
- [ ] All message types defined as records
- [ ] Full XML documentation
- [ ] Arabic field names/descriptions where customer-facing

---

### Phase 2: Analysis Executors (Day 1 - Afternoon)

**Task 2.1: Historical Performance Analyzer**

File: `src/Api/Workflows/ProfitProjection/Executors/HistoricalAnalyzer.cs`

```csharp
// Responsibilities:
- Fetch historical returns for matching funds
- Calculate Sharpe Ratio, Max Drawdown, Volatility
- Return percentile distribution (P10, P25, P50, P75, P90)
```

**Task 2.2: Market Conditions Analyzer**

File: `src/Api/Workflows/ProfitProjection/Executors/MarketConditionsAnalyzer.cs`

```csharp
// Responsibilities:
- Analyze current market sentiment
- Sector performance trends
- Economic indicators (for Saudi market)
- Return adjustment factors
```

**Task 2.3: Fund Selection Analyzer**

File: `src/Api/Workflows/ProfitProjection/Executors/FundSelectionAnalyzer.cs`

```csharp
// Responsibilities:
- Filter funds by risk profile
- Rank by risk-adjusted returns
- Match to investment horizon
- Return top candidates
```

**Acceptance Criteria:**
- [ ] Each executor has OpenTelemetry instrumentation
- [ ] Uses existing InvestmentDataStore
- [ ] Returns strongly-typed results

---

### Phase 3: Core Workflow Logic (Day 2)

**Task 3.1: Customer Context Executor**

File: `src/Api/Workflows/ProfitProjection/Executors/CustomerContextExecutor.cs`

```csharp
// Responsibilities:
- Fetch customer account if ID provided
- Get current holdings
- Extract risk profile
- Handle anonymous projections
```

**Task 3.2: Results Aggregator (Fan-in)**

File: `src/Api/Workflows/ProfitProjection/Executors/ResultsAggregator.cs`

```csharp
// Responsibilities:
- Wait for all parallel analyses
- Combine into unified model
- Validate completeness
```

**Task 3.3: Scenario Builder**

File: `src/Api/Workflows/ProfitProjection/Executors/ScenarioBuilder.cs`

```csharp
// Responsibilities:
- Build Conservative scenario (P25 returns)
- Build Expected scenario (P50 returns)
- Build Optimistic scenario (P75 returns)
- Calculate projected values over time
- Generate confidence intervals
```

**Task 3.4: Recommendation Engine**

File: `src/Api/Workflows/ProfitProjection/Executors/RecommendationEngine.cs`

```csharp
// Responsibilities:
- Generate optimal fund allocation
- Compare Lump Sum vs SIP strategies
- Add risk warnings
- Create call-to-action
```

---

### Phase 4: Workflow Orchestrator (Day 3 - Morning)

**Task 4.1: Main Workflow Class**

File: `src/Api/Workflows/ProfitProjection/ProfitProjectionWorkflow.cs`

```csharp
public class ProfitProjectionWorkflow
{
    // WorkflowBuilder pattern:
    // 1. Start with CustomerContext
    // 2. Fan-out to parallel analyzers
    // 3. Fan-in with Aggregator
    // 4. Sequential: ScenarioBuilder → RecommendationEngine
    // 5. Return ProjectionResult
}
```

**Acceptance Criteria:**
- [ ] Uses Microsoft Agent Framework Workflow patterns
- [ ] Proper Fan-out/Fan-in for parallel analysis
- [ ] Conditional routing for customer context
- [ ] Full observability with events

---

### Phase 5: Agent Integration (Day 3 - Afternoon)

**Task 5.1: Projection Tools for Agent**

File: `src/Tools/ProjectionTools.cs`

```csharp
// Tool methods that agents can invoke:
- CalculateProfitProjection(amount, months, risk)
- GetProjectionForCustomer(customerId, amount, months)
- CompareInvestmentStrategies(amount, lumpSumVsSip)
```

**Task 5.2: Update Investment Advisor SubAgent**

File: `src/Api/SubAgents/InvestmentAdvisorSubAgent.cs`

```csharp
// Add ProjectionTools to agent capabilities
// Update system prompt for projection conversations
```

---

### Phase 6: API & Frontend (Day 4)

**Task 6.1: Projection Controller**

File: `src/Api/Controllers/ProjectionController.cs`

```csharp
// Endpoints:
- POST /api/projection/calculate
- POST /api/projection/customer/{customerId}
- GET  /api/projection/strategies/compare
```

**Task 6.2: SignalR Streaming**

File: `src/Api/Hubs/ProjectionHub.cs`

```csharp
// Real-time updates for long projections
- Progress events during analysis
- Scenario streaming as they complete
- Final result delivery
```

**Task 6.3: Frontend Component (Optional)**

File: `frontend/src/components/ProjectionWidget.tsx`

```typescript
// Interactive projection UI:
- Amount slider/input
- Time horizon selector
- Risk profile dropdown
- Real-time projection chart
- Scenario comparison cards
```

---

## 🧪 Testing Plan

### Unit Tests

```
tests/
└── Workflows/
    └── ProfitProjection/
        ├── HistoricalAnalyzerTests.cs
        ├── ScenarioBuilderTests.cs
        ├── RecommendationEngineTests.cs
        └── WorkflowIntegrationTests.cs
```

**Test Scenarios:**
1. Projection with valid inputs → Success with 3 scenarios
2. Projection with customer ID → Includes existing holdings
3. Projection with invalid amount → Validation error
4. Projection for high-risk profile → Different fund mix
5. Lump Sum vs SIP comparison → Both strategies calculated

---

## 📊 Sample Input/Output

### Sample Request

```json
{
  "investmentAmount": 100000,
  "currency": "SAR",
  "timeHorizonMonths": 36,
  "riskProfile": "Moderate",
  "investmentType": "LumpSum",
  "customerId": "ACC001"
}
```

### Sample Response

```json
{
  "projectionId": "PROJ20251204001",
  "inputSummary": {
    "amount": 100000,
    "currency": "SAR",
    "horizon": "3 years",
    "risk": "Moderate"
  },
  "scenarios": {
    "conservative": {
      "confidence": "80%",
      "projectedValue": 115000,
      "totalReturn": 15000,
      "annualizedReturn": "4.77%",
      "description": "حتى في ظروف السوق الصعبة، من المتوقع أن تحقق هذا العائد على الأقل"
    },
    "expected": {
      "confidence": "50%",
      "projectedValue": 128000,
      "totalReturn": 28000,
      "annualizedReturn": "8.58%",
      "description": "العائد الأكثر احتمالاً بناءً على الأداء التاريخي وظروف السوق"
    },
    "optimistic": {
      "confidence": "20%",
      "projectedValue": 145000,
      "totalReturn": 45000,
      "annualizedReturn": "13.19%",
      "description": "العائد الممكن في حال تحقق أفضل ظروف السوق"
    }
  },
  "recommendedFunds": [
    {
      "fundId": "FUND001",
      "name": "SNB Growth Fund",
      "allocation": 40,
      "expectedContribution": 11200
    },
    {
      "fundId": "FUND002",
      "name": "SNB Balanced Fund",
      "allocation": 35,
      "expectedContribution": 9800
    },
    {
      "fundId": "FUND003",
      "name": "SNB Fixed Income",
      "allocation": 25,
      "expectedContribution": 7000
    }
  ],
  "strategyComparison": {
    "lumpSum": {
      "projectedValue": 128000,
      "benefit": "Immediate market exposure"
    },
    "monthlySIP": {
      "monthlyAmount": 2778,
      "projectedValue": 124000,
      "benefit": "Rupee cost averaging, lower risk"
    },
    "recommendation": "LumpSum recommended for longer horizons"
  },
  "riskWarnings": [
    "Past performance does not guarantee future results",
    "Market conditions may affect actual returns"
  ],
  "callToAction": {
    "primary": "Subscribe Now",
    "link": "/subscribe?projection=PROJ20251204001"
  }
}
```

---

## 🎯 Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Projection Accuracy | < 15% deviation | Compare projections to actual 1Y returns |
| Conversion Rate | 40% increase | Track subscribe clicks after projection |
| User Engagement | 3+ minutes | Time spent on projection page |
| API Response Time | < 3 seconds | P95 latency for full projection |
| Customer Satisfaction | 4.5+ stars | Post-projection survey |

---

## 🚀 Getting Started

### Step 1: Create the folder structure

```bash
mkdir -p src/Api/Workflows/ProfitProjection/Messages
mkdir -p src/Api/Workflows/ProfitProjection/Executors
```

### Step 2: Start with message types

Create `ProjectionMessages.cs` with all DTOs.

### Step 3: Implement executors one by one

Start with `HistoricalAnalyzer.cs` as it depends on existing data.

### Step 4: Wire up the workflow

Create `ProfitProjectionWorkflow.cs` with the full graph.

### Step 5: Add agent tools

Create `ProjectionTools.cs` so agents can invoke the workflow.

### Step 6: Test end-to-end

Use the existing demo scenarios pattern in `Program.cs`.

---

## 📝 Notes

- **Data Source**: Initially use `InvestmentDataStore` with mock historical data
- **Market Data**: Can integrate real APIs later (Tadawul, Bloomberg)
- **Localization**: All customer-facing text should support Arabic
- **Compliance**: Add disclaimer about projections not being guarantees

---

*Implementation Plan Version: 1.0*  
*Created: December 4, 2025*  
*Estimated Completion: 4 days*

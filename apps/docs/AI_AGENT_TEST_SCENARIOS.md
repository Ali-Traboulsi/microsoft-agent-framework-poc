# AI Agent Conversation Test Scenarios

This document provides comprehensive test scenarios for validating the AI Agent's capabilities including:
- Intent Classification Layer
- Structured Context Store
- Chain-of-Thought Reasoning
- Sub-Agent Coordination Protocol

## How to Use

1. Open the frontend at `http://localhost:3000`
2. Copy and paste the test messages into the chat
3. For multi-turn scenarios, wait for each response before sending the next message
4. Observe the agent's behavior against the expected outcomes

---

## 🎯 Intent Classification Tests

### Test 1: Simple Portfolio Query
**Purpose:** Verify basic intent detection for portfolio inquiries

```
User: What is my current portfolio value?
```

**Expected:**
- Intent: `PortfolioAnalysis`
- Agent: `PortfolioManager`
- Should show thinking indicator

---

### Test 2: Investment Recommendation with Parameters
**Purpose:** Verify entity extraction (amount, risk profile)

```
User: I have $50,000 to invest and I'm a moderate risk investor. What do you recommend?
```

**Expected:**
- Intent: `InvestmentAdvice`
- Agents: `InvestmentAdvisor`, `ComplianceOfficer`
- Extracted entities: `investment_amount: 50000`, `risk_profile: moderate`

---

### Test 3: Account Balance Inquiry
**Purpose:** Verify account operations intent

```
User: Check my account balance for account ACC001
```

**Expected:**
- Intent: `AccountBalance`
- Agent: `AccountServices`
- Extracted entity: `account_id: ACC001`

---

### Test 4: Profit Projection Request
**Purpose:** Verify projection intent with time horizon

```
User: Project my portfolio returns over the next 5 years with Monte Carlo simulation
```

**Expected:**
- Intent: `ProfitProjection`
- Agents: `ProfitProjection`, `PortfolioManager`
- Extracted entities: `projection_period: 5 years`, `simulation_type: monte_carlo`

---

### Test 5: Compliance Check
**Purpose:** Verify compliance verification intent

```
User: Is my portfolio compliant with regulatory requirements?
```

**Expected:**
- Intent: `ComplianceCheck`
- Agents: `ComplianceOfficer`, `PortfolioManager`

---

### Test 6: Ambiguous Request
**Purpose:** Test handling of vague requests

```
User: Can you help me with my investments?
```

**Expected:**
- Intent: `GeneralInquiry`
- Should show thinking/clarification
- May ask for more details

---

## 🔄 Multi-Turn Context Tests

### Test 7: Portfolio Deep Dive (3 turns)
**Purpose:** Test context retention across portfolio questions

```
Turn 1: Show me my portfolio holdings
[Wait for response]

Turn 2: What percentage is in technology stocks?
[Wait for response]

Turn 3: Should I rebalance based on current market conditions?
```

**Expected:**
- Turn 2 should reference holdings from Turn 1
- Turn 3 should consider both previous answers
- Context should flow naturally

---

### Test 8: Customer Data Lookup Flow (3 turns)
**Purpose:** Test CIF-based lookup with follow-ups

```
Turn 1: Look up customer with CIF 123456789
[Wait for response]

Turn 2: What is their risk profile?
[Wait for response]

Turn 3: What investments would you recommend for them?
```

**Expected:**
- "them" in Turn 3 should resolve to the customer from Turn 1
- Risk profile should inform recommendations

---

### Test 9: Pronoun Resolution (3 turns)
**Purpose:** Test "it" and "its" resolution

```
Turn 1: Tell me about the SNB Capital Equity Fund
[Wait for response]

Turn 2: What is its expense ratio?
[Wait for response]

Turn 3: Compare it with similar funds
```

**Expected:**
- "its" and "it" should correctly reference the fund
- No confusion about what's being discussed

---

### Test 10: Topic Switching
**Purpose:** Test context management when switching topics

```
Turn 1: Show my portfolio value
[Wait for response]

Turn 2: Actually, first check my account balance
[Wait for response]

Turn 3: Now back to the portfolio - what's my allocation?
```

**Expected:**
- Should handle the topic switch gracefully
- Should remember portfolio context from Turn 1

---

## 🤝 Multi-Agent Coordination Tests

### Test 11: Complete Investment Workflow
**Purpose:** Test full agent coordination pipeline

```
User: I want to invest $100,000 in mutual funds. I'm a conservative investor. 
Please verify my account, check compliance, get recommendations, and create a portfolio.
```

**Expected:**
- Should invoke: `AccountServices` → `ComplianceOfficer` → `InvestmentAdvisor` → `PortfolioManager`
- Should show coordination progress
- Should synthesize unified response
- Facts discovered: account_status, compliance_status, recommendations, portfolio_id

---

### Test 12: Risk Assessment (Multi-Perspective)
**Purpose:** Test coordination for risk evaluation

```
User: Assess the risk of adding high-yield bonds to my portfolio
```

**Expected:**
- Agents: `InvestmentAdvisor`, `ComplianceOfficer`, `PortfolioManager`
- May show conflicting viewpoints
- Should synthesize balanced recommendation

---

### Test 13: Portfolio with Projection
**Purpose:** Test composite analysis request

```
User: Analyze my current portfolio and project its growth for the next 3 years
```

**Expected:**
- Agents: `PortfolioManager`, `ProfitProjection`
- Should show sequential coordination
- Portfolio data should feed into projection

---

### Test 14: Investment Decision Pipeline
**Purpose:** Test fact sharing between agents

```
User: Should I sell my tech stocks and buy bonds? Consider my risk profile and current market conditions.
```

**Expected:**
- Multiple agents should share facts
- Facts discovered: current_holdings, risk_profile, market_conditions
- Should show reasoning process

---

## ⚔️ Conflict Resolution Tests

### Test 15: Advisor vs Compliance Conflict
**Purpose:** Test when recommendation conflicts with limits

```
User: I want to put all my money into a single high-risk cryptocurrency fund
```

**Expected:**
- `InvestmentAdvisor` may acknowledge the request
- `ComplianceOfficer` should flag concentration risk
- Should resolve with balanced response explaining limits

---

### Test 16: Strategy Conflict
**Purpose:** Test handling of conflicting strategies

```
User: My advisor told me to be aggressive but my profile says conservative. What should I do?
```

**Expected:**
- Should acknowledge the conflict
- Should provide thoughtful reasoning
- May ask clarifying questions

---

## 🧠 Chain-of-Thought Reasoning Tests

### Test 17: Complex Retirement Planning
**Purpose:** Test visible reasoning for complex decisions

```
User: I'm 45 years old, have $500k saved, want to retire at 60 with $80k/year income. How should I invest?
```

**Expected:**
- Should show thinking process
- Should extract: age=45, savings=500000, retirement_age=60, income_goal=80000
- Should calculate required growth rate
- Should provide reasoned recommendation

---

### Test 18: Market Analysis Reasoning
**Purpose:** Test reasoning about market conditions

```
User: Given current interest rates and inflation, should I move to bonds or stay in equities?
```

**Expected:**
- Should show analytical reasoning
- Should consider multiple factors
- Should explain trade-offs

---

### Test 19: Step-by-Step Calculation
**Purpose:** Test visible calculation reasoning

```
User: If I invest $10,000 monthly for 20 years at 7% return, how much will I have?
```

**Expected:**
- Should show calculation steps
- Formula: FV = P × ((1+r)^n - 1) / r
- Should arrive at approximately $5.2M

---

## 🔧 Edge Case Tests

### Test 20: Empty Input
**Purpose:** Test validation of empty requests

```
User: [Just spaces or empty]
```

**Expected:**
- Should handle gracefully
- May ask for clarification

---

### Test 21: Off-Topic Request
**Purpose:** Test handling of non-financial questions

```
User: What's the weather like today?
```

**Expected:**
- Should politely redirect to financial topics
- Should not invoke specialized agents

---

### Test 22: Very Long Input
**Purpose:** Test handling of lengthy messages

```
User: I need help with my portfolio. [Repeat "This is a very detailed request." 50 times] What should I do?
```

**Expected:**
- Should process without crashing
- Should extract the core intent
- May summarize the lengthy content

---

### Test 23: Multiple Questions in One
**Purpose:** Test compound request handling

```
User: What is my portfolio value? Also check my account balance. And what funds do you recommend? Plus show me a 5-year projection.
```

**Expected:**
- Should identify multiple intents
- Should coordinate multiple agents
- Should provide comprehensive response

---

### Test 24: Arabic Language Input
**Purpose:** Test multilingual support

```
User: ما هي قيمة محفظتي الاستثمارية؟
```

**Expected:**
- Should understand Arabic
- Intent: Portfolio inquiry
- May respond in Arabic or English

---

### Test 25: Invalid Account ID
**Purpose:** Test error handling

```
User: Show portfolio for account XYZ999999
```

**Expected:**
- Should attempt lookup
- Should gracefully report "account not found"
- Should not crash

---

## ⚡ Stress Tests

### Test 26: Rapid Fire Questions
**Purpose:** Test system under rapid requests

Send these quickly (within 1-2 seconds):
```
Message 1: Portfolio value?
Message 2: Account balance?
Message 3: Fund recommendations?
```

**Expected:**
- Should handle all three
- Should maintain context
- Should not get confused

---

### Test 27: Deep Conversation (10+ turns)
**Purpose:** Test context retention over long conversations

```
Turn 1: Show my portfolio
Turn 2: What's the largest holding?
Turn 3: How has it performed?
Turn 4: Should I keep it?
Turn 5: What would you replace it with?
Turn 6: Is that compliant?
Turn 7: Project the impact over 3 years
Turn 8: Compare with current allocation
Turn 9: Make the change
Turn 10: Confirm the new portfolio
```

**Expected:**
- Should maintain context through all 10 turns
- Each turn should build on previous
- Should correctly track the proposed changes

---

## 📋 Quick Test Checklist

| Test | Category | Status |
|------|----------|--------|
| Simple Portfolio Query | Intent | ⬜ |
| Investment Recommendation | Intent | ⬜ |
| Account Balance | Intent | ⬜ |
| Profit Projection | Intent | ⬜ |
| Portfolio Deep Dive | Context | ⬜ |
| Customer Lookup Flow | Context | ⬜ |
| Complete Workflow | Coordination | ⬜ |
| Risk Assessment | Coordination | ⬜ |
| Conflict Resolution | Conflict | ⬜ |
| Retirement Planning | CoT | ⬜ |
| Empty Input | Edge | ⬜ |
| Arabic Input | Edge | ⬜ |
| Deep Conversation | Stress | ⬜ |

---

## Notes

- Enable "Show Thinking" mode to see the agent's reasoning
- Use the Test Scenario Panel component for automated testing
- Check browser console for any errors during testing
- The CoordinationHub at `/hubs/coordination` provides real-time coordination events

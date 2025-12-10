# Conversation Context Awareness Test Scenarios

This document contains test scenarios to verify that the AI agent maintains context awareness across multiple turns in a conversation.

## Test Setup

1. Start the backend server
2. Open the frontend or use the API directly
3. **Important**: Use the same `conversationId` for all messages in a scenario

---

## Scenario 1: Fund Recommendation → Subscription Chain

### Purpose
Test that the agent remembers fund recommendations and can act on them in subsequent turns.

### Conversation Flow

**Turn 1 - User:**
```
Recommend mutual funds for a moderate risk investor with $50,000 to invest
```

**Expected Response:**
Agent should provide fund recommendations with details.

**Turn 2 - User:**
```
Based on your recommendation, subscribe me to the best performing one with $20,000
```

**Expected Behavior:**
- Agent should understand "your recommendation" refers to Turn 1
- Agent should identify the "best performing" fund from the previous list
- Agent should execute subscription for $20,000

**Turn 3 - User:**
```
Now show me the details of the subscription I just made
```

**Expected Behavior:**
- Agent should understand "subscription I just made" refers to Turn 2
- Agent should display the subscription details

---

## Scenario 2: Account → Portfolio → Fund-In Chain

### Purpose
Test context tracking across account, portfolio, and transaction operations.

### Conversation Flow

**Turn 1 - User:**
```
Show me the details for account ACC001
```

**Expected Response:**
Agent should show account details including balance, type, etc.

**Turn 2 - User:**
```
Create a growth portfolio for this account with the name "My Growth Portfolio"
```

**Expected Behavior:**
- Agent should understand "this account" refers to ACC001 from Turn 1
- Agent should create the portfolio linked to that account

**Turn 3 - User:**
```
Fund in $10,000 to the portfolio I just created
```

**Expected Behavior:**
- Agent should understand "portfolio I just created" refers to Turn 2
- Agent should execute fund-in operation for $10,000

**Turn 4 - User:**
```
What's the current balance of that portfolio now?
```

**Expected Behavior:**
- Agent should understand "that portfolio" is the one from Turn 2
- Agent should show updated balance after the fund-in

---

## Scenario 3: Compliance Check → Decision Chain

### Purpose
Test that compliance decisions are remembered and referenced.

### Conversation Flow

**Turn 1 - User:**
```
Run a compliance check on account ACC002 for a $100,000 investment in high-risk funds
```

**Expected Response:**
Agent should perform compliance check and provide recommendations/warnings.

**Turn 2 - User:**
```
Based on the compliance check, what's the maximum amount I can safely invest?
```

**Expected Behavior:**
- Agent should reference the compliance findings from Turn 1
- Agent should provide a recommended maximum based on that analysis

**Turn 3 - User:**
```
Okay, proceed with investing that recommended amount in the SNB Saudi Equity Fund
```

**Expected Behavior:**
- Agent should understand "that recommended amount" from Turn 2
- Agent should execute the investment

---

## Scenario 4: Projection → Investment Chain

### Purpose
Test that profit projections inform subsequent investment decisions.

### Conversation Flow

**Turn 1 - User:**
```
Project the returns for investing $30,000 in Shariah-compliant funds over 5 years
```

**Expected Response:**
Agent should provide projection with expected returns.

**Turn 2 - User:**
```
Which fund from the projection would give me the highest returns?
```

**Expected Behavior:**
- Agent should reference the projection data from Turn 1
- Agent should identify the highest return fund

**Turn 3 - User:**
```
Subscribe to that fund with my full $30,000 investment
```

**Expected Behavior:**
- Agent should understand "that fund" refers to Turn 2's identified fund
- Agent should execute subscription for $30,000

---

## Scenario 5: Complex Multi-Agent Chain

### Purpose
Test context tracking across multiple sub-agents in a single conversation.

### Conversation Flow

**Turn 1 - User:**
```
I have $75,000 to invest. What are my options?
```

**Expected Response:**
Agent should provide investment options overview.

**Turn 2 - User:**
```
I'm interested in the Shariah-compliant options. Tell me more.
```

**Expected Behavior:**
- Agent should understand this is about the options from Turn 1
- Agent should filter to Shariah-compliant funds

**Turn 3 - User:**
```
Run a compliance check for investing my full amount in those funds
```

**Expected Behavior:**
- Agent should understand "full amount" = $75,000 from Turn 1
- Agent should understand "those funds" = Shariah-compliant from Turn 2

**Turn 4 - User:**
```
If compliance passes, split my investment 60/40 between the top two recommended funds
```

**Expected Behavior:**
- Agent should reference compliance from Turn 3
- Agent should calculate 60% of $75,000 = $45,000 and 40% = $30,000
- Agent should identify "top two" from Turn 2 recommendations
- Agent should execute two subscriptions

**Turn 5 - User:**
```
Show me a summary of everything we did today
```

**Expected Behavior:**
- Agent should summarize all operations from Turns 1-4
- Should include: initial inquiry, fund selection, compliance, subscriptions

---

## API Testing with cURL

### Setup
```bash
# Set your conversation ID (use the same for all turns in a scenario)
CONV_ID="test-context-$(date +%s)"
```

### Turn 1
```bash
curl -X POST http://localhost:5000/api/unified/stream \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "'$CONV_ID'",
    "message": "Recommend mutual funds for a moderate risk investor with $50,000 to invest",
    "enableThinking": true
  }'
```

### Turn 2
```bash
curl -X POST http://localhost:5000/api/unified/stream \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "'$CONV_ID'",
    "message": "Based on your recommendation, subscribe me to the best performing one with $20,000",
    "enableThinking": true
  }'
```

### Turn 3
```bash
curl -X POST http://localhost:5000/api/unified/stream \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "'$CONV_ID'",
    "message": "Now show me the details of the subscription I just made",
    "enableThinking": true
  }'
```

---

## Verification Checklist

For each scenario, verify:

- [ ] Agent correctly references information from previous turns
- [ ] Pronouns like "it", "that", "those" resolve correctly
- [ ] Amounts mentioned in earlier turns are remembered
- [ ] Operations performed are tracked and can be referenced
- [ ] No explicit re-stating of context is required by the user

---

## Debugging Tips

### Check Conversation Memory
Look at the database to verify memory is being stored:
```sql
SELECT * FROM ConversationMemoryEntries 
WHERE ConversationId = 'your-conversation-id' 
ORDER BY SequenceNumber;
```

### Check Context Injection
Enable debug logging to see what context is being injected:
```
[DEBUG] Generated briefing for MasterAgent in conversation xxx with N history turns
```

### Check Briefing Content
The briefing injected into the AI should show:
```
=== CONVERSATION HISTORY ===
[Turn 1] User asked: Recommend mutual funds...
[Turn 1] MasterAgent responded: Here are the recommended funds...
[Turn 1] Operations: recommendation
=== END HISTORY ===
```

---

## Expected Log Output

When context is working correctly, you should see logs like:
```
info: MasterOrchestrator[0]
      Persisted conversation turn for {ConversationId}: {Agent} handled request
info: DatabaseConversationContextStore[0]
      Generated briefing for MasterAgent in conversation {ConversationId} with 3 history turns
```

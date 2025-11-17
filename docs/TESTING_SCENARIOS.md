# Chat Agent Testing Scenarios

This document provides comprehensive test scenarios for all four investment banking agents. Use these scenarios to validate functionality, streaming responses, and markdown rendering.

---

## 🚀 Getting Started

### Prerequisites
1. Set your OpenAI API key in `.env` file
2. Start the backend: `dotnet run` (runs on http://localhost:5000)
3. Start the frontend: `cd frontend && npm run dev` (runs on http://localhost:5173)
4. Open browser to http://localhost:5173
5. Ensure "Enable Streaming" is checked for best experience

---

## 📊 Investment Advisor Agent

The Investment Advisor provides market insights, investment recommendations, and financial analysis.

### Test Scenario 1: General Investment Advice
**Input:**
```
I'm a 35-year-old professional with $100,000 to invest. I have a moderate risk tolerance and a 20-year investment horizon. What would you recommend?
```

**Expected Response:**
- Personalized portfolio allocation strategy
- Mix of stocks, bonds, and other assets
- Reasoning based on age and risk profile
- Potential markdown formatting with lists or tables

### Test Scenario 2: Market Analysis Request
**Input:**
```
Can you provide an analysis of the current market conditions and how they might affect my investment strategy?
```

**Expected Response:**
- Market overview with key trends
- Impact on different asset classes
- Strategic recommendations
- May include bullet points or formatted sections

### Test Scenario 3: Specific Investment Query
**Input:**
```
What mutual funds do you recommend for long-term growth? Show me a comparison table.
```

**Expected Response:**
- List of available mutual funds from the system
- Comparison with key metrics
- May include markdown table with fund details
- Investment thesis for each recommendation

### Test Scenario 4: Risk Assessment
**Input:**
```
I want to invest aggressively but I'm worried about market volatility. How should I balance risk and return?
```

**Expected Response:**
- Discussion of risk-return tradeoff
- Diversification strategies
- Practical recommendations
- May include formatted lists or emphasis on key points

### Test Scenario 5: Code Example Request
**Input:**
```
Can you show me a Python example of how to calculate compound interest for my investment?
```

**Expected Response:**
- Python code block with syntax highlighting
- Explanation of the calculation
- Example usage with numbers
- Properly formatted markdown code block

---

## 💼 Portfolio Manager Agent

The Portfolio Manager handles portfolio creation, analysis, and rebalancing.

### Test Scenario 1: View All Portfolios
**Input:**
```
Show me all the portfolios in the system with their current values and performance.
```

**Expected Response:**
- List of all portfolios with details
- Account associations
- Current values and returns
- May be formatted as a table or structured list

### Test Scenario 2: Portfolio Analysis
**Input:**
```
Analyze the portfolio for account ACC001. What's the performance and allocation breakdown?
```

**Expected Response:**
- Portfolio composition details
- Performance metrics (returns, allocation percentages)
- Asset breakdown
- May include markdown table or formatted sections

### Test Scenario 3: Create New Portfolio
**Input:**
```
Create a new portfolio for account ACC002 called "Retirement Growth Fund" with an initial value of $150,000.
```

**Expected Response:**
- Confirmation of portfolio creation
- Portfolio ID and details
- Next steps or recommendations
- Successfully calls the CreatePortfolio tool

### Test Scenario 4: Portfolio Rebalancing
**Input:**
```
My portfolio PF001 has drifted from its target allocation. Can you help me rebalance it?
```

**Expected Response:**
- Current allocation analysis
- Recommended trades to rebalance
- Expected outcome after rebalancing
- May include before/after comparison table

### Test Scenario 5: Performance Comparison
**Input:**
```
Compare the performance of all portfolios and tell me which one is performing best. Show it in a table format.
```

**Expected Response:**
- Comparative analysis with metrics
- Markdown table with portfolio names, returns, values
- Winner identification with reasoning
- May include formatted emphasis on best performer

### Test Scenario 6: Multi-Step Portfolio Query
**Input:**
```
First, show me all portfolios. Then analyze the one with the highest value in detail.
```

**Expected Response:**
- Demonstrates multi-tool usage
- Lists all portfolios first
- Follows up with detailed analysis
- Shows agent's reasoning capability

---

## 🏦 Account Services Agent

The Account Services Agent manages client accounts, transactions, and account information.

### Test Scenario 1: View All Accounts
**Input:**
```
Show me all client accounts in the system with their balances and status.
```

**Expected Response:**
- List of all accounts
- Account numbers, names, balances, status
- May be formatted as a table
- Includes account type information

### Test Scenario 2: Account Details Query
**Input:**
```
Get me detailed information about account ACC001 including transaction history.
```

**Expected Response:**
- Complete account details
- Balance and account type
- Transaction history with dates and amounts
- Formatted with clear sections

### Test Scenario 3: Create New Account
**Input:**
```
Create a new Investment account for John Smith with an initial balance of $50,000.
```

**Expected Response:**
- Confirmation of account creation
- New account number
- Account details summary
- Successfully calls CreateAccount tool

### Test Scenario 4: Transaction History Analysis
**Input:**
```
Show me the transaction history for account ACC002 and analyze the spending patterns.
```

**Expected Response:**
- Transaction list with details
- Pattern analysis (deposits vs. withdrawals)
- Insights or observations
- May include formatted lists or tables

### Test Scenario 5: Account Status Check
**Input:**
```
Check the status of all accounts and tell me if any are suspended or closed.
```

**Expected Response:**
- Status summary for all accounts
- Identification of non-active accounts
- Reasons or next steps if applicable
- Clear categorization by status

### Test Scenario 6: Balance Summary
**Input:**
```
What's the total balance across all accounts? Show me a breakdown by account type.
```

**Expected Response:**
- Aggregate calculations
- Breakdown by account type (Investment, Retirement, etc.)
- Individual account contributions
- May include markdown table or formatted summary

---

## ⚖️ Compliance Officer Agent

The Compliance Officer ensures regulatory compliance and risk management.

### Test Scenario 1: General Compliance Check
**Input:**
```
Run a compliance check on all accounts and portfolios. Are there any issues I should be aware of?
```

**Expected Response:**
- Comprehensive compliance review
- Identification of any violations or risks
- Recommendations for remediation
- May include checklist format with task lists

### Test Scenario 2: Transaction Compliance
**Input:**
```
Check if the last transaction on account ACC001 complies with trading regulations.
```

**Expected Response:**
- Transaction details review
- Compliance assessment
- Regulatory requirements mentioned
- Pass/fail determination with reasoning

### Test Scenario 3: Portfolio Risk Assessment
**Input:**
```
Assess the risk level of portfolio PF002. Does it comply with the client's risk profile?
```

**Expected Response:**
- Risk analysis of portfolio holdings
- Comparison with client risk tolerance
- Compliance determination
- Recommendations if misaligned

### Test Scenario 4: Regulatory Requirements
**Input:**
```
What are the key compliance requirements for high-net-worth clients investing in mutual funds?
```

**Expected Response:**
- List of regulatory requirements
- Know Your Customer (KYC) requirements
- Suitability standards
- Documentation needs
- May be formatted as checklist or numbered list

### Test Scenario 5: Compliance Report Request
**Input:**
```
Generate a compliance report for account ACC003 including all risk factors and regulatory status. Use markdown formatting with sections.
```

**Expected Response:**
- Structured compliance report
- Multiple sections (Risk Assessment, Regulatory Status, Recommendations)
- Markdown headings and formatting
- Comprehensive coverage of compliance aspects

### Test Scenario 6: Pre-Trade Compliance
**Input:**
```
I want to purchase $200,000 of high-risk mutual funds for account ACC001. What compliance checks do I need to complete first? Show as a checklist.
```

**Expected Response:**
- Pre-trade compliance checklist
- Markdown task list format [ ] 
- Each requirement explained
- Approval workflow steps
- Risk warnings if applicable

---

## 🧪 Cross-Agent Integration Tests

These scenarios test how well agents work together and handle complex, multi-faceted queries.

### Integration Test 1: Complete Client Onboarding
**Steps:**
1. **Account Services**: "Create a new account for Sarah Johnson with $200,000"
2. **Portfolio Manager**: "Create a balanced portfolio for Sarah's new account"
3. **Investment Advisor**: "Recommend mutual funds for Sarah's portfolio based on moderate risk"
4. **Compliance Officer**: "Run a compliance check on Sarah's new setup"

**Expected Flow:**
- Seamless information flow between agents
- Consistent account/portfolio references
- Each agent accesses correct data
- No errors or data mismatches

### Integration Test 2: Portfolio Review Workflow
**Steps:**
1. **Portfolio Manager**: "Show me portfolio PF001's current allocation"
2. **Investment Advisor**: "Is this allocation optimal given current market conditions?"
3. **Compliance Officer**: "Does this portfolio meet regulatory requirements?"
4. **Portfolio Manager**: "Rebalance if needed based on the recommendations"

**Expected Flow:**
- Each agent builds on previous context
- Coordinated analysis and recommendations
- Actionable outcomes

### Integration Test 3: Problem Investigation
**Steps:**
1. **Account Services**: "Show me all suspended accounts"
2. **Compliance Officer**: "Why were these accounts suspended? What violations occurred?"
3. **Investment Advisor**: "What should these clients do to reactivate their accounts?"

**Expected Flow:**
- Problem identification
- Root cause analysis
- Resolution recommendations

---

## 🎨 Markdown Rendering Tests

Test the markdown capabilities across different formatting types.

### Test 1: Code Blocks
**Input to any agent:**
```
Show me a TypeScript example of calculating portfolio returns with proper error handling.
```

**Expected:**
- TypeScript code block with syntax highlighting
- Proper indentation preserved
- Comments included
- Code markers (```) properly rendered

### Test 2: Tables
**Input to Portfolio Manager:**
```
Show me all portfolios in a comparison table with columns: Portfolio ID, Account, Value, Return %, Risk Level.
```

**Expected:**
- Markdown table with headers and borders
- All columns properly aligned
- Data correctly formatted
- Table responsive in UI

### Test 3: Lists and Checklists
**Input to Compliance Officer:**
```
Create a compliance checklist for opening a new investment account. Use task list format.
```

**Expected:**
- Task list with [ ] checkboxes
- Multiple levels of nesting if applicable
- Clear task descriptions
- Properly rendered checkboxes in UI

### Test 4: Mixed Formatting
**Input to Investment Advisor:**
```
Write a comprehensive investment guide that includes:
- Bold headings for sections
- Bullet points for key concepts
- A code example in Python
- A table comparing investment options
- Blockquotes for important warnings
```

**Expected:**
- All markdown elements rendered correctly
- Proper hierarchy with headings
- Code syntax highlighting works
- Table displays properly
- Blockquotes styled differently
- Bold/italic text formatted correctly

### Test 5: Links and Emphasis
**Input to any agent:**
```
Explain the concept of dollar-cost averaging. Include links to resources and emphasize the key benefits.
```

**Expected:**
- Links render as clickable and open in new tab
- Bold and italic text properly formatted
- Clear visual hierarchy

---

## 🐛 Error Handling Tests

### Test 1: Invalid Account Reference
**Input to Account Services:**
```
Show me details for account INVALID123
```

**Expected:**
- Graceful error message
- Explanation that account doesn't exist
- Suggestion to list available accounts

### Test 2: Invalid Tool Parameters
**Input to Portfolio Manager:**
```
Create a portfolio with a negative value of -$5000
```

**Expected:**
- Validation error caught
- Clear explanation of the issue
- Correct parameter requirements stated

### Test 3: Backend Disconnection (with Streaming)
**Steps:**
1. Enable streaming
2. Start sending a message
3. Stop the backend server mid-response
4. Check error handling

**Expected:**
- Connection error banner appears
- Partial response preserved
- Clear error message
- Instructions to restart backend

### Test 4: Empty or Unclear Query
**Input to any agent:**
```
um... help?
```

**Expected:**
- Agent asks clarifying questions
- Provides examples of what it can do
- Friendly, helpful tone
- Doesn't crash or return generic error

---

## 🔄 Streaming Behavior Tests

### Test 1: Enable/Disable Streaming
**Steps:**
1. Send message with streaming enabled
2. Observe token-by-token rendering
3. Uncheck "Enable Streaming"
4. Send same message
5. Observe full response at once

**Expected:**
- Clear difference in rendering behavior
- No errors when switching modes
- Both modes return correct responses

### Test 2: Long Response Streaming
**Input to Investment Advisor:**
```
Write a detailed 1000-word investment strategy guide covering portfolio allocation, risk management, diversification, rebalancing, tax efficiency, and long-term growth strategies. Include code examples and tables.
```

**Expected:**
- Smooth token-by-token rendering
- No lag or stuttering
- Markdown renders correctly as it streams
- Final message complete and well-formatted

### Test 3: Rapid Messages
**Steps:**
1. Send a message
2. Immediately send another before first completes
3. Check if queueing works correctly

**Expected:**
- First message completes
- Second message processes after
- No message loss or mixing
- UI handles queuing gracefully

---

## 📱 Per-Agent Tab Tests

### Test 1: Tab Isolation
**Steps:**
1. Send message to Investment Advisor
2. Switch to Portfolio Manager tab
3. Send different message
4. Switch back to Investment Advisor

**Expected:**
- Each agent maintains separate chat history
- Message counts update correctly
- No message mixing between agents
- Unread indicators work properly

### Test 2: Message Count Badges
**Steps:**
1. Send messages to 3 different agents
2. Observe message count badges
3. Switch between tabs

**Expected:**
- Badges show correct message counts
- Numbers update in real-time
- Visual distinction for active vs inactive tabs

### Test 3: Multiple Agent Conversations
**Steps:**
1. Have ongoing conversation with Investment Advisor (5+ messages)
2. Switch to Compliance Officer and start new conversation
3. Switch to Account Services for another conversation
4. Return to Investment Advisor

**Expected:**
- All conversations preserved
- Correct context maintained per agent
- No performance degradation
- Smooth tab switching

---

## 📊 Test Results Tracking

Use this table to track your test results:

| Test Category | Scenario | Status | Notes |
|--------------|----------|--------|-------|
| Investment Advisor | General Advice | ⬜ | |
| Investment Advisor | Market Analysis | ⬜ | |
| Investment Advisor | Code Example | ⬜ | |
| Portfolio Manager | View Portfolios | ⬜ | |
| Portfolio Manager | Create Portfolio | ⬜ | |
| Portfolio Manager | Rebalancing | ⬜ | |
| Account Services | View Accounts | ⬜ | |
| Account Services | Create Account | ⬜ | |
| Account Services | Transactions | ⬜ | |
| Compliance Officer | General Check | ⬜ | |
| Compliance Officer | Risk Assessment | ⬜ | |
| Compliance Officer | Report Generation | ⬜ | |
| Markdown | Code Blocks | ⬜ | |
| Markdown | Tables | ⬜ | |
| Markdown | Task Lists | ⬜ | |
| Streaming | Enable/Disable | ⬜ | |
| Streaming | Long Response | ⬜ | |
| Error Handling | Invalid Reference | ⬜ | |
| Tab Management | Isolation | ⬜ | |
| Tab Management | Message Counts | ⬜ | |

**Status Legend:**
- ⬜ Not Tested
- ✅ Passed
- ❌ Failed
- ⚠️ Partial/Issues

---

## 🎯 Success Criteria

A successful test session should demonstrate:

1. **Functionality**
   - ✅ All agents respond correctly to queries
   - ✅ Tools are called appropriately
   - ✅ Data is retrieved and displayed accurately

2. **Markdown Rendering**
   - ✅ Code blocks have syntax highlighting
   - ✅ Tables render properly with borders
   - ✅ Lists and formatting work correctly
   - ✅ Links open in new tabs

3. **Streaming**
   - ✅ Token-by-token rendering is smooth
   - ✅ Can toggle streaming on/off
   - ✅ Markdown renders correctly during streaming

4. **Tab Management**
   - ✅ Each agent has isolated chat history
   - ✅ Message counts are accurate
   - ✅ Switching tabs is instant
   - ✅ No context mixing between agents

5. **Error Handling**
   - ✅ Graceful degradation on errors
   - ✅ Clear error messages
   - ✅ Recovery instructions provided
   - ✅ No application crashes

6. **Performance**
   - ✅ Responses arrive quickly
   - ✅ UI remains responsive
   - ✅ No memory leaks over extended use
   - ✅ Handles multiple conversations smoothly

---

## 🔧 Troubleshooting

### Issue: Agents not responding
**Solution:**
- Check backend is running on port 5000
- Verify OpenAI API key is set in `.env`
- Check browser console for errors
- Try disabling streaming

### Issue: Markdown not rendering
**Solution:**
- Ensure `npm install` was run in frontend
- Check that `react-markdown` and plugins are installed
- Verify `highlight.js` CSS is imported in `index.css`
- Clear browser cache and reload

### Issue: SignalR connection fails
**Solution:**
- Check connection error banner message
- Ensure backend is running
- Verify CORS settings in `Program.cs`
- Try disabling browser extensions
- Check browser console for WebSocket errors

### Issue: Chat history mixing between agents
**Solution:**
- This shouldn't happen - file a bug report
- Clear local storage and reload
- Check Zustand store configuration

---

## 📝 Notes

- Test with streaming **enabled** for best experience
- Some agents may take 5-10 seconds for complex queries
- Longer responses benefit most from streaming
- Each agent has access to the same data store
- Tool calls happen behind the scenes automatically

Happy Testing! 🚀

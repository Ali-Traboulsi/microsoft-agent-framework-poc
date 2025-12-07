# Projection Testing Scenarios

This document contains test scenarios to verify the streaming progress indicators, projection charts, and **smart defaults** are working correctly.

## Prerequisites

1. **Start the Backend**: 
   ```bash
   cd apps/backend
   dotnet run
   ```

2. **Start the Frontend**:
   ```bash
   cd apps/frontend
   pnpm dev
   ```

3. **Open the UI**: Navigate to `http://localhost:3000`

---

## Smart Defaults Behavior

The agent now uses intelligent defaults when information is missing:

| Missing Info | Default Value |
|-------------|---------------|
| Investment Amount | 100,000 SAR |
| Time Horizon | 36 months (3 years) |
| Risk Profile | Moderate |
| Currency | SAR |
| Shariah Compliance | false |

**The agent will NEVER ask clarifying questions - it will proceed with defaults and mention assumptions.**

---

## Test Scenarios

### Scenario 1: Minimal Input (Smart Defaults)

**Message:**
```
I want to invest
```

**Expected Behavior:**
1. ✅ Agent applies ALL defaults (100k SAR, 36 months, Moderate)
2. ✅ Streaming progress card appears showing 7 steps
3. ✅ Projection result card appears with charts
4. ✅ Response mentions: "Using defaults: 100,000 SAR, 3 years, Moderate risk"

---

### Scenario 2: Partial Input - Amount Only

**Message:**
```
invest 50k
```

**Expected Behavior:**
- Agent parses 50k → 50,000 SAR
- Uses defaults for time (36 months) and risk (Moderate)
- Shows streaming progress + projection charts

---

### Scenario 3: Partial Input - Amount and Time

**Message:**
```
what if I put in 200k for 5 years
```

**Expected Behavior:**
- Agent parses: 200,000 SAR, 60 months
- Uses default risk: Moderate
- Progress streaming + charts

---

### Scenario 4: Risk Keyword Only

**Message:**
```
safe investment
```

**Expected Behavior:**
- Agent interprets "safe" → Conservative risk
- Uses defaults: 100k SAR, 36 months
- Shows Conservative scenario highlighted

---

### Scenario 5: Casual/Vague Request

**Message:**
```
show me some returns
```

**Expected Behavior:**
- Agent understands intent = profit projection
- Uses ALL defaults
- Proceeds immediately with calculation

---

### Scenario 6: Very Casual Request

**Message:**
```
how do I make money here
```

**Expected Behavior:**
- Agent interprets as investment interest
- Uses ALL defaults
- Shows projection with explanation

---

### Scenario 7: Arabic Request

**Message:**
```
أريد استثمار مبلغ
```

**Expected Behavior:**
- Agent understands Arabic = investment request
- Uses defaults
- Response in Arabic (or bilingual)
- Progress shows Arabic step names

---

### Scenario 8: Amount with K notation

**Message:**
```
invest 250K for 2 years aggressive
```

**Expected Behavior:**
- Parses: 250,000 SAR, 24 months, Aggressive risk
- No defaults needed - all specified
- Shows higher projected returns with risk warnings

---

### Scenario 9: Shariah-Compliant Request

**Message:**
```
halal investment options
```

**Expected Behavior:**
- Agent sets Shariah-compliant = true
- Uses defaults for amount, time, risk
- Only shows Shariah-compliant funds

---

### Scenario 10: Full Specification (No Defaults)

**Message:**
```
I want to invest 500,000 SAR for 5 years with conservative risk, shariah compliant
```

**Expected Behavior:**
- All values extracted from message
- No defaults applied
- Progress streaming + full projection charts

---

## Verification Checklist

### Progress Streaming
- [ ] Progress card appears immediately when processing starts
- [ ] Steps animate/pulse when in progress
- [ ] Checkmarks appear when steps complete
- [ ] Duration shown for completed steps
- [ ] Progress bar fills as steps complete
- [ ] Arabic names display correctly when applicable

### Projection Results
- [ ] Three scenario cards (Conservative, Expected, Optimistic)
- [ ] Interactive chart with monthly projections
- [ ] Fund recommendation table with allocations
- [ ] Risk warnings section
- [ ] Call-to-action buttons
- [ ] Proper currency formatting
- [ ] Percentages and returns clearly displayed

### Error Handling
- [ ] Invalid amount shows appropriate error
- [ ] Missing parameters prompts for information
- [ ] Network errors handled gracefully

---

## Troubleshooting

### Progress Not Showing
1. Check browser console for SignalR connection status
2. Verify `DelegationEventMiddleware.CurrentConversationId` is set
3. Confirm `CalculateProfitProjectionWithProgress` tool is being called

### Charts Not Rendering
1. Check for `projectionResult` in streaming Complete response
2. Verify `ProjectionResultCard` receives data
3. Check browser console for chart rendering errors

### Streaming Disconnects
1. Check SignalR hub connection status
2. Look for timeout issues
3. Verify CORS configuration

---

## Console Debug Commands

Open browser DevTools (F12) and check:

```javascript
// Check SignalR connection
console.log('SignalR connected:', window.signalRConnection?.state);

// Watch for streaming events
// Should see chunks with type: StepStart, StepComplete, Content, Complete
```

---

## Backend Logging

Enable verbose logging to see workflow execution:

```bash
# In appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "AgentFrameworkQuickStart": "Debug"
    }
  }
}
```

Look for logs like:
- `Starting streaming projection for conversation...`
- `Step X started/completed`
- `Streaming projection completed: PROJ...`

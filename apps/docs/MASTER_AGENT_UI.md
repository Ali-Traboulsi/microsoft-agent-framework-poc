# Master Agent Frontend Implementation

## Overview

The frontend now features a **Master Agent Chat Interface** that provides full visibility into the AI's decision-making process, similar to Microsoft Copilot's thinking bubbles.

## Features

### 🤖 Master Agent Orchestrator
- **Single unified interface** that coordinates multiple specialized sub-agents
- Real-time visibility into agent thinking and delegation
- Telemetry dashboard showing performance metrics

### 📊 Thinking Process Visualization

The UI displays different message types with distinct styling:

| Type | Icon | Description | Color |
|------|------|-------------|-------|
| **Thinking** | 🤔 | Master agent analyzing request | Purple |
| **Delegation** | 🔄 | Routing to sub-agent | Blue |
| **Tool Execution** | 🔧 | Executing function/tool | Green |
| **Agent Response** | 💬 | Final response content | Gray |
| **Telemetry** | 📊 | Performance metrics | Gray (monospace) |

### 🎯 Telemetry Dashboard

Real-time metrics displayed at the top:
- **Duration**: Total request processing time (ms)
- **Delegations**: Number of sub-agent delegations
- **Sub-Agents**: Which agents were involved
- **Tools**: Number of tool executions

### 🔗 Architecture

```
User Input
    ↓
Master Agent (OpenAI GPT-4o-mini)
    ↓
    ├─→ 🤔 Thinking (analyzed in UI)
    ├─→ 🔄 Delegation → PortfolioManager
    ├─→ 🔄 Delegation → InvestmentAdvisor  
    ├─→ 🔄 Delegation → AccountServices
    └─→ 💬 Synthesized Response
    
Frontend displays each step in real-time via SignalR streaming
```

## Components

### `MasterAgentChat.tsx`
Main chat interface component with:
- Message rendering for all types
- Telemetry dashboard
- Real-time streaming support
- Error handling and connection status

### `services/masterAgent.ts`
SignalR service for Master Agent Hub:
- WebSocket connection management
- Async generator for streaming responses
- Queue-based iteration for real-time updates

## Usage

### 1. Start Backend
```bash
cd src
dotnet run
```

### 2. Start Frontend
```bash
cd frontend
npm run dev
```

### 3. Navigate to Master Agent Tab
- Open http://localhost:5173
- Click on "🤖 Master Agent" tab
- Start chatting!

## Example Interactions

### Simple Query
```
User: "Show my account balance"
↓
🤔 Analyzing your request...
🔄 Delegating to AccountServices...
✅ AccountServices completed
💬 Your current balance is $50,000.00 USD
📊 Duration: 2.3s | Delegations: 1 | Sub-Agents: AccountServices
```

### Complex Multi-Agent Query
```
User: "Show my portfolio and recommend technology funds"
↓
🤔 I need to coordinate multiple agents for this request...
🔄 Delegating to PortfolioManager...
✅ PortfolioManager completed
🔄 Delegating to InvestmentAdvisor...
✅ InvestmentAdvisor completed
💬 [Synthesized response with portfolio details and fund recommendations]
📊 Duration: 8.5s | Delegations: 2 | Sub-Agents: PortfolioManager, InvestmentAdvisor
```

## Features to Test

1. **Single Domain Requests**
   - "What's my account balance?"
   - "Show my portfolios"
   - "Find technology funds"

2. **Multi-Domain Requests**
   - "Create a portfolio and add some funds"
   - "Show my portfolio and check my account balance"

3. **Complex Orchestration**
   - "I want to invest $10,000 in a diversified portfolio with tech and healthcare funds"

4. **Telemetry Visibility**
   - Toggle telemetry on/off with checkbox
   - View duration, delegation count, and sub-agents used
   - Trace IDs for correlating with backend logs

## OpenTelemetry Integration

The frontend telemetry correlates with backend traces:

**Frontend shows:**
- Duration: 8.5s
- Delegations: 2
- Sub-Agents: PortfolioManager, InvestmentAdvisor

**Backend logs show:**
- Full distributed trace with TraceId
- Activity spans for each delegation
- OpenAI API call latencies
- Tool execution timing

## Configuration

### Show/Hide Telemetry
Toggle the "Show Telemetry" checkbox in the header to:
- Show: Display metrics dashboard + telemetry messages
- Hide: Clean chat interface without technical details

### SignalR Connection
The service automatically connects to `http://localhost:5000/hubs/master`

Update the URL in `services/masterAgent.ts` if needed:
```typescript
.withUrl('http://your-backend:5000/hubs/master', {
  skipNegotiation: true,
  transport: signalR.HttpTransportType.WebSockets,
})
```

## Next Steps

### Backend Enhancements Needed
1. **Explicit Delegation Events**
   - Emit `ResponseType.SubAgentDelegation` before calling sub-agent
   - Emit `ResponseType.SubAgentComplete` after sub-agent finishes

2. **Tool Execution Events**
   - Detect when tools are called
   - Emit `ResponseType.ToolExecution` with tool name

3. **Enhanced Metadata**
   - Add TraceId to response metadata
   - Include sub-agent names in delegation events
   - Add tool names to execution events

### Frontend Enhancements
1. **Collapsible Thinking Blocks**
   - Allow users to collapse/expand thinking process
   - Default to collapsed for cleaner UI

2. **Delegation Timeline**
   - Visual timeline showing sequence of delegations
   - Duration bars for each sub-agent

3. **Real-time Trace Viewer**
   - Click trace ID to view full OpenTelemetry trace
   - Integration with Application Insights (if configured)

## Troubleshooting

### Connection Errors
- Ensure backend is running on http://localhost:5000
- Check CORS configuration in `Program.cs`
- Verify SignalR hub is registered at `/hubs/master`

### No Thinking Messages
- Backend needs to include 🤔 emoji in thinking blocks
- Check that agent instructions prompt explicit reasoning

### Missing Telemetry
- Verify `showTelemetry` is enabled
- Check that responses include `metadata` field
- Ensure OpenTelemetry is configured in backend

## Benefits

✅ **Transparency**: See exactly what the Master Agent is doing  
✅ **Debugging**: Identify which sub-agent caused issues  
✅ **Performance**: Monitor delegation timing and optimize  
✅ **Trust**: Users understand AI decision-making process  
✅ **Learning**: Developers see orchestration patterns in action

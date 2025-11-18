# Testing Guide - Master Agent with Observability

This guide shows you how to test the Master Agent system and verify OpenTelemetry observability is working.

## Prerequisites

### 1. Set OpenAI API Key

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
```

**Windows (Command Prompt):**
```cmd
set OPENAI_API_KEY=sk-your-key-here
```

**macOS/Linux:**
```bash
export OPENAI_API_KEY="sk-your-key-here"
```

**Or use appsettings.json:**
```json
{
  "OpenAI": {
    "ApiKey": "sk-your-key-here"
  }
}
```

### 2. Start the Application

```bash
cd src
dotnet run
```

You should see:
```
=== Banking Investment Agent API - Multi-Agent System ===
API is running on: http://localhost:5000
Swagger UI: http://localhost:5000/swagger

ℹ️  OpenTelemetry configured with console exporter for 'InvestmentBankingAgents' v2.0.0
```

## Testing Methods

---

## Method 1: Swagger UI (Recommended for Quick Testing)

### Access Swagger
Open browser: **http://localhost:5000/swagger**

### Test Master Agent - Non-Streaming

1. Expand `POST /api/v2/masteragent/chat`
2. Click **"Try it out"**
3. Enter request body:
```json
{
  "message": "What mutual funds are available?",
  "conversationId": "test-123"
}
```
4. Click **Execute**
5. Check **Response** and **Console Output** for OpenTelemetry traces

### Example Requests

**Portfolio Query:**
```json
{
  "message": "Create a portfolio for me with a balanced allocation",
  "conversationId": "test-portfolio-1"
}
```

**Multi-Agent Coordination:**
```json
{
  "message": "Check my account balance, then create a portfolio and recommend mutual funds",
  "conversationId": "test-multi-1"
}
```

**Thinking Process Test:**
```json
{
  "message": "Analyze the best investment strategy for retirement savings",
  "conversationId": "test-thinking-1"
}
```

---

## Method 2: Postman / cURL

### Setup Postman Collection

**Base URL:** `http://localhost:5000`

### Request 1: Chat with Master Agent
  
**Method:** `POST`  
**URL:** `http://localhost:5000/api/v2/masteragent/chat`  
**Headers:**
```
Content-Type: application/json
```
**Body (raw JSON):**
```json
{
  "message": "Show me all available mutual funds",
  "conversationId": "postman-test-1"
}
```

### Request 2: Get Available Sub-Agents

**Method:** `GET`  
**URL:** `http://localhost:5000/api/v2/masteragent/agents`

Expected response:
```json
{
  "success": true,
  "data": [
    {
      "name": "PortfolioManager",
      "domain": "Portfolio Management",
      "capabilities": [...]
    },
    ...
  ]
}
```

### cURL Examples

**Test Master Agent:**
```bash
curl -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What investment options do I have?",
    "conversationId": "curl-test-1"
  }'
```

**Get Sub-Agents:**
```bash
curl http://localhost:5000/api/v2/masteragent/agents
```

---

## Method 3: SignalR Hub Testing (Real-time Streaming)

### Using Browser Console

1. Open browser: `http://localhost:5000`
2. Open Developer Tools (F12) → Console
3. Paste this JavaScript:

```javascript
// Load SignalR library from CDN
const script = document.createElement('script');
script.src = 'https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js';
script.onload = async () => {
    // Connect to Master Agent Hub
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5000/hubs/master")
        .withAutomaticReconnect()
        .build();

    try {
        await connection.start();
        console.log("✅ Connected to Master Agent Hub");

        // Stream responses
        connection.stream("ChatWithMasterAgent", 
            "Analyze my portfolio and suggest improvements", 
            "signalr-test-1"
        ).subscribe({
            next: (chunk) => {
                console.log(`[${chunk.type}]`, chunk.content);
            },
            error: (err) => {
                console.error("Stream error:", err);
            },
            complete: () => {
                console.log("✅ Stream completed");
            }
        });
    } catch (err) {
        console.error("Connection error:", err);
    }
};
document.head.appendChild(script);
```

---

## Method 4: .NET Test Client

Create a simple test file: `TestClient.cs`

```csharp
using System.Net.Http.Json;

var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

var request = new
{
    message = "What are the best mutual funds for growth?",
    conversationId = "test-client-1"
};

var response = await client.PostAsJsonAsync("/api/v2/masteragent/chat", request);
var result = await response.Content.ReadAsStringAsync();

Console.WriteLine(result);
```

Run:
```bash
dotnet script TestClient.cs
```

---

## Verifying Observability

### Console Output - What to Look For

When you make a request, you should see OpenTelemetry output in the console:

#### 1. Activity (Trace) Output
```
Activity.TraceId:            8d0c5e6f7a1b2c3d4e5f6a7b8c9d0e1f
Activity.SpanId:             1a2b3c4d5e6f7a8b
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       0a1b2c3d4e5f6a7b
Activity.ActivitySourceName: InvestmentBanking.MasterOrchestrator
Activity.DisplayName:        MasterOrchestrator.DelegateToSubAgent
Activity.Kind:               Internal
Activity.StartTime:          2025-11-17T10:30:45.1234567Z
Activity.Duration:           00:00:02.3456789
Activity.Tags:
    subagent.name: PortfolioManager
    request.length: 45
    response.tools_used: 2
    response.duration_ms: 2345
Status:
    StatusCode : Ok
```

#### 2. Metrics Output
```
Export orchestrator.delegations, Meter: InvestmentBanking.MasterOrchestrator/2.0.0
(2025-11-17T10:30:47.0000000Z, 2025-11-17T10:30:48.0000000Z] 
  subagent: PortfolioManager 
  success: true 
  LongSum Value: 1

Export orchestrator.delegation.duration, Meter: InvestmentBanking.MasterOrchestrator/2.0.0
(2025-11-17T10:30:47.0000000Z, 2025-11-17T10:30:48.0000000Z] 
  subagent: PortfolioManager 
  Histogram Value: Sum: 2345.00 Count: 1 Min: 2345.00 Max: 2345.00
```

#### 3. ASP.NET Core Traces
```
Activity.ActivitySourceName: Microsoft.AspNetCore
Activity.DisplayName:        POST /api/v2/masteragent/chat
Activity.Tags:
    http.request_id: 0HMVQJ8QKQQ9K:00000001
    http.method: POST
    http.scheme: http
    http.target: /api/v2/masteragent/chat
    http.url: http://localhost:5000/api/v2/masteragent/chat
    http.response.status_code: 200
```

### Key Observability Indicators

✅ **Traces Working:**
- You see `Activity.ActivitySourceName: InvestmentBanking.MasterOrchestrator`
- Activities have `TraceId`, `SpanId`, and `Duration`
- Status codes show `Ok` or `Error`

✅ **Metrics Working:**
- You see `Export orchestrator.delegations`
- You see `Export orchestrator.delegation.duration`
- Counters show incremented values
- Histograms show Sum/Count/Min/Max

✅ **Instrumentation Working:**
- ASP.NET Core traces show HTTP requests
- Tags contain useful metadata (subagent name, conversation ID, etc.)
- Parent-child span relationships are visible

---

## Test Scenarios

### Scenario 1: Single Sub-Agent Delegation

**Request:**
```json
{
  "message": "Show me my portfolio allocation",
  "conversationId": "scenario-1"
}
```

**Expected Observability:**
- 1 trace for `MasterOrchestrator.ProcessRequest`
- 1 trace for `MasterOrchestrator.DelegateToSubAgent`
- 1 trace for `PortfolioManagerSubAgent.HandleRequest`
- Metric: `orchestrator.delegations` = 1 (PortfolioManager)
- Metric: `subagent.requests` = 1 (PortfolioManager)

### Scenario 2: Multi-Agent Coordination

**Request:**
```json
{
  "message": "Check my balance, create a portfolio, and recommend funds",
  "conversationId": "scenario-2"
}
```

**Expected Observability:**
- 1 trace for `MasterOrchestrator.DelegateToMultipleSubAgents`
- 3 child traces for individual delegations
- Metric: `orchestrator.delegations` = 3
- Different sub-agents tagged in metrics

### Scenario 3: Error Handling

**Request:**
```json
{
  "message": "Delegate to NonExistentAgent",
  "conversationId": "scenario-3"
}
```

**Expected Observability:**
- Activity status: `Error`
- Error message in tags
- Metric: `orchestrator.delegation.errors` = 1 (error: not_found)

### Scenario 4: Streaming Response

Use SignalR to stream and watch for:
- `ResponseType.Thinking` chunks (Master Agent reasoning)
- `ResponseType.Content` chunks (actual response)
- `ResponseType.Complete` at end
- Console shows streaming activity with chunk counts

---

## Postman Collection (Import Ready)

Save as `MasterAgent-Tests.postman_collection.json`:

```json
{
  "info": {
    "name": "Master Agent API Tests",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Chat - Simple Query",
      "request": {
        "method": "POST",
        "header": [{"key": "Content-Type", "value": "application/json"}],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"message\": \"What mutual funds are available?\",\n  \"conversationId\": \"test-1\"\n}"
        },
        "url": {
          "raw": "http://localhost:5000/api/v2/masteragent/chat",
          "protocol": "http",
          "host": ["localhost"],
          "port": "5000",
          "path": ["api", "v2", "masteragent", "chat"]
        }
      }
    },
    {
      "name": "Chat - Portfolio Creation",
      "request": {
        "method": "POST",
        "header": [{"key": "Content-Type", "value": "application/json"}],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"message\": \"Create a balanced portfolio for retirement\",\n  \"conversationId\": \"test-2\"\n}"
        },
        "url": {
          "raw": "http://localhost:5000/api/v2/masteragent/chat",
          "protocol": "http",
          "host": ["localhost"],
          "port": "5000",
          "path": ["api", "v2", "masteragent", "chat"]
        }
      }
    },
    {
      "name": "Chat - Multi-Agent Request",
      "request": {
        "method": "POST",
        "header": [{"key": "Content-Type", "value": "application/json"}],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"message\": \"Check balance, create portfolio, and suggest funds\",\n  \"conversationId\": \"test-3\"\n}"
        },
        "url": {
          "raw": "http://localhost:5000/api/v2/masteragent/chat",
          "protocol": "http",
          "host": ["localhost"],
          "port": "5000",
          "path": ["api", "v2", "masteragent", "chat"]
        }
      }
    },
    {
      "name": "Get Available Sub-Agents",
      "request": {
        "method": "GET",
        "header": [],
        "url": {
          "raw": "http://localhost:5000/api/v2/masteragent/agents",
          "protocol": "http",
          "host": ["localhost"],
          "port": "5000",
          "path": ["api", "v2", "masteragent", "agents"]
        }
      }
    }
  ]
}
```

---

## Troubleshooting

### No OpenTelemetry Output

**Check:**
1. Console exporter is configured in `Program.cs`
2. Application is running in Development mode
3. Logging level allows OpenTelemetry output

**Fix:**
```csharp
// In Program.cs, ensure console exporter is added
.WithTracing(tracing => tracing
    .AddConsoleExporter())
.WithMetrics(metrics => metrics
    .AddConsoleExporter())
```

### Traces Missing

**Check:**
1. ActivitySource names match between instrumentation and configuration
2. Activities are properly disposed (use `using` statement)
3. Activity is started before operations

### Metrics Not Showing

**Check:**
1. Meter names match in configuration
2. Metrics are actually recorded (Counter.Add(), Histogram.Record())
3. Metric export interval (default 60 seconds)

**Force immediate export for testing:**
```csharp
.AddConsoleExporter(options =>
{
    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000; // 5 seconds
})
```

---

## Next Steps

1. **Start with Swagger UI** - Easiest way to test
2. **Watch Console Output** - See observability in real-time
3. **Try Different Scenarios** - Test single/multi-agent coordination
4. **Import Postman Collection** - For automated testing
5. **Setup Azure Monitor** - For production observability

## Quick Start Command

```bash
# Set API key (replace with your key)
export OPENAI_API_KEY="sk-your-key-here"

# Run application
cd src && dotnet run

# In another terminal/browser, test with:
curl -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What funds are available?", "conversationId": "test-1"}'

# Watch the first terminal for OpenTelemetry traces and metrics!
```

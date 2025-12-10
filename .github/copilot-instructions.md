# Agent Framework Quick Start - Copilot Instructions

## Project Overview

Multi-agent banking investment platform built on **Microsoft Agent Framework** (Semantic Kernel). Demonstrates master/sub-agent orchestration, streaming responses via SignalR, and comprehensive OpenTelemetry observability.

**Tech Stack**: .NET 9.0 (backend), React + TypeScript (frontend), SignalR, OpenAI/Azure OpenAI

## Architecture Patterns

### Agent System Architecture

The system follows a **Master-Sub-Agent** pattern with three layers:

1. **MasterOrchestrator** (`src/Api/Orchestration/MasterOrchestrator.cs`) - Routes requests to specialized sub-agents and provides web search capability
2. **Sub-Agents** (`src/Api/SubAgents/`) - Domain specialists (Portfolio Manager, Investment Advisor, Account Services, Compliance Officer)
3. **Tools** (`src/Tools/`) - Concrete operations that agents invoke (wrapped with `AIFunctionFactory.Create()`)

**Key Pattern**: Each sub-agent implements `ISubAgent` interface with `Name`, `Domain`, `Capabilities`, and `HandleRequestAsync()`. All agents are created using `_chatClient.CreateAIAgent()` from Microsoft.Agents.AI.

**Web Search Integration**: MasterOrchestrator includes `WebSearchTools` for real-time web information retrieval using Bing Search API.

### Dependency Injection

- **InvestmentDataStore**: Singleton (in-memory shared state)
- **Tools, Agents, Orchestrators**: Scoped (per-request isolation)
- **IChatClient**: Scoped with OpenTelemetry middleware wrapped

```csharp
// Pattern for registering new sub-agents
builder.Services.AddScoped<ISubAgent, YourNewSubAgent>();
```

### Workflows

**Sequential Workflows** (`src/Api/Workflows/CompleteInvestmentWorkflow.cs`) execute agents in order:
```
AccountServices → ComplianceOfficer → InvestmentAdvisor → PortfolioManager
```

**Concurrent Workflows** run agents in parallel using `Task.WhenAll()` for analysis tasks.

## Key Development Workflows

### Building & Running

```bash
# Backend (API + demo scenarios)
cd src
dotnet run
# Requires: OPENAI_API_KEY environment variable

# Full stack (API + React frontend)
./scripts/start.sh  # Linux/Mac
./scripts/start.bat # Windows
```

**Frontend**: Vite dev server on port 3000, SignalR connects to `http://localhost:5000/hubs/agent`

### Adding New Sub-Agents

1. Create class in `src/Api/SubAgents/` implementing `ISubAgent`
2. Define OpenTelemetry `ActivitySource` and `Meter` (see existing agents)
3. Register tools in agent constructor using `AIFunctionFactory.Create()`
4. Register in DI: `builder.Services.AddScoped<ISubAgent, NewAgent>()`
5. MasterOrchestrator auto-discovers all registered `ISubAgent` instances

### Adding New Tools

Tools are **instance methods** on tool classes (not static). Decorate with:

```csharp
[Description("What this tool does")]
public string ToolName(
    [Description("Parameter purpose")] string param)
{
    // Implementation
}
```

Register tools: `AIFunctionFactory.Create(_toolInstance.MethodName)`

### Adding Web Search Tools

The `WebSearchTools` class provides Bing Search API integration:

```csharp
[Description("Search the web for current information")]
public async Task<string> SearchWeb(
    [Description("The search query")] string query,
    [Description("Number of results (default: 5, max: 10)")] int count = 5)
```

**Configuration**: Set `BING_SEARCH_API_KEY` environment variable or configure in `appsettings.json`:
```json
{
  "BingSearch": {
    "ApiKey": "your-bing-search-api-key"
  }
}
```

## SignalR Streaming Pattern

**Backend**: Use `ChannelReader<T>` for streaming responses

```csharp
public ChannelReader<SubAgentStreamChunk> ChatStream(string message, string agentName)
{
    var channel = Channel.CreateUnbounded<SubAgentStreamChunk>();
    // Write chunks to channel.Writer
    // Complete with channel.Writer.Complete()
    return channel.Reader;
}
```

**Frontend**: Async generator pattern in `frontend/src/services/signalr.ts`:

```typescript
async *chatStream(message: string, agentName: string): AsyncGenerator<StreamingMessage>
```

## Observability

All agents and orchestrators use **OpenTelemetry** (traces + metrics):

- **ActivitySource**: `new ActivitySource("InvestmentBanking.ComponentName", "2.0.0")`
- **Meter**: `new Meter("InvestmentBanking.ComponentName", "2.0.0")`
- **Pattern**: Start activity with `ActivitySource.StartActivity()`, add tags, dispose on completion

Built-in framework traces: `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`

## Project Conventions

- **Error Handling**: Tools return error strings (e.g., `"Error: Account not found"`), not exceptions
- **Tool Responses**: Always return `string` - agents format output for users
- **Agent Instructions**: Define in `CreateAgent()` with explicit domain boundaries and response format guidelines
- **Data Storage**: `InvestmentDataStore` is in-memory - no persistence (demo purposes)
- **ID Generation**: Use timestamp-based IDs: `$"PORT{DateTime.Now.Ticks}"`

## Testing

Run demo scenarios: `dotnet run` (executes 9 predefined scenarios in `Program.cs`)

**Postman Collection**: `docs/MasterAgent-Tests.postman_collection.json` for API testing

## Common Integration Points

- **Controllers** → **MasterOrchestrator** → **Sub-Agents** → **Tools** → **InvestmentDataStore**
- **SignalR Hubs** (`src/Api/Hubs/`) stream responses directly to frontend
- **Middleware** (`src/Api/Middleware/DelegationEventMiddleware.cs`) intercepts function calls for observability

## Critical Files

- `src/Program.cs` - DI setup, OpenTelemetry config, demo execution
- `src/Api/Orchestration/MasterOrchestrator.cs` - Master agent delegation logic
- `src/Services/InvestmentDataStore.cs` - Shared data (seeded with sample accounts/funds)
- `frontend/src/services/signalr.ts` - SignalR streaming client
- `docs/OBSERVABILITY.md` - Detailed monitoring guide

## Notes

- **OpenAI key required** - set `OPENAI_API_KEY` env var or edit Program.cs
- **Frontend expects backend on port 5000** - both must run together
- **Agent Framework** is Microsoft's Semantic Kernel with additional abstractions
- **database:** migrations should be present and up to date
- Do not create files with over 500 lines of code. Split into multiple files as needed
- use primary constructor syntax for classes where possible

## Migrations 

To add a new migration, use the following command in the terminal:

```bash
export DOTNET_ROLL_FORWARD=LatestMajor && dotnet ef migrations add <MigrationName> --project ./apps/backend/src/Api --startup-project ./apps/backend/src/Api
```
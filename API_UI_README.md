# Banking Investment Agent System - API & UI

Complete full-stack implementation of Microsoft Agent Framework with REST API and modern React UI.

## 🏗️ Architecture

### Backend (ASP.NET Core Web API)
- **Controllers**: REST endpoints for Accounts, Portfolios, Funds, and Agent chat
- **SignalR Hub**: Real-time streaming for agent responses
- **Agent Service**: Factory for creating and managing AI agents
- **DTOs**: Clean data transfer objects for API contracts

### Frontend (React + TypeScript)
- **Chat Interface**: Real-time agent conversations with streaming support
- **Portfolio View**: Interactive portfolio management dashboard
- **Funds View**: Mutual fund browser with detailed information
- **Accounts View**: Customer account overview
- **State Management**: Zustand for global state
- **API Client**: Axios for REST calls, SignalR for streaming

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- Node.js 18+ and npm
- OpenAI API Key

### 1. Set OpenAI API Key

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY="your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set OPENAI_API_KEY=your-api-key-here
```

**Linux/Mac:**
```bash
export OPENAI_API_KEY="your-api-key-here"
```

### 2. Start the API Server

```bash
cd "c:\Users\Ali Traboulsi\Desktop\work\ICC DMCC\repos\AgentFrameworkQuickStart"
dotnet run
```

The API will start on `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- SignalR Hub: `http://localhost:5000/hubs/agent`

### 3. Start the Frontend (separate terminal)

```bash
cd "c:\Users\Ali Traboulsi\Desktop\work\ICC DMCC\repos\AgentFrameworkQuickStart\frontend"
npm install
npm run dev
```

The UI will start on `http://localhost:3000`

## 📡 API Endpoints

### Accounts
- `GET /api/accounts` - Get all accounts
- `GET /api/accounts/{id}` - Get account by ID
- `GET /api/accounts/{id}/balance` - Get balance via agent
- `POST /api/accounts/{id}/deposit` - Deposit funds
- `GET /api/accounts/{id}/transactions` - Get transaction history

### Portfolios
- `GET /api/portfolios` - Get all portfolios
- `GET /api/portfolios?accountId={id}` - Get portfolios by account
- `GET /api/portfolios/{id}` - Get portfolio by ID
- `POST /api/portfolios` - Create new portfolio
- `GET /api/portfolios/{id}/details` - Get details via agent
- `GET /api/portfolios/{id}/allocation` - Get allocation analysis
- `POST /api/portfolios/{id}/invest` - Invest in fund

### Funds
- `GET /api/funds` - Get all mutual funds
- `GET /api/funds/{id}` - Get fund by ID
- `POST /api/funds/search` - Search funds with criteria
- `POST /api/funds/compare` - Compare multiple funds
- `GET /api/funds/{id}/details` - Get details via agent

### Agents
- `GET /api/agents` - Get available agents
- `POST /api/agents/chat` - Chat with agent (non-streaming)

### SignalR Hub
- Hub URL: `/hubs/agent`
- Method: `ChatStream(message, agentName)` - Streaming chat

## 🤖 Available Agents

1. **InvestmentAdvisor** - Fund recommendations and analysis
2. **PortfolioManager** - Portfolio creation and management
3. **AccountServices** - Account operations and transactions
4. **ComplianceOfficer** - Regulatory compliance checks

## 💻 Frontend Features

### Chat Interface
- Select from 4 specialized agents
- Real-time streaming responses via SignalR
- Fallback to REST API (toggle streaming on/off)
- Message history with timestamps

### Portfolio Dashboard
- Grid view of all portfolios
- Portfolio selection for detailed view
- Holdings breakdown with gain/loss
- Total value and performance metrics

### Funds Browser
- Card-based fund display
- Risk level indicators
- Performance metrics (1Y, 3Y, 5Y returns)
- Detailed fund information panel

### Accounts Overview
- Customer account cards
- Balance display
- Account status indicators
- Transaction history access

## 🔧 Configuration

### Backend Configuration (`appsettings.json`)
```json
{
  "OpenAI": {
    "ApiKey": "your-key-here"
  }
}
```

### Frontend Configuration (`vite.config.ts`)
```typescript
server: {
  proxy: {
    '/api': 'http://localhost:5000',
    '/hubs': { target: 'http://localhost:5000', ws: true }
  }
}
```

## 📦 Project Structure

```
AgentFrameworkQuickStart/
├── Api/
│   ├── AgentService.cs          # Agent factory & management
│   ├── Controllers/
│   │   ├── AccountsController.cs
│   │   ├── PortfoliosController.cs
│   │   ├── FundsController.cs
│   │   └── AgentsController.cs
│   ├── DTOs/
│   │   └── ApiModels.cs         # Request/response models
│   └── Hubs/
│       └── AgentHub.cs          # SignalR streaming
├── Models/                       # Domain models
├── Services/
│   └── InvestmentDataStore.cs   # In-memory data store
├── Tools/                        # Agent tool classes
├── Program.cs                    # API startup
├── ConsoleDemo.cs               # Original console demo
└── frontend/
    ├── src/
    │   ├── components/
    │   │   ├── ChatInterface.tsx
    │   │   ├── PortfolioView.tsx
    │   │   ├── FundsView.tsx
    │   │   └── AccountsView.tsx
    │   ├── services/
    │   │   ├── api.ts           # REST API client
    │   │   └── signalr.ts       # SignalR service
    │   ├── store/
    │   │   └── store.ts         # Zustand state management
    │   ├── App.tsx              # Main app component
    │   └── main.tsx             # Entry point
    ├── package.json
    └── vite.config.ts
```

## 🧪 Testing

### Test API with Swagger
1. Navigate to `http://localhost:5000/swagger`
2. Expand endpoint sections
3. Click "Try it out"
4. Enter parameters and execute

### Test SignalR Streaming
Use the frontend chat interface with "Enable Streaming" checkbox

### Test Individual Agents
```bash
curl -X POST http://localhost:5000/api/agents/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Show me all mutual funds", "agentName": "InvestmentAdvisor"}'
```

## 🔐 CORS Configuration

The API allows requests from:
- `http://localhost:3000` (Vite dev server)
- `http://localhost:5173` (Alternative Vite port)

Update `Program.cs` to add more origins if needed.

## 🛠️ Troubleshooting

### API won't start
- Verify OPENAI_API_KEY is set
- Check port 5000 is not in use
- Run `dotnet restore` and `dotnet build`

### Frontend won't connect
- Ensure API is running first
- Check proxy settings in `vite.config.ts`
- Verify CORS configuration

### SignalR not streaming
- Enable streaming checkbox in UI
- Check browser console for connection errors
- Verify `/hubs/agent` endpoint is accessible

## 📚 Next Steps

1. **Add Authentication**: Implement JWT tokens or OAuth
2. **Persist Data**: Replace in-memory store with database
3. **Charts**: Add visualization with Chart.js or Recharts
4. **Testing**: Add unit/integration tests
5. **Docker**: Containerize both API and frontend
6. **CI/CD**: Set up deployment pipelines

## 📄 License

MIT License - See LICENSE file for details

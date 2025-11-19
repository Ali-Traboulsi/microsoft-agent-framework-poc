# Structured JSON Response Feature

## Overview

The Master Agent now supports **structured JSON responses** instead of just natural language text. This allows client applications to easily parse and use the data programmatically.

## New Endpoint

```
POST /api/v2/MasterAgent/chat/structured
```

**Request:**
```json
{
  "message": "Show me all portfolios for John Doe",
  "conversationId": "optional-conversation-id"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "success": true,
    "structuredResponse": {
      "summary": "Found 2 portfolios for John Doe with total value $125,000",
      "referenceLinks": [],
      "portfolios": [
        {
          "portfolioId": "PORT123",
          "portfolioName": "Growth Portfolio",
          "totalValue": 75000.00,
          "holdings": [
            {
              "fundSymbol": "VFIAX",
              "fundName": "Vanguard 500 Index Fund",
              "shares": 100,
              "currentValue": 50000,
              "percentOfPortfolio": 66.7
            }
          ],
          "performance": {
            "totalReturn": 5000,
            "returnPercentage": 7.1,
            "period": "YTD"
          }
        }
      ],
      "accounts": null,
      "fundRecommendations": null,
      "complianceAlerts": null,
      "keyMetrics": {
        "totalPortfolioValue": 125000,
        "portfolioCount": 2
      },
      "suggestedActions": [
        "Review portfolio allocation",
        "Consider rebalancing quarterly"
      ],
      "subAgentsUsed": ["PortfolioManager"],
      "webSearchesExecuted": null
    },
    "subAgentsUsed": ["PortfolioManager"],
    "durationMs": 3450,
    "errorMessage": null
  },
  "error": null
}
```

## Response Schema

### StructuredAgentResponse

| Field | Type | Description |
|-------|------|-------------|
| `summary` | string | Natural language summary of the response |
| `referenceLinks` | ReferenceLink[] | Sources used (web search, documentation, etc.) |
| `portfolios` | PortfolioData[] \| null | Portfolio data if query relates to portfolios |
| `accounts` | AccountData[] \| null | Account data if query relates to accounts |
| `fundRecommendations` | FundRecommendation[] \| null | Investment recommendations |
| `complianceAlerts` | ComplianceAlert[] \| null | Compliance issues or risk assessments |
| `keyMetrics` | object \| null | Key metrics and insights |
| `suggestedActions` | string[] \| null | Actions the user can take |
| `subAgentsUsed` | string[] | Sub-agents that were consulted |
| `webSearchesExecuted` | string[] \| null | Web search queries executed |

### ReferenceLink

```typescript
{
  title: string;
  url: string;
  source: string; // "Web Search", "Internal Documentation", etc.
  snippet?: string;
}
```

### PortfolioData

```typescript
{
  portfolioId: string;
  portfolioName: string;
  totalValue: number;
  holdings?: HoldingData[];
  performance?: PerformanceData;
}
```

### HoldingData

```typescript
{
  fundSymbol: string;
  fundName: string;
  shares: number;
  currentValue: number;
  percentOfPortfolio: number;
}
```

### PerformanceData

```typescript
{
  totalReturn: number;
  returnPercentage: number;
  period: string; // "YTD", "1Y", "5Y", etc.
}
```

### AccountData

```typescript
{
  accountId: string;
  accountName: string;
  balance: number;
  accountType: string;
  status: string;
}
```

### FundRecommendation

```typescript
{
  fundSymbol: string;
  fundName: string;
  category: string;
  riskLevel: string;
  expectedReturn?: number;
  expenseRatio?: number;
  recommendationReason: string;
  matchScore: number; // 0-100 score
}
```

### ComplianceAlert

```typescript
{
  severity: "Low" | "Medium" | "High" | "Critical";
  alertType: string;
  message: string;
  recommendation?: string;
  regulationReference?: string;
}
```

## Use Cases

### 1. Portfolio Queries

**Request:**
```json
{
  "message": "Show me all portfolios for John Doe"
}
```

**Structured Response Includes:**
- `portfolios[]` - Array of portfolio data
- `keyMetrics` - Total value, count, etc.
- `suggestedActions` - Recommended next steps

### 2. Investment Advice

**Request:**
```json
{
  "message": "I'm 35 with moderate risk. Recommend growth funds."
}
```

**Structured Response Includes:**
- `fundRecommendations[]` - Array of fund recommendations with match scores
- `complianceAlerts[]` - Risk warnings if applicable
- `suggestedActions` - Investment strategy suggestions

### 3. Web Search Queries

**Request:**
```json
{
  "message": "What are the latest AI trends in 2025?"
}
```

**Structured Response Includes:**
- `referenceLinks[]` - Web sources with titles, URLs, snippets
- `webSearchesExecuted[]` - Queries performed
- `summary` - Natural language summary of findings

### 4. Multi-Agent Workflows

**Request:**
```json
{
  "message": "I want to invest $50,000. Show me my portfolio, recommend funds, and check compliance."
}
```

**Structured Response Includes:**
- `portfolios[]` - Current portfolio state
- `fundRecommendations[]` - Suggested investments
- `complianceAlerts[]` - Regulatory checks
- `subAgentsUsed` - ["PortfolioManager", "InvestmentAdvisor", "ComplianceOfficer"]
- `suggestedActions` - Complete investment workflow steps

## Implementation Details

### How It Works

1. **Separate Agent Instance**: The structured endpoint uses a dedicated agent configured to return JSON-only responses
2. **Tool Access**: Has access to all sub-agents (DelegateToSubAgent) and web search (SearchWeb)
3. **JSON Parsing**: Response is automatically parsed into strongly-typed objects
4. **Error Handling**: Returns structured error response if parsing fails

### Agent Instructions

The structured agent is instructed to:
- Return **only** valid JSON (no markdown, no extra text)
- Use `null` for unused sections
- Populate arrays with actual data from tool responses
- Track which sub-agents and tools were used
- Format numbers properly (not as strings)

### Comparison: Text vs Structured

| Aspect | Text Endpoint (`/chat`) | Structured Endpoint (`/chat/structured`) |
|--------|-------------------------|------------------------------------------|
| Response Format | Natural language markdown | JSON schema |
| Parsing Required | Yes (complex) | No (typed objects) |
| Web UI | ✅ Great | ⚠️ Needs custom rendering |
| API Integration | ⚠️ Harder | ✅ Easy |
| Data Extraction | Manual regex/parsing | Automatic |
| Type Safety | ❌ None | ✅ Full type safety |
| Use Case | Chat interfaces | Dashboards, integrations |

## Testing

See `test-structured.http` for test cases:

```bash
# Run the API
cd src
dotnet run

# Test with REST Client extension in VS Code
# Or use Postman/curl
```

Example curl:
```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat/structured \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Show me all portfolios for John Doe",
    "conversationId": "test-123"
  }'
```

## Frontend Integration Example

```typescript
interface StructuredResponse {
  success: boolean;
  data: {
    success: boolean;
    structuredResponse: {
      summary: string;
      referenceLinks: ReferenceLink[];
      portfolios?: PortfolioData[];
      accounts?: AccountData[];
      fundRecommendations?: FundRecommendation[];
      complianceAlerts?: ComplianceAlert[];
      keyMetrics?: Record<string, any>;
      suggestedActions?: string[];
      subAgentsUsed: string[];
      webSearchesExecuted?: string[];
    };
    subAgentsUsed: string[];
    durationMs: number;
    errorMessage?: string;
  };
}

// Make request
const response = await fetch('http://localhost:5000/api/v2/MasterAgent/chat/structured', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    message: 'Show me all portfolios for John Doe',
    conversationId: 'conversation-123'
  })
});

const data: StructuredResponse = await response.json();

// Use typed data
if (data.success && data.data.structuredResponse.portfolios) {
  data.data.structuredResponse.portfolios.forEach(portfolio => {
    console.log(`${portfolio.portfolioName}: $${portfolio.totalValue}`);
  });
}
```

## Benefits

1. **Type Safety**: Full type definitions in TypeScript/C#
2. **Easy Parsing**: No regex or string manipulation needed
3. **Predictable Structure**: Always follows same schema
4. **API Integration**: Perfect for dashboards, mobile apps, third-party integrations
5. **Data Processing**: Easy to aggregate, filter, sort structured data
6. **Error Handling**: Structured error responses

## When to Use Each Endpoint

### Use `/chat` (Text) When:
- Building chat interfaces
- Need natural language explanations
- Streaming responses to UI
- User expects conversational interaction

### Use `/chat/structured` (JSON) When:
- Building dashboards
- Need to display data in tables/charts
- API-to-API integration
- Mobile app data binding
- Data export/reporting
- Automated workflows

## Architecture

```
Client Request
     ↓
MasterAgentController.ChatStructured()
     ↓
IMasterOrchestrator.ProcessRequestStructuredAsync()
     ↓
StructuredMasterAgent (specialized agent)
     ↓
Tools (DelegateToSubAgent, SearchWeb, etc.)
     ↓
JSON Response
     ↓
Deserialize to StructuredAgentResponse
     ↓
Return typed object to client
```

## Future Enhancements

- [ ] Add JSON schema validation
- [ ] Support partial responses for streaming
- [ ] Add pagination for large datasets
- [ ] Support custom response schemas
- [ ] Add GraphQL endpoint as alternative
- [ ] Cache structured responses
- [ ] Add response compression

## Related Files

- `src/Api/DTOs/StructuredResponse.cs` - Response models
- `src/Api/Controllers/MasterAgentController.cs` - Controller with `/chat/structured` endpoint
- `src/Api/Orchestration/MasterOrchestrator.cs` - Implementation of `ProcessRequestStructuredAsync`
- `src/Api/Abstractions/IMasterOrchestrator.cs` - Interface definition
- `test-structured.http` - Test cases

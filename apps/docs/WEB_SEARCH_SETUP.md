# Web Search Integration - Setup & Testing Guide

## Overview

The Master Agent now has web search capabilities powered by Bing Search API. This allows the AI agent to search the internet for current information, news, and real-time data.

## Setup Instructions

### 1. Get a Bing Search API Key

You need an Azure Bing Search resource to use this feature:

**Option A: Azure Portal (Recommended)**
1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new resource → Search for "Bing Search v7"
3. Create the resource (Free tier available: 1,000 transactions/month)
4. Once created, go to "Keys and Endpoint"
5. Copy one of the API keys

**Option B: Azure AI Services**
1. Create an Azure AI Services multi-service resource
2. This includes Bing Search API access
3. Copy the API key from the resource

### 2. Configure the API Key

**Option 1: Environment Variable (Recommended for Development)**
```bash
# Windows (PowerShell)
$env:BING_SEARCH_API_KEY="your-api-key-here"

# Windows (Command Prompt)
set BING_SEARCH_API_KEY=your-api-key-here

# Linux/Mac
export BING_SEARCH_API_KEY="your-api-key-here"
```

**Option 2: appsettings.json**
```json
{
  "BingSearch": {
    "ApiKey": "your-api-key-here"
  }
}
```

**Option 3: User Secrets (Best for Production)**
```bash
cd src
dotnet user-secrets set "BingSearch:ApiKey" "your-api-key-here"
```

### 3. Verify Installation

Run the application:
```bash
cd src
dotnet run
```

## Testing the Web Search Feature

### Test Scenarios

**1. Current Events/News**
```
User: "What are the latest developments in AI technology?"
User: "What's happening in the stock market today?"
User: "Search for recent news about OpenAI"
```

**2. Real-Time Information**
```
User: "What is the current weather in New York?"
User: "What's the latest price of Bitcoin?"
User: "Find information about upcoming tech conferences in 2025"
```

**3. General Knowledge with Current Context**
```
User: "Search the web for information about the latest Microsoft products"
User: "What are people saying about electric vehicles in 2025?"
```

**4. Combined Queries (Financial + Web Search)**
```
User: "Search for the latest news about the technology sector and recommend relevant funds"
User: "What's happening with interest rates? Should I adjust my portfolio?"
```

### Expected Behavior

When you ask a question requiring current information:

1. **Master Agent Analysis**: The agent will explain its reasoning:
   ```
   🤔 My Analysis:
   - User Request: Find latest AI technology news
   - Web Search Needed: Yes - requires current information
   - Approach: Use SearchWeb tool to find real-time data
   ```

2. **Web Search Execution**: The agent calls the SearchWeb tool with your query

3. **Results Formatting**: You'll receive:
   - Title of each result
   - URL source
   - Snippet/summary
   - Last updated timestamp (when available)

4. **Response**: The agent synthesizes the search results into a coherent answer

### Testing via API

**Using cURL:**
```bash
curl -X POST http://localhost:5000/api/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Search the web for latest AI news"}'
```

**Using the Frontend:**
1. Start both backend and frontend: `./scripts/start.sh` (or `start.bat` on Windows)
2. Open http://localhost:3000
3. Select "Master Agent" from the dropdown
4. Type a query requiring web search
5. Watch the streaming response

### Troubleshooting

**Error: "Web search is not configured"**
- Make sure you've set the `BING_SEARCH_API_KEY` environment variable
- Check that the API key is valid
- Verify the environment variable is accessible to the running process

**Error: "Web search failed with status 401"**
- Your API key is invalid or expired
- Get a new key from Azure Portal

**Error: "Web search failed with status 403"**
- Your API key doesn't have permission for Bing Search
- Make sure you created a "Bing Search v7" resource (not just generic Cognitive Services)

**Error: "No web results found"**
- The search query returned no results
- Try rephrasing your query

**Network Errors:**
- Check internet connectivity
- Verify firewall settings allow HTTPS to api.bing.microsoft.com

## Architecture Details

### Implementation

**WebSearchTools.cs** (`src/Tools/WebSearchTools.cs`)
- Implements `SearchWeb` method decorated with `[Description]` attribute
- Uses HttpClient to call Bing Search API v7
- Returns formatted results as string

**MasterOrchestrator.cs**
- Registers `WebSearchTools.SearchWeb` as an AI function
- Updated instructions to guide agent on when to use web search
- Agent decides autonomously when web search is needed

**Dependency Injection**
- `WebSearchTools` registered as Scoped service in `Program.cs`
- `HttpClient` factory injected for HTTP operations
- Configuration injected for API key access

### API Endpoint

```
GET https://api.bing.microsoft.com/v7.0/search?q={query}&count={count}
Header: Ocp-Apim-Subscription-Key: {your-api-key}
```

### Response Format

The tool returns formatted text:
```
Web search results for: {query}

**Title 1**
URL: https://example.com/article1
Snippet: Brief description of the result...
Last Updated: 2025-11-19T10:30:00

**Title 2**
URL: https://example.com/article2
Snippet: Another relevant result...
Last Updated: 2025-11-19T09:15:00
```

## Usage Limits

**Free Tier (F0)**:
- 1,000 transactions per month
- 1 transaction = 1 search request
- Rate limit: 3 queries per second

**Standard Tier (S1)**:
- Up to 1,000,000 transactions per month
- Pay per transaction after free quota
- Rate limit: 10 queries per second

## Best Practices

1. **Query Specificity**: More specific queries return better results
2. **Result Count**: Default is 5 results, adjust based on need (max 10)
3. **Caching**: Consider caching frequent queries to reduce API calls
4. **Error Handling**: The tool gracefully handles API failures and returns user-friendly errors
5. **Cost Monitoring**: Track usage in Azure Portal to avoid unexpected charges

## Example Conversations

**Example 1: Technology News**
```
User: What are the latest developments in quantum computing?

Agent: 🤔 My Analysis:
- User Request: Find latest quantum computing developments
- Web Search Needed: Yes - requires current information
- Approach: Use SearchWeb tool

[Performs web search and returns formatted results with sources]
```

**Example 2: Market Data + Investment Advice**
```
User: What's happening with tech stocks today? Should I invest more?

Agent: 🤔 My Analysis:
- User Request: Current tech stock information + investment advice
- Domain(s) Needed: Web Search + InvestmentAdvisor
- Approach: First search web for current data, then delegate to advisor

[Performs web search, then consults InvestmentAdvisor with findings]
```

## Next Steps

- Monitor API usage in Azure Portal
- Consider implementing caching for frequently searched topics
- Add rate limiting if needed
- Explore additional Bing Search API features (news, images, videos)

## Support

For issues or questions:
- Check the troubleshooting section above
- Review logs in the console output
- Verify API key permissions in Azure Portal
- Consult [Bing Search API documentation](https://learn.microsoft.com/en-us/bing/search-apis/)

# Azure AI Foundry Bing Search Setup Guide

## Current Azure Setup

You have:
- ✅ **Grounding with Bing Search** resource (Azure AI Foundry)
- ✅ OpenAI API (currently using `sk-proj-...`)

## What's Changed in Azure

Microsoft has **deprecated the old Bing Search v7 API** and replaced it with:
- **Grounding with Bing Search** - Only works through Azure AI Foundry Agent Service
- **Grounding with Bing Custom Search** - Same, but with custom search domains

These resources **cannot be used with direct HTTP API calls**. They must be used through:
- Azure OpenAI with data sources, OR
- Azure AI Foundry Agent Service

## Required Setup Steps

### Step 1: Create or Find Azure OpenAI Resource

1. Go to [Azure AI Foundry Portal](https://ai.azure.com)
2. Navigate to your project (or create one)
3. Go to **"Models + endpoints"** → **"Deployments"**
4. Check if you have a deployment of:
   - `gpt-4o-mini` (recommended - cheapest)
   - `gpt-4o` (better quality)
   - `gpt-4` (older but stable)

**If you don't have a deployment:**
1. Click **"Deploy model"**
2. Select **"gpt-4o-mini"** (recommended)
3. Give it a name: `gpt-4o-mini` or `investment-agent`
4. Click Deploy

### Step 2: Get Azure OpenAI Credentials

1. In Azure AI Foundry, go to **"Settings"** → **"Project settings"**
2. Or go to Azure Portal → Your Azure OpenAI resource
3. Copy these values:

**You need:**
```
Azure OpenAI Endpoint: https://YOUR-RESOURCE-NAME.openai.azure.com/
Azure OpenAI Key: [Copy from "Keys and Endpoint"]
Deployment Name: gpt-4o-mini (or whatever you named it)
```

### Step 3: Create Connection to Bing in Azure AI Foundry

1. In Azure AI Foundry, go to **"Management"** → **"Connected resources"** (or "Connections")
2. Click **"+ New connection"**
3. Select **"Bing Grounding"** or **"Bing Search"**
4. Select your existing "Grounding with Bing Search" resource
5. Give it a name: `bing-search` or `bing-grounding`
6. Save the connection
7. **Copy the Connection ID** or Connection Name (we'll need this)

### Step 4: Test Azure OpenAI with Bing Grounding

Before updating the code, let's test if it works:

```bash
# Replace these values with yours
AZURE_OPENAI_ENDPOINT="https://YOUR-RESOURCE.openai.azure.com/"
AZURE_OPENAI_KEY="your-key-here"
DEPLOYMENT_NAME="gpt-4o-mini"

curl -X POST "${AZURE_OPENAI_ENDPOINT}openai/deployments/${DEPLOYMENT_NAME}/chat/completions?api-version=2024-08-01-preview" \
  -H "Content-Type: application/json" \
  -H "api-key: ${AZURE_OPENAI_KEY}" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "What are the latest AI developments in November 2025?"
      }
    ],
    "data_sources": [
      {
        "type": "bing_grounding"
      }
    ],
    "max_tokens": 500
  }'
```

**Expected Response:**
- Should return a JSON response with `choices` array
- The response should include web search results and citations
- Should NOT return 401 or authentication errors

### Step 5: Provide Information for Code Update

Once you have completed the above steps, provide me with:

```
1. Azure OpenAI Endpoint: https://_________.openai.azure.com/
2. Azure OpenAI API Key: ________________________________
3. Deployment Name: ________________
4. Bing Connection Name/ID: ________________ (optional - can auto-detect)
```

## Code Changes Required

Once you provide the credentials, I will:

1. ✅ Update `Program.cs` to use `AzureOpenAIClient` instead of `OpenAIClient`
2. ✅ Remove the custom `WebSearchTools.cs` (no longer needed)
3. ✅ Configure Bing grounding through Azure OpenAI data sources
4. ✅ Update `MasterOrchestrator.cs` to use built-in grounding
5. ✅ Keep all the frontend UI enhancements (already done)
6. ✅ Update configuration for Azure OpenAI settings

## Benefits of This Approach

✅ **Uses your existing Bing resource** - no wasted resources
✅ **Better quality** - Azure OpenAI with Bing provides citations and grounding
✅ **Automatic formatting** - No need to parse search results
✅ **Microsoft recommended** - This is the official way forward
✅ **Better rate limiting** - Handled automatically by Azure
✅ **Easier maintenance** - Less custom code to maintain

## Cost Estimate

**Azure OpenAI (gpt-4o-mini):**
- Input: $0.150 per 1M tokens (~$0.0002 per query)
- Output: $0.600 per 1M tokens (~$0.0006 per query)
- **Typical query: < $0.001 (less than a penny)**

**Bing Grounding:**
- Included with your Azure AI Foundry Bing resource
- No per-query charges

**Total per 1,000 queries: ~$1-2**

## Alternative: Keep Using OpenAI API

If you prefer to keep using OpenAI API (not Azure OpenAI), you have two options:

### Option A: Use OpenAI's GPT-4 with Web Browsing
- OpenAI now has models with built-in web browsing: `gpt-4-turbo-preview` with `tools=["web_browser"]`
- No Bing resource needed
- More expensive (~$0.01-0.03 per query)

### Option B: Give Up on Bing Resource, Use Alternative
- Use a different web search API (SerpAPI, Google Custom Search, etc.)
- Your Azure Bing resource would go unused
- Requires new API subscriptions

**My Recommendation:** Go with Azure OpenAI + Bing Grounding. It's the modern approach and uses what you already have.

---

## Next Steps

Please:
1. ✅ Go to Azure AI Foundry Portal (ai.azure.com)
2. ✅ Create or find a `gpt-4o-mini` deployment
3. ✅ Get the endpoint and API key
4. ✅ Create a connection to your Bing resource
5. ✅ Test with the curl command above
6. ✅ Share the credentials with me

Then I'll update the code to use Azure OpenAI with Bing grounding!

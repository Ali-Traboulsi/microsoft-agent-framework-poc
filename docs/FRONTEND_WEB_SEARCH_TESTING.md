# Frontend Web Search Testing Guide

## Overview
This guide provides instructions for testing the web search capability through the React frontend.

## Prerequisites
✅ Backend running on `http://localhost:5000`
✅ Frontend running on `http://localhost:3000`
✅ Bing Search API key configured in User Secrets

## Visual Indicators to Look For

### 1. **Welcome Screen**
When you first open the Master Agent Chat, you should see:
- A new section titled "🌐 Web Search Capability Enabled"
- Four clickable example prompts with web search queries:
  - Latest AI trends and news
  - Current stock market performance
  - Recent company announcements
  - Economic indicators today

### 2. **Tool Execution Messages**
When web search is triggered, you'll see:
- **Special teal-colored message box** (instead of green for other tools)
- **Globe icon (🌐)** next to "Executing: SearchWeb"
- Distinct styling: `bg-teal-50 border-teal-300`

### 3. **Telemetry Display**
With telemetry enabled, you'll see:
- A special **"🌐 Web Search"** badge in the Tools section
- Teal-colored badge showing web search was used
- Total tool count includes SearchWeb

### 4. **Search Results**
The agent will return formatted search results with:
- **Numbered results** (1., 2., 3., etc.)
- **Clickable URLs** (blue, underlined, open in new tab)
- **Snippets/descriptions** for each result
- Clean markdown formatting

## Test Scenarios

### Test 1: Basic Web Search
1. Navigate to the Master Agent tab
2. Type: `What are the latest developments in AI?`
3. Press Send
4. **Expected Results:**
   - See "🤔 Thinking" message (purple)
   - See "🌐 Executing: SearchWeb" message (teal)
   - Agent response with numbered search results
   - Telemetry shows "🌐 Web Search" badge
   - URLs are clickable and formatted

### Test 2: Quick Action Buttons
1. Click on "Latest AI trends and news" button
2. Verify the text populates in the input field
3. Press Send
4. **Expected Results:**
   - Same as Test 1
   - Input field shows the clicked text before sending

### Test 3: Financial Query with Web Search
1. Type: `What's the current stock market performance today?`
2. Press Send
3. **Expected Results:**
   - Web search tool is invoked
   - Real-time market information is returned
   - Results include recent URLs with dates

### Test 4: Hybrid Query (Database + Web)
1. Type: `Show my portfolio and search for recent tech company news`
2. Press Send
3. **Expected Results:**
   - Multiple tools invoked (PortfolioTools + SearchWeb)
   - Both portfolio data and web results returned
   - Multiple tool messages appear (one teal for web search)

### Test 5: No Web Search Needed
1. Type: `Show my account balance`
2. Press Send
3. **Expected Results:**
   - Only AccountTools invoked (green message)
   - NO web search tool (no teal message)
   - Agent uses internal data only

### Test 6: Multiple Web Searches
1. Type: `Compare AI trends and blockchain developments this year`
2. Press Send
3. **Expected Results:**
   - Potentially 2+ SearchWeb invocations
   - Multiple teal tool messages
   - Combined results from multiple searches

## Visual Comparison

### Before Web Search (Regular Tool)
```
┌─────────────────────────────────┐
│ 🔧 Executing: GetPortfolio      │  ← Green background
│                                 │
└─────────────────────────────────┘
```

### After Web Search (SearchWeb Tool)
```
┌─────────────────────────────────┐
│ 🌐 Executing: SearchWeb         │  ← Teal background
│                                 │
└─────────────────────────────────┘
```

## Troubleshooting

### Issue: No Web Search Triggered
**Symptom:** Agent doesn't use SearchWeb even with relevant queries
**Solutions:**
- Verify backend is running with User Secrets configured
- Check browser console for SignalR connection errors
- Try more explicit queries: "Search the web for..."
- Check backend logs for tool registration

### Issue: SearchWeb Shows Green Instead of Teal
**Symptom:** Tool message has wrong color
**Solutions:**
- Hard refresh browser (Ctrl+Shift+R)
- Clear browser cache
- Verify `toolName === 'SearchWeb'` condition in code
- Check that `toolName` field is passed in SignalR stream

### Issue: URLs Not Clickable
**Symptom:** Web search results show plain text URLs
**Solutions:**
- Verify MessageContent component is rendering markdown
- Check that backend returns properly formatted markdown links
- Inspect browser console for rendering errors

### Issue: No Telemetry Badge
**Symptom:** "🌐 Web Search" badge not appearing
**Solutions:**
- Enable telemetry with the checkbox
- Verify `telemetry.toolsUsed` array includes 'SearchWeb'
- Check that telemetry stream message is received

## Sample Queries to Test

### Queries that SHOULD trigger web search:
- "What are the latest AI news?"
- "Current stock market trends"
- "Recent developments in quantum computing"
- "Today's economic indicators"
- "Breaking news in tech industry"
- "Latest Microsoft announcements"

### Queries that should NOT trigger web search:
- "Show my portfolio"
- "What's my account balance?"
- "Create a new portfolio"
- "List all mutual funds"
- "Explain investment diversification" (knowledge-based)

## Performance Expectations

- **Tool Invocation:** < 500ms
- **Bing API Call:** 1-2 seconds
- **Total Response:** 2-4 seconds
- **Streaming:** Real-time chunks appear progressively

## Success Criteria

✅ Teal-colored tool messages for SearchWeb
✅ Globe icon (🌐) appears for web search
✅ Example prompts are clickable and functional
✅ Telemetry shows "🌐 Web Search" badge
✅ Search results have clickable URLs
✅ Results are properly formatted with markdown
✅ Multiple searches handled correctly
✅ Non-web queries don't trigger web search

## Next Steps After Testing

1. **Adjust Styling:** If teal color needs tweaking, modify `getMessageStyle()`
2. **Add More Examples:** Update the 4 example prompts in welcome screen
3. **Custom Result Formatting:** Enhance how search results are displayed
4. **Error Handling:** Add specific error messages for web search failures
5. **Rate Limiting UI:** Show indicators if search quota is exceeded

## Notes

- Web search results are cached by OpenAI during the conversation
- Results reflect real-time web data at the time of the search
- Bing Search API has rate limits (check Azure portal)
- Search results quality depends on the query formulation

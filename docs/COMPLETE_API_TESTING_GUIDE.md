# Investment Banking Platform - Complete Testing & Troubleshooting Guide

## Table of Contents
1. [Prerequisites](#prerequisites)
2. [Environment Setup](#environment-setup)
3. [Profit Projection Workflow Tests](#profit-projection-workflow-tests)
4. [Master Agent Tests](#master-agent-tests)
5. [Accounts API Tests](#accounts-api-tests)
6. [Portfolios API Tests](#portfolios-api-tests)
7. [Funds API Tests](#funds-api-tests)
8. [Direct Agent Tests](#direct-agent-tests)
9. [SNB Capital API Tests](#snb-capital-api-tests)
10. [Error Scenarios](#error-scenarios)
11. [End-to-End Testing](#end-to-end-testing)
12. [Troubleshooting Guide](#troubleshooting-guide)
13. [Common Issues & Solutions](#common-issues--solutions)
14. [Monitoring & Observability](#monitoring--observability)

---

## Prerequisites

### Required API Keys
```bash
# Set these environment variables before running the application
export OPENAI_API_KEY="your-openai-api-key"
export SERPER_API_KEY="your-serper-api-key"  # For web search
```

### SNB Capital API Access
- **Base URL**: `https://snbc-api.onrender.com/snbc/api/v1`
- **Test CIF Range**: `100000000001` to `100000000030`
- No authentication required for test endpoints

### Software Requirements
- .NET 9.0 SDK
- Node.js 18+ (for frontend)
- Git
- Postman or curl (for API testing)

---

## Environment Setup

### 1. Clone and Build
```bash
cd /path/to/AgentFrameworkQuickStart
cd src
dotnet restore
dotnet build
```

### 2. Verify Configuration
Check `appsettings.json` for correct settings:
```json
{
  "SNBCapital": {
    "BaseUrl": "https://snbc-api.onrender.com/snbc/api/v1"
  },
  "Serper": {
    "ApiKey": "your-serper-api-key"
  }
}
```

### 3. Run the Application
```bash
cd src
dotnet run
```
The API will start on `http://localhost:5000`

---

## API Testing

### Test 1: Basic Projection Request (Anonymous User)

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 50000,
    "currency": "SAR",
    "timeHorizonMonths": 12,
    "riskProfile": "Moderate",
    "investmentType": "LumpSum",
    "shariahCompliantOnly": false
  }'
```

**Expected Response:**
```json
{
  "projectionId": "PROJ202412041234567",
  "inputSummary": {
    "amount": 50000,
    "currency": "SAR",
    "horizon": "1 year",
    "riskProfile": "Moderate"
  },
  "scenarios": {
    "conservative": { "projectedValue": 51500, ... },
    "expected": { "projectedValue": 53500, ... },
    "optimistic": { "projectedValue": 56000, ... }
  },
  "recommendedFunds": [...],
  "metadata": {
    "executionTimeMs": 2500,
    "dataSources": ["SNB Capital Mutual Funds API", "Market Conditions Analysis"]
  }
}
```

### Test 2: Projection with Customer Context

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 100000,
    "currency": "SAR",
    "timeHorizonMonths": 36,
    "riskProfile": "Aggressive",
    "investmentType": "LumpSum",
    "shariahCompliantOnly": true,
    "customerId": "100000000001"
  }'
```

**What to Verify:**
- ✅ Response includes `isExistingCustomer: true`
- ✅ Existing holdings are included
- ✅ Risk profile may be adjusted based on existing portfolio
- ✅ Fund recommendations consider existing positions

### Test 3: Monthly SIP Projection

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 60000,
    "currency": "SAR",
    "timeHorizonMonths": 24,
    "riskProfile": "Conservative",
    "investmentType": "Monthly",
    "monthlyAmount": 2500,
    "shariahCompliantOnly": false
  }'
```

**What to Verify:**
- ✅ Monthly projections show incremental growth
- ✅ Total invested equals `monthlyAmount * timeHorizonMonths`
- ✅ Strategy comparison is NOT included (only for LumpSum)

### Test 4: Different Risk Profiles

Test each risk profile to ensure appropriate fund selection:

```bash
# Conservative - Should favor Money Market and Fixed Income
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":50000,"currency":"SAR","timeHorizonMonths":12,"riskProfile":"Conservative","investmentType":"LumpSum"}'

# Moderate - Should include Balanced funds
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":50000,"currency":"SAR","timeHorizonMonths":12,"riskProfile":"Moderate","investmentType":"LumpSum"}'

# Aggressive - Should favor Equity funds
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":50000,"currency":"SAR","timeHorizonMonths":12,"riskProfile":"Aggressive","investmentType":"LumpSum"}'
```

---

## Master Agent Tests

The Master Agent (`/api/v2/MasterAgent`) is the primary AI orchestrator that routes requests to specialized sub-agents.

### Test 1: Basic Chat Request

**Request:**
```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What investment options are available for a conservative investor?",
    "conversationId": "test-conv-001"
  }'
```

**Expected Response:**
```json
{
  "response": "For a conservative investor, I recommend...",
  "conversationId": "test-conv-001",
  "agentUsed": "InvestmentAdvisor",
  "timestamp": "2024-12-04T10:30:00Z"
}
```

### Test 2: Structured Response

**Request:**
```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat/structured \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Give me a portfolio analysis",
    "conversationId": "test-conv-002",
    "responseFormat": "json"
  }'
```

### Test 3: Multimodal with Base64 Image

**Request:**
```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Analyze this portfolio chart",
    "conversationId": "test-conv-003",
    "content": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUA...",
    "contentType": "image/png"
  }'
```

### Test 4: Multimodal with File Upload

**Request (multipart/form-data):**
```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload \
  -F "message=Analyze this document" \
  -F "conversationId=test-conv-004" \
  -F "file=@/path/to/document.pdf"
```

### Test 5: List Available Sub-Agents

**Request:**
```bash
curl -X GET http://localhost:5000/api/v2/MasterAgent/subagents
```

**Expected Response:**
```json
{
  "subAgents": [
    {
      "name": "AccountServices",
      "domain": "Account Management",
      "capabilities": ["Balance inquiries", "Deposits", "Transactions"]
    },
    {
      "name": "InvestmentAdvisor",
      "domain": "Investment Advisory",
      "capabilities": ["Risk assessment", "Portfolio recommendations"]
    },
    {
      "name": "PortfolioManager",
      "domain": "Portfolio Management",
      "capabilities": ["Portfolio creation", "Rebalancing", "Performance"]
    },
    {
      "name": "ComplianceOfficer",
      "domain": "Regulatory Compliance",
      "capabilities": ["KYC verification", "Risk checks", "Regulatory compliance"]
    }
  ]
}
```

### Test 6: Clear Conversation History

**Request:**
```bash
curl -X DELETE http://localhost:5000/api/v2/MasterAgent/conversation/test-conv-001
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Conversation test-conv-001 cleared"
}
```

### Test 7: Get Conversation Statistics

**Request:**
```bash
curl -X GET http://localhost:5000/api/v2/MasterAgent/conversation/stats
```

**Expected Response:**
```json
{
  "activeConversations": 5,
  "totalMessages": 127,
  "averageConversationLength": 8
}
```

---

## Accounts API Tests

### Test 1: List All Accounts

**Request:**
```bash
curl -X GET http://localhost:5000/api/Accounts
```

**Expected Response:**
```json
[
  {
    "accountId": "ACC001",
    "accountName": "John Doe",
    "balance": 50000.00,
    "currency": "SAR"
  }
]
```

### Test 2: Get Account by ID

**Request:**
```bash
curl -X GET http://localhost:5000/api/Accounts/ACC001
```

### Test 3: Get Account Balance (AI-Powered)

**Request:**
```bash
curl -X GET http://localhost:5000/api/Accounts/ACC001/balance
```

**Note:** This endpoint uses the AccountServices agent for natural language balance inquiry.

### Test 4: Deposit to Account (AI-Powered)

**Request:**
```bash
curl -X POST http://localhost:5000/api/Accounts/ACC001/deposit \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 5000,
    "description": "Monthly investment deposit"
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Successfully deposited 5000 SAR",
  "newBalance": 55000.00,
  "transactionId": "TXN202412041234"
}
```

### Test 5: Get Transaction History

**Request:**
```bash
curl -X GET http://localhost:5000/api/Accounts/ACC001/transactions
```

**Expected Response:**
```json
{
  "transactions": [
    {
      "transactionId": "TXN001",
      "type": "DEPOSIT",
      "amount": 5000.00,
      "date": "2024-12-04T10:30:00Z",
      "description": "Monthly investment deposit"
    }
  ]
}
```

---

## Portfolios API Tests

### Test 1: List All Portfolios

**Request:**
```bash
curl -X GET http://localhost:5000/api/Portfolios
```

### Test 2: List Portfolios by Account

**Request:**
```bash
curl -X GET "http://localhost:5000/api/Portfolios?accountId=ACC001"
```

### Test 3: Get Portfolio by ID

**Request:**
```bash
curl -X GET http://localhost:5000/api/Portfolios/PORT001
```

### Test 4: Create New Portfolio

**Request:**
```bash
curl -X POST http://localhost:5000/api/Portfolios \
  -H "Content-Type: application/json" \
  -d '{
    "accountId": "ACC001",
    "portfolioName": "Growth Portfolio",
    "riskLevel": 3,
    "investmentGoal": "Long-term growth"
  }'
```

**Expected Response:**
```json
{
  "portfolioId": "PORT638697123456789",
  "accountId": "ACC001",
  "portfolioName": "Growth Portfolio",
  "createdAt": "2024-12-04T10:30:00Z"
}
```

### Test 5: Get Portfolio Details (AI-Powered)

**Request:**
```bash
curl -X GET http://localhost:5000/api/Portfolios/PORT001/details
```

**Note:** Returns AI-generated portfolio analysis including performance summary and recommendations.

### Test 6: Get Portfolio Allocation

**Request:**
```bash
curl -X GET http://localhost:5000/api/Portfolios/PORT001/allocation
```

**Expected Response:**
```json
{
  "portfolioId": "PORT001",
  "totalValue": 150000.00,
  "allocation": [
    {
      "assetClass": "Equity",
      "percentage": 60,
      "value": 90000.00
    },
    {
      "assetClass": "Fixed Income",
      "percentage": 30,
      "value": 45000.00
    },
    {
      "assetClass": "Cash",
      "percentage": 10,
      "value": 15000.00
    }
  ]
}
```

### Test 7: Invest in Fund

**Request:**
```bash
curl -X POST http://localhost:5000/api/Portfolios/PORT001/invest \
  -H "Content-Type: application/json" \
  -d '{
    "fundId": "FUND001",
    "amount": 10000.00
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "transactionId": "INVEST638697123456789",
  "fundId": "FUND001",
  "amount": 10000.00,
  "unitsAllocated": 645.16,
  "navAtPurchase": 15.50
}
```

---

## Funds API Tests

### Test 1: List All Funds

**Request:**
```bash
curl -X GET http://localhost:5000/api/Funds
```

**Expected Response:**
```json
[
  {
    "fundId": "FUND001",
    "fundName": "SNB Capital Saudi Equity Fund",
    "fundType": "Equity",
    "navValue": 15.50,
    "return1Y": 8.5,
    "isShariahCompliant": true
  }
]
```

### Test 2: Get Fund by ID

**Request:**
```bash
curl -X GET http://localhost:5000/api/Funds/FUND001
```

### Test 3: Search Funds with Filters

**Request:**
```bash
curl -X POST http://localhost:5000/api/Funds/search \
  -H "Content-Type: application/json" \
  -d '{
    "fundType": "Equity",
    "minReturn1Y": 5.0,
    "shariahCompliantOnly": true,
    "riskLevel": "Moderate",
    "pageSize": 10
  }'
```

**Expected Response:**
```json
{
  "funds": [...],
  "totalCount": 15,
  "pageSize": 10,
  "pageNumber": 1
}
```

### Test 4: Compare Multiple Funds

**Request:**
```bash
curl -X POST http://localhost:5000/api/Funds/compare \
  -H "Content-Type: application/json" \
  -d '{
    "fundIds": ["FUND001", "FUND002", "FUND003"]
  }'
```

**Expected Response:**
```json
{
  "comparison": [
    {
      "fundId": "FUND001",
      "fundName": "SNB Capital Saudi Equity Fund",
      "return1Y": 8.5,
      "return3Y": 25.2,
      "volatility": 15.3,
      "sharpeRatio": 0.85
    }
  ],
  "bestPerformer1Y": "FUND002",
  "lowestVolatility": "FUND001"
}
```

### Test 5: Get Fund Details (AI-Powered)

**Request:**
```bash
curl -X GET http://localhost:5000/api/Funds/FUND001/details
```

**Note:** Returns comprehensive AI analysis of the fund including performance trends, risk assessment, and investment suitability.

---

## Direct Agent Tests

The `/api/Agents` endpoints allow direct communication with specific sub-agents.

### Test 1: List Available Agents

**Request:**
```bash
curl -X GET http://localhost:5000/api/Agents
```

**Expected Response:**
```json
{
  "agents": [
    "AccountServices",
    "InvestmentAdvisor", 
    "PortfolioManager",
    "ComplianceOfficer"
  ]
}
```

### Test 2: Chat with Specific Agent

**Request - AccountServices:**
```bash
curl -X POST http://localhost:5000/api/Agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentName": "AccountServices",
    "message": "What is the balance of account ACC001?"
  }'
```

**Request - InvestmentAdvisor:**
```bash
curl -X POST http://localhost:5000/api/Agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentName": "InvestmentAdvisor",
    "message": "I have 100,000 SAR to invest. I am moderately risk-tolerant. What do you recommend?"
  }'
```

**Request - PortfolioManager:**
```bash
curl -X POST http://localhost:5000/api/Agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentName": "PortfolioManager",
    "message": "Analyze portfolio PORT001 and suggest rebalancing if needed"
  }'
```

**Request - ComplianceOfficer:**
```bash
curl -X POST http://localhost:5000/api/Agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentName": "ComplianceOfficer",
    "message": "Verify KYC status for account ACC001"
  }'
```

---

## SNB Capital API Tests

Direct tests against the SNB Capital test API (for validation and debugging).

### Test 1: Get Mutual Funds

**Request:**
```bash
curl -X GET "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000001"
```

**Expected Response Structure:**
```json
{
  "statusCode": 200,
  "message": "success",
  "isSuccess": true,
  "data": [
    {
      "fundId": "...",
      "fundName": "...",
      "fundCode": "...",
      "navValue": 15.5,
      "return1Y": 8.5,
      "return3Y": 25.2,
      "managementFee": 1.5
    }
  ]
}
```

### Test 2: Get Customer Portfolios

**Request:**
```bash
curl -X GET "https://snbc-api.onrender.com/snbc/api/v1/customer/portfolios?cif=100000000001"
```

### Test 3: Get Mutual Fund Holdings

**Request:**
```bash
curl -X GET "https://snbc-api.onrender.com/snbc/api/v1/customer/portfolios/{portfolioNumber}/holdings/mf?cif=100000000001"
```

### Test 4: Verify Different CIFs

**Test Multiple Customers:**
```bash
# Customer 1
curl -s "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000001" | jq '.data | length'

# Customer 5
curl -s "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000005" | jq '.data | length'

# Customer 10
curl -s "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000010" | jq '.data | length'
```

---

## Error Scenarios

### Test 1: Invalid Account ID

**Request:**
```bash
curl -X GET http://localhost:5000/api/Accounts/INVALID_ACC
```

**Expected Response:**
```json
{
  "error": "Account not found",
  "code": "ACCOUNT_NOT_FOUND",
  "status": 404
}
```

### Test 2: Invalid Portfolio ID

**Request:**
```bash
curl -X GET http://localhost:5000/api/Portfolios/INVALID_PORT
```

**Expected Response:**
```json
{
  "error": "Portfolio not found",
  "code": "PORTFOLIO_NOT_FOUND", 
  "status": 404
}
```

### Test 3: Missing Required Fields

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 50000
  }'
```

**Expected Response:**
```json
{
  "errors": {
    "currency": ["The currency field is required."],
    "timeHorizonMonths": ["The timeHorizonMonths field is required."],
    "riskProfile": ["The riskProfile field is required."]
  },
  "status": 400
}
```

### Test 4: Invalid Agent Name

**Request:**
```bash
curl -X POST http://localhost:5000/api/Agents/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentName": "NonExistentAgent",
    "message": "Hello"
  }'
```

**Expected Response:**
```json
{
  "error": "Agent not found",
  "code": "AGENT_NOT_FOUND",
  "availableAgents": ["AccountServices", "InvestmentAdvisor", "PortfolioManager", "ComplianceOfficer"],
  "status": 404
}
```

### Test 5: Invalid Fund ID in Comparison

**Request:**
```bash
curl -X POST http://localhost:5000/api/Funds/compare \
  -H "Content-Type: application/json" \
  -d '{
    "fundIds": ["FUND001", "INVALID_FUND"]
  }'
```

**Expected Response:**
```json
{
  "error": "One or more fund IDs not found",
  "invalidFundIds": ["INVALID_FUND"],
  "status": 400
}
```

### Test 6: Zero Investment Amount

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 0,
    "currency": "SAR",
    "timeHorizonMonths": 12,
    "riskProfile": "Moderate",
    "investmentType": "LumpSum"
  }'
```

**Expected Response:**
```json
{
  "errors": {
    "investmentAmount": ["Investment amount must be greater than 0"]
  },
  "status": 400
}
```

### Test 7: Invalid Risk Profile

**Request:**
```bash
curl -X POST http://localhost:5000/api/projection/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "investmentAmount": 50000,
    "currency": "SAR",
    "timeHorizonMonths": 12,
    "riskProfile": "SuperAggressive",
    "investmentType": "LumpSum"
  }'
```

**Expected Response:**
```json
{
  "errors": {
    "riskProfile": ["Risk profile must be one of: Conservative, Moderate, Aggressive"]
  },
  "status": 400
}
```

---

## End-to-End Testing

### Test Scenario Matrix

| Test Case | Amount | Horizon | Risk | Shariah | Customer | Expected Outcome |
|-----------|--------|---------|------|---------|----------|------------------|
| TC-001 | 10,000 SAR | 12 months | Conservative | No | None | Low-risk funds recommended |
| TC-002 | 50,000 SAR | 24 months | Moderate | Yes | None | Balanced Shariah funds |
| TC-003 | 100,000 SAR | 36 months | Aggressive | No | 100000000001 | Equity-heavy with existing holdings |
| TC-004 | 25,000 SAR | 60 months | Moderate | No | None | Long-term growth funds |
| TC-005 | 500,000 SAR | 12 months | Conservative | Yes | 100000000005 | Large investment warning |

### Automated Test Script

Create a test script `test_projection.sh`:
```bash
#!/bin/bash

BASE_URL="http://localhost:5000/api/projection"

echo "=== Testing Profit Projection Workflow ==="

# Test 1: Basic projection
echo -e "\n[Test 1] Basic Projection..."
response=$(curl -s -X POST "$BASE_URL/calculate" \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":50000,"currency":"SAR","timeHorizonMonths":12,"riskProfile":"Moderate","investmentType":"LumpSum"}')

if echo "$response" | grep -q "projectionId"; then
  echo "✅ PASS: Basic projection returned valid response"
else
  echo "❌ FAIL: Basic projection failed"
  echo "$response"
fi

# Test 2: With customer context
echo -e "\n[Test 2] Projection with Customer Context..."
response=$(curl -s -X POST "$BASE_URL/calculate" \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":100000,"currency":"SAR","timeHorizonMonths":36,"riskProfile":"Aggressive","investmentType":"LumpSum","customerId":"100000000001"}')

if echo "$response" | grep -q "isExistingCustomer"; then
  echo "✅ PASS: Customer context included"
else
  echo "❌ FAIL: Customer context missing"
fi

# Test 3: Shariah-compliant only
echo -e "\n[Test 3] Shariah-Compliant Projection..."
response=$(curl -s -X POST "$BASE_URL/calculate" \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":50000,"currency":"SAR","timeHorizonMonths":12,"riskProfile":"Moderate","investmentType":"LumpSum","shariahCompliantOnly":true}')

if echo "$response" | grep -q "recommendedFunds"; then
  echo "✅ PASS: Shariah-compliant projection returned funds"
else
  echo "❌ FAIL: Shariah-compliant projection failed"
fi

# Test 4: Monthly investment
echo -e "\n[Test 4] Monthly SIP Projection..."
response=$(curl -s -X POST "$BASE_URL/calculate" \
  -H "Content-Type: application/json" \
  -d '{"investmentAmount":60000,"currency":"SAR","timeHorizonMonths":24,"riskProfile":"Conservative","investmentType":"Monthly","monthlyAmount":2500}')

if echo "$response" | grep -q "monthlyProjections"; then
  echo "✅ PASS: Monthly projections included"
else
  echo "❌ FAIL: Monthly projections missing"
fi

echo -e "\n=== Testing Complete ==="
```

---

## Troubleshooting Guide

### Issue 1: "No funds returned from SNB Capital API"

**Symptoms:**
- Empty `recommendedFunds` array
- `fundsAnalyzed: 0` in response

**Diagnosis:**
```bash
# Test API directly
curl -X GET "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000001"
```

**Solutions:**
1. ✅ Verify the SNB Capital API is accessible
2. ✅ Check if the CIF is in valid range (100000000001-100000000030)
3. ✅ Review logs for API response parsing errors

### Issue 2: "Market conditions analysis returning zeros"

**Symptoms:**
- `economicIndicators` all show `0`
- `marketSentiment: "Neutral"`

**Diagnosis:**
```bash
# Check Serper API key
echo $SERPER_API_KEY

# Test web search directly
curl -X POST "https://google.serper.dev/search" \
  -H "X-API-KEY: $SERPER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"q": "Brent crude oil price today"}'
```

**Solutions:**
1. ✅ Verify `SERPER_API_KEY` is set correctly
2. ✅ Check Serper API quota/limits
3. ✅ Review web search parsing logic in `MarketConditionsAnalyzer`

### Issue 3: "Customer context not loading"

**Symptoms:**
- `isExistingCustomer: false` even with valid customerId
- No existing holdings shown

**Diagnosis:**
```bash
# Test portfolios API
curl -X GET "https://snbc-api.onrender.com/snbc/api/v1/customer/portfolios?cif=100000000001"
```

**Solutions:**
1. ✅ Verify customer has portfolios in the test data
2. ✅ Check CIF format (should be 12 digits)
3. ✅ Review `CustomerContextExecutor` logs

### Issue 4: "Workflow timeout or slow response"

**Symptoms:**
- Response takes > 30 seconds
- Timeout errors

**Diagnosis:**
Check which step is slow:
```bash
# Look for timing in logs
grep "execution_time" logs/app.log
```

**Solutions:**
1. ✅ Check network connectivity to external APIs
2. ✅ Verify parallel execution is working (should not exceed 5-6 seconds normally)
3. ✅ Consider adding caching for frequently accessed data

### Issue 5: "Invalid projection values (negative or unrealistic)"

**Symptoms:**
- Negative projected values
- Returns > 100% annually

**Diagnosis:**
Review the input data:
```bash
# Check raw fund returns from API
curl -s "https://snbc-api.onrender.com/snbc/api/v1/customer/mutualfunds?cif=100000000001" | jq '.data[].return1Y'
```

**Solutions:**
1. ✅ Verify API is returning valid return percentages
2. ✅ Check volatility estimation logic
3. ✅ Review percentile calculations in `HistoricalAnalyzer`

---

## Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| `Sequence contains no elements` | Empty fund list after filtering (Shariah/risk) | System now auto-falls back to all funds with warning |
| `NullReferenceException` in FundSelection | Missing NAV data from API | Check `NavValue` field in API response |
| Empty sector outlooks | Web search not returning results | Verify Serper API key and quota |
| `SNBCapitalApiException` | API unavailable or rate limited | Implement retry logic, check API status |
| Wrong risk profile match | Incorrect risk level mapping | Review `FilterFundsByRisk` logic |
| Currency mismatch | Mixed currency in holdings | Ensure all values use same currency |

### Issue 6: "Shariah-compliant funds not found"

**Symptoms:**
- Request with `shariahCompliantOnly: true` returns non-Shariah funds
- Log shows "No Shariah-compliant funds found"

**Explanation:**
The SNB Capital test API may not have the `isShariahCompliant` field populated for all funds. The system gracefully handles this by:
1. Logging a warning
2. Falling back to all matching funds
3. Adding a note "Shariah compliance could not be verified" to recommendations

**Note:** This is expected behavior for the POC. In production, ensure the API returns proper Shariah compliance data.

---

## Monitoring & Observability

### OpenTelemetry Traces

Key activities to monitor:
- `ProfitProjectionWorkflow` - Overall workflow execution
- `CustomerContextEnrichment` - Customer data fetch
- `HistoricalAnalysis` - Fund performance analysis
- `MarketConditionsAnalysis` - Web search for market data
- `FundSelection` - Fund scoring and ranking
- `ResultsAggregation` - Combining parallel results
- `ScenarioBuilding` - Projection scenarios
- `RecommendationGeneration` - Final output

### Metrics to Track

| Metric | Description | Target |
|--------|-------------|--------|
| `projections_completed` | Total successful projections | Growing |
| `projection_execution_time_ms` | End-to-end workflow time | < 5000ms |
| `funds_analyzed` | Number of funds processed | > 5 |
| `existing_customers` | Recognized returning customers | > 0 |
| `market_analysis_count` | Market analyses performed | Growing |

### Log Analysis

Key log patterns to search for:
```bash
# Successful projections
grep "Profit projection completed" logs/app.log

# API errors
grep "SNBCapitalApiException" logs/app.log

# Web search issues
grep "Failed to fetch" logs/app.log

# Performance bottlenecks
grep "execution_time_ms" logs/app.log | awk -F'=' '{print $2}' | sort -n
```

### Health Check Endpoint

Test overall system health:
```bash
curl http://localhost:5000/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": {
    "snb_capital_api": "Healthy",
    "web_search": "Healthy",
    "database": "Healthy"
  }
}
```

---

## Postman Collection

Import the collection from `docs/Complete-API-Tests.postman_collection.json` for pre-configured tests.

### Collection Structure
| Folder | Tests | Description |
|--------|-------|-------------|
| Profit Projection | 10 | Main projection workflow tests |
| Master Agent | 7 | AI orchestrator tests |
| Portfolios | 7 | Portfolio management tests |
| Funds | 5 | Fund search and comparison tests |
| Accounts | 5 | Account management tests |
| Agents | 5 | Direct agent communication tests |
| SNB Capital Direct | 4 | External API validation tests |
| Error Cases | 7 | Error handling tests |

### Collection Variables
| Variable | Default Value | Description |
|----------|---------------|-------------|
| `baseUrl` | `http://localhost:5000` | Backend API URL |
| `snbApiUrl` | `https://snbc-api.onrender.com/snbc/api/v1` | SNB Capital API URL |
| `testCif` | `100000000001` | Default test customer ID |
| `testAccountId` | `ACC001` | Default test account |
| `testPortfolioId` | `PORT001` | Default test portfolio |
| `testFundId` | `FUND001` | Default test fund |

### Running the Collection
1. Import `Complete-API-Tests.postman_collection.json` into Postman
2. Start the backend: `cd src && dotnet run`
3. Run collection or individual folders
4. Check test results in Postman console

---

## Quick Reference

### Valid Test CIFs
| CIF | Description |
|-----|-------------|
| `100000000001` | Customer with portfolios and holdings |
| `100000000002` | Customer with multiple portfolios |
| `100000000005` | Customer with mixed holdings |
| `100000000010` | Customer with Shariah-compliant holdings |
| `100000000015` | Customer with high-risk portfolio |
| `100000000020` | Customer with conservative portfolio |
| `100000000025` | New customer with minimal holdings |
| `100000000030` | Last valid test customer |

### All API Endpoints Summary

| Endpoint | Method | Description |
|----------|--------|-------------|
| **Accounts** | | |
| `/api/Accounts` | GET | List all accounts |
| `/api/Accounts/{id}` | GET | Get account by ID |
| `/api/Accounts/{id}/balance` | GET | AI-powered balance inquiry |
| `/api/Accounts/{id}/deposit` | POST | AI-powered deposit |
| `/api/Accounts/{id}/transactions` | GET | Transaction history |
| **Agents** | | |
| `/api/Agents` | GET | List available agents |
| `/api/Agents/chat` | POST | Chat with specific agent |
| **Funds** | | |
| `/api/Funds` | GET | List all funds |
| `/api/Funds/{id}` | GET | Get fund by ID |
| `/api/Funds/search` | POST | Search with filters |
| `/api/Funds/compare` | POST | Compare multiple funds |
| `/api/Funds/{id}/details` | GET | AI-powered fund analysis |
| **Master Agent** | | |
| `/api/v2/MasterAgent/chat` | POST | Main AI chat |
| `/api/v2/MasterAgent/chat/structured` | POST | Structured response |
| `/api/v2/MasterAgent/chat/multimodal` | POST | With base64 content |
| `/api/v2/MasterAgent/chat/multimodal/upload` | POST | With file upload |
| `/api/v2/MasterAgent/subagents` | GET | List sub-agents |
| `/api/v2/MasterAgent/conversation/{id}` | DELETE | Clear conversation |
| `/api/v2/MasterAgent/conversation/stats` | GET | Conversation statistics |
| **Portfolios** | | |
| `/api/Portfolios` | GET | List portfolios (optional accountId) |
| `/api/Portfolios/{id}` | GET | Get portfolio by ID |
| `/api/Portfolios` | POST | Create portfolio |
| `/api/Portfolios/{id}/details` | GET | AI-powered details |
| `/api/Portfolios/{id}/allocation` | GET | Allocation breakdown |
| `/api/Portfolios/{id}/invest` | POST | Invest in fund |
| **Projection** | | |
| `/api/Projection/calculate` | POST | Main projection |
| `/api/Projection/customer/{id}` | POST | Customer-specific projection |
| `/api/Projection/compare-strategies` | POST | LumpSum vs SIP |
| `/api/Projection/quick-estimate` | GET | Simple calculator |

### Response Time Benchmarks
| Operation | Expected Time | Warning Threshold |
|-----------|---------------|-------------------|
| Full projection (no customer) | 2-4 seconds | > 8 seconds |
| Full projection (with customer) | 3-5 seconds | > 10 seconds |
| Master Agent chat | 1-3 seconds | > 6 seconds |
| Historical analysis | 500-1000ms | > 2 seconds |
| Market conditions analysis | 1-2 seconds | > 4 seconds |
| Fund selection | 200-500ms | > 1 second |
| Account operations | 100-300ms | > 1 second |
| Portfolio operations | 200-500ms | > 1 second |

### Risk Profile Mapping
| Profile | Fund Types | Return Range | Volatility |
|---------|------------|--------------|------------|
| Conservative | Money Market, Fixed Income | 3-6% | Low |
| Moderate | Balanced, Mixed | 5-10% | Medium |
| Aggressive | Equity, Growth | 8-15%+ | High |

---

## Support

For issues not covered in this guide:
1. Check the application logs in detail
2. Review OpenTelemetry traces for the specific request
3. Verify all external API dependencies are accessible
4. Test individual components in isolation before full workflow

**Log Location**: Console output or configure in `appsettings.json`
**Trace Exporter**: Configure OTLP endpoint in `Program.cs`
**Postman Collection**: `docs/Complete-API-Tests.postman_collection.json`

#!/bin/bash

# Quick Test Script for Master Agent API
# This script helps you quickly test the observability implementation

echo "🚀 Master Agent API - Quick Test Script"
echo "========================================"
echo ""

# Check if OpenAI API key is set
if [ -z "$OPENAI_API_KEY" ]; then
    echo "⚠️  WARNING: OPENAI_API_KEY environment variable not set!"
    echo ""
    echo "Please set your OpenAI API key:"
    echo "  export OPENAI_API_KEY='sk-your-key-here'"
    echo ""
    read -p "Enter your OpenAI API key now (or press Enter to exit): " api_key
    
    if [ -z "$api_key" ]; then
        echo "❌ No API key provided. Exiting."
        exit 1
    fi
    
    export OPENAI_API_KEY="$api_key"
    echo "✅ API key set for this session"
    echo ""
fi

# Check if app is already running
if curl -s http://localhost:5000 > /dev/null 2>&1; then
    echo "✅ API is already running at http://localhost:5000"
else
    echo "🔧 Starting API..."
    echo "   (This will run in the background - check console for OpenTelemetry output)"
    echo ""
    cd src
    dotnet run > ../api-output.log 2>&1 &
    API_PID=$!
    echo "   API PID: $API_PID"
    
    # Wait for API to start
    echo "   Waiting for API to be ready..."
    for i in {1..30}; do
        if curl -s http://localhost:5000 > /dev/null 2>&1; then
            echo "   ✅ API is ready!"
            break
        fi
        sleep 1
        echo -n "."
    done
    echo ""
    cd ..
fi

echo ""
echo "🎯 Running Test Scenarios"
echo "========================="
echo ""

# Test 1: Get available agents
echo "📋 Test 1: Get Available Sub-Agents"
echo "   GET /api/v2/masteragent/agents"
curl -s http://localhost:5000/api/v2/masteragent/agents | jq '.' || curl -s http://localhost:5000/api/v2/masteragent/agents
echo ""
echo ""

# Test 2: Simple query
echo "💬 Test 2: Simple Query (Single Sub-Agent)"
echo "   Message: 'What mutual funds are available?'"
curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What mutual funds are available?",
    "conversationId": "test-simple"
  }' | jq '.' || curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What mutual funds are available?", "conversationId": "test-simple"}'
echo ""
echo ""

# Test 3: Portfolio creation
echo "📊 Test 3: Portfolio Creation"
echo "   Message: 'Create a balanced portfolio for retirement'"
curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Create a balanced portfolio for retirement",
    "conversationId": "test-portfolio"
  }' | jq '.' || curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Create a balanced portfolio for retirement", "conversationId": "test-portfolio"}'
echo ""
echo ""

# Test 4: Multi-agent coordination
echo "🎭 Test 4: Multi-Agent Coordination"
echo "   Message: 'Check balance, create portfolio, and suggest funds'"
curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Check my balance, create a portfolio, and suggest the best mutual funds",
    "conversationId": "test-multi"
  }' | jq '.' || curl -s -X POST http://localhost:5000/api/v2/masteragent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Check my balance, create a portfolio, and suggest the best mutual funds", "conversationId": "test-multi"}'
echo ""
echo ""

echo "✅ Tests Complete!"
echo ""
echo "📊 Observability Check:"
echo "   1. Check console output for OpenTelemetry traces"
echo "   2. Look for Activity traces with 'InvestmentBanking.MasterOrchestrator'"
echo "   3. Look for metrics: orchestrator.delegations, orchestrator.delegation.duration"
echo "   4. Verify sub-agent traces: InvestmentBanking.SubAgents.*"
echo ""
echo "📖 For more details, see:"
echo "   - TESTING.md - Comprehensive testing guide"
echo "   - OBSERVABILITY.md - Observability documentation"
echo "   - MasterAgent-Tests.postman_collection.json - Postman collection"
echo ""
echo "🌐 Useful URLs:"
echo "   - API Root: http://localhost:5000"
echo "   - Swagger UI: http://localhost:5000/swagger"
echo ""

# Option to view logs
read -p "View API logs? (y/n): " view_logs
if [ "$view_logs" = "y" ]; then
    echo ""
    echo "📜 API Logs (last 50 lines):"
    echo "=============================="
    tail -n 50 api-output.log
    echo ""
    echo "💡 Tip: Run 'tail -f api-output.log' to watch logs in real-time"
fi

echo ""
echo "🎉 Happy Testing!"

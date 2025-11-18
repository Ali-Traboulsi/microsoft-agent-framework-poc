#!/bin/bash

echo "================================================"
echo "  Banking Investment Agent System - Quick Start"
echo "================================================"
echo

# Check if OPENAI_API_KEY is set
if [ -z "$OPENAI_API_KEY" ]; then
    echo "ERROR: OPENAI_API_KEY environment variable is not set!"
    echo
    echo "Please set it first:"
    echo "  export OPENAI_API_KEY='your-api-key-here'"
    echo
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "Starting API Server..."
echo
cd "$PROJECT_ROOT/src"
dotnet run &
API_PID=$!

echo "Waiting for API to start (10 seconds)..."
sleep 10

echo
echo "Starting Frontend..."
echo
cd "$PROJECT_ROOT/frontend"
pnpm run dev &
FRONTEND_PID=$!

echo
echo "================================================"
echo "  Both servers are running!"
echo "================================================"
echo
echo "API Server:     http://localhost:5000"
echo "Swagger UI:     http://localhost:5000/swagger"
echo "Frontend UI:    http://localhost:3000"
echo
echo "Press Ctrl+C to stop both servers..."
echo

# Wait for interrupt
trap "kill $API_PID $FRONTEND_PID 2>/dev/null; echo; echo 'Servers stopped.'; exit" INT TERM

wait

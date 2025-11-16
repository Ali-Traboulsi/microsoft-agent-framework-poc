#!/bin/bash

# Setup script for Banking Investment Agent Framework

echo "🔧 Setting up Banking Investment Agent Framework..."
echo ""

# Check if .env file exists
if [ -f .env ]; then
    echo "⚠️  .env file already exists. Skipping..."
else
    echo "📝 Creating .env file from template..."
    cp .env.example .env
    echo "✓ .env file created"
    echo ""
    echo "⚠️  IMPORTANT: Edit .env file and add your OpenAI API key"
    echo "   Get your API key from: https://platform.openai.com/api-keys"
    echo ""
fi

# Check if OPENAI_API_KEY is set in environment
if [ -z "$OPENAI_API_KEY" ]; then
    echo "⚠️  OPENAI_API_KEY environment variable not set"
    echo "   You can either:"
    echo "   1. Set it in your .env file (recommended for development)"
    echo "   2. Set it as a system environment variable"
    echo "   3. Export it in your shell: export OPENAI_API_KEY=your_key_here"
    echo ""
else
    echo "✓ OPENAI_API_KEY environment variable is set"
    echo ""
fi

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9.0 SDK"
    echo "   Download from: https://dotnet.microsoft.com/download"
    exit 1
else
    echo "✓ .NET SDK found: $(dotnet --version)"
fi

# Check Node.js
if ! command -v node &> /dev/null; then
    echo "❌ Node.js not found. Please install Node.js 18+"
    echo "   Download from: https://nodejs.org/"
    exit 1
else
    echo "✓ Node.js found: $(node --version)"
fi

# Restore .NET packages
echo ""
echo "📦 Restoring .NET packages..."
dotnet restore

# Install npm packages
echo ""
echo "📦 Installing frontend packages..."
cd frontend
npm install
cd ..

echo ""
echo "═══════════════════════════════════════════════════════════"
echo "   ✅ Setup Complete!"
echo "═══════════════════════════════════════════════════════════"
echo ""
echo "Next steps:"
echo "  1. Edit .env file and add your OpenAI API key"
echo "  2. Run: ./start.sh (or start.bat on Windows)"
echo ""
echo "Documentation: See API_UI_README.md for detailed instructions"
echo ""

@echo off
echo ================================================
echo   Banking Investment Agent System - Quick Start
echo ================================================
echo.

REM Check if OPENAI_API_KEY is set
if "%OPENAI_API_KEY%"=="" (
    echo ERROR: OPENAI_API_KEY environment variable is not set!
    echo.
    echo Please set it first:
    echo   set OPENAI_API_KEY=your-api-key-here
    echo.
    pause
    exit /b 1
)

echo Starting API Server...
echo.
start "Agent API Server" cmd /k "cd /d "%~dp0" && dotnet run"

echo Waiting for API to start (10 seconds)...
timeout /t 10 /nobreak > nul

echo.
echo Starting Frontend...
echo.
start "Agent UI Frontend" cmd /k "cd /d "%~dp0frontend" && npm run dev"

echo.
echo ================================================
echo   Both servers are starting!
echo ================================================
echo.
echo API Server:     http://localhost:5000
echo Swagger UI:     http://localhost:5000/swagger
echo Frontend UI:    http://localhost:3000
echo.
echo Press any key to stop both servers...
pause > nul

taskkill /FI "WINDOWTITLE eq Agent API Server*" /T /F
taskkill /FI "WINDOWTITLE eq Agent UI Frontend*" /T /F

echo.
echo Servers stopped.
pause

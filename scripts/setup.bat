@echo off
REM Setup script for Banking Investment Agent Framework

echo ========================================================
echo    Setting up Banking Investment Agent Framework
echo ========================================================
echo.

REM Check if .env file exists
if exist .env (
    echo WARNING: .env file already exists. Skipping...
) else (
    echo Creating .env file from template...
    copy .env.example .env
    echo .env file created
    echo.
    echo IMPORTANT: Edit .env file and add your OpenAI API key
    echo Get your API key from: https://platform.openai.com/api-keys
    echo.
)

REM Check if OPENAI_API_KEY is set
if "%OPENAI_API_KEY%"=="" (
    echo WARNING: OPENAI_API_KEY environment variable not set
    echo You can either:
    echo   1. Set it in your .env file (recommended for development^)
    echo   2. Set it as a system environment variable
    echo   3. Set it for this session: set OPENAI_API_KEY=your_key_here
    echo.
) else (
    echo OPENAI_API_KEY environment variable is set
    echo.
)

REM Check .NET SDK
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found. Please install .NET 9.0 SDK
    echo Download from: https://dotnet.microsoft.com/download
    exit /b 1
) else (
    for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
    echo .NET SDK found: %DOTNET_VERSION%
)

REM Check Node.js
where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Node.js not found. Please install Node.js 18+
    echo Download from: https://nodejs.org/
    exit /b 1
) else (
    for /f "tokens=*" %%i in ('node --version') do set NODE_VERSION=%%i
    echo Node.js found: %NODE_VERSION%
)

REM Restore .NET packages
echo.
echo Restoring .NET packages...
dotnet restore

REM Install npm packages
echo.
echo Installing frontend packages...
cd frontend
call npm install
cd ..

echo.
echo ========================================================
echo    Setup Complete!
echo ========================================================
echo.
echo Next steps:
echo   1. Edit .env file and add your OpenAI API key
echo   2. Run: start.bat
echo.
echo Documentation: See API_UI_README.md for detailed instructions
echo.
pause

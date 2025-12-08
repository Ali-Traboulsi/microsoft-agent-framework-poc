# ==========================================
# Backend Dockerfile - .NET 9 API
# Multi-stage build for optimized image size
# ==========================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the project file first for dependency caching
COPY src/AgentFrameworkQuickStart.csproj ./src/

# Restore dependencies (creates fresh obj folder in container)
RUN dotnet restore ./src/AgentFrameworkQuickStart.csproj

# Copy source files (excluding bin/obj via .dockerignore)
COPY src/ ./src/

# Build and publish the project file directly (not the solution)
WORKDIR /src/src
RUN dotnet publish AgentFrameworkQuickStart.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

# Copy published app
COPY --from=build /app/publish .

# Create Data directory for SQLite database (capital D to match app code)
# Must be done before switching to non-root user
RUN mkdir -p /app/Data && chown -R appuser:appuser /app/Data && chown -R appuser:appuser /app

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
# PORT is set by Railway dynamically - use it, fallback to 5000 for local dev
ENV PORT=5000
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Switch to non-root user
USER appuser

# Expose port (documentation only, Railway uses PORT env var)
EXPOSE 5000

# Health check - use PORT env var
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:${PORT}/health || exit 1

# Entry point - set ASPNETCORE_URLS at runtime to use dynamic PORT
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} dotnet AgentFrameworkQuickStart.dll"]

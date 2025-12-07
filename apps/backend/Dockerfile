# ==========================================
# Backend Dockerfile - .NET 9 API
# Multi-stage build for optimized image size
# ==========================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/AgentFrameworkQuickStart.csproj ./src/
RUN dotnet restore ./src/AgentFrameworkQuickStart.csproj

# Copy all source files
COPY . .

# Build and publish
WORKDIR /src/src
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

# Copy published app
COPY --from=build /app/publish .

# Create data directory for SQLite database
RUN mkdir -p /app/data && chown -R appuser:appuser /app/data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "AgentFrameworkQuickStart.dll"]

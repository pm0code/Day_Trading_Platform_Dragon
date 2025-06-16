# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5003

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TradingPlatform.StrategyEngine/TradingPlatform.StrategyEngine.csproj", "TradingPlatform.StrategyEngine/"]
COPY ["TradingPlatform.Core/TradingPlatform.Core.csproj", "TradingPlatform.Core/"]
COPY ["TradingPlatform.Messaging/TradingPlatform.Messaging.csproj", "TradingPlatform.Messaging/"]

RUN dotnet restore "TradingPlatform.StrategyEngine/TradingPlatform.StrategyEngine.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TradingPlatform.StrategyEngine"
RUN dotnet build "TradingPlatform.StrategyEngine.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TradingPlatform.StrategyEngine.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install tools for strategy execution monitoring
RUN apt-get update && apt-get install -y redis-tools curl htop && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser
RUN chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .

# Environment variables for strategy execution
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5003
ENV ASPNETCORE_HTTP_PORTS=5003
ENV DOTNET_GCServer=1
ENV DOTNET_GCConcurrent=1
ENV DOTNET_GCLatencyMode=SustainedLowLatency

# Strategy engine specific settings
ENV STRATEGY_EXECUTION_TIMEOUT=45000
ENV GOLDEN_RULES_ENABLED=true
ENV MOMENTUM_STRATEGY_ENABLED=true
ENV GAP_STRATEGY_ENABLED=true
ENV MAX_CONCURRENT_STRATEGIES=10

# Health check for strategy engine
HEALTHCHECK --interval=30s --timeout=10s --start-period=45s --retries=3 \
  CMD curl -f http://localhost:5003/health || exit 1

ENTRYPOINT ["dotnet", "TradingPlatform.StrategyEngine.dll"]
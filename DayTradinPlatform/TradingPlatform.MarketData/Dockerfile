# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5002

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TradingPlatform.MarketData/TradingPlatform.MarketData.csproj", "TradingPlatform.MarketData/"]
COPY ["TradingPlatform.Core/TradingPlatform.Core.csproj", "TradingPlatform.Core/"]
COPY ["TradingPlatform.Messaging/TradingPlatform.Messaging.csproj", "TradingPlatform.Messaging/"]

RUN dotnet restore "TradingPlatform.MarketData/TradingPlatform.MarketData.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TradingPlatform.MarketData"
RUN dotnet build "TradingPlatform.MarketData.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TradingPlatform.MarketData.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install Redis tools for Redis Streams connectivity
RUN apt-get update && apt-get install -y redis-tools curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser
RUN chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .

# Environment variables for high-performance market data
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5002
ENV ASPNETCORE_HTTP_PORTS=5002
ENV DOTNET_GCServer=1
ENV DOTNET_GCConcurrent=1
ENV DOTNET_GCRetainVM=1

# Market data specific settings
ENV REDIS_STREAMS_ENABLED=true
ENV MARKET_DATA_CACHE_SIZE=1000000
ENV MARKET_DATA_BATCH_SIZE=100

# Health check for market data service
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:5002/health || exit 1

ENTRYPOINT ["dotnet", "TradingPlatform.MarketData.dll"]
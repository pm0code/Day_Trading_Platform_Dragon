# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5005

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TradingPlatform.PaperTrading/TradingPlatform.PaperTrading.csproj", "TradingPlatform.PaperTrading/"]
COPY ["TradingPlatform.Core/TradingPlatform.Core.csproj", "TradingPlatform.Core/"]
COPY ["TradingPlatform.Messaging/TradingPlatform.Messaging.csproj", "TradingPlatform.Messaging/"]

RUN dotnet restore "TradingPlatform.PaperTrading/TradingPlatform.PaperTrading.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TradingPlatform.PaperTrading"
RUN dotnet build "TradingPlatform.PaperTrading.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TradingPlatform.PaperTrading.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install tools for paper trading simulation
RUN apt-get update && apt-get install -y redis-tools curl jq && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser
RUN chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .

# Environment variables for paper trading
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5005
ENV ASPNETCORE_HTTP_PORTS=5005
ENV DOTNET_GCServer=1
ENV DOTNET_GCConcurrent=1
ENV DOTNET_GCLatencyMode=SustainedLowLatency

# Paper trading specific settings
ENV INITIAL_CAPITAL=100000
ENV COMMISSION_RATE=0.001
ENV SLIPPAGE_RATE=0.0005
ENV MARKET_IMPACT_ENABLED=true
ENV ORDER_BOOK_DEPTH=10
ENV EXECUTION_DELAY_MS=5

# Health check for paper trading service
HEALTHCHECK --interval=25s --timeout=8s --start-period=35s --retries=3 \
  CMD curl -f http://localhost:5005/health || exit 1

ENTRYPOINT ["dotnet", "TradingPlatform.PaperTrading.dll"]
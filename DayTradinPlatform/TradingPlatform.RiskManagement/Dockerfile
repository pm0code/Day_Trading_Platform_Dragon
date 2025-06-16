# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5004

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TradingPlatform.RiskManagement/TradingPlatform.RiskManagement.csproj", "TradingPlatform.RiskManagement/"]
COPY ["TradingPlatform.Core/TradingPlatform.Core.csproj", "TradingPlatform.Core/"]
COPY ["TradingPlatform.Messaging/TradingPlatform.Messaging.csproj", "TradingPlatform.Messaging/"]

RUN dotnet restore "TradingPlatform.RiskManagement/TradingPlatform.RiskManagement.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TradingPlatform.RiskManagement"
RUN dotnet build "TradingPlatform.RiskManagement.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TradingPlatform.RiskManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install monitoring tools for risk management
RUN apt-get update && apt-get install -y redis-tools curl netcat-openbsd && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser
RUN chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .

# Environment variables for risk management
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5004
ENV ASPNETCORE_HTTP_PORTS=5004
ENV DOTNET_GCServer=1
ENV DOTNET_GCConcurrent=1
ENV DOTNET_GCLatencyMode=SustainedLowLatency

# Risk management specific settings
ENV MAX_POSITION_SIZE=1000000
ENV MAX_DAILY_LOSS=50000
ENV VAR_CONFIDENCE_LEVEL=0.95
ENV RISK_CHECK_INTERVAL=1000
ENV PDT_COMPLIANCE_ENABLED=true
ENV MAXIMUM_LEVERAGE=4.0

# Health check for risk management service
HEALTHCHECK --interval=20s --timeout=8s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:5004/health || exit 1

ENTRYPOINT ["dotnet", "TradingPlatform.RiskManagement.dll"]
# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["TradingPlatform.Gateway/TradingPlatform.Gateway.csproj", "TradingPlatform.Gateway/"]
COPY ["TradingPlatform.Core/TradingPlatform.Core.csproj", "TradingPlatform.Core/"]
COPY ["TradingPlatform.Messaging/TradingPlatform.Messaging.csproj", "TradingPlatform.Messaging/"]
COPY ["TradingPlatform.WindowsOptimization/TradingPlatform.WindowsOptimization.csproj", "TradingPlatform.WindowsOptimization/"]

RUN dotnet restore "TradingPlatform.Gateway/TradingPlatform.Gateway.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/TradingPlatform.Gateway"
RUN dotnet build "TradingPlatform.Gateway.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "TradingPlatform.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Install Redis tools for debugging (optional)
RUN apt-get update && apt-get install -y redis-tools && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser
RUN chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .

# Environment variables for configuration
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
ENV ASPNETCORE_HTTP_PORTS=5000
ENV ASPNETCORE_HTTPS_PORTS=5001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "TradingPlatform.Gateway.dll"]
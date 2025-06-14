# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Structure

This is a C# .NET 8.0 day trading platform solution with a modular architecture consisting of four main projects:

- **TradingPlatform.Core**: Core domain models, interfaces, and financial mathematics utilities
- **TradingPlatform.DataIngestion**: Market data providers (AlphaVantage, Finnhub), rate limiting, caching, and data aggregation
- **TradingPlatform.Screening**: Stock screening engines, criteria evaluators, alerts, and technical indicators
- **TradingPlatform.Utilities**: Shared utilities and Roslyn scripting support

**Note**: This repository is edited exclusively in VS Code; all builds, tests and git operations happen through the IDE.

## Build Commands

**Navigate to solution directory first:**
```bash
cd DayTradinPlatform
```

**Common build commands:**
```bash
# Restore packages
dotnet restore

# Build solution (Debug)
dotnet build

# Build solution (Release)
dotnet build --configuration Release

# Run application
dotnet run

# Clean build artifacts
dotnet clean
```

**Platform-specific builds (solution is configured for x64):**
```bash
dotnet build --configuration Release --runtime win-x64
```

## Architecture Overview

### Data Flow Architecture
1. **Data Ingestion Layer**: Pulls market data from external APIs (AlphaVantage, Finnhub) with rate limiting and caching
2. **Core Domain Layer**: Contains financial models and mathematical calculations using `System.Decimal` for precision
3. **Screening Engine**: Evaluates stocks against multiple criteria (price, volume, volatility, gaps, news)
4. **Alert System**: Provides notifications based on screening results

### Key Design Patterns
- **Dependency Injection**: All services registered in `Program.cs` using Microsoft.Extensions.DependencyInjection
- **Provider Pattern**: Market data providers implement common interfaces for different data sources
- **Rate Limiting**: API calls are throttled using custom rate limiter implementations
- **Caching**: Market data is cached to reduce API calls and improve performance

### Financial Precision Standards
- **Critical**: All monetary values and financial calculations MUST use `System.Decimal` type, never `double` or `float`
- Financial calculation standards are documented in `TradingPlatform.Core/Documentation/FinancialCalculationStandards.md`
- Custom math utilities should be created for `decimal` types when `System.Math` functions return `double`

## Key Interfaces

- `IMarketDataProvider`: Contract for market data sources
- `IDataIngestionService`: Data collection orchestration
- `IScreeningEngine`: Stock screening logic
- `IAlertService`: Alert and notification management
- `IRateLimiter`: API rate limiting
- `IMarketDataAggregator`: Combines data from multiple sources

## Configuration

External API integrations require configuration for:
- AlphaVantage API keys and endpoints
- Finnhub API keys and endpoints  
- Rate limiting parameters
- Cache settings

## Testing

**Note**: No test projects currently exist in the solution. The `FinancialPrecisionTests.cs` file exists but contains only an empty class placeholder.

To add testing:
1. Create test projects for each main project
2. Add test framework references (xUnit, NUnit, or MSTest)
3. Implement unit tests especially for financial calculations

## Dependencies

Key external dependencies:
- **Newtonsoft.Json**: JSON serialization
- **Serilog**: Structured logging
- **RestSharp**: HTTP client for API calls
- **System.Reactive**: Reactive programming
- **Microsoft.Extensions.***: Dependency injection, caching, logging
- **Microsoft.CodeAnalysis**: Roslyn compiler services for utilities

## MainDocs

Key documentation files in the MainDocs directory:

- **Day Trading Stock Recommendation Platform - PRD.md**: Product Requirements Document defining platform specifications and features
- **FinancialCalculationStandards.md**: Detailed standards for financial calculations and decimal precision requirements
- **High Performance Stock Day Trading Platform.docx**: Technical architecture and performance requirements documentation
- **Professional Multi-Screen Trading Systems_ Architecture.md**: Multi-screen trading system architecture and design patterns

### MainDocs – Trading "Golden Rules"

- **The_Complete_Day_Trading_Reference_Guide_ Golden_Rules.md**: Comprehensive reference guide covering psychological frameworks, technical methodologies, risk management strategies, and the fundamental golden rules for successful day trading operations
- **The_12_Golden_Rulesof_Day_Trading.md**: Synthesized framework of 12 core trading principles including capital preservation, trading discipline, loss management, and systematic approaches based on multiple authoritative sources

## Financial Precision Standards

**CRITICAL REMINDER**: ALL monetary values must use System.Decimal; never double/float.

• Follow every rule in the two 'Golden Rules' guides; treat violations as test failures.

## VS Code Workflow

- **Ctrl + Esc**: Quick command palette access
- **Alt + Ctrl + K**: Git operations and source control
- **Integrated Terminal**: Use dotnet CLI build commands directly in VS Code
- **Build Tasks**: Configure tasks.json for automated dotnet build/restore/clean operations
- **Debugging**: F5 to start debugging, breakpoints for financial calculation validation

## Development Notes

- Solution targets .NET 8.0 and x64 platform exclusively
- Uses nullable reference types (`<Nullable>enable`)
- All projects use implicit usings
- The `TradingPlatform.Utilities` project includes PowerShell scripts for service registration automation
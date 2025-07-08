# MarketAnalyzer

High-performance day trading analysis & recommendation system for Windows 11 x64.

## Overview

MarketAnalyzer is a single-user desktop application built with C#/.NET 8/9 that provides:
- Real-time market analysis
- Technical indicators
- AI-driven insights
- Trading recommendations

## Architecture

Built using Clean Architecture principles with:
- **Foundation**: Canonical patterns and base classes
- **Domain**: Core business logic and entities
- **Infrastructure**: External integrations (Finnhub API, LiteDB, ML.NET)
- **Application**: Use cases and orchestration
- **Presentation**: WinUI 3 desktop application

## Technology Stack

- .NET 8/9
- WinUI 3 with MVVM
- Finnhub API for market data
- ML.NET + ONNX Runtime for AI/ML
- LiteDB for storage
- Skender.Stock.Indicators for technical analysis

## Getting Started

1. Clone the repository
2. Ensure .NET 8 SDK is installed
3. Run `dotnet restore` to install dependencies
4. Configure API keys in appsettings.json
5. Run `dotnet build` to build the solution

## Documentation

- [Engineering Design Document](../MainDocs/After_Pivot/EDD_MarketAnalyzer_Engineering_Design_Document_2025-07-07.md)
- [Master Todo List](../MainDocs/After_Pivot/MasterTodoList_MarketAnalyzer_2025-07-07.md)
- [Session Notes](SESSION_NOTES.md)

## Development

See [CLAUDE.md](CLAUDE.md) for AI development guidance and mandatory standards.

## License

Private project - All rights reserved
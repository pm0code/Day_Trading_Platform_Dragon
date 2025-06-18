# Day Trading Platform - Comprehensive File Structure Index

## Overview
This comprehensive index provides quick reference to all files, directories, and components in the Day Trading Platform project located at `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/`.

## Solution Structure

### Root Level
- **Solution File**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/DayTradinPlatform.sln`
- **Main Program**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/Program.cs`
- **Architecture Documentation**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/ARCHITECTURE.md`

## Project Index by Category

### 1. CORE FOUNDATION PROJECTS

#### TradingPlatform.Core
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/`
**Purpose**: Core domain models, financial mathematics, and fundamental interfaces

**Key Files**:
- **Project File**: `TradingPlatform.Core.csproj`
- **Financial Math**: `Mathematics/FinancialMath.cs`
- **Financial Tests**: `Mathematics/FinancialPrecisionTests.cs`
- **Documentation**: `Documentation/FinancialCalculationStandards.md`

**Models**:
- `Models/ApiResponse.cs`
- `Models/CompanyData.cs`
- `Models/CompanyFinancials.cs`
- `Models/CompanyProfile.cs`
- `Models/DailyData.cs`
- `Models/MarketConfiguration.cs`
- `Models/MarketData.cs`
- `Models/NewsItem.cs`
- `Models/SentimentData.cs`
- `Models/TradingCriteria.cs`

**Interfaces**:
- `Interfaces/ILogger.cs`
- `Interfaces/IMarketDataProvider.cs`

**Performance Components**:
- `Performance/HighPerformanceThreadManager.cs`
- `Performance/MemoryOptimizer.cs`
- `Performance/PerformanceMonitor.cs`

**Observability**:
- `Observability/InfrastructureMetrics.cs`
- `Observability/ObservabilityEnricher.cs`
- `Observability/OpenTelemetryInstrumentation.cs`
- `Observability/TradingMetrics.cs`

#### TradingPlatform.Common
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Common/`
**Purpose**: Shared utilities, extensions, and mathematical functions

**Key Files**:
- **Project File**: `TradingPlatform.Common.csproj`
- **Constants**: `Constants/TradingConstants.cs`
- **Extensions**: `Extensions/DateTimeExtensions.cs`, `Extensions/DecimalExtensions.cs`
- **Mathematics**: `Mathematics/TradingMath.cs`
- **Validation**: `Validation/TradingValidationExtensions.cs`

#### TradingPlatform.Foundation
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Foundation/`
**Purpose**: Foundational abstractions and service contracts

**Key Files**:
- **Project File**: `TradingPlatform.Foundation.csproj`
- **Enums**: `Enums/TradingEnums.cs`
- **Models**: `Models/OperationContext.cs`, `Models/TradingResult.cs`

**Interfaces**:
- `Interfaces/IHealthCheck.cs`
- `Interfaces/IRetryPolicy.cs`
- `Interfaces/ITradingCache.cs`
- `Interfaces/ITradingConfiguration.cs`
- `Interfaces/ITradingService.cs`

### 2. DATA AND MARKET ACCESS PROJECTS

#### TradingPlatform.DataIngestion
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DataIngestion/`
**Purpose**: Market data providers, rate limiting, caching, and data aggregation

**Key Files**:
- **Project File**: `TradingPlatform.DataIngestion.csproj`

**Configuration**:
- `Configuration/CacheConfig.cs`
- `Configuration/DataIngestionConfig.cs`
- `Configuration/FinnhubConfiguration.cs`

**Interfaces**:
- `Interfaces/IAlphaVantageProvider.cs`
- `Interfaces/IDataIngestionService.cs`
- `Interfaces/IFinnhubClient.cs`
- `Interfaces/IFinnhubProvider.cs`
- `Interfaces/IFinnhubService.cs`
- `Interfaces/IMarketDataAggregator.cs`
- `Interfaces/IRateLimiter.cs`

**Models**:
- `Models/AlphaVantageCandleResponse.cs`
- `Models/AlphaVantageSupportingTypes.cs`
- `Models/ApiConfiguration.cs`
- `Models/FinnhubCandleResponse.cs`
- `Models/FinnhubSupportingTypes.cs`

**Providers**:
- `Providers/AlphaVantageProvider.cs`
- `Providers/FinnhubProvider.cs`
- `Providers/FinnhubProvider.cs.backup`
- `Providers/MarketDataAggregator.cs`

**Services**:
- `Services/CacheService.cs`
- `Services/DataIngestionService.cs`

**Rate Limiting**:
- `RateLimiting/ApiRateLimiter.cs`

**Validation**:
- `Validation/FinnhubValidators.cs`

#### TradingPlatform.MarketData
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/`
**Purpose**: Market data service with caching and subscription management

**Key Files**:
- **Project File**: `TradingPlatform.MarketData.csproj`
- **Program**: `Program.cs`
- **Configuration**: `appsettings.json`, `appsettings.Development.json`
- **Launch Settings**: `Properties/launchSettings.json`
- **HTTP File**: `TradingPlatform.MarketData.http`

**Services**:
- `Services/IMarketDataService.cs`
- `Services/MarketDataCache.cs`
- `Services/MarketDataService.cs`
- `Services/SubscriptionManager.cs`

### 3. TRADING ENGINE PROJECTS

#### TradingPlatform.StrategyEngine
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/`
**Purpose**: Trading strategy execution and management

**Key Files**:
- **Project File**: `TradingPlatform.StrategyEngine.csproj`
- **Program**: `Program.cs`
- **Configuration**: `appsettings.json`, `appsettings.Development.json`
- **Launch Settings**: `Properties/launchSettings.json`
- **HTTP File**: `TradingPlatform.StrategyEngine.http`

**Models**:
- `Models/StrategyModels.cs`

**Services**:
- `Services/IStrategyExecutionService.cs`
- `Services/PerformanceTracker.cs`
- `Services/SignalProcessor.cs`
- `Services/StrategyExecutionService.cs`
- `Services/StrategyManager.cs`

**Strategies**:
- `Strategies/GapStrategy.cs`
- `Strategies/GoldenRulesStrategy.cs`
- `Strategies/IStrategyBase.cs`
- `Strategies/MomentumStrategy.cs`

#### TradingPlatform.FixEngine
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.FixEngine/`
**Purpose**: FIX protocol implementation for direct market access

**Key Files**:
- **Project File**: `TradingPlatform.FixEngine.csproj`
- **Class**: `Class1.cs`

**Core**:
- `Core/FixEngine.cs`
- `Core/FixSession.cs`
- `Core/MarketDataManager.cs`
- `Core/OrderManager.cs`

**Interfaces**:
- `Interfaces/IFixEngine.cs`

**Models**:
- `Models/FixMessage.cs`
- `Models/FixMessageTypes.cs`

**Trading**:
- `Trading/OrderRouter.cs`

### 4. SCREENING AND ALERTS PROJECTS

#### TradingPlatform.Screening
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/`
**Purpose**: Stock screening engines, criteria evaluators, and alerts

**Key Files**:
- **Project File**: `TradingPlatform.Screening.csproj`

**Interfaces**:
- `Interfaces/IAlertService.cs`
- `Interfaces/ICriteriaEvaluator.cs`
- `Interfaces/IScreeningEngine.cs`

**Models**:
- `Models/AlertConfiguration.cs`
- `Models/CriteriaResult.cs`
- `Models/ScreeningRequest.cs`
- `Models/ScreeningResult.cs`
- `Models/ScreeningSummary.cs`

**Criteria**:
- `Criteria/GapCriteria.cs`
- `Criteria/NewsCriteria.cs`
- `Criteria/PriceCriteria.cs`
- `Criteria/VolatilityCriteria.cs`
- `Criteria/VolumeCriteria.cs`

**Engines**:
- `Engines/RealTimeScreeningEngine.cs`
- `Engines/ScreeningOrchestrator.cs`

**Alerts**:
- `Alerts/AlertService.cs`
- `Alerts/NotificationService.cs`

**Indicators**:
- `Indicators/TechnicalIndicators.cs`
- `Indicators/VolumeIndicators.cs`

**Services**:
- `Services/CriteriaConfigurationService.cs`
- `Services/ScreeningHistoryService.cs`

### 5. RISK AND PAPER TRADING PROJECTS

#### TradingPlatform.RiskManagement
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/`
**Purpose**: Risk management and compliance monitoring

**Key Files**:
- **Project File**: `TradingPlatform.RiskManagement.csproj`
- **Program**: `Program.cs`
- **Configuration**: `appsettings.json`, `appsettings.Development.json`
- **Launch Settings**: `Properties/launchSettings.json`

**Models**:
- `Models/RiskModels.cs`

**Services**:
- `Services/ComplianceMonitor.cs`
- `Services/IRiskManagementService.cs`
- `Services/MockMessageBus.cs`
- `Services/PositionMonitor.cs`
- `Services/RiskAlertService.cs`
- `Services/RiskCalculator.cs`
- `Services/RiskManagementService.cs`
- `Services/RiskMonitoringBackgroundService.cs`

#### TradingPlatform.PaperTrading
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/`
**Purpose**: Paper trading simulation and backtesting

**Key Files**:
- **Project File**: `TradingPlatform.PaperTrading.csproj`
- **Program**: `Program.cs`
- **Configuration**: `appsettings.json`, `appsettings.Development.json`
- **Launch Settings**: `Properties/launchSettings.json`

**Models**:
- `Models/PaperTradingModels.cs`

**Services**:
- `Services/ExecutionAnalytics.cs`
- `Services/IPaperTradingService.cs`
- `Services/MockMessageBus.cs`
- `Services/OrderBookSimulator.cs`
- `Services/OrderExecutionEngine.cs`
- `Services/OrderProcessingBackgroundService.cs`
- `Services/PaperTradingService.cs`
- `Services/PortfolioManager.cs`
- `Services/SlippageCalculator.cs`

### 6. INFRASTRUCTURE PROJECTS

#### TradingPlatform.Database
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Database/`
**Purpose**: Database context and data models

**Key Files**:
- **Project File**: `TradingPlatform.Database.csproj`
- **Class**: `Class1.cs`

**Context**:
- `Context/TradingDbContext.cs`

**Models**:
- `Models/ExecutionRecord.cs`
- `Models/MarketDataRecord.cs`
- `Models/PerformanceMetric.cs`

**Services**:
- `Services/HighPerformanceDataService.cs`

#### TradingPlatform.Logging
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Logging/`
**Purpose**: Structured logging and performance monitoring

**Key Files**:
- **Project File**: `TradingPlatform.Logging.csproj`

**Configuration**:
- `Configuration/LoggingConfiguration.cs`

**Interfaces**:
- `Interfaces/ITradingLogger.cs`

**Services**:
- `Services/PerformanceLogger.cs`
- `Services/TradingLogger.cs`

#### TradingPlatform.Messaging
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/`
**Purpose**: Event-driven messaging and communication

**Key Files**:
- **Project File**: `TradingPlatform.Messaging.csproj`

**Events**:
- `Events/TradingEvent.cs`

**Extensions**:
- `Extensions/ServiceCollectionExtensions.cs`

**Interfaces**:
- `Interfaces/IMessageBus.cs`

**Services**:
- `Services/RedisMessageBus.cs`

#### TradingPlatform.Gateway
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/`
**Purpose**: API gateway and service orchestration

**Key Files**:
- **Project File**: `TradingPlatform.Gateway.csproj`
- **Program**: `Program.cs`
- **Configuration**: `appsettings.json`, `appsettings.Development.json`
- **Launch Settings**: `Properties/launchSettings.json`

**Services**:
- `Services/GatewayOrchestrator.cs`
- `Services/HealthMonitor.cs`
- `Services/IGatewayOrchestrator.cs`
- `Services/IHealthMonitor.cs`
- `Services/IProcessManager.cs`
- `Services/ProcessManager.cs`

### 7. USER INTERFACE PROJECTS

#### TradingPlatform.TradingApp
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.TradingApp/`
**Purpose**: WinUI 3 multi-screen trading application

**Key Files**:
- **Project File**: `TradingPlatform.TradingApp.csproj`
- **Solution File**: `TradingPlatform.TradingApp.sln`
- **App**: `App.xaml`, `App.xaml.cs`
- **Imports**: `Imports.cs`
- **Manifest**: `Package.appxmanifest`, `app.manifest`
- **Launch Settings**: `Properties/launchSettings.json`
- **Publish Profile**: `Properties/PublishProfiles/win-x64.pubxml`

**Assets**:
- `Assets/LockScreenLogo.scale-200.png`
- `Assets/SplashScreen.scale-200.png`
- `Assets/Square150x150Logo.scale-200.png`
- `Assets/Square44x44Logo.scale-200.png`
- `Assets/Square44x44Logo.targetsize-24_altform-unplated.png`
- `Assets/StoreLogo.png`
- `Assets/Wide310x150Logo.scale-200.png`

**Services**:
- `Services/IMonitorService.cs`
- `Services/ITradingWindowManager.cs`
- `Services/MonitorService.cs`
- `Services/TradingWindowManager.cs`

**Views**:
- `Views/MainPage.xaml`, `Views/MainPage.xaml.cs`
- `Views/TradingControlWindow.xaml`

**Settings Views**:
- `Views/Settings/MonitorSelectionView.xaml`
- `Views/Settings/MonitorSelectionView.xaml.cs`

**Trading Screens**:
- `Views/TradingScreens/MarketScannerScreen.xaml`
- `Views/TradingScreens/MarketScannerScreen.xaml.cs`
- `Views/TradingScreens/OrderExecutionScreen.xaml`
- `Views/TradingScreens/OrderExecutionScreen.xaml.cs`
- `Views/TradingScreens/PortfolioRiskScreen.xaml`
- `Views/TradingScreens/PortfolioRiskScreen.xaml.cs`
- `Views/TradingScreens/PrimaryChartingScreen.xaml`
- `Views/TradingScreens/PrimaryChartingScreen.xaml.cs`

### 8. UTILITY AND OPTIMIZATION PROJECTS

#### TradingPlatform.DisplayManagement
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/`
**Purpose**: Multi-monitor display management and GPU detection

**Key Files**:
- **Project File**: `TradingPlatform.DisplayManagement.csproj`

**Extensions**:
- `Extensions/ServiceCollectionExtensions.cs`

**Models**:
- `Models/DisplaySessionModels.cs`
- `Models/GpuModels.cs`
- `Models/MonitorModels.cs`

**Services**:
- `Services/DisplaySessionService.cs`
- `Services/GpuDetectionService.cs`
- `Services/MockGpuDetectionService.cs`
- `Services/MockMonitorDetectionService.cs`
- `Services/MonitorDetectionService.cs`

#### TradingPlatform.WindowsOptimization
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.WindowsOptimization/`
**Purpose**: Windows-specific performance optimizations

**Key Files**:
- **Project File**: `TradingPlatform.WindowsOptimization.csproj`
- **Class**: `Class1.cs`

**Extensions**:
- `Extensions/ServiceCollectionExtensions.cs`

**Models**:
- `Models/ProcessPriorityConfiguration.cs`

**Services**:
- `Services/IWindowsOptimizationService.cs`
- `Services/ProcessManager.cs`
- `Services/SystemMonitor.cs`
- `Services/WindowsOptimizationService.cs`

#### TradingPlatform.Utilities
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Utilities/`
**Purpose**: Shared utilities and Roslyn scripting support

**Key Files**:
- **Project File**: `TradingPlatform.Utilities.csproj`

**Scripts**:
- `Scripts/Register-Services-Roslyn.ps1`

### 9. TESTING PROJECT

#### TradingPlatform.Testing
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Testing/`
**Purpose**: Comprehensive testing framework and utilities

**Key Files**:
- **Project File**: `TradingPlatform.Testing.csproj`

**Examples**:
- `Examples/MockMessageBusExamples.cs`

**Mocks**:
- `Mocks/MockMessageBus.cs`

**Tests**:
- `Tests/FinancialMathTests.cs`

**Utilities**:
- `Utilities/MessageBusTestHelpers.cs`

## Configuration Files by Project

### Application Settings (appsettings.json)
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/appsettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/appsettings.Development.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/appsettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/appsettings.Development.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/appsettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/appsettings.Development.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/appsettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/appsettings.Development.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/appsettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/appsettings.Development.json`

### Launch Settings (launchSettings.json)
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/Properties/launchSettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/Properties/launchSettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Properties/launchSettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Properties/launchSettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Properties/launchSettings.json`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.TradingApp/Properties/launchSettings.json`

## Docker Files
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/Dockerfile.multi-stage`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/Dockerfile`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/Dockerfile`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Dockerfile`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Dockerfile`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Dockerfile`

## HTTP Testing Files
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/TradingPlatform.MarketData.http`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/TradingPlatform.StrategyEngine.http`

## Scripts and Automation

### DRAGON Build System
**Location**: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/scripts/`

- **Main Install**: `DRAGON-Complete-Build-System-Install.ps1`
- **Auto-start Services**: `DRAGON-Enable-AutoStart-Services.ps1`, `DRAGON-Fixed-AutoStart-Services.ps1`
- **Quick Setup**: `DRAGON-Quick-Setup.ps1`
- **Build Workspace**: `DRAGON-Setup-BuildWorkspace.ps1`
- **Documentation**: `README-DRAGON-BUILD.md`

### Build Scripts
- `build-in-buildworkspace.ps1`
- `build-windows-components.ps1`
- `create-dragon-project-structure.ps1`
- `dragon-remote-build.sh`
- `setup-buildworkspace.ps1`
- `setup-dragon-buildworkspace.sh`
- `setup-dragon-development.ps1`
- `sync-to-buildworkspace.ps1`
- `sync-to-buildworkspace.sh`

### Utility Scripts
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Utilities/Scripts/Register-Services-Roslyn.ps1`

## Documentation Files
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/ARCHITECTURE.md`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/DayTradingPlatform.filestructure.md`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Documentation/FinancialCalculationStandards.md`
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/scripts/README-DRAGON-BUILD.md`

## Quick Reference by File Type

### C# Source Files (.cs)
**Core Models**: Located in `*/Models/` directories across projects
**Interfaces**: Located in `*/Interfaces/` directories across projects
**Services**: Located in `*/Services/` directories across projects
**Configuration**: Located in `*/Configuration/` directories across projects

### Project Files (.csproj)
All project files are located in their respective project root directories with the naming pattern `TradingPlatform.{ProjectName}.csproj`

### XAML Files (.xaml)
Located exclusively in `TradingPlatform.TradingApp/Views/` and subdirectories

### Configuration Files
- **appsettings**: Web API projects (Gateway, MarketData, StrategyEngine, RiskManagement, PaperTrading)
- **launchSettings**: All executable projects
- **Package.appxmanifest**: WinUI 3 application only

### Log Files
- `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/2025.log`

## Project Dependencies and Relationships

### Dependency Hierarchy
1. **Foundation Layer**: TradingPlatform.Common, TradingPlatform.Foundation
2. **Core Layer**: TradingPlatform.Core
3. **Data Layer**: TradingPlatform.DataIngestion, TradingPlatform.Database
4. **Service Layer**: TradingPlatform.MarketData, TradingPlatform.Screening, TradingPlatform.Messaging, TradingPlatform.Logging
5. **Engine Layer**: TradingPlatform.StrategyEngine, TradingPlatform.FixEngine
6. **Management Layer**: TradingPlatform.RiskManagement, TradingPlatform.PaperTrading
7. **Infrastructure Layer**: TradingPlatform.Gateway, TradingPlatform.WindowsOptimization, TradingPlatform.DisplayManagement
8. **Presentation Layer**: TradingPlatform.TradingApp
9. **Utility Layer**: TradingPlatform.Utilities, TradingPlatform.Testing

### Special Directories
- **bin/**: Compiled binaries (excluded from source control)
- **obj/**: Build artifacts (excluded from source control)
- **TimestampDebug/**: Debug timestamp files
- **PDT/**: PDT-related files
- **Assets/**: WinUI 3 application assets

This comprehensive index provides immediate access to any file in the Day Trading Platform without requiring glob or grep searches. Each section is organized for quick navigation and reference during development work.
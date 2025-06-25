# TradingPlatform.Screening

This module provides comprehensive stock screening capabilities for the day trading platform using canonical patterns.

## Overview

The Screening module evaluates stocks against multiple criteria to identify trading opportunities. It uses a canonical architecture that provides:

- Standardized error handling and logging
- Built-in performance metrics
- Concurrent evaluation support
- Comprehensive observability

## Architecture

### Core Components

1. **Criteria Evaluators** - Evaluate individual aspects of stocks:
   - `PriceCriteriaCanonical` - Evaluates price ranges and penny stock rules
   - `VolumeCriteriaCanonical` - Analyzes trading volume and liquidity
   - `VolatilityCriteriaCanonical` - Measures price volatility using ATR
   - `GapCriteriaCanonical` - Detects and scores gap movements
   - `NewsCriteriaCanonical` - Analyzes news sentiment and velocity

2. **Screening Engine** - Processes screening requests:
   - `RealTimeScreeningEngineCanonical` - Real-time stock screening with pipeline architecture

3. **Orchestrator** - Coordinates multiple evaluators:
   - `ScreeningOrchestratorCanonical` - Aggregates results from all criteria evaluators

### Canonical Pattern Benefits

All components inherit from canonical base classes providing:

- **Automatic Logging**: Method entry/exit, performance timing, error context
- **Error Handling**: Standardized error responses with troubleshooting hints
- **Metrics Collection**: Success rates, throughput, latency tracking
- **Lifecycle Management**: Proper initialization, startup, and shutdown
- **Concurrency Control**: Built-in throttling and parallel execution

## Usage

### Basic Setup

```csharp
// In Program.cs or Startup.cs
services.AddScreeningServices();
```

### Custom Configuration

```csharp
services.AddScreeningServicesWithOverrides(builder =>
{
    // Replace specific evaluators
    builder.ReplaceCriteriaEvaluators<CustomPriceCriteria>();
    
    // Or add additional evaluators
    builder.AddCriteriaEvaluator<CustomTechnicalCriteria>();
});
```

### Using the Orchestrator

```csharp
public class TradingService
{
    private readonly ScreeningOrchestratorCanonical _orchestrator;
    
    public TradingService(ScreeningOrchestratorCanonical orchestrator)
    {
        _orchestrator = orchestrator;
    }
    
    public async Task<ScreeningResult> ScreenStockAsync(string symbol)
    {
        var marketData = await GetMarketDataAsync(symbol);
        var criteria = new TradingCriteria
        {
            MinPrice = 5m,
            MaxPrice = 100m,
            MinVolume = 1_000_000,
            // ... other criteria
        };
        
        return await _orchestrator.EvaluateAllAsync(marketData, criteria);
    }
}
```

### Using Individual Evaluators

```csharp
public class CustomScreener
{
    private readonly IServiceProvider _serviceProvider;
    
    public async Task<bool> CheckVolumeAsync(MarketData marketData)
    {
        var volumeEvaluator = _serviceProvider.GetRequiredService<VolumeCriteriaCanonical>();
        var criteria = new TradingCriteria { MinVolume = 500_000 };
        
        var result = await volumeEvaluator.EvaluateAsync(marketData, criteria);
        return result.Passed;
    }
}
```

## Scoring System

Each evaluator returns a score from 0-100:

- **Price**: Optimal range scoring, penny stock penalties
- **Volume**: Absolute and relative volume weighted scoring
- **Volatility**: ATR-based movement potential
- **Gap**: Gap size and fill percentage
- **News**: Sentiment and velocity scoring

The orchestrator aggregates scores using weighted averages:
- Price: 25%
- Volume: 25%
- Volatility: 20%
- Gap: 15%
- News: 15%

## Alert Levels

Results include alert levels based on overall score and pass rate:

- **Critical**: Score ≥ 90% and pass rate ≥ 80%
- **High**: Score ≥ 80% and pass rate ≥ 60%
- **Medium**: Score ≥ 70% and pass rate ≥ 50%
- **Low**: Score ≥ 60% and pass rate ≥ 40%
- **None**: Below thresholds

## Metrics and Monitoring

All canonical components expose metrics via `GetMetrics()`:

```csharp
var metrics = orchestrator.GetMetrics();
// Includes: TotalEvaluations, SuccessRate, AverageLatency, etc.
```

## Migration from Legacy

To migrate from non-canonical implementations:

1. Change service registration from `AddLegacyScreeningServices()` to `AddScreeningServices()`
2. Update injected types if using concrete classes
3. All interfaces remain compatible

## Performance Considerations

- Evaluators run concurrently by default (configurable)
- Built-in timeout protection (10s per evaluator)
- Automatic retry logic for transient failures
- Efficient metric collection with minimal overhead

## Testing

The canonical implementations are designed for testability:

```csharp
// Mock individual evaluators
var mockEvaluator = new Mock<ICriteriaEvaluator>();
services.AddSingleton(mockEvaluator.Object);

// Or use the builder pattern
services.AddScreeningServicesWithOverrides(builder =>
{
    builder.ReplaceCriteriaEvaluators<TestCriteriaEvaluator>();
});
```

## Future Enhancements

- Machine learning-based scoring adjustments
- Historical performance tracking
- Custom criteria plugin system
- Real-time alert streaming
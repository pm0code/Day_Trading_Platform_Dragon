# TradingPlatform.Backtesting

Comprehensive backtesting engine for validating trading strategies against historical market data with realistic simulation of market conditions.

## Features

### Core Capabilities
- **Event-Driven Architecture**: Tick-by-tick simulation of market events
- **Realistic Market Simulation**: Models slippage, transaction costs, and market impact
- **Multiple Asset Support**: Stocks, ETFs, options, and futures
- **Walk-Forward Analysis**: Robust out-of-sample testing
- **Monte Carlo Simulation**: Statistical confidence in results
- **Parameter Optimization**: Grid search and genetic algorithms
- **GPU Acceleration**: Optional CUDA support for intensive calculations

### Market Simulation
- **Order Book Reconstruction**: Rebuilds limit order book from historical data
- **Slippage Models**: Fixed, linear, square root, and custom models
- **Transaction Costs**: Commissions, fees, spreads, and market impact
- **Order Types**: Market, limit, stop, stop-limit, and more
- **Partial Fills**: Realistic order execution simulation
- **Corporate Actions**: Handles splits, dividends, and adjustments

### Performance Analytics
- **Return Metrics**: Total return, CAGR, monthly/yearly returns
- **Risk Metrics**: Sharpe, Sortino, Calmar ratios, max drawdown
- **Trade Analytics**: Win rate, profit factor, average win/loss
- **Statistical Analysis**: Monte Carlo, confidence intervals
- **Benchmark Comparison**: Alpha, beta, information ratio

## Architecture

```
TradingPlatform.Backtesting/
├── Engine/                 # Core backtesting engine
├── Data/                   # Historical data management
├── Models/                 # Domain models and DTOs
├── Strategies/             # Strategy implementations
├── Results/                # Result processing and reporting
├── Services/               # Supporting services
└── Interfaces/             # Public interfaces
```

## Usage

### Basic Backtest

```csharp
// Create engine
var engine = new BacktestEngine(logger, dataProvider, marketSimulator);

// Define strategy
var strategy = new MyTradingStrategy();

// Set parameters
var parameters = new BacktestParameters
{
    StartDate = new DateTime(2023, 1, 1),
    EndDate = new DateTime(2024, 1, 1),
    InitialCapital = 100000m,
    Symbols = new List<string> { "AAPL", "MSFT", "GOOGL" }
};

// Run backtest
var result = await engine.RunBacktestAsync(strategy, parameters);

// Analyze results
Console.WriteLine($"Total Return: {result.TotalReturnPercent:P}");
Console.WriteLine($"Sharpe Ratio: {result.SharpeRatio:F2}");
Console.WriteLine($"Max Drawdown: {result.MaxDrawdownPercent:P}");
```

### Walk-Forward Analysis

```csharp
var walkForwardParams = new WalkForwardParameters
{
    StartDate = new DateTime(2020, 1, 1),
    EndDate = new DateTime(2024, 1, 1),
    InSampleDays = 252,      // 1 year
    OutOfSampleDays = 63,    // 3 months
    StepSizeDays = 21        // 1 month
};

var result = await engine.RunWalkForwardAnalysisAsync(strategy, walkForwardParams);
```

### Monte Carlo Simulation

```csharp
var monteCarloParams = new MonteCarloParameters
{
    NumberOfSimulations = 1000,
    ConfidenceLevels = new List<double> { 0.95, 0.99 },
    UseBootstrapping = true
};

var mcResult = await engine.RunMonteCarloSimulationAsync(backtestResult, monteCarloParams);
Console.WriteLine($"95% Confidence Return: {mcResult.ConfidenceIntervals[0.95]:P}");
```

## Strategy Development

### Implementing a Strategy

```csharp
public class MomentumStrategy : IBacktestStrategy
{
    public string Name => "Momentum Strategy";
    public string Version => "1.0";

    public async Task<IEnumerable<Signal>> OnMarketDataAsync(MarketDataUpdate data)
    {
        var signals = new List<Signal>();
        
        // Strategy logic here
        var sma20 = await _context.Indicators.SMA(data.Symbol, 20);
        var sma50 = await _context.Indicators.SMA(data.Symbol, 50);
        
        if (sma20 > sma50 && !_context.Portfolio.HasPosition(data.Symbol))
        {
            signals.Add(new Signal
            {
                Symbol = data.Symbol,
                Type = SignalType.Entry,
                Action = SignalAction.Buy,
                Quantity = CalculatePositionSize(data.Symbol),
                OrderType = OrderType.Market,
                Reason = "Golden cross detected"
            });
        }
        
        return signals;
    }
}
```

## Configuration

### Slippage Models

```csharp
// Fixed slippage
parameters.SlippageModel = SlippageModel.Fixed;
parameters.BaseSlippageBps = 5; // 5 basis points

// Linear slippage (scales with order size)
parameters.SlippageModel = SlippageModel.Linear;
parameters.BaseSlippageBps = 10;

// Market impact model
parameters.SlippageModel = SlippageModel.Market;
```

### Transaction Costs

```csharp
// Per-trade commission
parameters.CommissionPerTrade = 4.95m;

// Per-share commission
parameters.CommissionPerShare = 0.005m;
parameters.MinimumCommission = 1.00m;
```

## Performance Optimization

### Parallel Backtesting

```csharp
var optimizationSettings = new ParameterOptimizationSettings
{
    Parameters = new List<ParameterRange>
    {
        new("SMA_Fast", 10, 30, 5),
        new("SMA_Slow", 40, 60, 5)
    },
    EnableParallel = true,
    MaxDegreeOfParallelism = 8
};

var result = await engine.OptimizeStrategyAsync(strategy, parameters, optimizationSettings);
```

### GPU Acceleration

```csharp
// Enable GPU for intensive calculations
parameters.EnableGPUAcceleration = true;
parameters.GPUDeviceId = 0;
```

## Best Practices

1. **Data Quality**: Ensure historical data is adjusted for splits and dividends
2. **Warmup Period**: Allow sufficient warmup for indicators
3. **Transaction Costs**: Include realistic costs to avoid overestimating returns
4. **Out-of-Sample Testing**: Always validate with walk-forward analysis
5. **Position Sizing**: Implement proper risk management
6. **Survivorship Bias**: Include delisted symbols in universe

## Integration

The backtesting engine integrates with:
- **TradingPlatform.ML**: Use ML models in strategies
- **TradingPlatform.RiskManagement**: Apply risk limits
- **TradingPlatform.DataIngestion**: Historical data access
- **TradingPlatform.Screening**: Use screeners as filters

## Troubleshooting

### Common Issues

1. **Insufficient Data**: Ensure data covers the entire backtest period
2. **Look-Ahead Bias**: Check that indicators don't use future data
3. **Memory Usage**: Use streaming for large datasets
4. **Slow Performance**: Enable parallel processing or reduce data frequency

### Debug Mode

```csharp
parameters.EnableDetailedLogging = true;
parameters.SaveTradeLogs = true;

// Subscribe to debug events
engine.Events.Subscribe(evt => Console.WriteLine($"[{evt.Timestamp}] {evt.Type}: {evt.Message}"));
```
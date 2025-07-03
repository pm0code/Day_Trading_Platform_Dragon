# Enhanced TradingLogOrchestrator Usage Guide
**Created**: 2025-01-30  
**Purpose**: Comprehensive guide for using enhanced logging features

## Overview

The `TradingLogOrchestratorEnhanced` extends the base `TradingLogOrchestrator` with three major enhancements:
1. **SCREAMING_SNAKE_CASE Event Codes**: Standardized event categorization
2. **Operation Tracking**: Automatic timing and lifecycle tracking
3. **Child Logger Support**: Component-isolated logging contexts

## Quick Start

```csharp
// Get the enhanced instance
var logger = TradingLogOrchestratorEnhanced.Instance;

// Use event codes
logger.LogEventCode(TradingLogOrchestratorEnhanced.TRADE_EXECUTED, 
    "Trade completed successfully", 
    new { Symbol = "AAPL", Quantity = 100 });

// Track operations
var operationId = logger.StartOperation("ProcessMarketData");
try 
{
    // Do work...
    logger.CompleteOperation(operationId, result);
}
catch (Exception ex)
{
    logger.FailOperation(operationId, ex);
}

// Create child logger
using var childLogger = logger.CreateChildLogger("MarketDataService");
childLogger.LogInfo("Service initialized");
```

## Event Code Reference

### Trading Events
- `TRADE_EXECUTED` - Trade successfully executed
- `TRADE_REJECTED` - Trade rejected by broker/exchange
- `TRADE_CANCELLED` - Trade cancelled
- `ORDER_PLACED` - Order submitted to market
- `ORDER_FILLED` - Order completely filled
- `ORDER_PARTIAL_FILL` - Order partially filled

### Market Data Events
- `MARKET_DATA_RECEIVED` - New market data received
- `MARKET_DATA_STALE` - Market data is stale/outdated
- `MARKET_DATA_ERROR` - Error receiving market data
- `QUOTE_UPDATE` - Quote update received
- `TICK_RECEIVED` - Tick data received

### Risk Events
- `RISK_LIMIT_BREACH` - Risk limit exceeded
- `RISK_WARNING` - Risk warning threshold reached
- `POSITION_LIMIT_REACHED` - Position limit reached
- `LOSS_LIMIT_APPROACHING` - Loss limit approaching

### System Events
- `SYSTEM_STARTUP` - System starting up
- `SYSTEM_SHUTDOWN` - System shutting down
- `COMPONENT_INITIALIZED` - Component initialized
- `COMPONENT_FAILED` - Component failure
- `HEALTH_CHECK_PASSED` - Health check passed
- `HEALTH_CHECK_FAILED` - Health check failed

### Performance Events
- `PERFORMANCE_DEGRADATION` - Performance degraded
- `LATENCY_SPIKE` - Latency spike detected
- `THROUGHPUT_DROP` - Throughput dropped
- `MEMORY_PRESSURE` - Memory pressure detected

### Data Pipeline Events
- `DATA_INGESTION_START` - Data ingestion started
- `DATA_INGESTION_COMPLETE` - Data ingestion completed
- `DATA_VALIDATION_FAILED` - Data validation failed
- `DATA_TRANSFORMATION_ERROR` - Data transformation error

### Operation Status Events
- `OPERATION_STARTED` - Operation started
- `OPERATION_COMPLETED` - Operation completed successfully
- `OPERATION_FAILED` - Operation failed
- `OPERATION_TIMEOUT` - Operation timed out
- `OPERATION_CANCELLED` - Operation cancelled

## Operation Tracking

### Basic Usage

```csharp
// Manual tracking
var operationId = logger.StartOperation("CalculateRisk", 
    new { Portfolio = "Main", Positions = 10 });

try
{
    var risk = CalculatePortfolioRisk();
    logger.CompleteOperation(operationId, new { RiskValue = risk });
}
catch (Exception ex)
{
    logger.FailOperation(operationId, ex, "Risk calculation failed");
}
```

### Automatic Tracking (Async)

```csharp
// Automatically tracks start, completion, and failures
var result = await logger.TrackOperationAsync("FetchMarketData", 
    async () =>
    {
        return await marketDataService.GetQuotesAsync();
    },
    parameters: new { Symbols = symbols });
```

### Automatic Tracking (Sync)

```csharp
// Synchronous version
var calculation = logger.TrackOperation("CalculatePnL",
    () =>
    {
        return portfolio.CalculatePnL();
    },
    parameters: new { Date = DateTime.Today });
```

## Child Logger Support

### Creating Child Loggers

```csharp
// Create child logger for a service
var marketDataLogger = logger.CreateChildLogger("MarketDataService",
    new Dictionary<string, string>
    {
        ["ServiceVersion"] = "1.0",
        ["Environment"] = "Production"
    });

// Use it like a regular logger
marketDataLogger.LogInfo("Service started");
marketDataLogger.LogEvent(MARKET_DATA_RECEIVED, "Received 100 quotes");

// Child operations are prefixed with component name
var opId = marketDataLogger.StartOperation("ProcessQuotes");
marketDataLogger.CompleteOperation(opId);

// Dispose when done
marketDataLogger.Dispose();
```

### Using Statements

```csharp
using (var childLogger = logger.CreateChildLogger("OrderProcessor"))
{
    childLogger.LogInfo("Processing order batch");
    
    foreach (var order in orders)
    {
        var opId = childLogger.StartOperation("ProcessOrder", 
            new { OrderId = order.Id });
        
        try
        {
            ProcessOrder(order);
            childLogger.CompleteOperation(opId);
        }
        catch (Exception ex)
        {
            childLogger.FailOperation(opId, ex);
        }
    }
}
```

## Integration Examples

### Trading Service

```csharp
public class TradingService
{
    private readonly IChildLogger _logger;
    
    public TradingService()
    {
        _logger = TradingLogOrchestratorEnhanced.Instance
            .CreateChildLogger("TradingService");
    }
    
    public async Task<Trade> ExecuteTradeAsync(Order order)
    {
        var opId = _logger.StartOperation("ExecuteTrade", 
            new { OrderId = order.Id, Symbol = order.Symbol });
        
        try
        {
            _logger.LogEvent(ORDER_PLACED, "Order sent to exchange", order);
            
            var trade = await _exchange.SendOrderAsync(order);
            
            _logger.LogEvent(TRADE_EXECUTED, "Trade executed successfully", 
                new { TradeId = trade.Id, Price = trade.Price });
            
            _logger.CompleteOperation(opId, trade);
            return trade;
        }
        catch (Exception ex)
        {
            _logger.LogEvent(TRADE_REJECTED, "Trade rejected", 
                new { Reason = ex.Message });
            _logger.FailOperation(opId, ex);
            throw;
        }
    }
}
```

### Risk Monitor

```csharp
public class RiskMonitor
{
    private readonly IChildLogger _logger;
    
    public RiskMonitor()
    {
        _logger = TradingLogOrchestratorEnhanced.Instance
            .CreateChildLogger("RiskMonitor");
    }
    
    public void CheckRiskLimits(Portfolio portfolio)
    {
        var risk = portfolio.CalculateRisk();
        
        if (risk.Value > risk.Limit)
        {
            _logger.LogEvent(RISK_LIMIT_BREACH, 
                "Risk limit exceeded", 
                new { Current = risk.Value, Limit = risk.Limit },
                LogLevel.Critical);
        }
        else if (risk.Value > risk.Limit * 0.8)
        {
            _logger.LogEvent(RISK_WARNING, 
                "Risk approaching limit", 
                new { Current = risk.Value, Limit = risk.Limit },
                LogLevel.Warning);
        }
    }
}
```

### Performance Monitoring

```csharp
public class PerformanceMonitor
{
    private readonly TradingLogOrchestratorEnhanced _logger;
    
    public PerformanceMonitor()
    {
        _logger = TradingLogOrchestratorEnhanced.Instance;
    }
    
    public async Task MonitorLatencyAsync(Func<Task> operation)
    {
        var sw = Stopwatch.StartNew();
        await operation();
        sw.Stop();
        
        if (sw.ElapsedMilliseconds > 100)
        {
            _logger.LogEventCode(LATENCY_SPIKE, 
                "Operation exceeded latency threshold",
                new { 
                    DurationMs = sw.ElapsedMilliseconds,
                    Threshold = 100
                },
                LogLevel.Warning);
        }
    }
}
```

## Best Practices

### 1. Use Event Codes Consistently
```csharp
// Good - uses standard event code
logger.LogEventCode(TRADE_EXECUTED, "Trade completed", tradeData);

// Avoid - generic logging without event code
logger.LogInfo("Trade completed", tradeData);
```

### 2. Track Long-Running Operations
```csharp
// Good - tracks operation lifecycle
await logger.TrackOperationAsync("BacktestStrategy", async () =>
{
    return await RunBacktest(strategy, startDate, endDate);
});

// Avoid - no tracking
await RunBacktest(strategy, startDate, endDate);
```

### 3. Use Child Loggers for Services
```csharp
// Good - isolated context
public class MarketDataService
{
    private readonly IChildLogger _logger;
    
    public MarketDataService()
    {
        _logger = TradingLogOrchestratorEnhanced.Instance
            .CreateChildLogger(nameof(MarketDataService));
    }
}

// Avoid - using global logger directly
public class MarketDataService
{
    public void ProcessData()
    {
        TradingLogOrchestrator.Instance.LogInfo("Processing...");
    }
}
```

### 4. Include Relevant Context
```csharp
// Good - rich context
logger.LogEventCode(ORDER_FILLED, "Order filled",
    new
    {
        OrderId = order.Id,
        Symbol = order.Symbol,
        Quantity = order.Quantity,
        Price = fill.Price,
        Venue = fill.Venue,
        Latency = fill.LatencyMs
    });

// Avoid - minimal context
logger.LogEventCode(ORDER_FILLED, $"Order {order.Id} filled");
```

### 5. Handle Operation Failures
```csharp
// Good - comprehensive error handling
var opId = logger.StartOperation("ProcessMarketData");
try
{
    var data = await FetchData();
    ProcessData(data);
    logger.CompleteOperation(opId, new { RecordCount = data.Count });
}
catch (TimeoutException ex)
{
    logger.CancelOperation(opId, "Market data fetch timeout");
    throw;
}
catch (Exception ex)
{
    logger.FailOperation(opId, ex, "Failed to process market data");
    throw;
}
```

## Migration Guide

### From Base TradingLogOrchestrator

```csharp
// Before
TradingLogOrchestrator.Instance.LogTrade(
    symbol, action, quantity, price);

// After - with event code
TradingLogOrchestratorEnhanced.Instance.LogTradeEvent(
    TRADE_EXECUTED, symbol, action, quantity, price);

// Before - manual timing
var start = DateTime.UtcNow;
ProcessData();
var duration = DateTime.UtcNow - start;
logger.LogPerformance("ProcessData", duration);

// After - automatic tracking
await logger.TrackOperationAsync("ProcessData", async () =>
{
    await ProcessDataAsync();
});
```

## Performance Considerations

1. **Event codes are constants** - No string allocation
2. **Operation tracking is lightweight** - Uses high-precision Stopwatch
3. **Child loggers add minimal overhead** - Simple wrapper pattern
4. **All logging remains non-blocking** - Inherits base performance

---

*For more details, see the implementation in `TradingLogOrchestratorEnhanced.cs`*
# Error Logging Examples - Day Trading Platform

## Quick Reference for Error Logging

### Basic Error Logging
```csharp
// Simple error with automatic context
_logger.LogError("Market connection lost");

// Error with exception
_logger.LogError("Market data processing failed", exception);
```

### Comprehensive Error Logging (Recommended)
```csharp
_logger.LogError(
    message: "Order execution failed for AAPL",
    exception: ex,
    operationContext: "Buy order placement", 
    userImpact: "Order not placed - position unchanged",
    troubleshootingHints: "Check market hours, account balance, and symbol validity",
    additionalData: new { 
        Symbol = "AAPL", 
        Quantity = 100, 
        Price = 150.25m, 
        OrderId = "ORD123",
        AccountId = "ACC456"
    }
);
```

### Trading-Specific Error Logging
```csharp
// Use dedicated trading methods
_logger.LogTradingError("OrderExecution", exception, correlationId, 
    new { Symbol = "MSFT", Quantity = 50, Price = 300.00m });

// Critical errors that need immediate attention
_logger.LogCriticalError("RiskEngine", exception, correlationId,
    new { RiskLevel = "CRITICAL", ExposureLimit = 1000000m });
```

### Automatic Context Injection
The enhanced logger automatically captures:
- **Method name** (CallerMemberName)
- **File path and line number** (CallerFilePath, CallerLineNumber) 
- **Timestamp** (UTC with microsecond precision)
- **Structured data** for query and analysis

### Log Output Example
```json
{
  "timestamp": "2025-06-18T20:30:15.123456Z",
  "level": "ERROR",
  "method": "ExecuteBuyOrder",
  "file": "TradingEngine.cs:142",
  "message": "Order execution failed for AAPL",
  "exception": {
    "type": "MarketDataException",
    "message": "Symbol not found",
    "stackTrace": "..."
  },
  "context": {
    "operation": "Buy order placement",
    "userImpact": "Order not placed - position unchanged",
    "troubleshooting": "Check market hours, account balance, and symbol validity",
    "data": {
      "symbol": "AAPL",
      "quantity": 100,
      "price": 150.25,
      "orderId": "ORD123"
    }
  }
}
```

## Usage Patterns

### 1. Constructor Injection
```csharp
public class TradingEngine
{
    private readonly ILogger _logger;
    
    public TradingEngine(ILogger logger)
    {
        _logger = logger;
    }
}
```

### 2. Static Instance (Alternative)
```csharp
// Direct access to orchestrator
TradingLogOrchestrator.Instance.LogError("Critical system failure", ex);
```

### 3. Exception Handling Pattern
```csharp
try
{
    // Trading operation
    ExecuteTrade(symbol, quantity);
}
catch (Exception ex)
{
    _logger.LogError(
        message: $"Trade execution failed for {symbol}",
        exception: ex,
        operationContext: "Trade execution",
        userImpact: "Trade not executed - no position change",
        troubleshootingHints: "Verify market connectivity and account status",
        additionalData: new { Symbol = symbol, Quantity = quantity }
    );
    throw; // Re-throw for upstream handling
}
```

## All Available Logging Methods

### Core Methods
- `LogInfo()` - General information
- `LogWarning()` - Warnings with impact and recommended actions
- `LogError()` - Errors with comprehensive context
- `LogTrade()` - Trading operations
- `LogPositionChange()` - Position modifications
- `LogPerformance()` - Performance metrics
- `LogHealth()` - System health
- `LogRisk()` - Risk management
- `LogDataPipeline()` - Data processing
- `LogMarketData()` - Market data events

### Method Lifecycle
- `LogMethodEntry()` - Automatic method entry logging
- `LogMethodExit()` - Automatic method exit logging

### Advanced Methods
- `LogTradingError()` - Trading-specific errors
- `LogCriticalError()` - Critical system errors
- `LogValidation()` - Data validation results
- `LogConfigurationChange()` - Configuration changes
- `LogSecurityEvent()` - Security events
- `LogAlert()` - System alerts

## Best Practices
1. **Always include context** - Use operationContext and userImpact
2. **Provide troubleshooting hints** - Help operations team resolve issues
3. **Use structured data** - Include relevant objects for analysis
4. **Follow exception handling** - Log and re-throw for proper flow
5. **Use appropriate log levels** - Error for actual errors, Warning for degraded performance

All logs are automatically written to `/logs` directory with structured JSON format for analysis and alerting.
# Trading Platform Code Quality Report
Generated: 2025-06-23 11:39:07
Analysis Time: 15.69 seconds

## Summary
- **Total Issues**: 1082
- **Critical**: 304
- **High**: 514
- **Medium**: 264
- **Low**: 0

## Issues by Category
- Compiler: 817
- Logging: 265

## Critical Issues
### ApiRateLimiter.cs:165
**LogError has exception as first parameter - should be (string message, Exception? exception, ...)**
```csharp
_logger.LogError($"Rate limiter recorded failure: {exception.Message}", exception)
```
**Fix**: Swap parameters: LogError(exception, $"Rate limiter recorded failure: {exception.Message}", ...)

### ScreeningRequest.cs:28
**The type or namespace name 'ScreeningMode' could not be found (are you missing a using directive or an assembly reference?)**

### CriteriaConfigurationService.cs:30
**The name 'ValidateCriteria' does not exist in the current context**

### ScreeningRequest.cs:28
**The name 'ScreeningMode' does not exist in the current context**

### RealTimeScreeningEngine.cs:58
**'IMarketDataProvider' does not contain a definition for 'GetRealTimeDataAsync' and no accessible extension method 'GetRealTimeDataAsync' accepting a first argument of type 'IMarketDataProvider' could be found (are you missing a using directive or an assembly reference?)**

### MarketDataManager.cs:96
**The name 'ex' does not exist in the current context**

### OrderManager.cs:87
**Argument 1 may not be passed with the 'out' keyword**

### OrderManager.cs:87
**Argument 2: cannot convert from 'string' to 'System.Exception?'**

### OrderManager.cs:95
**Argument 1: cannot convert from 'string' to 'System.Collections.Generic.KeyValuePair<string, TradingPlatform.FixEngine.Core.Order>'**

### FixEngine.cs:477
**The name 'ex' does not exist in the current context**

### ProcessManager.cs:57
**Argument 2: cannot convert from 'string' to 'System.Exception?'**

### ProcessManager.cs:57
**Argument 3: cannot convert from 'System.Exception' to 'string?'**

### HealthMonitor.cs:70
**Argument 2: cannot convert from 'string' to 'System.Exception?'**

### HealthMonitor.cs:70
**Argument 3: cannot convert from 'System.Exception' to 'string?'**

### GatewayOrchestrator.cs:82
**Argument 1: cannot convert from 'Microsoft.Extensions.Logging.ILogger<TradingPlatform.Core.Models.MarketData>' to 'TradingPlatform.Core.Interfaces.ITradingLogger'**

### ProcessManager.cs:116
**Argument 2: cannot convert from 'string' to 'System.Exception?'**

### ProcessManager.cs:122
**Argument 3: cannot convert from 'int' to 'string'**

### ProcessManager.cs:126
**Argument 2: cannot convert from 'string' to 'System.Exception?'**

### ProcessManager.cs:126
**Argument 3: cannot convert from 'System.Exception' to 'string?'**

### ProcessManager.cs:162
**Argument 2: cannot convert from 'string' to 'System.Exception?'**


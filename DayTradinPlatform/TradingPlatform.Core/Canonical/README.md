# Canonical System Documentation

The Canonical System provides standardized, harmonized base implementations for all components in the Day Trading Platform. This ensures consistency, comprehensive logging, error handling, and maintainability across the entire codebase.

## Core Principles

1. **Harmonized Design**: All canonical classes follow the same patterns and conventions
2. **Comprehensive Logging**: Every method entry/exit, operation, and error is logged
3. **Standardized Error Handling**: Consistent error reporting with context and troubleshooting
4. **Progress Tracking**: Built-in progress reporting for long-running operations
5. **Performance Monitoring**: Automatic performance tracking and metrics
6. **Testability**: Standardized testing patterns with full logging

## Canonical Components

### 1. CanonicalBase
Base class for all canonical implementations providing:
- Automatic method entry/exit logging
- Standardized error handling patterns
- Parameter validation helpers
- Performance tracking utilities
- Retry logic with exponential backoff

```csharp
public class MyComponent : CanonicalBase
{
    public MyComponent(ITradingLogger logger) : base(logger) { }
    
    public async Task<string> DoSomethingAsync()
    {
        return await ExecuteWithLoggingAsync(
            async () => {
                // Your logic here
                return "result";
            },
            "Performing operation",
            "Operation failed - user impact",
            "Check configuration and retry"
        );
    }
}
```

### 2. CanonicalServiceBase
Base class for all services providing:
- Service lifecycle management (Initialize, Start, Stop)
- Health monitoring and reporting
- Metrics collection and tracking
- Graceful shutdown handling
- State management with thread safety

```csharp
public class MyTradingService : CanonicalServiceBase
{
    public MyTradingService(ITradingLogger logger) 
        : base(logger, "MyTradingService") { }
    
    protected override async Task OnInitializeAsync(CancellationToken ct)
    {
        // Initialize resources
    }
    
    protected override async Task OnStartAsync(CancellationToken ct)
    {
        // Start service operations
    }
    
    protected override async Task OnStopAsync(CancellationToken ct)
    {
        // Clean up resources
    }
}
```

### 3. CanonicalErrorHandler
Static class for standardized error handling:
- Consistent error logging with full context
- Automatic severity determination
- User-friendly error messages
- Technical troubleshooting hints
- Error response generation

```csharp
try
{
    // Your code
}
catch (Exception ex)
{
    CanonicalErrorHandler.HandleError(
        ex,
        "ComponentName",
        "Operation description",
        ErrorSeverity.Error,
        "User impact description",
        "Troubleshooting hints"
    );
}
```

### 4. CanonicalProgressReporter
Progress tracking for long-running operations:
- Percentage-based progress reporting
- Multi-stage operation support
- Time estimation
- Sub-progress for nested operations
- Automatic logging of progress updates

```csharp
using var progress = new CanonicalProgressReporter("Data Import");

for (int i = 0; i < items.Count; i++)
{
    await ProcessItem(items[i]);
    progress.ReportProgress((i + 1) * 100.0 / items.Count, 
        $"Processing item {i + 1} of {items.Count}");
}

progress.Complete("Import completed successfully");
```

### 5. CanonicalTestBase
Base class for all unit tests providing:
- Test method logging with timing
- Test data logging for transparency
- Assertion logging with actual vs expected
- Performance measurement utilities
- Test artifact cleanup
- Retry logic for flaky tests

```csharp
public class MyServiceTests : CanonicalTestBase
{
    public MyServiceTests(ITestOutputHelper output) : base(output) { }
    
    [Fact]
    public async Task MyService_Should_Work()
    {
        await ExecuteTestAsync(async () =>
        {
            // Arrange
            LogTestStep("Creating test data");
            var testData = CreateTestData(() => new TestModel(), "Test model");
            
            // Act
            LogTestStep("Executing service operation");
            var result = await _service.ProcessAsync(testData);
            
            // Assert
            AssertWithLogging(result.Success, true, "Operation should succeed");
        });
    }
}
```

## Usage Guidelines

### 1. Always Inherit from Canonical Base Classes
```csharp
// ✅ Good
public class MarketDataProcessor : CanonicalBase { }

// ❌ Bad
public class MarketDataProcessor { }
```

### 2. Use Canonical Error Handling
```csharp
// ✅ Good
return await ExecuteWithLoggingAsync(
    async () => await FetchDataAsync(),
    "Fetching market data",
    "Unable to retrieve current prices",
    "Check API connectivity and credentials"
);

// ❌ Bad
try
{
    return await FetchDataAsync();
}
catch (Exception ex)
{
    // Custom error handling
}
```

### 3. Report Progress for Long Operations
```csharp
// ✅ Good
using var progress = new CanonicalProgressReporter("Batch Processing");
foreach (var item in items)
{
    await ProcessItem(item);
    progress.ReportBatchProgress(items, items.IndexOf(item));
}

// ❌ Bad
foreach (var item in items)
{
    await ProcessItem(item);
    // No progress reporting
}
```

### 4. Implement Proper Service Lifecycle
```csharp
// ✅ Good
var service = new MyTradingService(logger);
await service.InitializeAsync();
await service.StartAsync();
// ... use service
await service.StopAsync();

// ❌ Bad
var service = new MyTradingService(logger);
// Start using without initialization
```

### 5. Use Canonical Test Patterns
```csharp
// ✅ Good
await ExecuteTestAsync(async () =>
{
    LogTestStep("Arrange");
    var data = CreateTestData(...);
    
    LogTestStep("Act");
    var result = await operation();
    
    LogTestStep("Assert");
    AssertWithLogging(result, expected, "Result should match");
});

// ❌ Bad
var result = await operation();
Assert.Equal(expected, result);
```

## Benefits

1. **Consistency**: Same patterns everywhere reduce cognitive load
2. **Debugging**: Comprehensive logging makes troubleshooting easier
3. **Monitoring**: Built-in metrics and health checks
4. **Maintenance**: Standardized code is easier to maintain
5. **Quality**: Enforced error handling and validation
6. **Performance**: Built-in performance tracking identifies bottlenecks

## Best Practices

1. **Always provide meaningful operation descriptions** in error handling
2. **Include user impact and troubleshooting hints** in error messages
3. **Use correlation IDs** for tracing operations across components
4. **Report progress** for any operation taking more than a few seconds
5. **Log test data** in unit tests for debugging failures
6. **Monitor service health** regularly in production
7. **Track metrics** for performance analysis

## Example: Complete Service Implementation

```csharp
public class RealTimeScreeningService : CanonicalServiceBase
{
    private readonly IMarketDataProvider _marketData;
    private readonly IScreeningEngine _screeningEngine;
    private Timer? _screeningTimer;

    public RealTimeScreeningService(
        ITradingLogger logger,
        IMarketDataProvider marketData,
        IScreeningEngine screeningEngine) 
        : base(logger, "RealTimeScreeningService")
    {
        _marketData = marketData;
        _screeningEngine = screeningEngine;
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        LogInfo("Initializing screening engine");
        
        await ExecuteWithRetryAsync(
            async () => await _screeningEngine.InitializeAsync(),
            "Initialize screening engine",
            maxRetries: 3
        );
        
        UpdateMetric("ScreeningEngineInitialized", true);
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        LogInfo("Starting real-time screening");
        
        _screeningTimer = new Timer(
            async _ => await ScreenMarketAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30)
        );
        
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        LogInfo("Stopping real-time screening");
        
        _screeningTimer?.Dispose();
        _screeningTimer = null;
        
        return Task.CompletedTask;
    }

    protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
        OnCheckHealthAsync(CancellationToken cancellationToken)
    {
        var marketDataHealthy = await _marketData.IsHealthyAsync();
        var screeningEngineHealthy = await _screeningEngine.IsHealthyAsync();
        
        var isHealthy = marketDataHealthy && screeningEngineHealthy;
        var message = isHealthy ? "All systems operational" : "Some systems degraded";
        
        var details = new Dictionary<string, object>
        {
            ["MarketDataProvider"] = marketDataHealthy ? "Healthy" : "Unhealthy",
            ["ScreeningEngine"] = screeningEngineHealthy ? "Healthy" : "Unhealthy",
            ["LastScreeningRun"] = GetMetrics().GetValueOrDefault("LastScreeningRun", "Never")
        };
        
        return (isHealthy, message, details);
    }

    private async Task ScreenMarketAsync()
    {
        using var progress = new CanonicalProgressReporter("Market Screening", true);
        
        try
        {
            var symbols = await GetActiveSymbolsAsync();
            progress.ReportProgress(10, $"Retrieved {symbols.Count} symbols");
            
            var results = new List<ScreeningResult>();
            
            for (int i = 0; i < symbols.Count; i++)
            {
                var result = await ExecuteServiceOperationAsync(
                    async () => await _screeningEngine.ScreenSymbolAsync(symbols[i]),
                    "ScreenSymbol"
                );
                
                if (result.MeetsCriteria)
                {
                    results.Add(result);
                }
                
                progress.ReportBatchProgress(symbols, i);
            }
            
            UpdateMetric("LastScreeningRun", DateTime.UtcNow);
            UpdateMetric("SymbolsScreened", symbols.Count);
            UpdateMetric("PositiveResults", results.Count);
            
            progress.Complete($"Screened {symbols.Count} symbols, found {results.Count} opportunities");
        }
        catch (Exception ex)
        {
            progress.ReportError("Screening failed", ex);
            throw;
        }
    }
}
```

This canonical system ensures that every component in the Day Trading Platform follows the same high standards for logging, error handling, testing, and operational excellence.
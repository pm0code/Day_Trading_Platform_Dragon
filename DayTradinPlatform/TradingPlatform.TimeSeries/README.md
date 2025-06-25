# TradingPlatform.TimeSeries

High-performance time-series database integration for the Day Trading Platform using InfluxDB.

## Overview

The TradingPlatform.TimeSeries module provides:
- **Microsecond-precision** time-series data storage
- **High-throughput** batch writing capabilities
- **Optimized queries** for real-time analytics
- **Canonical pattern** compliance with comprehensive monitoring
- **Built-in data models** for all trading scenarios

## Architecture

### Core Components

1. **CanonicalInfluxDbService**: Main service implementing time-series operations
   - Extends `CanonicalServiceBase` for lifecycle management
   - Implements batching and connection pooling
   - Provides automatic bucket management
   - Includes comprehensive metrics and health checks

2. **Time-Series Models**: Pre-defined data structures for trading
   - `MarketDataPoint`: Real-time quotes and trades
   - `OrderExecutionPoint`: Order lifecycle tracking
   - `PositionPoint`: Position and P&L tracking
   - `SignalPoint`: Trading signal storage
   - `RiskMetricsPoint`: Portfolio risk metrics
   - `PerformanceMetricsPoint`: System performance data
   - `MarketDepthPoint`: Level 2 order book data
   - `TradingStatsPoint`: Aggregated trading statistics
   - `AlertPoint`: System alerts and events

3. **Configuration**: Flexible setup for different environments
   - Development, staging, and production presets
   - Customizable retention policies
   - Batch size and flush interval tuning
   - Multi-bucket organization

## Setup

### InfluxDB Installation

#### Docker (Recommended)
```bash
docker run -d \
  --name influxdb \
  -p 8086:8086 \
  -v influxdb-data:/var/lib/influxdb2 \
  -v influxdb-config:/etc/influxdb2 \
  -e DOCKER_INFLUXDB_INIT_MODE=setup \
  -e DOCKER_INFLUXDB_INIT_USERNAME=admin \
  -e DOCKER_INFLUXDB_INIT_PASSWORD=adminpassword \
  -e DOCKER_INFLUXDB_INIT_ORG=trading-platform \
  -e DOCKER_INFLUXDB_INIT_BUCKET=trading-data \
  -e DOCKER_INFLUXDB_INIT_ADMIN_TOKEN=your-super-secret-token \
  influxdb:2.7
```

#### Windows Installation
1. Download InfluxDB from https://portal.influxdata.com/downloads/
2. Extract and run `influxd.exe`
3. Access UI at http://localhost:8086
4. Complete setup wizard

### Configuration

#### appsettings.json
```json
{
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Token": "your-influxdb-token",
    "Organization": "trading-platform",
    "DefaultBucket": "trading-data",
    "MarketDataBucket": "market-data",
    "OrderDataBucket": "order-data",
    "MetricsBucket": "performance-metrics",
    "BatchSize": 1000,
    "FlushInterval": 100,
    "RetentionDays": 365
  }
}
```

#### Service Registration
```csharp
// In Program.cs
builder.Services.AddInfluxDbTimeSeries(builder.Configuration);

// Or with custom configuration
builder.Services.AddInfluxDbTimeSeries(options =>
{
    options.Url = "http://influxdb-server:8086";
    options.Token = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN");
    options.Organization = "prod-org";
    options.BatchSize = 5000;
});

// Development setup
builder.Services.AddInfluxDbTimeSeriesForDevelopment();
```

## Usage

### Writing Data

#### Single Point Write
```csharp
public class MarketDataService
{
    private readonly ITimeSeriesService _timeSeriesService;

    public async Task RecordMarketDataAsync(string symbol, decimal price, decimal bid, decimal ask)
    {
        var dataPoint = new MarketDataPoint
        {
            Symbol = symbol,
            Exchange = "NYSE",
            Price = price,
            Bid = bid,
            Ask = ask,
            BidSize = 1000,
            AskSize = 1200,
            Volume = 1500000,
            DataType = "quote",
            Source = "MarketDataFeed",
            Timestamp = DateTime.UtcNow
        };
        
        dataPoint.Tags["session"] = "regular";
        dataPoint.Tags["tier"] = "1";
        
        var result = await _timeSeriesService.WritePointAsync(dataPoint);
        
        if (!result.IsSuccess)
        {
            // Handle error
            _logger.LogError($"Failed to write market data: {result.Error}");
        }
    }
}
```

#### Batch Writing for Performance
```csharp
public async Task RecordMarketDataBatchAsync(List<Quote> quotes)
{
    var dataPoints = quotes.Select(q => new MarketDataPoint
    {
        Symbol = q.Symbol,
        Price = q.Price,
        Bid = q.Bid,
        Ask = q.Ask,
        Volume = q.Volume,
        Timestamp = q.Timestamp,
        Source = "QuoteFeed"
    }).ToList();
    
    // Batch write for efficiency
    var result = await _timeSeriesService.WritePointsAsync(dataPoints);
}
```

### Querying Data

#### Get Latest Data
```csharp
// Get latest price for a symbol
var latestPrice = await _timeSeriesService.GetLatestAsync<MarketDataPoint>(
    "market_data",
    new Dictionary<string, string> { ["symbol"] = "AAPL" }
);

if (latestPrice.IsSuccess)
{
    Console.WriteLine($"Latest AAPL price: ${latestPrice.Value.Price}");
}
```

#### Time Range Queries
```csharp
// Get 5-minute price history
var priceHistory = await _timeSeriesService.GetRangeAsync<MarketDataPoint>(
    "market_data",
    DateTime.UtcNow.AddMinutes(-5),
    DateTime.UtcNow,
    new Dictionary<string, string> { ["symbol"] = "TSLA" },
    limit: 300
);
```

#### Aggregated Data
```csharp
// Get 1-minute OHLC bars
var ohlcBars = await _timeSeriesService.AggregateAsync(
    "market_data",
    "price",
    AggregationType.Mean,
    TimeSpan.FromMinutes(1),
    DateTime.UtcNow.AddHours(-1),
    DateTime.UtcNow,
    new Dictionary<string, string> { ["symbol"] = "MSFT" }
);
```

#### Custom Flux Queries
```csharp
// Complex analysis with Flux
var fluxQuery = @"
    from(bucket: ""market-data"")
        |> range(start: -1h)
        |> filter(fn: (r) => r._measurement == ""market_data"")
        |> filter(fn: (r) => r.symbol == ""GOOGL"")
        |> filter(fn: (r) => r._field == ""price"")
        |> movingAverage(n: 20)
        |> yield(name: ""ma20"")";

var movingAverage = await _timeSeriesService.QueryAsync<MarketDataPoint>(fluxQuery);
```

### Performance Tracking

```csharp
public class TradingService
{
    private readonly ITimeSeriesService _timeSeriesService;

    public async Task ExecuteTradeAsync(Order order)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Execute trade logic
            var result = await ProcessOrder(order);
            
            stopwatch.Stop();
            
            // Record performance metrics
            var metrics = new PerformanceMetricsPoint
            {
                Component = "TradingService",
                Operation = "ExecuteTrade",
                LatencyNanoseconds = stopwatch.Elapsed.TotalNanoseconds,
                MemoryBytes = GC.GetTotalMemory(false),
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
                Source = "TradingService",
                Timestamp = DateTime.UtcNow
            };
            
            metrics.CustomMetrics["order_size"] = (long)order.Quantity;
            metrics.CustomMetrics["order_type"] = order.OrderType.GetHashCode();
            
            await _timeSeriesService.WritePointAsync(metrics);
        }
        catch (Exception ex)
        {
            // Record error
            await RecordErrorMetrics("ExecuteTrade", ex);
            throw;
        }
    }
}
```

### Risk Monitoring

```csharp
public async Task MonitorPortfolioRiskAsync()
{
    var positions = await GetCurrentPositions();
    var marketData = await GetLatestMarketData(positions.Select(p => p.Symbol));
    
    // Calculate risk metrics
    var totalValue = positions.Sum(p => p.Quantity * GetPrice(p.Symbol, marketData));
    var var95 = CalculateValueAtRisk(positions, 0.95m);
    
    var riskMetrics = new RiskMetricsPoint
    {
        Portfolio = "main",
        TotalValue = totalValue,
        VaR95 = var95,
        VaR99 = CalculateValueAtRisk(positions, 0.99m),
        ActivePositions = positions.Count,
        Source = "RiskMonitor",
        Timestamp = DateTime.UtcNow
    };
    
    // Add position concentration
    foreach (var position in positions)
    {
        var positionValue = position.Quantity * GetPrice(position.Symbol, marketData);
        riskMetrics.RiskBySymbol[position.Symbol] = positionValue / totalValue;
    }
    
    await _timeSeriesService.WritePointAsync(riskMetrics);
}
```

## Data Organization

### Bucket Strategy

The module uses multiple buckets for optimal organization:

1. **market-data**: High-frequency market data
   - Retention: 30-90 days
   - Precision: Microsecond
   - Volume: Very high

2. **order-data**: Order execution and position data
   - Retention: 1-2 years
   - Precision: Microsecond
   - Volume: Medium

3. **performance-metrics**: System performance metrics
   - Retention: 30 days
   - Precision: Millisecond
   - Volume: High

4. **trading-data**: General trading data and aggregates
   - Retention: 1 year
   - Precision: Second
   - Volume: Low

### Tag Best Practices

Use tags for efficient filtering:
```csharp
point.Tags["symbol"] = "AAPL";        // Always tag with symbol
point.Tags["exchange"] = "NASDAQ";     // Exchange for multi-market
point.Tags["strategy"] = "momentum";   // Strategy identification
point.Tags["account"] = "main";        // Account segregation
point.Tags["session"] = "regular";     // Market session
```

## Performance Optimization

### Batching
```csharp
// Configure optimal batch settings
services.AddInfluxDbTimeSeries(options =>
{
    options.BatchSize = 5000;      // Larger batches for throughput
    options.FlushInterval = 1000;  // 1 second flush interval
    options.EnableGzip = true;     // Compress large batches
});
```

### Connection Pooling
The service maintains persistent connections with automatic reconnection.

### Query Optimization
1. Use time ranges to limit data scanned
2. Filter by tags before field values
3. Use `limit()` to cap result sets
4. Leverage continuous queries for pre-aggregation

### Memory Management
```csharp
// Use streaming for large datasets
var query = "large dataset query...";
await foreach (var point in _timeSeriesService.StreamAsync<MarketDataPoint>(query))
{
    ProcessPoint(point);
}
```

## Monitoring and Health

### Health Checks
```csharp
var health = await _timeSeriesService.IsHealthyAsync();
if (!health.Value)
{
    _logger.LogError("InfluxDB is unhealthy");
}
```

### Database Statistics
```csharp
var stats = await _timeSeriesService.GetStatsAsync();
_logger.LogInfo($"Total points: {stats.Value.TotalPoints:N0}");
_logger.LogInfo($"Write rate: {stats.Value.WriteRatePerSecond:F2} pts/s");
_logger.LogInfo($"Database size: {stats.Value.DatabaseSizeBytes / 1024 / 1024} MB");
```

### Metrics Dashboard

Create Grafana dashboards using InfluxDB as data source:
1. Market data visualization
2. Order execution analytics
3. Risk metrics monitoring
4. System performance tracking

## Troubleshooting

### Common Issues

1. **Connection Failures**
   - Check InfluxDB is running: `curl http://localhost:8086/health`
   - Verify token permissions
   - Check firewall settings

2. **Slow Writes**
   - Increase batch size
   - Enable gzip compression
   - Check network latency

3. **Query Timeouts**
   - Add time range filters
   - Use downsampling for historical data
   - Create continuous queries for aggregates

4. **Memory Usage**
   - Monitor batch sizes
   - Implement streaming for large queries
   - Configure retention policies

### Debug Logging
```csharp
services.AddInfluxDbTimeSeries(options =>
{
    options.EnableDebugLogging = true;
});
```

## Examples

See `Examples/TimeSeriesUsageExample.cs` for comprehensive examples:
- High-frequency market data collection
- Order execution tracking
- Real-time risk monitoring
- Performance analysis
- Trading statistics calculation

## Best Practices

1. **Always use UTC** for timestamps
2. **Tag strategically** for efficient queries
3. **Batch writes** for high-frequency data
4. **Set retention policies** to manage storage
5. **Monitor performance** metrics regularly
6. **Use appropriate precision** (microseconds for trades)
7. **Implement error handling** for network issues
8. **Create indexes** on frequently queried tags
9. **Use continuous queries** for real-time aggregations
10. **Regular backups** of critical data
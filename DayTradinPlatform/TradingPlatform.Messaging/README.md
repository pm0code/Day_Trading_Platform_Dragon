# TradingPlatform.Messaging

High-performance, canonical message queue infrastructure for the Day Trading Platform using Redis Streams.

## Overview

The TradingPlatform.Messaging module provides:
- **Sub-millisecond latency** message publishing and consumption
- **Canonical design pattern** compliance with comprehensive monitoring
- **Priority-based routing** for critical trading events
- **Fault-tolerant subscriptions** with automatic retry and dead-letter queues
- **Built-in metrics** for latency tracking and performance monitoring

## Architecture

### Core Components

1. **CanonicalMessageQueue**: Main service implementing canonical patterns
   - Extends `CanonicalServiceBase` for lifecycle management
   - Provides wrapped operations with automatic logging/metrics
   - Implements circuit breaker patterns for fault tolerance

2. **RedisMessageBus**: Low-level Redis Streams implementation
   - Direct Redis operations optimized for trading latency
   - Consumer group support for scalable processing
   - Message acknowledgment for delivery guarantees

3. **TradingEvents**: Comprehensive event types for all trading scenarios
   - Market data events (quotes, trades, depth)
   - Order lifecycle events (submission, fills, cancellations)
   - Position updates and P&L tracking
   - Risk alerts and compliance violations
   - System health and service lifecycle

### Message Flow

```
Publishers → CanonicalMessageQueue → Redis Streams → CanonicalMessageQueue → Subscribers
                    ↓                                           ↓
                 Metrics                                   Acknowledgments
                Monitoring                                Error Handling
```

## Usage

### Configuration

```csharp
// In Program.cs or Startup.cs
services.AddRedisMessageBus("localhost:6379");

// For production with cluster support
services.AddRedisMessageBusForProduction(
    new[] { "redis1:6379", "redis2:6379", "redis3:6379" },
    password: "your-password"
);
```

### Publishing Messages

```csharp
public class MarketDataService
{
    private readonly ICanonicalMessageQueue _messageQueue;

    public async Task PublishQuoteAsync(string symbol, decimal bid, decimal ask)
    {
        var marketData = new MarketDataEvent
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Price = (bid + ask) / 2,
            DataType = MarketDataType.Quote,
            Source = "MarketDataService"
        };

        // Publish with priority based on volatility
        var priority = IsVolatile(symbol) ? MessagePriority.High : MessagePriority.Normal;
        
        var result = await _messageQueue.PublishAsync(
            "market-data",
            marketData,
            priority
        );

        if (!result.IsSuccess)
        {
            // Handle error
        }
    }
}
```

### Subscribing to Messages

```csharp
public class OrderProcessor : IHostedService
{
    private readonly ICanonicalMessageQueue _messageQueue;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _messageQueue.SubscribeAsync<OrderEvent>(
            streamName: "orders",
            consumerGroup: "order-processors",
            handler: ProcessOrderAsync,
            options: new SubscriptionOptions
            {
                ConsumerName = $"processor-{Environment.MachineName}",
                MaxRetries = 3,
                EnableDeadLetterQueue = true
            },
            cancellationToken
        );
    }

    private async Task<bool> ProcessOrderAsync(OrderEvent order, CancellationToken ct)
    {
        // Process order
        // Return true if successful, false to retry
        return true;
    }
}
```

### Batch Publishing

```csharp
// Publish multiple events efficiently
var signals = GenerateTradingSignals();
var result = await _messageQueue.PublishBatchAsync(
    "signals",
    signals,
    MessagePriority.Normal
);
```

## Event Types

### Market Data Events
- `MarketDataEvent`: Real-time quotes and trades
- `MarketDepthEvent`: Level 2 order book data
- `MarketTradeEvent`: Executed trades in the market

### Order Events
- `OrderEvent`: Order submission and status updates
- `FillEvent`: Trade executions and partial fills

### Position Events
- `PositionEvent`: Position updates with P&L

### Strategy Events
- `SignalEvent`: Trading signals from strategies
- `StrategyEvent`: Strategy lifecycle and parameter updates

### Risk Events
- `RiskEvent`: Risk alerts and violations
- `RiskLimitEvent`: Limit breaches requiring action

### System Events
- `SystemEvent`: System health and performance
- `ServiceEvent`: Service lifecycle notifications

## Performance Considerations

### Latency Targets
- **Publishing**: < 100 microseconds (local Redis)
- **Subscription delivery**: < 500 microseconds
- **End-to-end**: < 1 millisecond

### Optimization Tips

1. **Use Priority Routing**
   ```csharp
   // Critical events get dedicated streams
   await _messageQueue.PublishAsync("orders", order, MessagePriority.Critical);
   ```

2. **Batch Operations**
   ```csharp
   // Reduce round trips for bulk operations
   await _messageQueue.PublishBatchAsync("market-data", updates);
   ```

3. **Local Caching**
   ```csharp
   // Cache frequently accessed data to reduce Redis calls
   var metrics = _messageQueue.GetStreamMetrics("orders");
   ```

## Monitoring

### Built-in Metrics

The CanonicalMessageQueue provides comprehensive metrics:

```csharp
var metrics = messageQueue.GetMetrics();
// Includes:
// - TotalMessagesPublished
// - TotalMessagesReceived
// - TotalErrors
// - Per-stream publish/receive counts
// - Average latencies by stream
```

### Health Checks

```csharp
var health = await messageQueue.CheckHealthAsync();
// Monitors:
// - Redis connection status
// - Network latency
// - Error rates
// - Active subscriptions
```

### Stream-Specific Metrics

```csharp
var streamMetrics = messageQueue.GetStreamMetrics("orders");
// Returns:
// - PublishCount
// - ReceiveCount
// - AveragePublishLatency
// - AverageReceiveLatency
// - LastActivity
```

## Error Handling

### Subscription Failures

The canonical message queue implements:
- Automatic retry with exponential backoff
- Circuit breaker after consecutive failures
- Dead letter queue for unprocessable messages
- Comprehensive error logging with context

### Connection Resilience

- Automatic reconnection to Redis
- Connection pooling for high throughput
- Graceful degradation during outages

## Integration Examples

See `Examples/MessageQueueIntegration.cs` for complete examples:
- Market data publishing service
- Order processing with acknowledgments
- Risk monitoring with multi-stream aggregation
- Signal aggregation and batching

## Best Practices

1. **Use Appropriate Stream Names**
   - `market-data`: Real-time market updates
   - `orders`: Order submissions and updates
   - `fills`: Trade executions
   - `positions`: Position and P&L updates
   - `signals`: Trading signals
   - `risk-alerts`: Risk notifications

2. **Consumer Group Naming**
   - Use descriptive names: `order-processors`, `risk-monitors`
   - Include environment: `prod-order-processors`

3. **Message Design**
   - Keep messages small (< 1KB preferred)
   - Include correlation IDs for tracing
   - Use appropriate event types from TradingEvents

4. **Error Handling**
   - Always check TradingResult.IsSuccess
   - Log errors with context
   - Implement compensation logic for failures

5. **Performance**
   - Monitor latency metrics regularly
   - Use priority levels appropriately
   - Batch operations when possible
   - Consider message size impact

## Troubleshooting

### High Latency
- Check Redis connection latency
- Verify network conditions
- Review message sizes
- Check for blocking operations in handlers

### Message Loss
- Ensure consumer groups are created
- Verify acknowledgment logic
- Check dead letter queues
- Review error logs

### Memory Issues
- Monitor Redis memory usage
- Implement message expiration
- Clean up old consumer groups
- Use appropriate data retention policies
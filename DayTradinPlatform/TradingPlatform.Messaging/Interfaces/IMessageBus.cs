using System;
using System.Threading;
using System.Threading.Tasks;

namespace TradingPlatform.Messaging.Interfaces;

/// <summary>
/// High-performance message bus interface for sub-millisecond trading communication
/// Implements Redis Streams for event-driven microservices architecture
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to a stream with microsecond precision timestamp
    /// Critical path - optimized for sub-millisecond latency
    /// </summary>
    /// <param name="stream">Target stream name (e.g., "market-data", "orders", "alerts")</param>
    /// <param name="message">Message payload with event data</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>Message ID for tracking and acknowledgment</returns>
    Task<string> PublishAsync<T>(string stream, T message, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Subscribe to a stream with consumer group for parallel processing
    /// Maintains message ordering guarantees within partitions
    /// </summary>
    /// <param name="stream">Source stream name</param>
    /// <param name="consumerGroup">Consumer group for load balancing</param>
    /// <param name="consumerName">Unique consumer identifier</param>
    /// <param name="handler">Message processing callback</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    Task SubscribeAsync<T>(string stream, string consumerGroup, string consumerName, 
        Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Acknowledge message processing completion
    /// Required for consumer group message delivery guarantees
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="consumerGroup">Consumer group name</param>
    /// <param name="messageId">Message ID to acknowledge</param>
    Task AcknowledgeAsync(string stream, string consumerGroup, string messageId);

    /// <summary>
    /// Get real-time latency metrics for performance monitoring
    /// Essential for validating sub-millisecond targets
    /// </summary>
    Task<TimeSpan> GetLatencyAsync();

    /// <summary>
    /// Health check for Redis connection and stream availability
    /// Critical for trading system uptime requirements (99.9%)
    /// </summary>
    Task<bool> IsHealthyAsync();
}
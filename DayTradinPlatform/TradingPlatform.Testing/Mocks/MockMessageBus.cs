using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Messaging.Interfaces;
using TradingPlatform.Common.Constants;

namespace TradingPlatform.Testing.Mocks;

/// <summary>
/// Comprehensive mock implementation of IMessageBus for testing purposes.
/// Provides extensive testing capabilities including message capture, latency simulation,
/// error injection, and comprehensive verification methods.
/// </summary>
public class MockMessageBus : IMessageBus
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, List<PublishedMessage>> _publishedMessages = new();
    private readonly ConcurrentDictionary<string, List<SubscriptionInfo>> _subscriptions = new();
    private readonly ConcurrentQueue<string> _acknowledgments = new();
    private readonly Random _random = new();
    
    // Configuration for testing scenarios
    private TimeSpan _simulatedLatency = TimeSpan.FromMicroseconds(50);
    private bool _isHealthy = true;
    private double _errorRate = 0.0; // 0-1 probability of errors
    private bool _captureMessages = true;
    private int _maxCapturedMessages = 10000;

    public MockMessageBus(ILogger? logger = null)
    {
        _logger = logger;
    }

    #region Test Configuration

    /// <summary>
    /// Configures the mock to simulate specific latency for performance testing.
    /// </summary>
    public void SetSimulatedLatency(TimeSpan latency)
    {
        _simulatedLatency = latency;
        _logger?.LogDebug("MockMessageBus latency set to {Latency}ms", latency.TotalMilliseconds);
    }

    /// <summary>
    /// Configures the health status for testing failure scenarios.
    /// </summary>
    public void SetHealthStatus(bool isHealthy)
    {
        _isHealthy = isHealthy;
        _logger?.LogDebug("MockMessageBus health status set to {IsHealthy}", isHealthy);
    }

    /// <summary>
    /// Configures error injection rate for resilience testing.
    /// </summary>
    /// <param name="errorRate">Probability (0-1) of operations failing</param>
    public void SetErrorRate(double errorRate)
    {
        _errorRate = Math.Clamp(errorRate, 0.0, 1.0);
        _logger?.LogDebug("MockMessageBus error rate set to {ErrorRate}%", errorRate * 100);
    }

    /// <summary>
    /// Enables or disables message capture for verification.
    /// </summary>
    public void SetMessageCapture(bool enabled, int maxMessages = 10000)
    {
        _captureMessages = enabled;
        _maxCapturedMessages = maxMessages;
        if (!enabled)
        {
            ClearCapturedMessages();
        }
        _logger?.LogDebug("MockMessageBus message capture set to {Enabled}, max: {MaxMessages}", enabled, maxMessages);
    }

    #endregion

    #region IMessageBus Implementation

    public async Task<string> PublishAsync<T>(string stream, T message, CancellationToken cancellationToken = default) 
        where T : class
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        // Simulate error injection
        if (_random.NextDouble() < _errorRate)
        {
            var error = TradingError.System(
                new InvalidOperationException("Simulated message bus error"), 
                Guid.NewGuid().ToString());
            _logger?.LogError("MockMessageBus simulated publish error for stream {Stream}", stream);
            throw new TradingOperationException(error);
        }

        // Simulate latency
        if (_simulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(_simulatedLatency, cancellationToken);
        }

        var messageId = Guid.NewGuid().ToString();
        var publishedMessage = new PublishedMessage(
            messageId,
            stream,
            typeof(T).Name,
            message,
            DateTime.UtcNow);

        // Capture message for verification
        if (_captureMessages)
        {
            _publishedMessages.AddOrUpdate(stream,
                new List<PublishedMessage> { publishedMessage },
                (key, existing) =>
                {
                    lock (existing)
                    {
                        existing.Add(publishedMessage);
                        // Limit memory usage
                        if (existing.Count > _maxCapturedMessages)
                        {
                            existing.RemoveAt(0);
                        }
                        return existing;
                    }
                });
        }

        _logger?.LogDebug("MockMessageBus published message {MessageId} to stream {Stream}: {MessageType}", 
            messageId, stream, typeof(T).Name);

        return messageId;
    }

    public async Task SubscribeAsync<T>(string stream, string consumerGroup, string consumerName, 
        Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        // Simulate error injection
        if (_random.NextDouble() < _errorRate)
        {
            var error = TradingError.System(
                new InvalidOperationException("Simulated subscription error"), 
                Guid.NewGuid().ToString());
            _logger?.LogError("MockMessageBus simulated subscription error for stream {Stream}", stream);
            throw new TradingOperationException(error);
        }

        // Simulate latency
        if (_simulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(_simulatedLatency, cancellationToken);
        }

        var subscription = new SubscriptionInfo(
            stream,
            consumerGroup,
            consumerName,
            typeof(T).Name,
            DateTime.UtcNow);

        _subscriptions.AddOrUpdate(stream,
            new List<SubscriptionInfo> { subscription },
            (key, existing) =>
            {
                lock (existing)
                {
                    existing.Add(subscription);
                    return existing;
                }
            });

        _logger?.LogDebug("MockMessageBus subscribed {ConsumerName} in group {ConsumerGroup} to stream {Stream}", 
            consumerName, consumerGroup, stream);
    }

    public async Task AcknowledgeAsync(string stream, string consumerGroup, string messageId)
    {
        // Simulate error injection
        if (_random.NextDouble() < _errorRate)
        {
            var error = TradingError.System(
                new InvalidOperationException("Simulated acknowledgment error"), 
                Guid.NewGuid().ToString());
            _logger?.LogError("MockMessageBus simulated acknowledgment error for message {MessageId}", messageId);
            throw new TradingOperationException(error);
        }

        // Simulate latency
        if (_simulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(_simulatedLatency);
        }

        _acknowledgments.Enqueue(messageId);
        _logger?.LogDebug("MockMessageBus acknowledged message {MessageId} for group {ConsumerGroup}", 
            messageId, consumerGroup);
    }

    public async Task<TimeSpan> GetLatencyAsync()
    {
        // Simulate latency
        if (_simulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(TimeSpan.FromMicroseconds(10)); // Minimal delay for latency check
        }

        return _simulatedLatency;
    }

    public async Task<bool> IsHealthyAsync()
    {
        // Simulate latency
        if (_simulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(TimeSpan.FromMicroseconds(10)); // Minimal delay for health check
        }

        return _isHealthy;
    }

    #endregion

    #region Verification Methods

    /// <summary>
    /// Gets all published messages for verification in tests.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PublishedMessage>> GetPublishedMessages()
    {
        return _publishedMessages.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<PublishedMessage>)kvp.Value.ToList());
    }

    /// <summary>
    /// Gets published messages for a specific stream.
    /// </summary>
    public IReadOnlyList<PublishedMessage> GetPublishedMessages(string stream)
    {
        return _publishedMessages.TryGetValue(stream, out var messages) 
            ? messages.ToList() 
            : new List<PublishedMessage>();
    }

    /// <summary>
    /// Gets count of published messages for a specific stream.
    /// </summary>
    public int GetPublishedMessageCount(string stream)
    {
        return _publishedMessages.TryGetValue(stream, out var messages) ? messages.Count : 0;
    }

    /// <summary>
    /// Gets all subscriptions for verification in tests.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SubscriptionInfo>> GetSubscriptions()
    {
        return _subscriptions.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<SubscriptionInfo>)kvp.Value.ToList());
    }

    /// <summary>
    /// Gets subscriptions for a specific stream.
    /// </summary>
    public IReadOnlyList<SubscriptionInfo> GetSubscriptions(string stream)
    {
        return _subscriptions.TryGetValue(stream, out var subs) 
            ? subs.ToList() 
            : new List<SubscriptionInfo>();
    }

    /// <summary>
    /// Gets all acknowledged message IDs.
    /// </summary>
    public IReadOnlyList<string> GetAcknowledgedMessages()
    {
        return _acknowledgments.ToList();
    }

    /// <summary>
    /// Verifies that a message was published to the specified stream.
    /// </summary>
    public bool WasMessagePublished(string stream, string messageType)
    {
        if (!_publishedMessages.TryGetValue(stream, out var messages))
            return false;

        lock (messages)
        {
            return messages.Any(m => m.MessageType == messageType);
        }
    }

    /// <summary>
    /// Verifies that a specific number of messages were published to a stream.
    /// </summary>
    public bool WasMessagePublished(string stream, string messageType, int expectedCount)
    {
        if (!_publishedMessages.TryGetValue(stream, out var messages))
            return expectedCount == 0;

        lock (messages)
        {
            return messages.Count(m => m.MessageType == messageType) == expectedCount;
        }
    }

    /// <summary>
    /// Verifies that a subscription was created for the specified stream.
    /// </summary>
    public bool WasSubscriptionCreated(string stream, string consumerGroup, string consumerName)
    {
        if (!_subscriptions.TryGetValue(stream, out var subs))
            return false;

        lock (subs)
        {
            return subs.Any(s => s.ConsumerGroup == consumerGroup && s.ConsumerName == consumerName);
        }
    }

    /// <summary>
    /// Verifies that a message was acknowledged.
    /// </summary>
    public bool WasMessageAcknowledged(string messageId)
    {
        return _acknowledgments.Contains(messageId);
    }

    /// <summary>
    /// Clears all captured messages and subscriptions for test isolation.
    /// </summary>
    public void ClearCapturedMessages()
    {
        _publishedMessages.Clear();
        _subscriptions.Clear();
        
        // Clear acknowledgments queue
        while (_acknowledgments.TryDequeue(out _))
        {
            // Empty the queue
        }
        
        _logger?.LogDebug("MockMessageBus cleared all captured data");
    }

    /// <summary>
    /// Resets the mock to default state for test isolation.
    /// </summary>
    public void Reset()
    {
        ClearCapturedMessages();
        _simulatedLatency = TimeSpan.FromMicroseconds(50);
        _isHealthy = true;
        _errorRate = 0.0;
        _captureMessages = true;
        _maxCapturedMessages = 10000;
        _logger?.LogDebug("MockMessageBus reset to default state");
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Represents a captured published message for verification.
    /// </summary>
    public record PublishedMessage(
        string MessageId,
        string Stream,
        string MessageType,
        object Message,
        DateTime Timestamp);

    /// <summary>
    /// Represents a captured subscription for verification.
    /// </summary>
    public record SubscriptionInfo(
        string Stream,
        string ConsumerGroup,
        string ConsumerName,
        string MessageType,
        DateTime Timestamp);

    #endregion
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Messaging.Interfaces;

namespace TradingPlatform.Messaging.Services
{
    /// <summary>
    /// Canonical implementation of message queue infrastructure.
    /// Provides high-performance, fault-tolerant messaging with built-in monitoring.
    /// </summary>
    public class CanonicalMessageQueue : CanonicalServiceBase, ICanonicalMessageQueue
    {
        private readonly IMessageBus _messageBus;
        private readonly ConcurrentDictionary<string, SubscriptionInfo> _activeSubscriptions;
        private readonly ConcurrentDictionary<string, MessageMetrics> _streamMetrics;
        private readonly SemaphoreSlim _subscriptionLock;
        
        private long _totalMessagesPublished;
        private long _totalMessagesReceived;
        private long _totalErrors;

        public CanonicalMessageQueue(
            IMessageBus messageBus,
            ITradingLogger logger)
            : base(logger, "CanonicalMessageQueue")
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _activeSubscriptions = new ConcurrentDictionary<string, SubscriptionInfo>();
            _streamMetrics = new ConcurrentDictionary<string, MessageMetrics>();
            _subscriptionLock = new SemaphoreSlim(1, 1);
        }

        #region Message Publishing

        /// <summary>
        /// Publishes a message with canonical tracking and error handling
        /// </summary>
        public async Task<TradingResult<string>> PublishAsync<T>(
            string streamName,
            T message,
            MessagePriority priority = MessagePriority.Normal,
            CancellationToken cancellationToken = default) where T : class
        {
            if (ServiceState != ServiceState.Running)
            {
                return TradingResult<string>.Failure("SERVICE_NOT_RUNNING", 
                    "Message queue service is not in running state");
            }

            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    try
                    {
                        // Add metadata to message
                        var envelope = new MessageEnvelope<T>
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            Timestamp = DateTime.UtcNow,
                            Priority = priority,
                            CorrelationId = CorrelationId,
                            Source = ServiceName,
                            Payload = message
                        };

                        // Publish with priority-based stream selection
                        var targetStream = GetPriorityStream(streamName, priority);
                        var messageId = await _messageBus.PublishAsync(
                            targetStream, 
                            envelope, 
                            cancellationToken);

                        stopwatch.Stop();

                        // Update metrics
                        Interlocked.Increment(ref _totalMessagesPublished);
                        UpdateStreamMetrics(streamName, true, stopwatch.Elapsed);

                        // Log if latency exceeds threshold
                        if (stopwatch.Elapsed.TotalMilliseconds > 1)
                        {
                            LogWarning($"Message publish latency exceeded 1ms: {stopwatch.Elapsed.TotalMilliseconds:F2}ms",
                                additionalData: new { Stream = streamName, MessageId = messageId });
                        }
                        else
                        {
                            LogDebug($"Message published to {streamName} in {stopwatch.Elapsed.TotalMicroseconds:F0}μs",
                                new { MessageId = messageId, Priority = priority });
                        }

                        return TradingResult<string>.Success(messageId);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        Interlocked.Increment(ref _totalErrors);
                        
                        LogError($"Failed to publish message to {streamName}", ex,
                            "Message publishing",
                            "Message not delivered",
                            "Check Redis connection and retry",
                            new { Stream = streamName, Priority = priority });
                            
                        return TradingResult<string>.Failure(
                            TradingError.System(ex, CorrelationId));
                    }
                },
                nameof(PublishAsync));
        }

        /// <summary>
        /// Publishes multiple messages as a batch for efficiency
        /// </summary>
        public async Task<TradingResult<List<string>>> PublishBatchAsync<T>(
            string streamName,
            IEnumerable<T> messages,
            MessagePriority priority = MessagePriority.Normal,
            CancellationToken cancellationToken = default) where T : class
        {
            var results = new List<string>();
            var errors = new List<TradingError>();

            foreach (var message in messages)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var result = await PublishAsync(streamName, message, priority, cancellationToken);
                
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    errors.Add(result.Error!);
                }
            }

            if (errors.Count > 0)
            {
                LogWarning($"Batch publish completed with {errors.Count} errors",
                    additionalData: new { Stream = streamName, SuccessCount = results.Count, ErrorCount = errors.Count });
            }

            return results.Count > 0
                ? TradingResult<List<string>>.Success(results)
                : TradingResult<List<string>>.Failure("BATCH_PUBLISH_FAILED", 
                    $"All messages failed to publish. Errors: {errors.Count}");
        }

        #endregion

        #region Message Subscription

        /// <summary>
        /// Subscribes to a message stream with canonical error handling and monitoring
        /// </summary>
        public async Task<TradingResult> SubscribeAsync<T>(
            string streamName,
            string consumerGroup,
            Func<T, CancellationToken, Task<bool>> handler,
            SubscriptionOptions? options = null,
            CancellationToken cancellationToken = default) where T : class
        {
            if (ServiceState != ServiceState.Running)
            {
                return TradingResult.Failure("SERVICE_NOT_RUNNING", 
                    "Message queue service is not in running state");
            }

            options ??= SubscriptionOptions.Default;
            var subscriptionId = $"{streamName}:{consumerGroup}:{options.ConsumerName}";

            await _subscriptionLock.WaitAsync(cancellationToken);
            try
            {
                if (_activeSubscriptions.ContainsKey(subscriptionId))
                {
                    return TradingResult.Failure("DUPLICATE_SUBSCRIPTION", 
                        $"Subscription already exists: {subscriptionId}");
                }

                var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, 
                    ServiceCancellationToken);

                var subscription = new SubscriptionInfo
                {
                    Id = subscriptionId,
                    StreamName = streamName,
                    ConsumerGroup = consumerGroup,
                    ConsumerName = options.ConsumerName,
                    StartTime = DateTime.UtcNow,
                    CancellationTokenSource = cts
                };

                // Start subscription task
                subscription.SubscriptionTask = Task.Run(async () =>
                {
                    await ProcessSubscriptionAsync(
                        streamName,
                        consumerGroup,
                        options.ConsumerName,
                        handler,
                        options,
                        cts.Token);
                }, cts.Token);

                _activeSubscriptions[subscriptionId] = subscription;

                LogInfo($"Subscription created: {subscriptionId}",
                    additionalData: new
                    {
                        Stream = streamName,
                        ConsumerGroup = consumerGroup,
                        ConsumerName = options.ConsumerName,
                        MaxRetries = options.MaxRetries
                    });

                return TradingResult.Success();
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        /// <summary>
        /// Unsubscribes from a message stream
        /// </summary>
        public async Task<TradingResult> UnsubscribeAsync(
            string streamName,
            string consumerGroup,
            string consumerName)
        {
            var subscriptionId = $"{streamName}:{consumerGroup}:{consumerName}";

            if (!_activeSubscriptions.TryRemove(subscriptionId, out var subscription))
            {
                return TradingResult.Failure("SUBSCRIPTION_NOT_FOUND", 
                    $"No active subscription found: {subscriptionId}");
            }

            try
            {
                subscription.CancellationTokenSource.Cancel();
                
                // Wait for graceful shutdown with timeout
                if (subscription.SubscriptionTask != null)
                {
                    await subscription.SubscriptionTask.WaitAsync(TimeSpan.FromSeconds(5));
                }

                LogInfo($"Subscription cancelled: {subscriptionId}",
                    additionalData: new
                    {
                        Duration = DateTime.UtcNow - subscription.StartTime,
                        MessagesProcessed = subscription.MessagesProcessed,
                        Errors = subscription.ErrorCount
                    });

                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError($"Error unsubscribing: {subscriptionId}", ex);
                return TradingResult.Failure(TradingError.System(ex, CorrelationId));
            }
            finally
            {
                subscription.CancellationTokenSource?.Dispose();
            }
        }

        #endregion

        #region Private Methods

        private async Task ProcessSubscriptionAsync<T>(
            string streamName,
            string consumerGroup,
            string consumerName,
            Func<T, CancellationToken, Task<bool>> handler,
            SubscriptionOptions options,
            CancellationToken cancellationToken) where T : class
        {
            var subscriptionId = $"{streamName}:{consumerGroup}:{consumerName}";
            var consecutiveErrors = 0;

            try
            {
                await _messageBus.SubscribeAsync<MessageEnvelope<T>>(
                    streamName,
                    consumerGroup,
                    consumerName,
                    async (envelope) =>
                    {
                        var messageStopwatch = Stopwatch.StartNew();
                        
                        try
                        {
                            // Extract and process payload
                            var success = await handler(envelope.Payload, cancellationToken);
                            
                            messageStopwatch.Stop();
                            
                            if (success)
                            {
                                consecutiveErrors = 0;
                                Interlocked.Increment(ref _totalMessagesReceived);
                                UpdateStreamMetrics(streamName, false, messageStopwatch.Elapsed);
                                
                                if (_activeSubscriptions.TryGetValue(subscriptionId, out var sub))
                                {
                                    Interlocked.Increment(ref sub.MessagesProcessed);
                                }
                            }
                            else
                            {
                                consecutiveErrors++;
                                LogWarning($"Message handler returned false for {streamName}",
                                    additionalData: new { MessageId = envelope.MessageId });
                            }
                        }
                        catch (Exception ex)
                        {
                            consecutiveErrors++;
                            Interlocked.Increment(ref _totalErrors);
                            
                            if (_activeSubscriptions.TryGetValue(subscriptionId, out var sub))
                            {
                                Interlocked.Increment(ref sub.ErrorCount);
                            }
                            
                            LogError($"Error processing message from {streamName}", ex,
                                "Message processing",
                                "Message may be redelivered",
                                "Check handler implementation",
                                new { MessageId = envelope.MessageId, Stream = streamName });
                            
                            // Check if we should circuit break
                            if (consecutiveErrors >= options.MaxRetries)
                            {
                                LogError($"Max consecutive errors reached for {streamName}", null,
                                    "Subscription circuit breaker",
                                    "Subscription will be terminated",
                                    "Investigate root cause and restart subscription");
                                    
                                throw new InvalidOperationException(
                                    $"Subscription circuit breaker triggered after {consecutiveErrors} errors");
                            }
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogInfo($"Subscription cancelled: {subscriptionId}");
            }
            catch (Exception ex)
            {
                LogError($"Subscription failed: {subscriptionId}", ex);
                throw;
            }
        }

        private void UpdateStreamMetrics(string streamName, bool isPublish, TimeSpan latency)
        {
            var metrics = _streamMetrics.GetOrAdd(streamName, _ => new MessageMetrics());
            
            if (isPublish)
            {
                Interlocked.Increment(ref metrics.PublishCount);
                metrics.UpdatePublishLatency(latency);
            }
            else
            {
                Interlocked.Increment(ref metrics.ReceiveCount);
                metrics.UpdateReceiveLatency(latency);
            }
            
            UpdateMetric($"Stream_{streamName}_PublishCount", metrics.PublishCount);
            UpdateMetric($"Stream_{streamName}_ReceiveCount", metrics.ReceiveCount);
            UpdateMetric($"Stream_{streamName}_AvgPublishLatencyMs", metrics.AveragePublishLatency.TotalMilliseconds);
            UpdateMetric($"Stream_{streamName}_AvgReceiveLatencyMs", metrics.AverageReceiveLatency.TotalMilliseconds);
        }

        private static string GetPriorityStream(string baseStream, MessagePriority priority)
        {
            return priority switch
            {
                MessagePriority.Critical => $"{baseStream}:critical",
                MessagePriority.High => $"{baseStream}:high",
                MessagePriority.Low => $"{baseStream}:low",
                _ => baseStream
            };
        }

        #endregion

        #region Lifecycle Implementation

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing canonical message queue");
            
            // Test Redis connection
            var isHealthy = await _messageBus.IsHealthyAsync();
            if (!isHealthy)
            {
                throw new InvalidOperationException("Redis connection is not healthy");
            }
            
            var latency = await _messageBus.GetLatencyAsync();
            LogInfo($"Redis connection established with latency: {latency.TotalMilliseconds:F2}ms");
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Canonical message queue started");
            return Task.CompletedTask;
        }

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Stopping canonical message queue");
            
            // Cancel all active subscriptions
            var subscriptions = _activeSubscriptions.Values.ToList();
            foreach (var sub in subscriptions)
            {
                sub.CancellationTokenSource.Cancel();
            }
            
            // Wait for all subscriptions to complete
            var tasks = subscriptions
                .Where(s => s.SubscriptionTask != null)
                .Select(s => s.SubscriptionTask!);
                
            await Task.WhenAll(tasks);
            
            LogInfo($"Canonical message queue stopped",
                additionalData: new
                {
                    TotalPublished = _totalMessagesPublished,
                    TotalReceived = _totalMessagesReceived,
                    TotalErrors = _totalErrors,
                    ActiveStreams = _streamMetrics.Count
                });
        }

        protected override async Task<(bool IsHealthy, string Message, Dictionary<string, object>? Details)> 
            OnCheckHealthAsync(CancellationToken cancellationToken)
        {
            try
            {
                var isHealthy = await _messageBus.IsHealthyAsync();
                var latency = await _messageBus.GetLatencyAsync();
                
                var details = new Dictionary<string, object>
                {
                    ["RedisHealthy"] = isHealthy,
                    ["LatencyMs"] = latency.TotalMilliseconds,
                    ["ActiveSubscriptions"] = _activeSubscriptions.Count,
                    ["TotalPublished"] = _totalMessagesPublished,
                    ["TotalReceived"] = _totalMessagesReceived,
                    ["TotalErrors"] = _totalErrors,
                    ["ErrorRate"] = _totalMessagesPublished + _totalMessagesReceived > 0 
                        ? (double)_totalErrors / (_totalMessagesPublished + _totalMessagesReceived) 
                        : 0
                };
                
                if (!isHealthy)
                {
                    return (false, "Redis connection unhealthy", details);
                }
                
                if (latency.TotalMilliseconds > 10)
                {
                    return (false, $"High Redis latency: {latency.TotalMilliseconds:F2}ms", details);
                }
                
                var errorRate = (double)details["ErrorRate"];
                if (errorRate > 0.05) // 5% error rate threshold
                {
                    return (false, $"High error rate: {errorRate:P1}", details);
                }
                
                return (true, "Message queue healthy", details);
            }
            catch (Exception ex)
            {
                return (false, $"Health check failed: {ex.Message}", null);
            }
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var baseMetrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            baseMetrics["TotalMessagesPublished"] = _totalMessagesPublished;
            baseMetrics["TotalMessagesReceived"] = _totalMessagesReceived;
            baseMetrics["TotalErrors"] = _totalErrors;
            baseMetrics["ActiveSubscriptions"] = _activeSubscriptions.Count;
            baseMetrics["ActiveStreams"] = _streamMetrics.Count;
            
            // Add per-stream metrics
            foreach (var kvp in _streamMetrics)
            {
                baseMetrics[$"Stream_{kvp.Key}_PublishCount"] = kvp.Value.PublishCount;
                baseMetrics[$"Stream_{kvp.Key}_ReceiveCount"] = kvp.Value.ReceiveCount;
                baseMetrics[$"Stream_{kvp.Key}_AvgPublishLatencyMs"] = kvp.Value.AveragePublishLatency.TotalMilliseconds;
                baseMetrics[$"Stream_{kvp.Key}_AvgReceiveLatencyMs"] = kvp.Value.AverageReceiveLatency.TotalMilliseconds;
            }
            
            return baseMetrics;
        }

        /// <summary>
        /// Gets detailed metrics for a specific stream
        /// </summary>
        public MessageStreamMetrics GetStreamMetrics(string streamName)
        {
            if (!_streamMetrics.TryGetValue(streamName, out var metrics))
            {
                return new MessageStreamMetrics
                {
                    StreamName = streamName,
                    PublishCount = 0,
                    ReceiveCount = 0,
                    AveragePublishLatency = TimeSpan.Zero,
                    AverageReceiveLatency = TimeSpan.Zero,
                    LastActivity = DateTime.MinValue
                };
            }
            
            return new MessageStreamMetrics
            {
                StreamName = streamName,
                PublishCount = metrics.PublishCount,
                ReceiveCount = metrics.ReceiveCount,
                AveragePublishLatency = metrics.AveragePublishLatency,
                AverageReceiveLatency = metrics.AverageReceiveLatency,
                LastActivity = metrics.LastActivity
            };
        }

        #endregion

        #region Disposal

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cancel all subscriptions
                foreach (var sub in _activeSubscriptions.Values)
                {
                    sub.CancellationTokenSource?.Cancel();
                    sub.CancellationTokenSource?.Dispose();
                }
                
                _activeSubscriptions.Clear();
                _subscriptionLock?.Dispose();
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Canonical message queue interface
    /// </summary>
    public interface ICanonicalMessageQueue
    {
        Task<TradingResult<string>> PublishAsync<T>(
            string streamName,
            T message,
            MessagePriority priority = MessagePriority.Normal,
            CancellationToken cancellationToken = default) where T : class;
            
        Task<TradingResult<List<string>>> PublishBatchAsync<T>(
            string streamName,
            IEnumerable<T> messages,
            MessagePriority priority = MessagePriority.Normal,
            CancellationToken cancellationToken = default) where T : class;
            
        Task<TradingResult> SubscribeAsync<T>(
            string streamName,
            string consumerGroup,
            Func<T, CancellationToken, Task<bool>> handler,
            SubscriptionOptions? options = null,
            CancellationToken cancellationToken = default) where T : class;
            
        Task<TradingResult> UnsubscribeAsync(
            string streamName,
            string consumerGroup,
            string consumerName);
            
        MessageStreamMetrics GetStreamMetrics(string streamName);
    }

    /// <summary>
    /// Message envelope with metadata
    /// </summary>
    public class MessageEnvelope<T> where T : class
    {
        public string MessageId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public MessagePriority Priority { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public T Payload { get; set; } = default!;
    }

    /// <summary>
    /// Message priority levels
    /// </summary>
    public enum MessagePriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Subscription options
    /// </summary>
    public class SubscriptionOptions
    {
        public string ConsumerName { get; set; } = Guid.NewGuid().ToString();
        public int MaxRetries { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool EnableDeadLetterQueue { get; set; } = true;
        
        public static SubscriptionOptions Default => new();
    }

    /// <summary>
    /// Active subscription information
    /// </summary>
    internal class SubscriptionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string StreamName { get; set; } = string.Empty;
        public string ConsumerGroup { get; set; } = string.Empty;
        public string ConsumerName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public Task? SubscriptionTask { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
        public long MessagesProcessed;
        public long ErrorCount;
    }

    /// <summary>
    /// Per-stream metrics
    /// </summary>
    internal class MessageMetrics
    {
        private readonly object _lock = new();
        
        public long PublishCount;
        public long ReceiveCount;
        public TimeSpan TotalPublishLatency;
        public TimeSpan TotalReceiveLatency;
        public DateTime LastActivity { get; private set; }
        
        public TimeSpan AveragePublishLatency => 
            PublishCount > 0 ? TimeSpan.FromTicks(TotalPublishLatency.Ticks / PublishCount) : TimeSpan.Zero;
            
        public TimeSpan AverageReceiveLatency => 
            ReceiveCount > 0 ? TimeSpan.FromTicks(TotalReceiveLatency.Ticks / ReceiveCount) : TimeSpan.Zero;
        
        public void UpdatePublishLatency(TimeSpan latency)
        {
            lock (_lock)
            {
                TotalPublishLatency += latency;
                LastActivity = DateTime.UtcNow;
            }
        }
        
        public void UpdateReceiveLatency(TimeSpan latency)
        {
            lock (_lock)
            {
                TotalReceiveLatency += latency;
                LastActivity = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Stream metrics for external consumption
    /// </summary>
    public class MessageStreamMetrics
    {
        public string StreamName { get; set; } = string.Empty;
        public long PublishCount { get; set; }
        public long ReceiveCount { get; set; }
        public TimeSpan AveragePublishLatency { get; set; }
        public TimeSpan AverageReceiveLatency { get; set; }
        public DateTime LastActivity { get; set; }
    }

    #endregion
}
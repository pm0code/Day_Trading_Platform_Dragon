using System.Collections.Concurrent;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Messaging.Events;
using TradingPlatform.Messaging.Interfaces;

namespace TradingPlatform.MarketData.Services;

/// <summary>
/// Manages real-time market data subscriptions for the trading platform
/// Handles subscription lifecycle and real-time data distribution
/// </summary>
public class SubscriptionManager : ISubscriptionManager
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _activeSubscriptions;
    private readonly Timer _heartbeatTimer;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public SubscriptionManager(IMessageBus messageBus, ILogger logger)
    {
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeSubscriptions = new ConcurrentDictionary<string, SubscriptionInfo>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Send subscription heartbeats every 30 seconds
        _heartbeatTimer = new Timer(SendSubscriptionHeartbeat, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task SubscribeAsync(string symbol)
    {
        try
        {
            var normalizedSymbol = symbol.ToUpperInvariant();
            
            if (_activeSubscriptions.ContainsKey(normalizedSymbol))
            {
                _logger.LogInformation("Already subscribed to {Symbol}, updating subscription time", symbol);
                _activeSubscriptions[normalizedSymbol].LastActivity = DateTime.UtcNow;
                return;
            }

            var subscriptionInfo = new SubscriptionInfo
            {
                Symbol = normalizedSymbol,
                SubscribedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true
            };

            _activeSubscriptions[normalizedSymbol] = subscriptionInfo;

            // Publish subscription event to Redis Streams
            var subscriptionEvent = new MarketDataSubscriptionEvent
            {
                Symbols = new[] { normalizedSymbol },
                Action = "Subscribe",
                Source = "MarketDataService"
            };

            await _messageBus.PublishAsync("market-data-subscriptions", subscriptionEvent);

            _logger.LogInformation("Subscribed to real-time data for {Symbol}", symbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to {Symbol}", symbol);
            throw;
        }
    }

    public async Task UnsubscribeAsync(string symbol)
    {
        try
        {
            var normalizedSymbol = symbol.ToUpperInvariant();
            
            if (!_activeSubscriptions.TryRemove(normalizedSymbol, out var subscriptionInfo))
            {
                _logger.LogWarning("Attempted to unsubscribe from {Symbol} but no active subscription found", symbol);
                return;
            }

            // Publish unsubscription event to Redis Streams
            var subscriptionEvent = new MarketDataSubscriptionEvent
            {
                Symbols = new[] { normalizedSymbol },
                Action = "Unsubscribe",
                Source = "MarketDataService"
            };

            await _messageBus.PublishAsync("market-data-subscriptions", subscriptionEvent);

            _logger.LogInformation("Unsubscribed from real-time data for {Symbol} (active for {Duration})", 
                symbol, DateTime.UtcNow - subscriptionInfo.SubscribedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from {Symbol}", symbol);
            throw;
        }
    }

    public async Task<string[]> GetActiveSubscriptionsAsync()
    {
        await Task.CompletedTask;
        return _activeSubscriptions.Keys.ToArray();
    }

    public async Task<bool> IsSubscribedAsync(string symbol)
    {
        await Task.CompletedTask;
        var normalizedSymbol = symbol.ToUpperInvariant();
        return _activeSubscriptions.ContainsKey(normalizedSymbol);
    }

    // Additional subscription management methods

    /// <summary>
    /// Get subscription statistics
    /// </summary>
    public async Task<SubscriptionStats> GetSubscriptionStatsAsync()
    {
        await Task.CompletedTask;
        
        var now = DateTime.UtcNow;
        var subscriptions = _activeSubscriptions.Values.ToArray();
        
        var averageAge = subscriptions.Length > 0 ?
            TimeSpan.FromTicks((long)subscriptions.Average(s => (now - s.SubscribedAt).Ticks)) :
            TimeSpan.Zero;

        var oldestSubscription = subscriptions.Length > 0 ?
            subscriptions.Min(s => s.SubscribedAt) : now;

        return new SubscriptionStats(
            subscriptions.Length,
            averageAge,
            now - oldestSubscription,
            subscriptions.Count(s => now - s.LastActivity < TimeSpan.FromMinutes(5)), // Active in last 5 minutes
            now);
    }

    /// <summary>
    /// Cleanup stale subscriptions (no activity for more than 1 hour)
    /// </summary>
    public async Task CleanupStaleSubscriptionsAsync()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-1);
            var staleSubscriptions = _activeSubscriptions
                .Where(kvp => kvp.Value.LastActivity < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToArray();

            foreach (var symbol in staleSubscriptions)
            {
                await UnsubscribeAsync(symbol);
                _logger.LogInformation("Cleaned up stale subscription for {Symbol}", symbol);
            }

            if (staleSubscriptions.Length > 0)
            {
                _logger.LogInformation("Cleaned up {StaleCount} stale subscriptions", staleSubscriptions.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up stale subscriptions");
        }
    }

    /// <summary>
    /// Update activity timestamp for a subscription
    /// </summary>
    public async Task UpdateSubscriptionActivityAsync(string symbol)
    {
        await Task.CompletedTask;
        
        var normalizedSymbol = symbol.ToUpperInvariant();
        if (_activeSubscriptions.TryGetValue(normalizedSymbol, out var subscription))
        {
            subscription.LastActivity = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Bulk subscribe to multiple symbols
    /// </summary>
    public async Task SubscribeBatchAsync(string[] symbols)
    {
        var tasks = symbols.Select(SubscribeAsync);
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Batch subscribed to {SymbolCount} symbols: {Symbols}", 
            symbols.Length, string.Join(", ", symbols));
    }

    /// <summary>
    /// Bulk unsubscribe from multiple symbols
    /// </summary>
    public async Task UnsubscribeBatchAsync(string[] symbols)
    {
        var tasks = symbols.Select(UnsubscribeAsync);
        await Task.WhenAll(tasks);
        
        _logger.LogInformation("Batch unsubscribed from {SymbolCount} symbols: {Symbols}", 
            symbols.Length, string.Join(", ", symbols));
    }

    // Private helper methods
    private async void SendSubscriptionHeartbeat(object? state)
    {
        try
        {
            if (_activeSubscriptions.IsEmpty)
                return;

            var activeSymbols = _activeSubscriptions.Keys.ToArray();
            
            var heartbeatEvent = new MarketDataSubscriptionEvent
            {
                Symbols = activeSymbols,
                Action = "Heartbeat",
                Source = "MarketDataService"
            };

            await _messageBus.PublishAsync("market-data-subscriptions", heartbeatEvent);
            
            _logger.LogDebug("Sent subscription heartbeat for {SymbolCount} symbols", activeSymbols.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending subscription heartbeat");
        }
    }

    // Supporting classes
    private class SubscriptionInfo
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime SubscribedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public bool IsActive { get; set; }
    }
}

// Supporting data models
public record SubscriptionStats(
    int ActiveSubscriptionCount,
    TimeSpan AverageSubscriptionAge,
    TimeSpan OldestSubscriptionAge,
    int RecentActivityCount,
    DateTime LastUpdated);

// Additional event types for subscription management
public record MarketDataSubscriptionEvent : TradingEvent
{
    public override string EventType => "MarketDataSubscription";
    public string[] Symbols { get; init; } = Array.Empty<string>();
    public string Action { get; init; } = string.Empty; // Subscribe/Unsubscribe/Heartbeat
}
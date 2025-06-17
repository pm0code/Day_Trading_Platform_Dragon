using System.Collections.Concurrent;
using TradingPlatform.FixEngine.Models;
using TradingPlatform.Core.Interfaces;

namespace TradingPlatform.FixEngine.Core;

/// <summary>
/// High-performance market data subscription manager for FIX protocol
/// Handles Level I/II market data with microsecond precision timestamping
/// </summary>
public sealed class MarketDataManager : IDisposable
{
    private readonly FixSession _fixSession;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, MarketDataSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, MarketDataSnapshot> _snapshots = new();
    private readonly Timer _subscriptionHeartbeat;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    private int _requestIdCounter = 1;
    
    public event EventHandler<MarketDataUpdate>? MarketDataReceived;
    public event EventHandler<string>? SubscriptionStatusChanged;
    
    public MarketDataManager(FixSession fixSession, ILogger logger)
    {
        _fixSession = fixSession ?? throw new ArgumentNullException(nameof(fixSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Subscribe to incoming FIX messages
        _fixSession.MessageReceived += OnFixMessageReceived;
        
        // Periodic subscription health check (every 30 seconds)
        _subscriptionHeartbeat = new Timer(CheckSubscriptionHealth, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
    
    /// <summary>
    /// Subscribe to Level I market data for a symbol
    /// </summary>
    public async Task<bool> SubscribeToQuotesAsync(string symbol, MarketDataEntryType[] entryTypes)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        
        var requestId = Interlocked.Increment(ref _requestIdCounter).ToString();
        
        try
        {
            var subscription = new MarketDataSubscription
            {
                Symbol = symbol,
                RequestId = requestId,
                EntryTypes = entryTypes,
                SubscriptionTime = DateTime.UtcNow,
                Status = SubscriptionStatus.Pending
            };
            
            _subscriptions[symbol] = subscription;
            
            // Create FIX Market Data Request (MsgType V)
            var mdRequest = new FixMessage
            {
                MsgType = FixMessageTypes.MarketDataRequest,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            // Set required fields for market data request
            mdRequest.SetField(262, requestId); // MDReqID
            mdRequest.SetField(263, '1'); // SubscriptionRequestType (1=Snapshot+Updates)
            mdRequest.SetField(264, 1); // MarketDepth (1=Top of Book)
            mdRequest.SetField(267, entryTypes.Length); // NoMDEntryTypes
            
            // Add entry types (Bid=0, Offer=1, Trade=2)
            for (int i = 0; i < entryTypes.Length; i++)
            {
                mdRequest.SetField(269, ((int)entryTypes[i]).ToString()); // MDEntryType
            }
            
            mdRequest.SetField(146, 1); // NoRelatedSym
            mdRequest.SetField(55, symbol); // Symbol
            
            var success = await _fixSession.SendMessageAsync(mdRequest);
            
            if (success)
            {
                _logger.LogInfo($"Market data subscription requested for {symbol}, RequestId: {requestId}");
                subscription.Status = SubscriptionStatus.Active;
                SubscriptionStatusChanged?.Invoke(this, $"Subscribed: {symbol}");
            }
            else
            {
                subscription.Status = SubscriptionStatus.Failed;
                _subscriptions.TryRemove(symbol, out _);
                _logger.LogError($"Failed to send market data request for {symbol}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error subscribing to market data for {symbol}", ex);
            _subscriptions.TryRemove(symbol, out _);
            return false;
        }
    }
    
    /// <summary>
    /// Unsubscribe from market data for a symbol
    /// </summary>
    public async Task<bool> UnsubscribeFromQuotesAsync(string symbol)
    {
        if (!_subscriptions.TryGetValue(symbol, out var subscription))
        {
            _logger.LogWarning($"No active subscription found for {symbol}");
            return false;
        }
        
        try
        {
            // Create FIX Market Data Request with unsubscribe type
            var mdRequest = new FixMessage
            {
                MsgType = FixMessageTypes.MarketDataRequest,
                HardwareTimestamp = GetHardwareTimestamp()
            };
            
            mdRequest.SetField(262, subscription.RequestId); // MDReqID
            mdRequest.SetField(263, '2'); // SubscriptionRequestType (2=Disable previous snapshot+updates)
            mdRequest.SetField(146, 1); // NoRelatedSym
            mdRequest.SetField(55, symbol); // Symbol
            
            var success = await _fixSession.SendMessageAsync(mdRequest);
            
            if (success)
            {
                _subscriptions.TryRemove(symbol, out _);
                _snapshots.TryRemove(symbol, out _);
                _logger.LogInfo($"Market data unsubscribed for {symbol}");
                SubscriptionStatusChanged?.Invoke(this, $"Unsubscribed: {symbol}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error unsubscribing from market data for {symbol}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Get current market data snapshot for a symbol
    /// </summary>
    public MarketDataSnapshot? GetSnapshot(string symbol)
    {
        return _snapshots.TryGetValue(symbol, out var snapshot) ? snapshot : null;
    }
    
    /// <summary>
    /// Get all active subscriptions
    /// </summary>
    public IReadOnlyDictionary<string, MarketDataSubscription> GetActiveSubscriptions()
    {
        return _subscriptions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
    
    private void OnFixMessageReceived(object? sender, FixMessage message)
    {
        try
        {
            switch (message.MsgType)
            {
                case FixMessageTypes.MarketDataSnapshotFullRefresh:
                    HandleMarketDataSnapshot(message);
                    break;
                    
                case FixMessageTypes.MarketDataIncrementalRefresh:
                    HandleMarketDataIncrement(message);
                    break;
                    
                case FixMessageTypes.MarketDataRequestReject:
                    HandleMarketDataReject(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing market data message: {message.MsgType}", ex);
        }
    }
    
    private void HandleMarketDataSnapshot(FixMessage message)
    {
        var symbol = message.GetField(55); // Symbol
        if (string.IsNullOrEmpty(symbol)) return;
        
        var snapshot = new MarketDataSnapshot
        {
            Symbol = symbol,
            Timestamp = DateTime.UtcNow,
            HardwareTimestamp = message.HardwareTimestamp
        };
        
        // Parse market data entries
        var noMDEntries = message.GetIntField(268); // NoMDEntries
        
        for (int i = 0; i < noMDEntries; i++)
        {
            var entryType = message.GetField(269); // MDEntryType
            var entryPx = message.GetDecimalField(270); // MDEntryPx
            var entrySize = message.GetDecimalField(271); // MDEntrySize
            
            switch (entryType)
            {
                case "0": // Bid
                    snapshot.BidPrice = entryPx;
                    snapshot.BidSize = entrySize;
                    break;
                case "1": // Offer
                    snapshot.OfferPrice = entryPx;
                    snapshot.OfferSize = entrySize;
                    break;
                case "2": // Trade
                    snapshot.LastPrice = entryPx;
                    snapshot.LastSize = entrySize;
                    break;
            }
        }
        
        _snapshots[symbol] = snapshot;
        
        var update = new MarketDataUpdate
        {
            Symbol = symbol,
            UpdateType = MarketDataUpdateType.Snapshot,
            Snapshot = snapshot,
            HardwareTimestamp = message.HardwareTimestamp
        };
        
        MarketDataReceived?.Invoke(this, update);
        
        _logger.LogDebug($"Market data snapshot updated for {symbol}: Bid={snapshot.BidPrice}@{snapshot.BidSize}, " +
            $"Offer={snapshot.OfferPrice}@{snapshot.OfferSize}, Last={snapshot.LastPrice}@{snapshot.LastSize}");
    }
    
    private void HandleMarketDataIncrement(FixMessage message)
    {
        var symbol = message.GetField(55); // Symbol
        if (string.IsNullOrEmpty(symbol)) return;
        
        if (!_snapshots.TryGetValue(symbol, out var snapshot))
        {
            // Create new snapshot if none exists
            snapshot = new MarketDataSnapshot
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                HardwareTimestamp = message.HardwareTimestamp
            };
        }
        
        // Parse incremental updates
        var noMDEntries = message.GetIntField(268); // NoMDEntries
        bool hasUpdates = false;
        
        for (int i = 0; i < noMDEntries; i++)
        {
            var entryType = message.GetField(269); // MDEntryType
            var entryPx = message.GetDecimalField(270); // MDEntryPx
            var entrySize = message.GetDecimalField(271); // MDEntrySize
            var updateAction = message.GetField(279); // MDUpdateAction (0=New, 1=Change, 2=Delete)
            
            switch (entryType)
            {
                case "0": // Bid
                    if (updateAction != "2") // Not delete
                    {
                        snapshot.BidPrice = entryPx;
                        snapshot.BidSize = entrySize;
                        hasUpdates = true;
                    }
                    break;
                case "1": // Offer
                    if (updateAction != "2") // Not delete
                    {
                        snapshot.OfferPrice = entryPx;
                        snapshot.OfferSize = entrySize;
                        hasUpdates = true;
                    }
                    break;
                case "2": // Trade
                    if (updateAction != "2") // Not delete
                    {
                        snapshot.LastPrice = entryPx;
                        snapshot.LastSize = entrySize;
                        hasUpdates = true;
                    }
                    break;
            }
        }
        
        if (hasUpdates)
        {
            snapshot.Timestamp = DateTime.UtcNow;
            snapshot.HardwareTimestamp = message.HardwareTimestamp;
            _snapshots[symbol] = snapshot;
            
            var update = new MarketDataUpdate
            {
                Symbol = symbol,
                UpdateType = MarketDataUpdateType.Incremental,
                Snapshot = snapshot,
                HardwareTimestamp = message.HardwareTimestamp
            };
            
            MarketDataReceived?.Invoke(this, update);
        }
    }
    
    private void HandleMarketDataReject(FixMessage message)
    {
        var mdReqId = message.GetField(262); // MDReqID
        var rejectReason = message.GetField(281); // MDReqRejReason
        var text = message.GetField(58); // Text
        
        _logger.LogWarning($"Market data request rejected - RequestId: {mdReqId}, Reason: {rejectReason}, Text: {text}");
        
        // Find and update subscription status
        var failedSubscription = _subscriptions.Values.FirstOrDefault(s => s.RequestId == mdReqId);
        if (failedSubscription != null)
        {
            failedSubscription.Status = SubscriptionStatus.Failed;
            SubscriptionStatusChanged?.Invoke(this, $"Rejected: {failedSubscription.Symbol} - {text}");
        }
    }
    
    private void CheckSubscriptionHealth(object? state)
    {
        var staleThreshold = TimeSpan.FromMinutes(5);
        var now = DateTime.UtcNow;
        
        foreach (var subscription in _subscriptions.Values.ToList())
        {
            if (subscription.Status == SubscriptionStatus.Active && 
                now - subscription.SubscriptionTime > staleThreshold)
            {
                _logger.LogWarning($"Stale subscription detected for {subscription.Symbol}, resubscribing...");
                
                // Attempt to resubscribe
                _ = Task.Run(async () => 
                {
                    await UnsubscribeFromQuotesAsync(subscription.Symbol);
                    await Task.Delay(1000); // Brief delay
                    await SubscribeToQuotesAsync(subscription.Symbol, subscription.EntryTypes);
                });
            }
        }
    }
    
    private long GetHardwareTimestamp()
    {
        return (DateTimeOffset.UtcNow.Ticks - DateTimeOffset.UnixEpoch.Ticks) * 100L;
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _subscriptionHeartbeat?.Dispose();
        _cancellationTokenSource.Dispose();
        
        // Unsubscribe from all active subscriptions
        var symbols = _subscriptions.Keys.ToList();
        foreach (var symbol in symbols)
        {
            _ = UnsubscribeFromQuotesAsync(symbol);
        }
    }
}

/// <summary>
/// Market data subscription information
/// </summary>
public class MarketDataSubscription
{
    public required string Symbol { get; set; }
    public required string RequestId { get; set; }
    public required MarketDataEntryType[] EntryTypes { get; set; }
    public DateTime SubscriptionTime { get; set; }
    public SubscriptionStatus Status { get; set; }
}

/// <summary>
/// Market data snapshot with microsecond precision
/// </summary>
public class MarketDataSnapshot
{
    public required string Symbol { get; set; }
    public DateTime Timestamp { get; set; }
    public long HardwareTimestamp { get; set; }
    
    public decimal BidPrice { get; set; }
    public decimal BidSize { get; set; }
    public decimal OfferPrice { get; set; }
    public decimal OfferSize { get; set; }
    public decimal LastPrice { get; set; }
    public decimal LastSize { get; set; }
    
    public decimal Spread => OfferPrice - BidPrice;
    public decimal MidPrice => (BidPrice + OfferPrice) / 2m;
}

/// <summary>
/// Market data update event args
/// </summary>
public class MarketDataUpdate
{
    public required string Symbol { get; set; }
    public MarketDataUpdateType UpdateType { get; set; }
    public required MarketDataSnapshot Snapshot { get; set; }
    public long HardwareTimestamp { get; set; }
}

/// <summary>
/// Market data entry types for FIX protocol
/// </summary>
public enum MarketDataEntryType
{
    Bid = 0,
    Offer = 1,
    Trade = 2,
    OpeningPrice = 4,
    ClosingPrice = 5,
    SettlementPrice = 6,
    TradingSessionHighPrice = 7,
    TradingSessionLowPrice = 8,
    TradingSessionVWAPPrice = 9
}

/// <summary>
/// Subscription status enumeration
/// </summary>
public enum SubscriptionStatus
{
    Pending,
    Active,
    Failed,
    Cancelled
}

/// <summary>
/// Market data update type
/// </summary>
public enum MarketDataUpdateType
{
    Snapshot,
    Incremental
}
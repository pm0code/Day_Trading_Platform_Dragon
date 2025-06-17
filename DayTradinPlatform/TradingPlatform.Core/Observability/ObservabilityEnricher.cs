// File: TradingPlatform.Core\Observability\ObservabilityEnricher.cs

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace TradingPlatform.Core.Observability;

/// <summary>
/// Enriches observability data with trading-specific context and correlation
/// Provides comprehensive context propagation across all platform components
/// </summary>
public class ObservabilityEnricher : IObservabilityEnricher
{
    private static readonly ThreadLocal<TradingContext> _tradingContext = new();
    private static readonly string _machineId = Environment.MachineName;
    private static readonly string _processId = Environment.ProcessId.ToString();
    
    /// <summary>
    /// Enriches an activity with comprehensive trading context
    /// </summary>
    public void EnrichActivity(Activity activity, string context, object? data = null)
    {
        if (activity == null) return;
        
        // Add core context information
        activity.SetTag("trading.machine_id", _machineId);
        activity.SetTag("trading.process_id", _processId);
        activity.SetTag("trading.thread_id", Thread.CurrentThread.ManagedThreadId.ToString());
        activity.SetTag("trading.context", context);
        activity.SetTag("trading.timestamp", DateTimeOffset.UtcNow.ToString("O"));
        
        // Add trading session context if available
        var tradingContext = _tradingContext.Value;
        if (tradingContext != null)
        {
            activity.SetTag("trading.session_id", tradingContext.SessionId);
            activity.SetTag("trading.account_id", tradingContext.AccountId);
            activity.SetTag("trading.user_id", tradingContext.UserId);
            activity.SetTag("trading.session_start", tradingContext.SessionStart.ToString("O"));
        }
        
        // Add correlation ID if not already present
        if (activity.GetTagItem("trading.correlation_id") == null)
        {
            activity.SetTag("trading.correlation_id", GenerateCorrelationId());
        }
        
        // Add environment-specific context
        activity.SetTag("trading.environment", Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Development");
        activity.SetTag("trading.deployment_version", Environment.GetEnvironmentVariable("DEPLOYMENT_VERSION") ?? "1.0.0");
        
        // Add performance context
        activity.SetTag("trading.gc_generation", GC.GetGeneration(new object()).ToString());
        activity.SetTag("trading.managed_memory", GC.GetTotalMemory(false).ToString());
        
        // Enrich with specific data if provided
        if (data != null)
        {
            EnrichWithDataContext(activity, data);
        }
        
        // Add latency tracking
        if (activity.StartTimeUtc != default)
        {
            var currentLatency = DateTimeOffset.UtcNow - activity.StartTimeUtc;
            activity.SetTag("trading.current_latency_microseconds", currentLatency.TotalMicroseconds.ToString("F2"));
            
            // Flag potential latency issues early
            if (currentLatency.TotalMicroseconds > 50) // 50μs warning threshold
            {
                activity.SetTag("trading.latency_warning", true);
            }
        }
    }
    
    /// <summary>
    /// Generates a unique correlation ID for request tracing
    /// </summary>
    public string GenerateCorrelationId()
    {
        // Create a correlation ID with timestamp and randomness for uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var randomBytes = new byte[8];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var randomHex = Convert.ToHexString(randomBytes);
        
        return $"TRD-{timestamp}-{randomHex}";
    }
    
    /// <summary>
    /// Sets the trading context for the current thread
    /// </summary>
    public void SetTradingContext(string sessionId, string accountId, string userId)
    {
        _tradingContext.Value = new TradingContext
        {
            SessionId = sessionId,
            AccountId = accountId,
            UserId = userId,
            SessionStart = DateTimeOffset.UtcNow
        };
        
        // Create activity to log context establishment
        using var activity = OpenTelemetryInstrumentation.TradingActivitySource.StartActivity("TradingContextEstablished");
        activity?.SetTag("trading.session_id", sessionId);
        activity?.SetTag("trading.account_id", accountId);
        activity?.SetTag("trading.user_id", userId);
        activity?.SetTag("trading.context_timestamp", DateTimeOffset.UtcNow.ToString("O"));
    }
    
    /// <summary>
    /// Gets the current trading context
    /// </summary>
    public TradingContext? GetTradingContext() => _tradingContext.Value;
    
    /// <summary>
    /// Clears the trading context for the current thread
    /// </summary>
    public void ClearTradingContext()
    {
        var context = _tradingContext.Value;
        if (context != null)
        {
            // Log context cleanup
            using var activity = OpenTelemetryInstrumentation.TradingActivitySource.StartActivity("TradingContextCleared");
            activity?.SetTag("trading.session_id", context.SessionId);
            activity?.SetTag("trading.session_duration", (DateTimeOffset.UtcNow - context.SessionStart).TotalMinutes.ToString("F2"));
        }
        
        _tradingContext.Value = null;
    }
    
    /// <summary>
    /// Enriches activity with specific data context
    /// </summary>
    private static void EnrichWithDataContext(Activity activity, object data)
    {
        switch (data)
        {
            case Models.MarketData marketData:
                EnrichWithMarketDataContext(activity, marketData);
                break;
            case Exception exception:
                EnrichWithExceptionContext(activity, exception);
                break;
            default:
                // Generic data enrichment for all types
                activity.SetTag("trading.data_type", data.GetType().Name);
                activity.SetTag("trading.data_assembly", data.GetType().Assembly.GetName().Name);
                
                // Use reflection to safely extract common properties
                var type = data.GetType();
                
                // Common Order properties
                if (type.Name.Contains("Order"))
                {
                    EnrichWithGenericOrderContext(activity, data);
                }
                // Common Position properties  
                else if (type.Name.Contains("Position"))
                {
                    EnrichWithGenericPositionContext(activity, data);
                }
                // Common Risk properties
                else if (type.Name.Contains("Risk"))
                {
                    EnrichWithGenericRiskContext(activity, data);
                }
                else
                {
                    activity.SetTag("trading.data_hash", ComputeDataHash(data));
                }
                break;
        }
    }
    
    private static void EnrichWithGenericOrderContext(Activity activity, object order)
    {
        var type = order.GetType();
        
        // Use reflection to safely extract common order properties
        TrySetProperty(activity, order, "OrderId", "trading.order.id");
        TrySetProperty(activity, order, "Symbol", "trading.order.symbol");
        TrySetProperty(activity, order, "Side", "trading.order.side");
        TrySetProperty(activity, order, "Quantity", "trading.order.quantity");
        TrySetProperty(activity, order, "LimitPrice", "trading.order.limit_price");
        TrySetProperty(activity, order, "Price", "trading.order.price");
        TrySetProperty(activity, order, "Type", "trading.order.type");
        TrySetProperty(activity, order, "OrderType", "trading.order.type");
        TrySetProperty(activity, order, "Status", "trading.order.status");
        TrySetProperty(activity, order, "ClientOrderId", "trading.order.client_id");
        
        // Calculate risk if possible
        var quantity = GetPropertyValue<decimal>(order, "Quantity");
        var price = GetPropertyValue<decimal>(order, "LimitPrice") ?? GetPropertyValue<decimal>(order, "Price");
        
        if (quantity.HasValue && price.HasValue)
        {
            var orderValue = quantity.Value * price.Value;
            activity.SetTag("trading.order.value_usd", orderValue.ToString());
            activity.SetTag("trading.order.risk_category", GetRiskCategory(orderValue));
        }
    }
    
    private static void EnrichWithMarketDataContext(Activity activity, Models.MarketData marketData)
    {
        activity.SetTag("trading.market_data.symbol", marketData.Symbol);
        activity.SetTag("trading.market_data.price", marketData.Price.ToString());
        activity.SetTag("trading.market_data.volume", marketData.Volume.ToString());
        activity.SetTag("trading.market_data.timestamp", marketData.Timestamp.ToString("O"));
        activity.SetTag("trading.market_data.exchange", "unknown");
        
        // Add market condition indicators
        var spread = marketData.CalculateSpread();
        activity.SetTag("trading.market_data.spread", spread.ToString());
        activity.SetTag("trading.market_data.liquidity", GetLiquidityCategory(spread));
    }
    
    private static void EnrichWithGenericPositionContext(Activity activity, object position)
    {
        // Use reflection to safely extract common position properties
        TrySetProperty(activity, position, "Symbol", "trading.position.symbol");
        TrySetProperty(activity, position, "Quantity", "trading.position.quantity");
        TrySetProperty(activity, position, "AveragePrice", "trading.position.avg_price");
        TrySetProperty(activity, position, "MarketValue", "trading.position.market_value");
        TrySetProperty(activity, position, "UnrealizedPnL", "trading.position.unrealized_pnl");
        TrySetProperty(activity, position, "RealizedPnL", "trading.position.realized_pnl");
        TrySetProperty(activity, position, "RiskExposure", "trading.position.risk_exposure");
        
        // Calculate risk indicators if possible
        var marketValue = GetPropertyValue<decimal>(position, "MarketValue");
        if (marketValue.HasValue)
        {
            activity.SetTag("trading.position.risk_exposure", Math.Abs(marketValue.Value).ToString());
            activity.SetTag("trading.position.risk_category", GetRiskCategory(Math.Abs(marketValue.Value)));
        }
    }
    
    private static void EnrichWithGenericRiskContext(Activity activity, object risk)
    {
        // Use reflection to safely extract common risk properties
        TrySetProperty(activity, risk, "VaR95", "trading.risk.var_95");
        TrySetProperty(activity, risk, "VaR99", "trading.risk.var_99");
        TrySetProperty(activity, risk, "VaR1Day", "trading.risk.var_1day");
        TrySetProperty(activity, risk, "ExpectedShortfall", "trading.risk.expected_shortfall");
        TrySetProperty(activity, risk, "MaxDrawdown", "trading.risk.max_drawdown");
        TrySetProperty(activity, risk, "SharpeRatio", "trading.risk.sharpe_ratio");
        TrySetProperty(activity, risk, "Beta", "trading.risk.beta");
        TrySetProperty(activity, risk, "PortfolioVolatility", "trading.risk.portfolio_volatility");
        TrySetProperty(activity, risk, "Leverage", "trading.risk.leverage");
        TrySetProperty(activity, risk, "Concentration", "trading.risk.concentration");
        TrySetProperty(activity, risk, "Correlation", "trading.risk.correlation");
        
        // Add risk level classification using any available VaR metric
        var var95 = GetPropertyValue<decimal>(risk, "VaR95");
        var var99 = GetPropertyValue<decimal>(risk, "VaR99");
        var var1Day = GetPropertyValue<decimal>(risk, "VaR1Day");
        
        var varValue = var95 ?? var99 ?? var1Day ?? 0m;
        activity.SetTag("trading.risk.level", GetRiskLevel(varValue));
    }
    
    private static void EnrichWithExceptionContext(Activity activity, Exception exception)
    {
        activity.SetTag("trading.exception.type", exception.GetType().Name);
        activity.SetTag("trading.exception.message", exception.Message);
        activity.SetTag("trading.exception.stack_trace_hash", ComputeStackTraceHash(exception));
        
        // Add exception severity
        activity.SetTag("trading.exception.severity", GetExceptionSeverity(exception));
        
        // Mark activity as error
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
    
    private static string ComputeDataHash(object data)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hashBytes)[..16]; // First 16 characters
        }
        catch
        {
            return "hash_error";
        }
    }
    
    private static string ComputeStackTraceHash(Exception exception)
    {
        if (string.IsNullOrEmpty(exception.StackTrace)) return "no_stack_trace";
        
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(exception.StackTrace));
        return Convert.ToHexString(hashBytes)[..16]; // First 16 characters
    }
    
    private static string GetRiskCategory(decimal value)
    {
        return value switch
        {
            < 1000 => "low",
            < 10000 => "medium",
            < 100000 => "high",
            _ => "critical"
        };
    }
    
    private static string GetLiquidityCategory(decimal spread)
    {
        return spread switch
        {
            < 0.01m => "high",
            < 0.05m => "medium",
            < 0.10m => "low",
            _ => "poor"
        };
    }
    
    private static string GetRiskLevel(decimal var1Day)
    {
        return Math.Abs(var1Day) switch
        {
            < 1000 => "low",
            < 5000 => "medium",
            < 10000 => "high",
            _ => "critical"
        };
    }
    
    private static string GetExceptionSeverity(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => "critical",
            StackOverflowException => "critical",
            AccessViolationException => "critical",
            InvalidOperationException => "high",
            ArgumentException => "medium",
            TimeoutException => "medium",
            _ => "low"
        };
    }
    
    /// <summary>
    /// Safely attempts to set a property value as an activity tag using reflection
    /// </summary>
    private static void TrySetProperty(Activity activity, object obj, string propertyName, string tagName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(obj);
                if (value != null)
                {
                    activity.SetTag(tagName, value.ToString());
                }
            }
        }
        catch
        {
            // Silently ignore reflection errors to prevent observability from breaking the application
        }
    }
    
    /// <summary>
    /// Safely attempts to get a property value using reflection
    /// </summary>
    private static T? GetPropertyValue<T>(object obj, string propertyName) where T : struct
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(obj);
                if (value is T typedValue)
                {
                    return typedValue;
                }
                // Try to convert if possible
                if (value != null && (typeof(T) == typeof(decimal) || typeof(T) == typeof(int) || typeof(T) == typeof(long)))
                {
                    if (decimal.TryParse(value.ToString(), out var decimalValue))
                    {
                        if (typeof(T) == typeof(decimal))
                            return (T)(object)decimalValue;
                        if (typeof(T) == typeof(int))
                            return (T)(object)(int)decimalValue;
                        if (typeof(T) == typeof(long))
                            return (T)(object)(long)decimalValue;
                    }
                }
            }
        }
        catch
        {
            // Silently ignore reflection errors
        }
        return null;
    }
}

/// <summary>
/// Trading context information for thread-local storage
/// </summary>
public class TradingContext
{
    public string SessionId { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTimeOffset SessionStart { get; init; }
}
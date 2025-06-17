namespace TradingPlatform.Common.Constants;

/// <summary>
/// Trading platform constants including error codes, configuration keys, and business rules.
/// Centralizes all constant values to ensure consistency across the platform.
/// </summary>
public static class TradingConstants
{
    #region Error Codes

    /// <summary>
    /// Standardized error codes for trading operations.
    /// Used for consistent error handling and monitoring across all services.
    /// </summary>
    public static class ErrorCodes
    {
        // Validation Errors (1000-1999)
        public const string ValidationFailed = "TRD-1001";
        public const string InvalidPrice = "TRD-1002";
        public const string InvalidQuantity = "TRD-1003";
        public const string InvalidSymbol = "TRD-1004";
        public const string InvalidOrderId = "TRD-1005";
        public const string InvalidAccountId = "TRD-1006";
        public const string InvalidTimestamp = "TRD-1007";
        public const string InvalidDateRange = "TRD-1008";
        public const string PriceOutOfRange = "TRD-1009";
        public const string QuantityOutOfRange = "TRD-1010";

        // Market Data Errors (2000-2999)
        public const string MarketDataUnavailable = "TRD-2001";
        public const string MarketDataStale = "TRD-2002";
        public const string MarketDataCorrupted = "TRD-2003";
        public const string ProviderUnavailable = "TRD-2004";
        public const string QuotaExceeded = "TRD-2005";
        public const string RateLimitExceeded = "TRD-2006";
        public const string SymbolNotFound = "TRD-2007";
        public const string MarketClosed = "TRD-2008";
        public const string HistoricalDataUnavailable = "TRD-2009";
        public const string RealtimeDataUnavailable = "TRD-2010";

        // Order Management Errors (3000-3999)
        public const string OrderRejected = "TRD-3001";
        public const string OrderNotFound = "TRD-3002";
        public const string OrderAlreadyFilled = "TRD-3003";
        public const string OrderAlreadyCancelled = "TRD-3004";
        public const string InsufficientFunds = "TRD-3005";
        public const string PositionSizeExceeded = "TRD-3006";
        public const string RiskLimitExceeded = "TRD-3007";
        public const string OrderExpired = "TRD-3008";
        public const string MarketOrderAfterHours = "TRD-3009";
        public const string DuplicateOrderId = "TRD-3010";

        // Risk Management Errors (4000-4999)
        public const string RiskCheckFailed = "TRD-4001";
        public const string MaxDrawdownExceeded = "TRD-4002";
        public const string DailyLossLimitExceeded = "TRD-4003";
        public const string PositionLimitExceeded = "TRD-4004";
        public const string ConcentrationRiskExceeded = "TRD-4005";
        public const string LeverageExceeded = "TRD-4006";
        public const string VolatilityLimitExceeded = "TRD-4007";
        public const string PatternDayTraderViolation = "TRD-4008";
        public const string ComplianceViolation = "TRD-4009";
        public const string RiskModelUnavailable = "TRD-4010";

        // System Errors (5000-5999)
        public const string SystemError = "TRD-5001";
        public const string DatabaseError = "TRD-5002";
        public const string NetworkError = "TRD-5003";
        public const string TimeoutError = "TRD-5004";
        public const string ConfigurationError = "TRD-5005";
        public const string ServiceUnavailable = "TRD-5006";
        public const string AuthenticationFailed = "TRD-5007";
        public const string AuthorizationFailed = "TRD-5008";
        public const string ConcurrencyError = "TRD-5009";
        public const string ResourceExhausted = "TRD-5010";

        // Performance Errors (6000-6999)
        public const string LatencyThresholdExceeded = "TRD-6001";
        public const string ThroughputThresholdExceeded = "TRD-6002";
        public const string MemoryThresholdExceeded = "TRD-6003";
        public const string CpuThresholdExceeded = "TRD-6004";
        public const string CircuitBreakerOpen = "TRD-6005";
        public const string RetryLimitExceeded = "TRD-6006";
        public const string PerformanceDegraded = "TRD-6007";
        public const string QueueOverflow = "TRD-6008";
        public const string ConnectionPoolExhausted = "TRD-6009";
        public const string DeadlockDetected = "TRD-6010";
    }

    #endregion

    #region Configuration Keys

    /// <summary>
    /// Configuration keys used throughout the trading platform.
    /// Ensures consistent configuration access across all services.
    /// </summary>
    public static class ConfigurationKeys
    {
        // Database Configuration
        public const string DatabaseConnectionString = "Database:ConnectionString";
        public const string DatabaseTimeout = "Database:CommandTimeout";
        public const string DatabaseRetryCount = "Database:RetryCount";
        public const string DatabasePoolSize = "Database:MaxPoolSize";

        // Redis Configuration
        public const string RedisConnectionString = "Redis:ConnectionString";
        public const string RedisDatabase = "Redis:Database";
        public const string RedisTimeout = "Redis:Timeout";
        public const string RedisRetryCount = "Redis:RetryCount";

        // Market Data Configuration
        public const string AlphaVantageApiKey = "MarketData:AlphaVantage:ApiKey";
        public const string AlphaVantageBaseUrl = "MarketData:AlphaVantage:BaseUrl";
        public const string AlphaVantageRateLimit = "MarketData:AlphaVantage:RateLimit";
        public const string FinnhubApiKey = "MarketData:Finnhub:ApiKey";
        public const string FinnhubBaseUrl = "MarketData:Finnhub:BaseUrl";
        public const string FinnhubRateLimit = "MarketData:Finnhub:RateLimit";

        // Risk Management Configuration
        public const string MaxPositionSize = "RiskManagement:MaxPositionSize";
        public const string MaxDailyLoss = "RiskManagement:MaxDailyLoss";
        public const string MaxDrawdown = "RiskManagement:MaxDrawdown";
        public const string MaxLeverage = "RiskManagement:MaxLeverage";
        public const string ConcentrationLimit = "RiskManagement:ConcentrationLimit";

        // Performance Configuration
        public const string MaxLatencyMs = "Performance:MaxLatencyMs";
        public const string MaxThroughput = "Performance:MaxThroughput";
        public const string CircuitBreakerThreshold = "Performance:CircuitBreakerThreshold";
        public const string RetryMaxAttempts = "Performance:RetryMaxAttempts";
        public const string CacheExpirationMinutes = "Performance:CacheExpirationMinutes";

        // Logging Configuration
        public const string LogLevel = "Logging:LogLevel:Default";
        public const string LogFilePath = "Logging:File:Path";
        public const string LogRetentionDays = "Logging:File:RetentionDays";
        public const string StructuredLogging = "Logging:StructuredLogging";

        // Security Configuration
        public const string EncryptionKey = "Security:EncryptionKey";
        public const string JwtSecret = "Security:JwtSecret";
        public const string ApiKeyHeaderName = "Security:ApiKeyHeaderName";
        public const string SessionTimeout = "Security:SessionTimeoutMinutes";
    }

    #endregion

    #region Business Rules

    /// <summary>
    /// Business rules and limits for trading operations.
    /// These values should be configurable but have sensible defaults.
    /// </summary>
    public static class BusinessRules
    {
        // Position Limits
        public const decimal MaxPositionPercent = 25m; // 25% of account
        public const decimal MinPositionValue = 100m; // $100 minimum
        public const decimal MaxPositionValue = 1000000m; // $1M maximum
        public const int MaxOpenPositions = 50;

        // Risk Limits
        public const decimal MaxRiskPerTradePercent = 2m; // 2% per trade
        public const decimal MaxDailyRiskPercent = 5m; // 5% per day
        public const decimal MaxDrawdownPercent = 10m; // 10% maximum drawdown
        public const decimal DefaultStopLossPercent = 5m; // 5% stop loss

        // Price Limits
        public const decimal MinPrice = 0.01m;
        public const decimal MaxPrice = 100000m;
        public const decimal PricePrecision = 0.01m; // 2 decimal places

        // Quantity Limits
        public const decimal MinQuantity = 1m;
        public const decimal MaxQuantity = 1000000m;
        public const int QuantityPrecision = 0; // Whole shares only

        // Time Limits
        public const int MaxOrderLifeMinutes = 480; // 8 hours
        public const int MarketDataMaxAgeMinutes = 5;
        public const int SessionTimeoutMinutes = 30;

        // Trading Hours (ET)
        public static readonly TimeSpan MarketOpen = new(9, 30, 0);
        public static readonly TimeSpan MarketClose = new(16, 0, 0);
        public static readonly TimeSpan PreMarketOpen = new(4, 0, 0);
        public static readonly TimeSpan AfterHoursClose = new(20, 0, 0);

        // Pattern Day Trading Rules
        public const decimal PdtMinimumEquity = 25000m; // $25,000
        public const int PdtMaxDayTrades = 3; // Per 5 business days
        public const decimal PdtBuyingPower = 4m; // 4:1 leverage

        // Commission and Fees
        public const decimal DefaultCommissionPerShare = 0.005m; // $0.005 per share
        public const decimal MinCommission = 1m; // $1 minimum
        public const decimal MaxCommission = 10m; // $10 maximum

        // Market Data Limits
        public const int MaxSymbolsPerRequest = 100;
        public const int MaxHistoricalDays = 365;
        public const int DefaultCacheExpirationMinutes = 5;
    }

    #endregion

    #region Performance Thresholds

    /// <summary>
    /// Performance thresholds for monitoring and alerting.
    /// Used for SLA monitoring and performance optimization.
    /// </summary>
    public static class PerformanceThresholds
    {
        // Latency Thresholds (milliseconds)
        public const int OrderExecutionMaxLatencyMs = 100; // Sub-millisecond target
        public const int MarketDataMaxLatencyMs = 50;
        public const int DatabaseQueryMaxLatencyMs = 100;
        public const int ApiCallMaxLatencyMs = 1000;
        public const int CacheAccessMaxLatencyMs = 10;

        // Throughput Thresholds (per second)
        public const int MaxOrdersPerSecond = 1000;
        public const int MaxMarketDataUpdatesPerSecond = 10000;
        public const int MaxApiCallsPerSecond = 100;
        public const int MaxDatabaseTransactionsPerSecond = 500;

        // Resource Thresholds
        public const double MaxCpuUsagePercent = 80.0;
        public const double MaxMemoryUsagePercent = 85.0;
        public const double MaxDiskUsagePercent = 90.0;
        public const int MaxActiveConnections = 1000;

        // Circuit Breaker Thresholds
        public const int CircuitBreakerFailureThreshold = 5;
        public const int CircuitBreakerSuccessThreshold = 3;
        public const int CircuitBreakerTimeoutSeconds = 30;

        // Retry Thresholds
        public const int MaxRetryAttempts = 3;
        public const int RetryDelayMs = 100;
        public const double RetryBackoffMultiplier = 2.0;
    }

    #endregion

    #region Cache Keys

    /// <summary>
    /// Cache key templates for consistent caching across the platform.
    /// Includes TTL recommendations for different data types.
    /// </summary>
    public static class CacheKeys
    {
        // Market Data Cache Keys
        public const string MarketDataPrefix = "market:";
        public const string RealTimeQuote = "market:quote:{symbol}";
        public const string HistoricalData = "market:history:{symbol}:{startDate}:{endDate}";
        public const string CompanyProfile = "market:profile:{symbol}";
        public const string MarketStatus = "market:status";

        // Order Cache Keys
        public const string OrderPrefix = "order:";
        public const string OrderById = "order:id:{orderId}";
        public const string OrdersByAccount = "order:account:{accountId}";
        public const string OrdersBySymbol = "order:symbol:{symbol}";

        // Portfolio Cache Keys
        public const string PortfolioPrefix = "portfolio:";
        public const string PositionsByAccount = "portfolio:positions:{accountId}";
        public const string AccountBalance = "portfolio:balance:{accountId}";
        public const string PortfolioValue = "portfolio:value:{accountId}";

        // Risk Cache Keys
        public const string RiskPrefix = "risk:";
        public const string RiskLimits = "risk:limits:{accountId}";
        public const string RiskMetrics = "risk:metrics:{accountId}";
        public const string DailyPnL = "risk:pnl:{accountId}:{date}";

        // Configuration Cache Keys
        public const string ConfigPrefix = "config:";
        public const string TradingHours = "config:trading-hours";
        public const string Holidays = "config:holidays:{year}";
        public const string SymbolMaster = "config:symbols";

        // Cache TTL Recommendations (seconds)
        public const int RealTimeDataTtl = 30; // 30 seconds
        public const int HistoricalDataTtl = 3600; // 1 hour
        public const int ConfigurationTtl = 86400; // 24 hours
        public const int SessionDataTtl = 1800; // 30 minutes
        public const int ReferenceDataTtl = 43200; // 12 hours
    }

    #endregion

    #region Message Types

    /// <summary>
    /// Message types for inter-service communication.
    /// Used for consistent messaging across the platform.
    /// </summary>
    public static class MessageTypes
    {
        // Order Events
        public const string OrderSubmitted = "order.submitted";
        public const string OrderAccepted = "order.accepted";
        public const string OrderRejected = "order.rejected";
        public const string OrderFilled = "order.filled";
        public const string OrderPartiallyFilled = "order.partially_filled";
        public const string OrderCancelled = "order.cancelled";
        public const string OrderExpired = "order.expired";

        // Market Data Events
        public const string MarketDataUpdate = "market.data.update";
        public const string QuoteUpdate = "market.quote.update";
        public const string TradeUpdate = "market.trade.update";
        public const string MarketStatusUpdate = "market.status.update";

        // Risk Events
        public const string RiskLimitBreached = "risk.limit.breached";
        public const string PositionLimitExceeded = "risk.position.exceeded";
        public const string DrawdownAlert = "risk.drawdown.alert";
        public const string MarginCall = "risk.margin.call";

        // System Events
        public const string ServiceStarted = "system.service.started";
        public const string ServiceStopped = "system.service.stopped";
        public const string HealthCheckFailed = "system.health.failed";
        public const string PerformanceAlert = "system.performance.alert";

        // User Events
        public const string UserLoggedIn = "user.logged.in";
        public const string UserLoggedOut = "user.logged.out";
        public const string UserSessionExpired = "user.session.expired";
        public const string UserPreferencesUpdated = "user.preferences.updated";
    }

    #endregion

    #region Metric Names

    /// <summary>
    /// Standardized metric names for monitoring and observability.
    /// Used for consistent metrics collection across all services.
    /// </summary>
    public static class MetricNames
    {
        // Performance Metrics
        public const string OrderExecutionLatency = "trading.order.execution.latency";
        public const string MarketDataLatency = "trading.market_data.latency";
        public const string DatabaseLatency = "trading.database.latency";
        public const string CacheHitRate = "trading.cache.hit_rate";
        public const string ThroughputPerSecond = "trading.throughput.per_second";

        // Business Metrics
        public const string OrdersPerMinute = "trading.orders.per_minute";
        public const string DailyPnL = "trading.pnl.daily";
        public const string PositionCount = "trading.positions.count";
        public const string AccountValue = "trading.account.value";
        public const string RiskUtilization = "trading.risk.utilization";

        // Error Metrics
        public const string ErrorRate = "trading.errors.rate";
        public const string OrderRejectionRate = "trading.orders.rejection_rate";
        public const string MarketDataErrors = "trading.market_data.errors";
        public const string SystemErrors = "trading.system.errors";

        // Resource Metrics
        public const string CpuUsage = "system.cpu.usage";
        public const string MemoryUsage = "system.memory.usage";
        public const string DiskUsage = "system.disk.usage";
        public const string NetworkUsage = "system.network.usage";
        public const string ConnectionCount = "system.connections.count";
    }

    #endregion

    #region Trading Sessions

    /// <summary>
    /// Trading session definitions and market hours.
    /// Used for session-aware trading logic and market data handling.
    /// </summary>
    public static class TradingSessions
    {
        public const string PreMarket = "PRE_MARKET";
        public const string RegularHours = "REGULAR_HOURS";
        public const string AfterHours = "AFTER_HOURS";
        public const string Closed = "CLOSED";

        // Session Times (Eastern Time)
        public static readonly Dictionary<string, (TimeSpan Start, TimeSpan End)> SessionTimes = new()
        {
            [PreMarket] = (new TimeSpan(4, 0, 0), new TimeSpan(9, 30, 0)),
            [RegularHours] = (new TimeSpan(9, 30, 0), new TimeSpan(16, 0, 0)),
            [AfterHours] = (new TimeSpan(16, 0, 0), new TimeSpan(20, 0, 0))
        };
    }

    #endregion

    #region Default Values

    /// <summary>
    /// Default values for various trading operations.
    /// Provides sensible defaults when explicit values are not provided.
    /// </summary>
    public static class Defaults
    {
        // Order Defaults
        public const string DefaultOrderType = "MARKET";
        public const string DefaultTimeInForce = "DAY";
        public const decimal DefaultQuantity = 100m;

        // Risk Defaults
        public const decimal DefaultStopLoss = 0.05m; // 5%
        public const decimal DefaultTakeProfit = 0.10m; // 10%
        public const decimal DefaultRiskPerTrade = 0.02m; // 2%

        // Configuration Defaults
        public const int DefaultCacheExpiration = 300; // 5 minutes
        public const int DefaultRetryAttempts = 3;
        public const int DefaultTimeout = 30; // seconds
        public const int DefaultPageSize = 50;
        public const int DefaultMaxResults = 1000;

        // Precision Defaults
        public const int PricePrecisionDigits = 2;
        public const int QuantityPrecisionDigits = 0;
        public const int PercentagePrecisionDigits = 4;
        public const int CurrencyPrecisionDigits = 2;
    }

    #endregion
}
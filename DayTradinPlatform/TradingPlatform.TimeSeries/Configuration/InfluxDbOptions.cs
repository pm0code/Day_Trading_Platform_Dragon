namespace TradingPlatform.TimeSeries.Configuration
{
    /// <summary>
    /// Configuration options for InfluxDB connection and behavior
    /// </summary>
    public class InfluxDbOptions
    {
        /// <summary>
        /// InfluxDB connection URL (e.g., http://localhost:8086)
        /// </summary>
        public string Url { get; set; } = "http://localhost:8086";

        /// <summary>
        /// Authentication token for InfluxDB
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Organization name in InfluxDB
        /// </summary>
        public string Organization { get; set; } = "trading-platform";

        /// <summary>
        /// Default bucket for trading data
        /// </summary>
        public string DefaultBucket { get; set; } = "trading-data";

        /// <summary>
        /// Bucket for high-frequency market data
        /// </summary>
        public string MarketDataBucket { get; set; } = "market-data";

        /// <summary>
        /// Bucket for order and execution data
        /// </summary>
        public string OrderDataBucket { get; set; } = "order-data";

        /// <summary>
        /// Bucket for performance metrics
        /// </summary>
        public string MetricsBucket { get; set; } = "performance-metrics";

        /// <summary>
        /// Batch size for write operations
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Flush interval in milliseconds
        /// </summary>
        public int FlushInterval { get; set; } = 100;

        /// <summary>
        /// Retry interval in milliseconds
        /// </summary>
        public int RetryInterval { get; set; } = 5000;

        /// <summary>
        /// Maximum retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Enable gzip compression for writes
        /// </summary>
        public bool EnableGzip { get; set; } = true;

        /// <summary>
        /// Write timeout in milliseconds
        /// </summary>
        public int WriteTimeout { get; set; } = 10000;

        /// <summary>
        /// Read timeout in milliseconds
        /// </summary>
        public int ReadTimeout { get; set; } = 30000;

        /// <summary>
        /// Maximum retention period in days (0 = infinite)
        /// </summary>
        public int RetentionDays { get; set; } = 365;

        /// <summary>
        /// Enable debug logging
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;
    }
}
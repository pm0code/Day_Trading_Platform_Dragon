// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\DataIngestionConfig.cs
using TradingPlatform.DataIngestion.Models;

namespace TradingPlatform.DataIngestion.Configuration
{
    public class DataIngestionConfig
    {
        public ApiConfiguration ApiSettings { get; set; } = new();
        public RetryPolicy RetrySettings { get; set; } = new();
        public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
        public string[] PrioritySymbols { get; set; } = { "SPY", "QQQ", "AAPL", "TSLA", "MSFT" };
        public bool EnableBackupProviders { get; set; } = true;
        public int MaxConcurrentRequests { get; set; } = 10;
    }

    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public double BackoffMultiplier { get; set; } = 2.0;
    }

    public class CircuitBreakerConfig
    {
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public int SamplingDuration { get; set; } = 60; // seconds
    }
}
// 28 lines

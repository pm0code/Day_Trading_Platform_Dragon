// d:\Projects\C#.Net\DayTradingPlatform-P\DayTradinPlatform\ApiConfiguration.cs
namespace TradingPlatform.DataIngestion.Models
{
    public class ApiConfiguration
    {
        public AlphaVantageConfig AlphaVantage { get; set; } = new();
        public FinnhubConfig Finnhub { get; set; } = new();
        public CacheConfig Cache { get; set; } = new();
    }

    public class AlphaVantageConfig
    {
        public string ApiKey { get; set; } = "demo";
        public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";
        public int DailyLimit { get; set; } = 500;
        public int RequestsPerMinute { get; set; } = 5;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class FinnhubConfig
    {
        public string ApiKey { get; set; } = "sandbox_token";
        public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";
        public int RequestsPerMinute { get; set; } = 60;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class CacheConfig
    {
        public int QuoteCacheMinutes { get; set; } = 1;
        public int HistoricalCacheHours { get; set; } = 24;
        public int NewsCacheMinutes { get; set; } = 15;
        public int MaxCacheSize { get; set; } = 1000;
    }
}
// 32 lines

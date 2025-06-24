// File: TradingPlatform.Core.Configuration\TradingPlatformSettings.cs

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Central configuration settings for the Trading Platform
    /// </summary>
    public class TradingPlatformSettings
    {
        /// <summary>
        /// API configuration settings
        /// </summary>
        public ApiSettings Api { get; set; } = new();

        /// <summary>
        /// Cache configuration settings
        /// </summary>
        public CacheSettings Cache { get; set; } = new();

        /// <summary>
        /// Risk management settings
        /// </summary>
        public RiskSettings Risk { get; set; } = new();

        /// <summary>
        /// Screening configuration settings
        /// </summary>
        public ScreeningSettings Screening { get; set; } = new();

        /// <summary>
        /// Logging configuration settings
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();
    }

    public class ApiSettings
    {
        /// <summary>
        /// Finnhub API configuration
        /// </summary>
        public FinnhubSettings Finnhub { get; set; } = new();

        /// <summary>
        /// AlphaVantage API configuration
        /// </summary>
        public AlphaVantageSettings AlphaVantage { get; set; } = new();
    }

    public class FinnhubSettings
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";
        public int RateLimitPerMinute { get; set; } = 60;
        public bool Enabled { get; set; } = true;
    }

    public class AlphaVantageSettings
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";
        public int RateLimitPerMinute { get; set; } = 5;
        public bool Enabled { get; set; } = true;
        public bool UsePremium { get; set; } = false;
    }

    public class CacheSettings
    {
        public int DefaultExpirationMinutes { get; set; } = 5;
        public int MarketDataExpirationMinutes { get; set; } = 1;
        public int CompanyDataExpirationMinutes { get; set; } = 60;
        public long MaxSizeInMB { get; set; } = 100;
        public bool Enabled { get; set; } = true;
    }

    public class RiskSettings
    {
        public decimal MaxPositionSizePercent { get; set; } = 0.10m; // 10%
        public decimal MaxSectorExposurePercent { get; set; } = 0.30m; // 30%
        public decimal MaxDrawdownPercent { get; set; } = 0.20m; // 20%
        public decimal DefaultStopLossPercent { get; set; } = 0.02m; // 2%
        public int PatternDayTraderMinBalance { get; set; } = 25000;
        public int NonPDTMaxDayTrades { get; set; } = 3;
    }

    public class ScreeningSettings
    {
        public decimal DefaultMinPrice { get; set; } = 5.00m;
        public decimal DefaultMaxPrice { get; set; } = 500.00m;
        public long DefaultMinVolume { get; set; } = 1_000_000;
        public decimal DefaultMinATR { get; set; } = 1.00m;
        public decimal DefaultMinGapPercent { get; set; } = 2.00m;
        public bool EnablePennyStocks { get; set; } = false;
        public int MaxConcurrentScreenings { get; set; } = 50;
    }

    public class LoggingSettings
    {
        public string LogLevel { get; set; } = "Information";
        public string LogDirectory { get; set; } = "logs";
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableConsoleLogging { get; set; } = true;
        public int MaxFileSizeInMB { get; set; } = 10;
        public int RetentionDays { get; set; } = 30;
    }
}
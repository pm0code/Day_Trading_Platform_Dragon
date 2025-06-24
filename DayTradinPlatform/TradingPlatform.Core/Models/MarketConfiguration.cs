// File: TradingPlatform.Core\Models\MarketConfiguration.cs

using System;
using System.Collections.Generic;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Models
{
    public class MarketConfiguration
    {
        private readonly ITradingLogger _logger;

        public MarketConfiguration(ITradingLogger logger)
        {
            _logger = logger;
        }

        public string MarketCode { get; set; } = string.Empty;
        public string MarketName { get; set; } = string.Empty;
        public List<string> Exchanges { get; set; } = new();
        public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

        private TimeSpan _marketOpen;
        public TimeSpan MarketOpen
        {
            get => _marketOpen;
            set
            {
                if (value > MarketClose)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Invalid MarketOpen ({value}) after MarketClose ({MarketClose}) for {MarketCode}. Setting MarketOpen to default (9:30 AM).");
                    _marketOpen = new TimeSpan(9, 30, 0);
                }
                else
                {
                    _marketOpen = value;
                }
            }
        }


        private TimeSpan _marketClose;
        public TimeSpan MarketClose
        {
            get => _marketClose;
            set
            {
                if (value < MarketOpen)
                {
                    TradingLogOrchestrator.Instance.LogWarning($"Invalid MarketClose ({value}) before MarketOpen ({MarketOpen}) for {MarketCode}. Setting MarketClose to default (4:00 PM).");
                    _marketClose = new TimeSpan(16, 0, 0);
                }
                else
                {
                    _marketClose = value;
                }
            }
        }


        public List<DayOfWeek> TradingDays { get; set; } = new();
        public string CurrencyCode { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<DateTime> MarketHolidays { get; set; } = new();

        // Improved DataProviderMapping using a Dictionary
        public Dictionary<string, List<string>> DataProviderMapping { get; set; } = new();


        // Validation method
        public bool IsValid()
        {
            if (MarketOpen > MarketClose)
            {
                TradingLogOrchestrator.Instance.LogWarning($"Invalid configuration for {MarketCode}: MarketOpen is after MarketClose.");
                return false;
            }

            // Other validations can be added here as needed

            return true;
        }
    }

    public class MarketSelector
    {
        public List<string> SelectedMarkets { get; set; } = new() { "US" };
        public string PrimaryMarket { get; set; } = "US";
        public bool EnableMultiMarket { get; set; } = false;
        public Dictionary<string, MarketConfiguration> AvailableMarkets { get; set; } = new();

        public static MarketSelector CreateDefault()
        {
            var selector = new MarketSelector();
            var logger = TradingLogOrchestrator.Instance; // Create logger for default config

            selector.AvailableMarkets["US"] = new MarketConfiguration(logger)
            {
                MarketCode = "US",
                MarketName = "United States",
                Exchanges = new List<string> { "NYSE", "NASDAQ", "AMEX" },
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
                MarketOpen = new TimeSpan(9, 30, 0),
                MarketClose = new TimeSpan(16, 0, 0),
                TradingDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                CurrencyCode = "USD",
                IsActive = true,
                DataProviderMapping = new Dictionary<string, List<string>>
                {
                    { "primary", new List<string> { "alphavantage", "finnhub" } }
                }
            };

            return selector;
        }
    }

    public interface IMarketService
    {
        Task<bool> IsMarketOpenAsync(string marketCode);
        Task<MarketConfiguration> GetMarketConfigAsync(string marketCode);
        Task<List<string>> GetActiveMarketsAsync();
        Task<bool> IsSymbolValidForMarketAsync(string symbol, string marketCode);
    }
}
// 92 lines

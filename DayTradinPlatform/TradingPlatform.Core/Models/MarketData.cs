// File: TradingPlatform.Core\Models\MarketData.cs

using System;
using System.Collections.Generic;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging; // For logging

namespace TradingPlatform.Core.Models
{
    public class MarketData
    {
        private readonly ITradingLogger _logger; // Add logger

        public MarketData(ITradingLogger logger) // Inject logger
        {
            _logger = logger;
        }


        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public long Volume { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Open { get; set; }
        public decimal PreviousClose { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal MarketCap { get; set; }
        public long AverageDailyVolume { get; set; }

        private decimal _bid;
        public decimal Bid
        {
            get => _bid;
            set
            {
                if (value > Ask)
                {
                    // Log warning and correct the value
                    TradingLogOrchestrator.Instance.LogWarning($"Invalid Bid ({value}) > Ask ({Ask}) for {Symbol}. Setting Bid to Ask.");
                    _bid = Ask; // Consistent correction
                }
                else
                {
                    _bid = value;
                }
            }
        }

        private decimal _ask;
        public decimal Ask
        {
            get => _ask;
            set
            {
                if (value < Bid)
                {
                    // Log warning and correct the value
                    TradingLogOrchestrator.Instance.LogWarning($"Invalid Ask ({value}) < Bid ({Bid}) for {Symbol}. Setting Ask to Bid.");
                    _ask = Bid; // Consistent correction
                }
                else
                {
                    _ask = value;
                }
            }
        }

        public int BidSize { get; set; }
        public int AskSize { get; set; }

        public decimal CalculateSpread()
        {
            return Ask - Bid;
        }

        public decimal CalculateSpreadPercent()
        {
            var midPrice = (Bid + Ask) / 2m;
            if (midPrice == 0m) return 0m;
            return (CalculateSpread() / midPrice) * 100m;
        }

        public decimal CalculateRelativeVolume()
        {
            if (AverageDailyVolume == 0) return 0m;
            return (decimal)Volume / AverageDailyVolume;
        }
    }

    public class MarketTick
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Volume { get; set; }
        public DateTime Timestamp { get; set; }
        public string Exchange { get; set; } = string.Empty;
    }

    public class HistoricalData
    {
        public string Symbol { get; set; } = string.Empty;
        public List<DailyData> DailyPrices { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsChronologicallyOrdered()
        {
            for (int i = 1; i < DailyPrices.Count; i++)
            {
                if (DailyPrices[i].Date <= DailyPrices[i - 1].Date)
                    return false;
            }
            return true;
        }

        public DailyData? GetLatestData()
        {
            return DailyPrices.Count > 0 ? DailyPrices[DailyPrices.Count - 1] : null;
        }
    }
}
// 95 lines

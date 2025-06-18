// File: TradingPlatform.Core\Models\TradingCriteria.cs

using System;
using TradingPlatform.Core.Interfaces; // For logging

namespace TradingPlatform.Core.Models
{
    public class TradingCriteria
    {
        private readonly ILogger _logger; // Add logger for validation messages

        public TradingCriteria(ILogger logger)
        {
            _logger = logger;
        }


        private long _minimumVolume = 1_000_000;
        public long MinimumVolume
        {
            get => _minimumVolume;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumVolume cannot be negative. Setting to default (1,000,000).");
                    _minimumVolume = 1_000_000;
                }
                else
                {
                    _minimumVolume = value;
                }
            }
        }


        private decimal _minimumRelativeVolume = 2.0m;
        public decimal MinimumRelativeVolume
        {
            get => _minimumRelativeVolume;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumRelativeVolume cannot be negative. Setting to default (2.0).");
                    _minimumRelativeVolume = 2.0m;
                }
                else
                {
                    _minimumRelativeVolume = value;
                }
            }
        }

        private decimal _minimumPrice = 5.00m;
        public decimal MinimumPrice
        {
            get => _minimumPrice;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumPrice cannot be negative. Setting to default (5.00).");
                    _minimumPrice = 5.00m;
                }
                else
                {
                    _minimumPrice = value;
                }
            }
        }


        private decimal _maximumPrice = 500.00m;
        public decimal MaximumPrice
        {
            get => _maximumPrice;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MaximumPrice cannot be negative. Setting to default (500.00).");
                    _maximumPrice = 500.00m;
                }
                else if (value < MinimumPrice)
                {
                    _logger.LogWarning("MaximumPrice cannot be less than MinimumPrice. Setting to MinimumPrice.");
                    _maximumPrice = MinimumPrice;
                }
                else
                {
                    _maximumPrice = value;
                }
            }
        }


        public bool EnablePennyStocks { get; set; } = false;


        private decimal _minimumATR = 0.25m;
        public decimal MinimumATR
        {
            get => _minimumATR;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumATR cannot be negative. Setting to default (0.25).");
                    _minimumATR = 0.25m;
                }
                else
                {
                    _minimumATR = value;
                }
            }
        }


        private decimal _minimumChangePercent = 2.0m;
        public decimal MinimumChangePercent
        {
            get => _minimumChangePercent;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumChangePercent cannot be negative. Setting to default (2.0).");
                    _minimumChangePercent = 2.0m;
                }
                else
                {
                    _minimumChangePercent = value;
                }
            }
        }


        private decimal _minimumMarketCap = 100_000_000m;
        public decimal MinimumMarketCap
        {
            get => _minimumMarketCap;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumMarketCap cannot be negative. Setting to default (100,000,000).");
                    _minimumMarketCap = 100_000_000m;
                }
                else
                {
                    _minimumMarketCap = value;
                }
            }
        }


        private decimal _minimumGapPercent = 3.0m;
        public decimal MinimumGapPercent
        {
            get => _minimumGapPercent;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MinimumGapPercent cannot be negative. Setting to default (3.0).");
                    _minimumGapPercent = 3.0m;
                }
                else
                {
                    _minimumGapPercent = value;
                }
            }
        }


        public bool RequireNewsEvent { get; set; } = false;
        public bool RequireEarningsEvent { get; set; } = false;
        public bool RequireBreakoutPattern { get; set; } = false;
        public bool RequireTrendAlignment { get; set; } = false;


        private decimal _maximumSpread = 0.05m;
        public decimal MaximumSpread
        {
            get => _maximumSpread;
            set
            {
                if (value < 0)
                {
                    _logger.LogWarning("MaximumSpread cannot be negative. Setting to default (0.05).");
                    _maximumSpread = 0.05m;
                }
                else
                {
                    _maximumSpread = value;
                }
            }
        }


        public bool RequireOptionsActivity { get; set; } = false;


        public bool IsValidPrice(decimal price)
        {
            if (!EnablePennyStocks && price < 5.00m) return false;
            return price >= MinimumPrice && price <= MaximumPrice;
        }

        public bool IsValidVolume(long volume, long averageVolume)
        {
            return volume >= MinimumVolume &&
                   (averageVolume == 0 || (decimal)volume / averageVolume >= MinimumRelativeVolume);
        }
    }
}
// 166 lines

// File: TradingPlatform.Screening.Indicators\VolumeIndicators.cs

using TradingPlatform.Core.Models;

namespace TradingPlatform.Screening.Indicators
{
    /// <summary>
    /// Provides modular, mathematically correct volume-based indicators for trading strategies.
    /// All calculations use decimal arithmetic and comply with FinancialCalculationStandards.md.
    /// </summary>
    public static class VolumeIndicators
    {
        /// <summary>
        /// Calculates the average volume over the specified period.
        /// </summary>
        public static decimal AverageVolume(IEnumerable<DailyData> data, int period)
        {
            var recent = data.TakeLast(period).Select(d => (decimal)d.Volume);
            return recent.Any() ? recent.Average() : 0m;
        }

        /// <summary>
        /// Calculates the relative volume (current volume divided by average volume).
        /// </summary>
        public static decimal RelativeVolume(long currentVolume, IEnumerable<DailyData> data, int period)
        {
            var avg = AverageVolume(data, period);
            return avg > 0m ? (decimal)currentVolume / avg : 0m;
        }

        /// <summary>
        /// Calculates the volume spike ratio (current volume divided by max recent volume).
        /// </summary>
        public static decimal VolumeSpike(long currentVolume, IEnumerable<DailyData> data, int period)
        {
            var max = data.TakeLast(period).Select(d => d.Volume).DefaultIfEmpty(0).Max();
            return max > 0 ? (decimal)currentVolume / max : 0m;
        }

        /// <summary>
        /// Returns true if current volume is at least spikeThreshold times the average volume.
        /// </summary>
        public static bool IsVolumeSpiking(long currentVolume, IEnumerable<DailyData> data, int period, decimal spikeThreshold = 3.0m)
        {
            var avg = AverageVolume(data, period);
            return avg > 0m && ((decimal)currentVolume / avg) >= spikeThreshold;
        }
    }
}

// Total Lines: 44

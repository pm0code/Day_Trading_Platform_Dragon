// File: TradingPlatform.Screening.Criteria\VolumeCriteria.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.Screening.Criteria
{
    /// <summary>
    /// Evaluates volume-based trading criteria using mathematically correct, standards-compliant logic.
    /// </summary>
    public class VolumeCriteria
    {
        private readonly ILogger _logger;

        public VolumeCriteria(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<CriteriaResult> EvaluateVolumeAsync(MarketData marketData, TradingCriteria criteria)
        {
            var result = new CriteriaResult
            {
                CriteriaName = "Volume",
                EvaluatedAt = DateTime.UtcNow
            };

            try
            {
                var volumeScore = CalculateVolumeScore(marketData.Volume, criteria.MinimumVolume);
                var relativeVolumeScore = CalculateRelativeVolumeScore(
                    marketData.Volume,
                    marketData.AverageDailyVolume,
                    criteria.MinimumRelativeVolume);

                result.Score = (volumeScore * 0.6m) + (relativeVolumeScore * 0.4m);
                result.Passed = result.Score >= 0.7m;

                result.Metrics["CurrentVolume"] = marketData.Volume;
                result.Metrics["AverageDailyVolume"] = marketData.AverageDailyVolume;
                result.Metrics["RelativeVolume"] = marketData.AverageDailyVolume > 0
                    ? (decimal)marketData.Volume / marketData.AverageDailyVolume
                    : 0m;
                result.Metrics["VolumeScore"] = volumeScore;
                result.Metrics["RelativeVolumeScore"] = relativeVolumeScore;

                if (result.Passed)
                {
                    result.Reason = $"Strong volume: {marketData.Volume:N0} shares " +
                                    $"({result.Metrics["RelativeVolume"]:F1}x average)";
                }
                else
                {
                    var issues = new List<string>();
                    if (marketData.Volume < criteria.MinimumVolume)
                        issues.Add($"Volume {marketData.Volume:N0} below minimum {criteria.MinimumVolume:N0}");
                    if ((decimal)marketData.Volume / (marketData.AverageDailyVolume == 0 ? 1 : marketData.AverageDailyVolume) < criteria.MinimumRelativeVolume)
                        issues.Add($"Relative volume {result.Metrics["RelativeVolume"]:F1}x below minimum {criteria.MinimumRelativeVolume:F1}x");
                    result.Reason = string.Join("; ", issues);
                }

                _logger.LogTrace($"Volume evaluation for {marketData.Symbol}: Score={result.Score:F2}, Passed={result.Passed}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error evaluating volume criteria for {marketData.Symbol}");
                result.Passed = false;
                result.Score = 0m;
                result.Reason = $"Evaluation error: {ex.Message}";
                return result;
            }
        }

        private decimal CalculateVolumeScore(long currentVolume, long minimumVolume)
        {
            if (currentVolume <= 0) return 0m;
            if (currentVolume >= minimumVolume * 2) return 1.0m;
            if (currentVolume >= minimumVolume) return 0.8m;
            return Math.Max(0m, (decimal)currentVolume / minimumVolume * 0.6m);
        }

        private decimal CalculateRelativeVolumeScore(long currentVolume, long averageDailyVolume, decimal minimumRelativeVolume)
        {
            if (averageDailyVolume <= 0 || currentVolume <= 0) return 0m;
            var relativeVolume = (decimal)currentVolume / averageDailyVolume;
            if (relativeVolume >= minimumRelativeVolume * 2m) return 1.0m;
            if (relativeVolume >= minimumRelativeVolume) return 0.8m;
            return Math.Max(0m, relativeVolume / minimumRelativeVolume * 0.6m);
        }
    }
}

// Total Lines: 84

// File: TradingPlatform.Screening.Indicators\TechnicalIndicators.cs

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Mathematics;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Indicators
{
    /// <summary>
    /// Provides mathematical, standards-compliant technical indicator calculations.
    /// </summary>
    public class TechnicalIndicators
    {
        private readonly ITradingLogger _logger;

        public TechnicalIndicators(ITradingLogger logger)
        {
            _logger = logger;
        }

        public Task<decimal> CalculateRSIAsync(List<DailyData> priceData, int period = 14)
        {
            if (priceData.Count < period + 1) return Task.FromResult(50m);

            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < priceData.Count; i++)
            {
                var change = priceData[i].Close - priceData[i - 1].Close;
                gains.Add(change > 0m ? change : 0m);
                losses.Add(change < 0m ? Math.Abs(change) : 0m);
            }

            var avgGain = gains.TakeLast(period).Average();
            var avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0m) return Task.FromResult(100m);

            var rs = avgGain / avgLoss;
            var rsi = 100m - (100m / (1m + rs));

            TradingLogOrchestrator.Instance.LogInfo($"RSI calculated: {rsi:F2}");
            return Task.FromResult(rsi);
        }

        public Task<(decimal SMA20, decimal SMA50)> CalculateMovingAveragesAsync(List<DailyData> priceData)
        {
            var sma20 = priceData.Count >= 20
                ? priceData.TakeLast(20).Average(d => d.Close)
                : priceData.Average(d => d.Close);

            var sma50 = priceData.Count >= 50
                ? priceData.TakeLast(50).Average(d => d.Close)
                : priceData.Average(d => d.Close);

            TradingLogOrchestrator.Instance.LogInfo($"SMAs calculated: SMA20={sma20:F2}, SMA50={sma50:F2}");
            return Task.FromResult((sma20, sma50));
        }

        public Task<decimal> CalculateBollingerBandPositionAsync(MarketData marketData, List<DailyData> priceData, int period = 20)
        {
            if (priceData.Count < period) return Task.FromResult(0.5m);

            var recentPrices = priceData.TakeLast(period).Select(d => d.Close).ToList();
            var sma = recentPrices.Average();
            var stdDev = FinancialMath.StandardDeviation(recentPrices);

            var upperBand = sma + (2m * stdDev);
            var lowerBand = sma - (2m * stdDev);

            if ((upperBand - lowerBand) == 0m) return Task.FromResult(0.5m);

            var position = (marketData.Price - lowerBand) / (upperBand - lowerBand);
            position = Math.Max(0m, Math.Min(1m, position));

            TradingLogOrchestrator.Instance.LogInfo($"Bollinger position: {position:F2} (Price: {marketData.Price:F2}, Upper: {upperBand:F2}, Lower: {lowerBand:F2})");
            return Task.FromResult(position);
        }

        public Task<string> DetectCandlestickPatternAsync(List<DailyData> priceData)
        {
            if (priceData.Count < 3) return Task.FromResult("Insufficient Data");

            var current = priceData.Last();
            var previous = priceData[priceData.Count - 2];
            var beforePrevious = priceData[priceData.Count - 3];

            if (IsDoji(current)) return Task.FromResult("Doji");
            if (IsHammer(current, previous)) return Task.FromResult("Hammer");
            if (IsEngulfing(current, previous)) return Task.FromResult("Engulfing");
            if (IsThreeWhiteSoldiers(current, previous, beforePrevious)) return Task.FromResult("Three White Soldiers");
            if (IsThreeBlackCrows(current, previous, beforePrevious)) return Task.FromResult("Three Black Crows");

            return Task.FromResult("No Pattern");
        }

        public Task<TrendDirection> AnalyzeTrendAsync(List<DailyData> priceData, int period = 20)
        {
            if (priceData.Count < period) return Task.FromResult(TrendDirection.Sideways);

            var recentData = priceData.TakeLast(period).ToList();
            var firstPrice = recentData.First().Close;
            var lastPrice = recentData.Last().Close;
            var changePercent = ((lastPrice - firstPrice) / firstPrice) * 100m;

            var slope = CalculateLinearRegressionSlope(recentData);

            if (changePercent > 5m && slope > 0.1m) return Task.FromResult(TrendDirection.StrongUptrend);
            if (changePercent > 2m && slope > 0.05m) return Task.FromResult(TrendDirection.Uptrend);
            if (changePercent < -5m && slope < -0.1m) return Task.FromResult(TrendDirection.StrongDowntrend);
            if (changePercent < -2m && slope < -0.05m) return Task.FromResult(TrendDirection.Downtrend);

            return Task.FromResult(TrendDirection.Sideways);
        }

        public Task<bool> IsBreakoutSetupAsync(MarketData marketData, List<DailyData> priceData)
        {
            if (priceData.Count < 20) return Task.FromResult(false);

            var recentHigh = priceData.TakeLast(20).Max(d => d.High);
            var recentLow = priceData.TakeLast(20).Min(d => d.Low);
            var range = recentHigh - recentLow;

            if (range == 0m) return Task.FromResult(false);

            var nearResistance = (recentHigh - marketData.Price) / range < 0.05m;
            var nearSupport = (marketData.Price - recentLow) / range < 0.05m;

            // CRITICAL FIX: Cast volume to decimal before averaging to avoid double/decimal mixing
            var avgVolume = priceData.TakeLast(10).Average(d => (decimal)d.Volume);
            var volumeSpike = marketData.Volume > (avgVolume * 1.5m);

            return Task.FromResult((nearResistance || nearSupport) && volumeSpike);
        }

        // --- Candlestick Pattern Helpers ---
        private bool IsDoji(DailyData candle)
        {
            var bodySize = Math.Abs(candle.Close - candle.Open);
            var totalRange = candle.High - candle.Low;
            return totalRange > 0m && (bodySize / totalRange) < 0.1m;
        }

        private bool IsHammer(DailyData current, DailyData previous)
        {
            var bodySize = Math.Abs(current.Close - current.Open);
            var lowerShadow = Math.Min(current.Open, current.Close) - current.Low;
            var upperShadow = current.High - Math.Max(current.Open, current.Close);

            return lowerShadow > (bodySize * 2m) && upperShadow < bodySize && current.Close > previous.Close;
        }

        private bool IsEngulfing(DailyData current, DailyData previous)
        {
            var currentBullish = current.Close > current.Open;
            var previousBullish = previous.Close > previous.Open;

            if (currentBullish && !previousBullish)
            {
                return current.Open < previous.Close && current.Close > previous.Open;
            }

            if (!currentBullish && previousBullish)
            {
                return current.Open > previous.Close && current.Close < previous.Open;
            }

            return false;
        }

        private bool IsThreeWhiteSoldiers(DailyData current, DailyData previous, DailyData beforePrevious)
        {
            return current.Close > current.Open &&
                   previous.Close > previous.Open &&
                   beforePrevious.Close > beforePrevious.Open &&
                   current.Close > previous.Close &&
                   previous.Close > beforePrevious.Close;
        }

        private bool IsThreeBlackCrows(DailyData current, DailyData previous, DailyData beforePrevious)
        {
            return current.Close < current.Open &&
                   previous.Close < previous.Open &&
                   beforePrevious.Close < beforePrevious.Open &&
                   current.Close < previous.Close &&
                   previous.Close < beforePrevious.Close;
        }

        private decimal CalculateLinearRegressionSlope(List<DailyData> data)
        {
            if (data == null || data.Count < 2)
            {
                TradingLogOrchestrator.Instance.LogWarning("CalculateLinearRegressionSlope: Not enough data points. Returning 0.");
                return 0m;
            }

            var n = (decimal)data.Count;
            var sumX = 0m;
            var sumY = 0m;
            var sumXY = 0m;
            var sumX2 = 0m;

            for (int i = 0; i < data.Count; i++)
            {
                var x = (decimal)i;
                var y = data[i].Close;

                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumX2 += x * x;
            }

            var denominator = (n * sumX2) - (sumX * sumX);

            if (denominator == 0m)
            {
                TradingLogOrchestrator.Instance.LogWarning("CalculateLinearRegressionSlope: Denominator is zero. Returning 0.");
                return 0m;
            }

            return ((n * sumXY) - (sumX * sumY)) / denominator;
        }

        public enum TrendDirection
        {
            StrongDowntrend,
            Downtrend,
            Sideways,
            Uptrend,
            StrongUptrend
        }
    }
}

// Total Lines: 144

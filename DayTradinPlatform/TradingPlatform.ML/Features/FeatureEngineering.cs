// File: TradingPlatform.ML/Features/FeatureEngineering.cs

using TradingPlatform.Core.Models;
using TradingPlatform.Common.Extensions;

namespace TradingPlatform.ML.Features
{
    /// <summary>
    /// Feature engineering for ML models
    /// </summary>
    public static class FeatureEngineering
    {
        /// <summary>
        /// Extract technical indicators from market data
        /// </summary>
        public static TechnicalFeatures ExtractTechnicalFeatures(
            IList<MarketDataSnapshot> data,
            int currentIndex)
        {
            if (currentIndex < 50 || currentIndex >= data.Count)
                throw new ArgumentException("Insufficient data for feature extraction");
            
            var current = data[currentIndex];
            var features = new TechnicalFeatures();
            
            // Price features
            features.Open = current.Open;
            features.High = current.High;
            features.Low = current.Low;
            features.Close = current.Close;
            features.Volume = current.Volume;
            
            // Calculate RSI (14-period)
            features.RSI = CalculateRSI(data, currentIndex, 14);
            
            // Calculate MACD
            var macd = CalculateMACD(data, currentIndex);
            features.MACD = macd.MACD;
            features.MACDSignal = macd.Signal;
            features.MACDHistogram = macd.Histogram;
            
            // Calculate Bollinger Bands
            var bollinger = CalculateBollingerBands(data, currentIndex, 20);
            features.BollingerUpper = bollinger.Upper;
            features.BollingerLower = bollinger.Lower;
            features.BollingerMiddle = bollinger.Middle;
            
            // Moving averages
            features.SMA20 = CalculateSMA(data, currentIndex, 20);
            features.SMA50 = CalculateSMA(data, currentIndex, 50);
            features.EMA12 = CalculateEMA(data, currentIndex, 12);
            features.EMA26 = CalculateEMA(data, currentIndex, 26);
            
            // Volume features
            features.VolumeRatio = CalculateVolumeRatio(data, currentIndex);
            features.VWAP = CalculateVWAP(data, currentIndex);
            
            // Price change features
            features.PriceChangePercent = CalculatePriceChange(data[currentIndex-1].Close, current.Close);
            features.DailyRange = (current.High - current.Low) / current.Close;
            
            // Pattern features
            features.IsGapUp = current.Open > data[currentIndex-1].High;
            features.IsGapDown = current.Open < data[currentIndex-1].Low;
            
            return features;
        }
        
        /// <summary>
        /// Extract market microstructure features
        /// </summary>
        public static MicrostructureFeatures ExtractMicrostructureFeatures(
            MarketDataSnapshot snapshot,
            IList<MarketDataSnapshot> recentData)
        {
            var features = new MicrostructureFeatures();
            
            // Spread features
            features.BidAskSpread = snapshot.Ask - snapshot.Bid;
            features.RelativeSpread = features.BidAskSpread / snapshot.Close;
            
            // Liquidity features
            features.BidSize = snapshot.BidSize ?? 0;
            features.AskSize = snapshot.AskSize ?? 0;
            features.LiquidityImbalance = (features.BidSize - features.AskSize) / (features.BidSize + features.AskSize + 1);
            
            // Volatility features
            features.RealizedVolatility = CalculateRealizedVolatility(recentData, 20);
            features.ParkinsonVolatility = CalculateParkinsonVolatility(recentData, 20);
            
            // Order flow features
            features.TradeImbalance = CalculateTradeImbalance(snapshot);
            features.VolumeWeightedPrice = CalculateVWAP(recentData, recentData.Count - 1);
            
            return features;
        }
        
        /// <summary>
        /// Create time-based features
        /// </summary>
        public static TimeFeatures ExtractTimeFeatures(DateTime timestamp)
        {
            return new TimeFeatures
            {
                HourOfDay = timestamp.Hour,
                DayOfWeek = (int)timestamp.DayOfWeek,
                DayOfMonth = timestamp.Day,
                MonthOfYear = timestamp.Month,
                IsMarketOpen = IsMarketOpen(timestamp),
                MinutesFromOpen = GetMinutesFromMarketOpen(timestamp),
                MinutesToClose = GetMinutesToMarketClose(timestamp),
                IsPreMarket = IsPreMarket(timestamp),
                IsAfterHours = IsAfterHours(timestamp)
            };
        }
        
        // Technical indicator calculations
        private static decimal CalculateRSI(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period) return 50m; // Default neutral RSI
            
            decimal avgGain = 0, avgLoss = 0;
            
            for (int i = index - period + 1; i <= index; i++)
            {
                var change = data[i].Close - data[i-1].Close;
                if (change > 0)
                    avgGain += change;
                else
                    avgLoss += Math.Abs(change);
            }
            
            avgGain /= period;
            avgLoss /= period;
            
            if (avgLoss == 0) return 100m;
            
            var rs = avgGain / avgLoss;
            return 100m - (100m / (1m + rs));
        }
        
        private static (decimal MACD, decimal Signal, decimal Histogram) CalculateMACD(
            IList<MarketDataSnapshot> data, int index)
        {
            var ema12 = CalculateEMA(data, index, 12);
            var ema26 = CalculateEMA(data, index, 26);
            var macd = ema12 - ema26;
            
            // Simplified signal line (9-period EMA of MACD)
            var signal = macd * 0.2m + ema12 * 0.8m; // Approximation
            var histogram = macd - signal;
            
            return (macd, signal, histogram);
        }
        
        private static (decimal Upper, decimal Middle, decimal Lower) CalculateBollingerBands(
            IList<MarketDataSnapshot> data, int index, int period)
        {
            var sma = CalculateSMA(data, index, period);
            var stdDev = CalculateStandardDeviation(data, index, period);
            
            return (sma + 2 * stdDev, sma, sma - 2 * stdDev);
        }
        
        private static decimal CalculateSMA(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period - 1) return data[index].Close;
            
            var sum = 0m;
            for (int i = index - period + 1; i <= index; i++)
            {
                sum += data[i].Close;
            }
            return sum / period;
        }
        
        private static decimal CalculateEMA(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period - 1) return data[index].Close;
            
            var multiplier = 2m / (period + 1);
            var ema = data[index - period + 1].Close;
            
            for (int i = index - period + 2; i <= index; i++)
            {
                ema = (data[i].Close - ema) * multiplier + ema;
            }
            
            return ema;
        }
        
        private static decimal CalculateStandardDeviation(IList<MarketDataSnapshot> data, int index, int period)
        {
            var mean = CalculateSMA(data, index, period);
            var sumSquares = 0m;
            
            for (int i = index - period + 1; i <= index; i++)
            {
                var diff = data[i].Close - mean;
                sumSquares += diff * diff;
            }
            
            return DecimalMath.Sqrt(sumSquares / period);
        }
        
        private static decimal CalculateVolumeRatio(IList<MarketDataSnapshot> data, int index)
        {
            if (index < 20) return 1m;
            
            var currentVolume = data[index].Volume;
            var avgVolume = 0m;
            
            for (int i = index - 20; i < index; i++)
            {
                avgVolume += data[i].Volume;
            }
            avgVolume /= 20;
            
            return avgVolume > 0 ? currentVolume / avgVolume : 1m;
        }
        
        private static decimal CalculateVWAP(IList<MarketDataSnapshot> data, int index)
        {
            decimal totalValue = 0;
            decimal totalVolume = 0;
            
            // Calculate for current trading day (simplified)
            var dayStart = Math.Max(0, index - 390); // ~6.5 hours of minute bars
            
            for (int i = dayStart; i <= index; i++)
            {
                var typicalPrice = (data[i].High + data[i].Low + data[i].Close) / 3;
                totalValue += typicalPrice * data[i].Volume;
                totalVolume += data[i].Volume;
            }
            
            return totalVolume > 0 ? totalValue / totalVolume : data[index].Close;
        }
        
        private static decimal CalculatePriceChange(decimal previousClose, decimal currentClose)
        {
            return previousClose > 0 ? (currentClose - previousClose) / previousClose * 100 : 0m;
        }
        
        private static decimal CalculateRealizedVolatility(IList<MarketDataSnapshot> data, int period)
        {
            if (data.Count < period + 1) return 0m;
            
            var returns = new List<decimal>();
            for (int i = data.Count - period; i < data.Count; i++)
            {
                var ret = CalculatePriceChange(data[i-1].Close, data[i].Close) / 100m;
                returns.Add(ret);
            }
            
            var mean = returns.Average();
            var sumSquares = returns.Sum(r => (r - mean) * (r - mean));
            
            return DecimalMath.Sqrt(sumSquares / (period - 1)) * DecimalMath.Sqrt(252m); // Annualized
        }
        
        private static decimal CalculateParkinsonVolatility(IList<MarketDataSnapshot> data, int period)
        {
            if (data.Count < period) return 0m;
            
            var sum = 0m;
            for (int i = data.Count - period; i < data.Count; i++)
            {
                var ratio = DecimalMath.Log(data[i].High / data[i].Low);
                sum += ratio * ratio;
            }
            
            return DecimalMath.Sqrt(sum / (4 * period * DecimalMath.Log(2m))) * DecimalMath.Sqrt(252m);
        }
        
        private static decimal CalculateTradeImbalance(MarketDataSnapshot snapshot)
        {
            // Placeholder - would use actual trade data in production
            return 0m;
        }
        
        private static bool IsMarketOpen(DateTime timestamp)
        {
            if (timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday)
                return false;
            
            var time = timestamp.TimeOfDay;
            var marketOpen = new TimeSpan(9, 30, 0);
            var marketClose = new TimeSpan(16, 0, 0);
            
            return time >= marketOpen && time <= marketClose;
        }
        
        private static bool IsPreMarket(DateTime timestamp)
        {
            var time = timestamp.TimeOfDay;
            var preMarketStart = new TimeSpan(4, 0, 0);
            var marketOpen = new TimeSpan(9, 30, 0);
            
            return time >= preMarketStart && time < marketOpen;
        }
        
        private static bool IsAfterHours(DateTime timestamp)
        {
            var time = timestamp.TimeOfDay;
            var marketClose = new TimeSpan(16, 0, 0);
            var afterHoursEnd = new TimeSpan(20, 0, 0);
            
            return time > marketClose && time <= afterHoursEnd;
        }
        
        private static int GetMinutesFromMarketOpen(DateTime timestamp)
        {
            var marketOpen = timestamp.Date.AddHours(9).AddMinutes(30);
            return Math.Max(0, (int)(timestamp - marketOpen).TotalMinutes);
        }
        
        private static int GetMinutesToMarketClose(DateTime timestamp)
        {
            var marketClose = timestamp.Date.AddHours(16);
            return Math.Max(0, (int)(marketClose - timestamp).TotalMinutes);
        }
    }
    
    /// <summary>
    /// Technical indicator features
    /// </summary>
    public class TechnicalFeatures
    {
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
        public decimal RSI { get; set; }
        public decimal MACD { get; set; }
        public decimal MACDSignal { get; set; }
        public decimal MACDHistogram { get; set; }
        public decimal BollingerUpper { get; set; }
        public decimal BollingerMiddle { get; set; }
        public decimal BollingerLower { get; set; }
        public decimal SMA20 { get; set; }
        public decimal SMA50 { get; set; }
        public decimal EMA12 { get; set; }
        public decimal EMA26 { get; set; }
        public decimal VolumeRatio { get; set; }
        public decimal VWAP { get; set; }
        public decimal PriceChangePercent { get; set; }
        public decimal DailyRange { get; set; }
        public bool IsGapUp { get; set; }
        public bool IsGapDown { get; set; }
    }
    
    /// <summary>
    /// Market microstructure features
    /// </summary>
    public class MicrostructureFeatures
    {
        public decimal BidAskSpread { get; set; }
        public decimal RelativeSpread { get; set; }
        public decimal BidSize { get; set; }
        public decimal AskSize { get; set; }
        public decimal LiquidityImbalance { get; set; }
        public decimal RealizedVolatility { get; set; }
        public decimal ParkinsonVolatility { get; set; }
        public decimal TradeImbalance { get; set; }
        public decimal VolumeWeightedPrice { get; set; }
    }
    
    /// <summary>
    /// Time-based features
    /// </summary>
    public class TimeFeatures
    {
        public int HourOfDay { get; set; }
        public int DayOfWeek { get; set; }
        public int DayOfMonth { get; set; }
        public int MonthOfYear { get; set; }
        public bool IsMarketOpen { get; set; }
        public int MinutesFromOpen { get; set; }
        public int MinutesToClose { get; set; }
        public bool IsPreMarket { get; set; }
        public bool IsAfterHours { get; set; }
    }
}
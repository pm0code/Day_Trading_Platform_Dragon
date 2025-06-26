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
            features.Open = (float)current.Open;
            features.High = (float)current.High;
            features.Low = (float)current.Low;
            features.Close = (float)current.Close;
            features.Volume = (float)current.Volume;
            
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
            features.DailyRange = (float)((current.High - current.Low) / current.Close);
            
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
            features.BidAskSpread = (float)(snapshot.Ask - snapshot.Bid);
            features.RelativeSpread = features.BidAskSpread / (float)snapshot.Close;
            
            // Liquidity features
            features.BidSize = (float)(snapshot.BidSize ?? 0);
            features.AskSize = (float)(snapshot.AskSize ?? 0);
            features.LiquidityImbalance = (features.BidSize - features.AskSize) / (features.BidSize + features.AskSize + 1);
            
            // Volatility features
            features.RealizedVolatility = CalculateRealizedVolatility(recentData, 20);
            features.ParkinsonVolatility = CalculateParkinsonVolatility(recentData, 20);
            
            // Order flow features
            features.TradeImbalance = CalculateTradeImbalance(snapshot);
            features.VolumeWeightedPrice = (float)CalculateVWAP(recentData, recentData.Count - 1);
            
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
        private static float CalculateRSI(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period) return 50f; // Default neutral RSI
            
            float avgGain = 0, avgLoss = 0;
            
            for (int i = index - period + 1; i <= index; i++)
            {
                var change = (float)(data[i].Close - data[i-1].Close);
                if (change > 0)
                    avgGain += change;
                else
                    avgLoss += Math.Abs(change);
            }
            
            avgGain /= period;
            avgLoss /= period;
            
            if (avgLoss == 0) return 100f;
            
            var rs = avgGain / avgLoss;
            return 100f - (100f / (1f + rs));
        }
        
        private static (float MACD, float Signal, float Histogram) CalculateMACD(
            IList<MarketDataSnapshot> data, int index)
        {
            var ema12 = CalculateEMA(data, index, 12);
            var ema26 = CalculateEMA(data, index, 26);
            var macd = ema12 - ema26;
            
            // Simplified signal line (9-period EMA of MACD)
            var signal = macd * 0.2f + ema12 * 0.8f; // Approximation
            var histogram = macd - signal;
            
            return (macd, signal, histogram);
        }
        
        private static (float Upper, float Middle, float Lower) CalculateBollingerBands(
            IList<MarketDataSnapshot> data, int index, int period)
        {
            var sma = CalculateSMA(data, index, period);
            var stdDev = CalculateStandardDeviation(data, index, period);
            
            return (sma + 2 * stdDev, sma, sma - 2 * stdDev);
        }
        
        private static float CalculateSMA(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period - 1) return (float)data[index].Close;
            
            var sum = 0m;
            for (int i = index - period + 1; i <= index; i++)
            {
                sum += data[i].Close;
            }
            return (float)(sum / period);
        }
        
        private static float CalculateEMA(IList<MarketDataSnapshot> data, int index, int period)
        {
            if (index < period - 1) return (float)data[index].Close;
            
            var multiplier = 2f / (period + 1);
            var ema = (float)data[index - period + 1].Close;
            
            for (int i = index - period + 2; i <= index; i++)
            {
                ema = ((float)data[i].Close - ema) * multiplier + ema;
            }
            
            return ema;
        }
        
        private static float CalculateStandardDeviation(IList<MarketDataSnapshot> data, int index, int period)
        {
            var mean = CalculateSMA(data, index, period);
            var sumSquares = 0f;
            
            for (int i = index - period + 1; i <= index; i++)
            {
                var diff = (float)data[i].Close - mean;
                sumSquares += diff * diff;
            }
            
            return (float)Math.Sqrt(sumSquares / period);
        }
        
        private static float CalculateVolumeRatio(IList<MarketDataSnapshot> data, int index)
        {
            if (index < 20) return 1f;
            
            var currentVolume = (float)data[index].Volume;
            var avgVolume = 0f;
            
            for (int i = index - 20; i < index; i++)
            {
                avgVolume += (float)data[i].Volume;
            }
            avgVolume /= 20;
            
            return avgVolume > 0 ? currentVolume / avgVolume : 1f;
        }
        
        private static float CalculateVWAP(IList<MarketDataSnapshot> data, int index)
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
            
            return totalVolume > 0 ? (float)(totalValue / totalVolume) : (float)data[index].Close;
        }
        
        private static float CalculatePriceChange(decimal previousClose, decimal currentClose)
        {
            return previousClose > 0 ? (float)((currentClose - previousClose) / previousClose * 100) : 0f;
        }
        
        private static float CalculateRealizedVolatility(IList<MarketDataSnapshot> data, int period)
        {
            if (data.Count < period + 1) return 0f;
            
            var returns = new List<float>();
            for (int i = data.Count - period; i < data.Count; i++)
            {
                var ret = CalculatePriceChange(data[i-1].Close, data[i].Close) / 100f;
                returns.Add(ret);
            }
            
            var mean = returns.Average();
            var sumSquares = returns.Sum(r => (r - mean) * (r - mean));
            
            return (float)Math.Sqrt(sumSquares / (period - 1)) * (float)Math.Sqrt(252); // Annualized
        }
        
        private static float CalculateParkinsonVolatility(IList<MarketDataSnapshot> data, int period)
        {
            if (data.Count < period) return 0f;
            
            var sum = 0.0;
            for (int i = data.Count - period; i < data.Count; i++)
            {
                var ratio = Math.Log((double)data[i].High / (double)data[i].Low);
                sum += ratio * ratio;
            }
            
            return (float)Math.Sqrt(sum / (4 * period * Math.Log(2))) * (float)Math.Sqrt(252);
        }
        
        private static float CalculateTradeImbalance(MarketDataSnapshot snapshot)
        {
            // Placeholder - would use actual trade data in production
            return 0f;
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
        public float Open { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
        public float Close { get; set; }
        public float Volume { get; set; }
        public float RSI { get; set; }
        public float MACD { get; set; }
        public float MACDSignal { get; set; }
        public float MACDHistogram { get; set; }
        public float BollingerUpper { get; set; }
        public float BollingerMiddle { get; set; }
        public float BollingerLower { get; set; }
        public float SMA20 { get; set; }
        public float SMA50 { get; set; }
        public float EMA12 { get; set; }
        public float EMA26 { get; set; }
        public float VolumeRatio { get; set; }
        public float VWAP { get; set; }
        public float PriceChangePercent { get; set; }
        public float DailyRange { get; set; }
        public bool IsGapUp { get; set; }
        public bool IsGapDown { get; set; }
    }
    
    /// <summary>
    /// Market microstructure features
    /// </summary>
    public class MicrostructureFeatures
    {
        public float BidAskSpread { get; set; }
        public float RelativeSpread { get; set; }
        public float BidSize { get; set; }
        public float AskSize { get; set; }
        public float LiquidityImbalance { get; set; }
        public float RealizedVolatility { get; set; }
        public float ParkinsonVolatility { get; set; }
        public float TradeImbalance { get; set; }
        public float VolumeWeightedPrice { get; set; }
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
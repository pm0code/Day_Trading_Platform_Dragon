using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Canonical implementation of slippage calculator for paper trading.
    /// Provides realistic slippage estimation using market microstructure models.
    /// </summary>
    public class SlippageCalculatorCanonical : CanonicalServiceBase, ISlippageCalculator
    {
        #region Configuration

        protected virtual decimal BaseSlippage => 0.0001m; // 1 basis point base
        protected virtual decimal ImpactCoefficient => 0.05m; // Square root impact coefficient
        protected virtual decimal TemporaryImpactGamma => 0.5m; // Temporary impact coefficient
        protected virtual decimal PermanentImpactEta => 0.01m; // Permanent impact coefficient
        protected virtual decimal VolatilitySigma => 0.3m; // Default volatility parameter
        protected virtual decimal MaxSlippageCap => 0.10m; // 10% max slippage cap
        protected virtual int TradingHoursPerDay => 6.5m; // Market hours

        #endregion

        #region Infrastructure

        private readonly Dictionary<string, decimal> _averageDailyVolumes = new();
        private readonly Dictionary<string, SymbolCharacteristics> _symbolCharacteristics = new();
        private readonly object _volumeLock = new();
        private long _slippageCalculations = 0;
        private long _impactEstimations = 0;

        #endregion

        #region Constructor

        public SlippageCalculatorCanonical(ITradingLogger logger)
            : base(logger, "SlippageCalculator")
        {
            LogMethodEntry();
            InitializeVolumeData();
            InitializeSymbolCharacteristics();
        }

        #endregion

        #region ISlippageCalculator Implementation

        public decimal CalculateSlippage(decimal requestedPrice, decimal executedPrice, OrderSide side)
        {
            return ExecuteWithLogging(() =>
            {
                ValidateParameter(requestedPrice, nameof(requestedPrice), p => p > 0, "Requested price must be positive");
                ValidateParameter(executedPrice, nameof(executedPrice), p => p > 0, "Executed price must be positive");

                if (requestedPrice <= 0)
                    return 0m;

                var priceDifference = side == OrderSide.Buy
                    ? executedPrice - requestedPrice
                    : requestedPrice - executedPrice;

                var slippage = Math.Max(0, priceDifference / requestedPrice);
                
                Interlocked.Increment(ref _slippageCalculations);
                UpdateMetric("SlippageCalculations", _slippageCalculations);
                UpdateMetric("LastCalculatedSlippage", slippage);

                LogDebug($"Calculated slippage: {slippage:P4} for {side} order " +
                        $"(requested: {requestedPrice:C}, executed: {executedPrice:C})");

                return slippage;

            }, "Calculate realized slippage",
               "Failed to calculate slippage",
               "Verify price inputs are valid");
        }

        public async Task<decimal> EstimateSlippageAsync(string symbol, OrderSide side, decimal quantity)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");
                ValidateParameter(quantity, nameof(quantity), q => q > 0, "Quantity must be positive");

                var adv = GetAverageDailyVolume(symbol);
                var participationRate = quantity / adv;

                // Square root market impact model
                var impactSlippage = BaseSlippage + (ImpactCoefficient * DecimalMath.Sqrt(participationRate));

                // Add bid-ask spread component
                var spreadComponent = GetTypicalSpread(symbol) / 2m;
                var totalSlippage = impactSlippage + spreadComponent;

                // Add side-specific adjustment
                var sideAdjustment = CalculateSideAdjustment(symbol, side);
                totalSlippage *= (1 + sideAdjustment);

                // Cap at maximum allowed slippage
                var cappedSlippage = Math.Min(totalSlippage, MaxSlippageCap);

                Interlocked.Increment(ref _impactEstimations);
                UpdateMetric("ImpactEstimations", _impactEstimations);
                UpdateMetric($"EstimatedSlippage.{symbol}", cappedSlippage);
                UpdateMetric($"ParticipationRate.{symbol}", participationRate);

                LogInfo($"Estimated slippage for {symbol} {side} {quantity:N0} shares: " +
                       $"{cappedSlippage:P4} (participation: {participationRate:P2}, ADV: {adv:N0})");

                return cappedSlippage;

            }, "Estimate slippage",
               incrementOperationCounter: true);
        }

        public async Task<decimal> CalculateMarketImpactAsync(string symbol, decimal quantity, TimeSpan duration)
        {
            return await ExecuteServiceOperationAsync(async () =>
            {
                ValidateParameter(symbol, nameof(symbol), s => !string.IsNullOrWhiteSpace(s), "Symbol is required");
                ValidateParameter(quantity, nameof(quantity), q => q > 0, "Quantity must be positive");
                ValidateParameter(duration, nameof(duration), d => d > TimeSpan.Zero, "Duration must be positive");

                var adv = GetAverageDailyVolume(symbol);
                var dailyParticipation = quantity / adv;

                // Adjust for execution duration
                var durationHours = duration.TotalHours;
                var timeAdjustment = DecimalMath.Sqrt(TradingHoursPerDay / Math.Max((decimal)durationHours, 0.1m));
                var adjustedParticipation = dailyParticipation * timeAdjustment;

                // Almgren-Chriss model components
                var temporaryImpact = CalculateTemporaryImpact(adjustedParticipation);
                var permanentImpact = CalculatePermanentImpact(adjustedParticipation);
                var totalImpact = temporaryImpact + permanentImpact;

                // Add symbol-specific adjustments
                var characteristics = GetSymbolCharacteristics(symbol);
                totalImpact *= characteristics.ImpactMultiplier;

                UpdateMetric($"MarketImpact.{symbol}.Temporary", temporaryImpact);
                UpdateMetric($"MarketImpact.{symbol}.Permanent", permanentImpact);
                UpdateMetric($"MarketImpact.{symbol}.Total", totalImpact);

                LogInfo($"Market impact for {symbol} {quantity:N0} shares over {duration}: " +
                       $"{totalImpact:P4} (temp: {temporaryImpact:P4}, perm: {permanentImpact:P4})");

                return totalImpact;

            }, "Calculate market impact",
               incrementOperationCounter: true);
        }

        #endregion

        #region Impact Calculations

        private decimal CalculateTemporaryImpact(decimal participationRate)
        {
            // Temporary impact is proportional to square root of participation rate
            return TemporaryImpactGamma * VolatilitySigma * DecimalMath.Sqrt(participationRate);
        }

        private decimal CalculatePermanentImpact(decimal participationRate)
        {
            // Permanent impact is linear in participation rate
            return PermanentImpactEta * participationRate;
        }

        private decimal CalculateSideAdjustment(string symbol, OrderSide side)
        {
            // Asymmetric impact: sells typically have more impact than buys
            var characteristics = GetSymbolCharacteristics(symbol);
            
            return side == OrderSide.Sell 
                ? characteristics.SellPressureMultiplier 
                : characteristics.BuyPressureMultiplier;
        }

        #endregion

        #region Symbol Data Management

        private decimal GetAverageDailyVolume(string symbol)
        {
            lock (_volumeLock)
            {
                if (_averageDailyVolumes.TryGetValue(symbol, out var volume))
                    return volume;

                // Generate realistic ADV based on symbol characteristics
                var characteristics = GetSymbolCharacteristics(symbol);
                var baseVolume = characteristics.MarketCapCategory switch
                {
                    MarketCapCategory.LargeCap => 50_000_000m,
                    MarketCapCategory.MidCap => 10_000_000m,
                    MarketCapCategory.SmallCap => 2_000_000m,
                    MarketCapCategory.ETF => 75_000_000m,
                    _ => 5_000_000m
                };

                // Add some randomness based on symbol hash
                var symbolHash = Math.Abs(symbol.GetHashCode());
                var volumeVariation = 0.5m + (symbolHash % 100) / 100m; // 0.5x to 1.5x
                var finalVolume = baseVolume * volumeVariation;

                _averageDailyVolumes[symbol] = finalVolume;
                UpdateMetric($"ADV.{symbol}", finalVolume);

                return finalVolume;
            }
        }

        private decimal GetTypicalSpread(string symbol)
        {
            var characteristics = GetSymbolCharacteristics(symbol);
            
            return characteristics.MarketCapCategory switch
            {
                MarketCapCategory.LargeCap => 0.0001m,    // 1 basis point
                MarketCapCategory.MidCap => 0.0005m,      // 5 basis points
                MarketCapCategory.SmallCap => 0.002m,     // 20 basis points
                MarketCapCategory.ETF => 0.0001m,         // 1 basis point
                _ => 0.001m                               // 10 basis points default
            };
        }

        private SymbolCharacteristics GetSymbolCharacteristics(string symbol)
        {
            lock (_volumeLock)
            {
                if (_symbolCharacteristics.TryGetValue(symbol, out var characteristics))
                    return characteristics;

                var category = DetermineMarketCapCategory(symbol);
                
                characteristics = new SymbolCharacteristics
                {
                    Symbol = symbol,
                    MarketCapCategory = category,
                    ImpactMultiplier = category switch
                    {
                        MarketCapCategory.LargeCap => 0.8m,  // Lower impact
                        MarketCapCategory.MidCap => 1.0m,    // Normal impact
                        MarketCapCategory.SmallCap => 1.5m,  // Higher impact
                        MarketCapCategory.ETF => 0.7m,       // Lower impact
                        _ => 1.0m
                    },
                    SellPressureMultiplier = 0.1m,  // 10% more impact for sells
                    BuyPressureMultiplier = -0.05m  // 5% less impact for buys
                };

                _symbolCharacteristics[symbol] = characteristics;
                return characteristics;
            }
        }

        private MarketCapCategory DetermineMarketCapCategory(string symbol)
        {
            // Well-known large caps
            var largeCaps = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "BRK.B", "JPM", "V" };
            
            // Well-known ETFs
            var etfs = new[] { "SPY", "QQQ", "IWM", "VTI", "VOO", "EFA", "EEM", "GLD", "TLT", "XLF" };

            if (largeCaps.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                return MarketCapCategory.LargeCap;

            if (etfs.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                return MarketCapCategory.ETF;

            // Use symbol length as proxy for market cap
            return symbol.Length <= 3 ? MarketCapCategory.MidCap : MarketCapCategory.SmallCap;
        }

        private void InitializeVolumeData()
        {
            // Initialize with known volume data for common symbols
            var knownVolumes = new Dictionary<string, decimal>
            {
                ["AAPL"] = 50_000_000m,
                ["MSFT"] = 30_000_000m,
                ["GOOGL"] = 25_000_000m,
                ["AMZN"] = 35_000_000m,
                ["TSLA"] = 75_000_000m,
                ["NVDA"] = 40_000_000m,
                ["SPY"] = 100_000_000m,
                ["QQQ"] = 60_000_000m,
                ["IWM"] = 45_000_000m,
                ["META"] = 28_000_000m,
                ["JPM"] = 22_000_000m,
                ["V"] = 18_000_000m
            };

            lock (_volumeLock)
            {
                foreach (var kvp in knownVolumes)
                {
                    _averageDailyVolumes[kvp.Key] = kvp.Value;
                }
            }

            LogInfo($"Initialized volume data for {knownVolumes.Count} symbols");
        }

        private void InitializeSymbolCharacteristics()
        {
            LogDebug("Symbol characteristics initialized with market cap categories");
        }

        #endregion

        #region Metrics

        public override IReadOnlyDictionary<string, object> GetMetrics()
        {
            var metrics = base.GetMetrics().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add slippage calculator specific metrics
            metrics["SlippageCalculator.BaseSlippage"] = BaseSlippage;
            metrics["SlippageCalculator.ImpactCoefficient"] = ImpactCoefficient;
            metrics["SlippageCalculator.MaxSlippageCap"] = MaxSlippageCap;
            metrics["SlippageCalculator.SymbolsTracked"] = _averageDailyVolumes.Count;
            metrics["SlippageCalculator.CalculationsPerformed"] = _slippageCalculations + _impactEstimations;

            return metrics;
        }

        #endregion

        #region Nested Types

        private enum MarketCapCategory
        {
            LargeCap,
            MidCap,
            SmallCap,
            ETF
        }

        private class SymbolCharacteristics
        {
            public string Symbol { get; set; } = string.Empty;
            public MarketCapCategory MarketCapCategory { get; set; }
            public decimal ImpactMultiplier { get; set; }
            public decimal SellPressureMultiplier { get; set; }
            public decimal BuyPressureMultiplier { get; set; }
        }

        #endregion

        #region Math Utilities

        private static class DecimalMath
        {
            public static decimal Sqrt(decimal value)
            {
                if (value < 0)
                    throw new ArgumentException("Cannot calculate square root of negative number");
                if (value == 0)
                    return 0;

                // Newton-Raphson method for decimal square root
                var x = value;
                var root = value / 2;
                
                for (int i = 0; i < 10; i++) // 10 iterations for good precision
                {
                    root = (root + value / root) / 2;
                }
                
                return root;
            }
        }

        #endregion
    }
}
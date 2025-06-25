using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Strategies
{
    /// <summary>
    /// Canonical implementation of gap trading strategy
    /// Identifies and trades gap patterns with statistical probability analysis
    /// </summary>
    public class GapStrategyCanonical : CanonicalStrategyBase, IGapStrategy
    {
        private readonly Dictionary<string, GapHistory> _gapHistoryCache;
        private readonly object _cacheLock = new();

        public override string StrategyName => "Gap Trading Strategy";
        public override string Description => "Gap-based trading strategy focusing on gap fill probabilities and reversal patterns";

        public GapStrategyCanonical(ITradingLogger logger)
            : base(logger, "GapStrategy")
        {
            _gapHistoryCache = new Dictionary<string, GapHistory>();
        }

        protected override async Task<TradingResult<TradingSignal>> GenerateSignalAsync(
            string symbol,
            MarketData marketData,
            PositionInfo? currentPosition,
            CancellationToken cancellationToken)
        {
            try
            {
                // Convert MarketData to MarketConditions for compatibility
                var conditions = ConvertToMarketConditions(marketData);

                // Detect gap patterns
                var gaps = await DetectGapsAsync(symbol, conditions);
                if (gaps.Length == 0)
                {
                    return TradingResult<TradingSignal>.Failure("NO_GAP", "No gap patterns detected");
                }

                // Analyze each gap for trading opportunity
                TradingSignal? bestSignal = null;
                decimal highestProbability = 0m;

                foreach (var gap in gaps)
                {
                    // Assess gap fill probability
                    var fillProbability = await AssessGapFillProbabilityAsync(symbol, gap);
                    
                    if (fillProbability < 0.65m) // Minimum 65% fill probability
                    {
                        LogDebug($"Gap fill probability {fillProbability:P1} below threshold for {gap.GapType}");
                        continue;
                    }

                    // Check if gap is likely to fill intraday
                    var intraDayFillLikely = await IsIntraDayFillLikelyAsync(gap);
                    if (!intraDayFillLikely && currentPosition == null) // Only take new positions for intraday fills
                    {
                        LogDebug($"Gap unlikely to fill intraday - skipping {gap.GapType}");
                        continue;
                    }

                    // Create gap signal
                    var signal = await CreateGapSignalAsync(gap, fillProbability);
                    if (signal != null && signal.Confidence > highestProbability)
                    {
                        bestSignal = signal;
                        highestProbability = signal.Confidence;
                    }
                }

                if (bestSignal == null)
                {
                    return TradingResult<TradingSignal>.Failure("NO_QUALIFIED_GAP", "No gaps meet trading criteria");
                }

                // Update gap history
                UpdateGapHistory(symbol, gaps);

                LogInfo($"Generated gap signal: {bestSignal.SignalType} at {bestSignal.Price:C}, Confidence: {bestSignal.Confidence:F2}");
                return TradingResult<TradingSignal>.Success(bestSignal);
            }
            catch (Exception ex)
            {
                LogError("Failed to generate gap signal", ex);
                return TradingResult<TradingSignal>.Failure("SIGNAL_GENERATION_ERROR", ex.Message);
            }
        }

        public async Task<TradingSignal[]> GenerateSignalsAsync(string symbol, MarketConditions conditions)
        {
            // Convert to canonical format
            var marketData = ConvertToMarketData(conditions);
            var result = await GenerateSignalAsync(symbol, marketData, null, CancellationToken.None);
            
            if (result.IsSuccess && result.Value != null)
            {
                return new[] { result.Value };
            }
            
            return Array.Empty<TradingSignal>();
        }

        public async Task<GapPattern[]> DetectGapsAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var gaps = new List<GapPattern>();

                    // Get historical data for gap detection
                    var previousClose = await GetPreviousCloseAsync(symbol, conditions);
                    if (previousClose == 0)
                    {
                        LogWarning("Unable to determine previous close - using estimate");
                        previousClose = conditions.Volatility * 95; // Fallback estimate
                    }

                    var currentOpen = conditions.Volatility * 100; // Current open price
                    var gapSize = Math.Abs(currentOpen - previousClose);
                    var gapPercentage = gapSize / previousClose;

                    // Only consider significant gaps (>0.5%)
                    if (gapPercentage >= 0.005m)
                    {
                        var gapType = DetermineGapType(currentOpen, previousClose, gapPercentage, conditions);
                        var hasVolumeConfirmation = conditions.Volume > 1000000; // Volume threshold
                        
                        // Additional gap validation
                        var isValidGap = ValidateGap(gapType, gapPercentage, conditions);
                        if (!isValidGap)
                        {
                            LogDebug($"Gap validation failed for {gapType} gap of {gapPercentage:P2}");
                            return TradingResult<GapPattern[]>.Success(Array.Empty<GapPattern>());
                        }

                        var gap = new GapPattern(
                            symbol,
                            gapType,
                            gapSize,
                            gapPercentage,
                            currentOpen,
                            previousClose,
                            hasVolumeConfirmation,
                            DateTimeOffset.UtcNow);

                        gaps.Add(gap);

                        LogInfo($"Detected {gapType} gap: {gapPercentage:P2}, Volume confirmation: {hasVolumeConfirmation}",
                            additionalData: new
                            {
                                Symbol = symbol,
                                GapSize = gapSize,
                                CurrentOpen = currentOpen,
                                PreviousClose = previousClose
                            });
                    }

                    return TradingResult<GapPattern[]>.Success(gaps.ToArray());
                },
                nameof(DetectGapsAsync));
        }

        public async Task<decimal> AssessGapFillProbabilityAsync(string symbol, GapPattern gap)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Statistical gap fill probability based on historical patterns
                    var baseProbability = gap.GapType switch
                    {
                        GapType.CommonGap => 0.85m,      // Common gaps fill 85% of the time
                        GapType.BreakoutGap => 0.25m,    // Breakout gaps rarely fill quickly
                        GapType.ExhaustionGap => 0.90m,  // Exhaustion gaps usually fill
                        GapType.GapUp => 0.70m,          // Gap ups have moderate fill rate
                        GapType.GapDown => 0.75m,        // Gap downs slightly higher fill rate
                        _ => 0.50m
                    };

                    // Adjust probability based on gap size
                    var sizeAdjustment = gap.GapPercentage switch
                    {
                        < 0.02m => 0.10m,   // Small gaps more likely to fill
                        < 0.05m => 0.05m,   // Medium gaps
                        < 0.10m => 0.0m,    // Large gaps
                        _ => -0.15m         // Very large gaps less likely to fill
                    };

                    // Volume confirmation adjustment
                    var volumeAdjustment = gap.HasVolumeConfirmation ? 0.05m : -0.10m;

                    // Historical fill rate adjustment
                    var historicalAdjustment = GetHistoricalFillRateAdjustment(symbol, gap.GapType);

                    // Time of day adjustment (gaps in first hour more likely to fill)
                    var timeAdjustment = gap.Timestamp.Hour < 11 ? 0.05m : -0.05m; // EST assumption

                    // Market conditions adjustment
                    var marketAdjustment = 0.0m;
                    if (gap.GapPercentage > 0.03m) // 3% or larger gap
                    {
                        marketAdjustment = -0.05m; // Less likely in volatile conditions
                    }

                    var finalProbability = baseProbability + sizeAdjustment + volumeAdjustment + 
                                         historicalAdjustment + timeAdjustment + marketAdjustment;
                    
                    finalProbability = Math.Max(0.0m, Math.Min(1.0m, finalProbability)); // Clamp to 0-1

                    LogInfo($"Gap fill probability: {finalProbability:P1}",
                        additionalData: new
                        {
                            Symbol = symbol,
                            GapType = gap.GapType,
                            BaseProbability = baseProbability,
                            SizeAdjustment = sizeAdjustment,
                            VolumeAdjustment = volumeAdjustment,
                            HistoricalAdjustment = historicalAdjustment
                        });

                    return TradingResult<decimal>.Success(finalProbability);
                },
                nameof(AssessGapFillProbabilityAsync));
        }

        public bool CanTrade(string symbol)
        {
            // Gap strategy works well with ETFs and large-cap stocks
            var gapTradingSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SPY", "QQQ", "IWM", "DIA", "EEM", "XLF", "XLE", "XLK",
                "AAPL", "MSFT", "AMZN", "GOOGL", "TSLA", "META", "NVDA",
                "JPM", "BAC", "WMT", "JNJ", "PG", "UNH", "HD", "DIS"
            };
            
            return gapTradingSymbols.Contains(symbol);
        }

        public RiskLimits GetRiskLimits()
        {
            return new RiskLimits(
                MaxPositionSize: 8000.0m,     // $8k max position
                MaxDailyLoss: -400.0m,        // $400 max daily loss
                MaxPortfolioRisk: 0.015m,     // 1.5% portfolio risk
                MaxOpenPositions: 2,          // Max 2 concurrent gap trades
                StopLossPercentage: 0.025m);  // 2.5% stop loss
        }

        // Additional gap-specific methods

        public async Task<decimal> CalculateOptimalEntryAsync(GapPattern gap)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Optimal entry strategies based on gap type
                    var optimalEntry = gap.GapType switch
                    {
                        GapType.GapUp => gap.PreviousClose + (gap.GapSize * 0.25m), // Enter on partial retracement
                        GapType.GapDown => gap.PreviousClose - (gap.GapSize * 0.25m), // Enter on partial bounce
                        GapType.CommonGap => gap.OpenPrice, // Enter immediately for common gaps
                        GapType.ExhaustionGap => gap.OpenPrice, // Enter immediately for exhaustion gaps
                        GapType.BreakoutGap => gap.OpenPrice + (gap.GapSize * 0.1m), // Enter on confirmation
                        _ => gap.OpenPrice
                    };

                    LogDebug($"Optimal entry for {gap.GapType}: {optimalEntry:C} (Open: {gap.OpenPrice:C})");
                    
                    return TradingResult<decimal>.Success(optimalEntry);
                },
                nameof(CalculateOptimalEntryAsync));
        }

        public async Task<decimal> CalculateStopLossAsync(GapPattern gap, SignalType signalType)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    var stopLossPercent = gap.GapType switch
                    {
                        GapType.CommonGap => 0.015m,      // Tight stop for common gaps
                        GapType.ExhaustionGap => 0.02m,   // Moderate stop for exhaustion gaps
                        GapType.BreakoutGap => 0.035m,    // Wider stop for breakout gaps
                        _ => 0.025m                       // Default stop loss
                    };

                    // Adjust stop loss based on gap size
                    if (gap.GapPercentage > 0.05m) // Large gap
                    {
                        stopLossPercent *= 1.5m; // Wider stop for large gaps
                    }

                    decimal stopLoss;
                    if (signalType == SignalType.Buy)
                    {
                        stopLoss = gap.OpenPrice * (1 - stopLossPercent);
                    }
                    else
                    {
                        stopLoss = gap.OpenPrice * (1 + stopLossPercent);
                    }

                    LogDebug($"Stop loss for {signalType} on {gap.GapType}: {stopLoss:C} ({stopLossPercent:P1})");
                    
                    return TradingResult<decimal>.Success(stopLoss);
                },
                nameof(CalculateStopLossAsync));
        }

        public async Task<bool> IsIntraDayFillLikelyAsync(GapPattern gap)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Factors that increase intraday fill probability
                    var smallGap = gap.GapPercentage < 0.03m; // Less than 3%
                    var commonOrExhaustionGap = gap.GapType == GapType.CommonGap || gap.GapType == GapType.ExhaustionGap;
                    var hasVolumeConfirmation = gap.HasVolumeConfirmation;
                    var morningGap = gap.Timestamp.Hour < 11; // EST assumption

                    var fillScore = 0;
                    if (smallGap) fillScore += 2;
                    if (commonOrExhaustionGap) fillScore += 2;
                    if (hasVolumeConfirmation) fillScore += 1;
                    if (morningGap) fillScore += 1;

                    var isLikely = fillScore >= 3; // Need at least 3 points for likely intraday fill

                    LogDebug($"Intraday fill likely: {isLikely} (Score: {fillScore})");
                    
                    return TradingResult<bool>.Success(isLikely);
                },
                nameof(IsIntraDayFillLikelyAsync));
        }

        // Private helper methods

        private async Task<TradingSignal?> CreateGapSignalAsync(GapPattern gap, decimal fillProbability)
        {
            try
            {
                // Determine signal direction based on gap type and expected fill
                var signalType = gap.GapType switch
                {
                    GapType.GapUp => SignalType.Sell, // Expect gap to fill (price to go down)
                    GapType.GapDown => SignalType.Buy, // Expect gap to fill (price to go up)
                    GapType.ExhaustionGap => gap.OpenPrice > gap.PreviousClose ? SignalType.Sell : SignalType.Buy,
                    GapType.CommonGap => gap.OpenPrice > gap.PreviousClose ? SignalType.Sell : SignalType.Buy,
                    GapType.BreakoutGap => gap.OpenPrice > gap.PreviousClose ? SignalType.Buy : SignalType.Sell, // Follow breakout
                    _ => SignalType.Hold
                };

                if (signalType == SignalType.Hold)
                {
                    return null;
                }

                var entryPrice = await CalculateOptimalEntryAsync(gap);
                var stopLoss = await CalculateStopLossAsync(gap, signalType);
                var positionSize = CalculatePositionSize(gap.GapPercentage, fillProbability);

                // Calculate take profit based on gap fill target
                var takeProfit = gap.GapType == GapType.BreakoutGap 
                    ? entryPrice * (signalType == SignalType.Buy ? 1.05m : 0.95m) // 5% profit for breakouts
                    : gap.PreviousClose; // Fill target for other gaps

                var signal = new TradingSignal(
                    Guid.NewGuid().ToString(),
                    "gap-fill",
                    gap.Symbol,
                    signalType,
                    entryPrice.Value,
                    positionSize,
                    fillProbability,
                    $"Gap {gap.GapType}: {gap.GapPercentage:P2} gap, {fillProbability:P1} fill probability",
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["GapType"] = gap.GapType.ToString(),
                        ["GapSize"] = gap.GapSize,
                        ["GapPercentage"] = gap.GapPercentage,
                        ["FillProbability"] = fillProbability,
                        ["VolumeConfirmation"] = gap.HasVolumeConfirmation,
                        ["PreviousClose"] = gap.PreviousClose,
                        ["OpenPrice"] = gap.OpenPrice,
                        ["StopLoss"] = stopLoss.Value,
                        ["TakeProfit"] = takeProfit
                    });

                return signal;
            }
            catch (Exception ex)
            {
                LogError($"Error creating gap signal for {gap.Symbol}", ex);
                return null;
            }
        }

        private GapType DetermineGapType(decimal currentOpen, decimal previousClose, decimal gapPercentage, MarketConditions conditions)
        {
            var isGapUp = currentOpen > previousClose;

            // Consider market context for gap classification
            var strongTrend = conditions.Trend == TrendDirection.Up || conditions.Trend == TrendDirection.Down;
            var highVolume = conditions.Volume > 1500000;

            // Classify gap type based on size, direction, and context
            if (gapPercentage >= 0.05m) // Large gaps (5%+)
            {
                if (strongTrend && highVolume)
                    return GapType.BreakoutGap;
                else
                    return GapType.ExhaustionGap;
            }
            else if (gapPercentage >= 0.03m) // Medium gaps (3-5%)
            {
                return isGapUp ? GapType.GapUp : GapType.GapDown;
            }
            else // Small gaps (0.5-3%)
            {
                return GapType.CommonGap;
            }
        }

        private bool ValidateGap(GapType gapType, decimal gapPercentage, MarketConditions conditions)
        {
            // Validate gap based on type and market conditions
            switch (gapType)
            {
                case GapType.BreakoutGap:
                    // Breakout gaps need volume confirmation
                    return conditions.Volume > 2000000 && gapPercentage > 0.03m;
                    
                case GapType.ExhaustionGap:
                    // Exhaustion gaps occur after extended moves
                    return Math.Abs(conditions.PriceChange) > 0.1m || conditions.RSI > 70 || conditions.RSI < 30;
                    
                case GapType.CommonGap:
                    // Common gaps are always valid
                    return true;
                    
                default:
                    return true;
            }
        }

        private int CalculatePositionSize(decimal gapPercentage, decimal fillProbability)
        {
            // Larger positions for higher probability, smaller gaps
            var baseSize = 150;

            // Size based on fill probability
            var probabilityMultiplier = (double)fillProbability; // 0.0 to 1.0

            // Size based on gap size (smaller gaps = larger positions)
            var gapMultiplier = gapPercentage switch
            {
                < 0.02m => 1.2, // Small gaps
                < 0.05m => 1.0, // Medium gaps
                _ => 0.8        // Large gaps
            };

            return (int)(baseSize * probabilityMultiplier * gapMultiplier);
        }

        private async Task<decimal> GetPreviousCloseAsync(string symbol, MarketConditions conditions)
        {
            // In a real implementation, this would fetch historical data
            // For now, return a mock value based on current conditions
            await Task.CompletedTask;
            
            // Estimate previous close from current price and typical daily range
            var typicalDailyRange = conditions.Volatility * 2; // 2x volatility as daily range
            var estimatedPreviousClose = conditions.Price - (conditions.PriceChange * conditions.Price);
            
            return estimatedPreviousClose > 0 ? estimatedPreviousClose : conditions.Price * 0.98m;
        }

        private decimal GetHistoricalFillRateAdjustment(string symbol, GapType gapType)
        {
            // Check cache for historical performance
            lock (_cacheLock)
            {
                if (_gapHistoryCache.TryGetValue(symbol, out var history))
                {
                    var fillRate = history.GetFillRate(gapType);
                    
                    // Adjust based on historical performance
                    if (fillRate > 0.8m) return 0.1m;  // High fill rate
                    if (fillRate > 0.6m) return 0.05m; // Good fill rate
                    if (fillRate < 0.4m) return -0.1m; // Poor fill rate
                }
            }
            
            return 0m; // No historical data
        }

        private void UpdateGapHistory(string symbol, GapPattern[] gaps)
        {
            lock (_cacheLock)
            {
                if (!_gapHistoryCache.TryGetValue(symbol, out var history))
                {
                    history = new GapHistory();
                    _gapHistoryCache[symbol] = history;
                }
                
                foreach (var gap in gaps)
                {
                    history.AddGap(gap);
                }
            }
        }

        private MarketConditions ConvertToMarketConditions(MarketData marketData)
        {
            return new MarketConditions
            {
                Price = marketData.Close,
                Volume = marketData.Volume,
                Volatility = marketData.Volatility,
                PriceChange = (marketData.Close - marketData.Open) / marketData.Open,
                Trend = DetermineTrend(marketData),
                RSI = marketData.RSI ?? 50m,
                MarketBreadth = 0.5m // Default value
            };
        }

        private MarketData ConvertToMarketData(MarketConditions conditions)
        {
            return new MarketData
            {
                Symbol = string.Empty, // Will be set by caller
                Timestamp = DateTime.UtcNow,
                Open = conditions.Volatility * 100,
                High = conditions.Volatility * 105,
                Low = conditions.Volatility * 95,
                Close = conditions.Price,
                Volume = conditions.Volume,
                Volatility = conditions.Volatility,
                RSI = conditions.RSI
            };
        }

        private TrendDirection DetermineTrend(MarketData data)
        {
            var change = (data.Close - data.Open) / data.Open;
            
            if (change > 0.02m) return TrendDirection.Up;
            if (change < -0.02m) return TrendDirection.Down;
            return TrendDirection.Sideways;
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogInfo("Initializing Gap Strategy");
            
            // Clear cache on initialization
            lock (_cacheLock)
            {
                _gapHistoryCache.Clear();
            }
            
            await Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Gap Strategy started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Gap Strategy stopped");
            
            // Clear cache
            lock (_cacheLock)
            {
                _gapHistoryCache.Clear();
            }
            
            return Task.CompletedTask;
        }

        // Helper class for tracking gap history
        private class GapHistory
        {
            private readonly List<GapRecord> _records = new();
            private readonly object _lock = new();

            public void AddGap(GapPattern gap)
            {
                lock (_lock)
                {
                    _records.Add(new GapRecord
                    {
                        GapType = gap.GapType,
                        GapPercentage = gap.GapPercentage,
                        Timestamp = gap.Timestamp,
                        Filled = false // Will be updated by monitoring service
                    });
                    
                    // Keep only last 100 gaps
                    if (_records.Count > 100)
                    {
                        _records.RemoveAt(0);
                    }
                }
            }

            public decimal GetFillRate(GapType gapType)
            {
                lock (_lock)
                {
                    var relevantGaps = _records.Where(r => r.GapType == gapType).ToList();
                    if (relevantGaps.Count == 0) return 0.5m; // Default
                    
                    var filledCount = relevantGaps.Count(r => r.Filled);
                    return (decimal)filledCount / relevantGaps.Count;
                }
            }
        }

        private class GapRecord
        {
            public GapType GapType { get; set; }
            public decimal GapPercentage { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public bool Filled { get; set; }
        }
    }
}
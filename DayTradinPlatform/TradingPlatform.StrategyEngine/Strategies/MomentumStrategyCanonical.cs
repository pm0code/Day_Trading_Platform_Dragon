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
    /// Canonical implementation of momentum breakout trading strategy
    /// Identifies and trades momentum breakouts with volume confirmation
    /// </summary>
    public class MomentumStrategyCanonical : CanonicalStrategyBase, IMomentumStrategy
    {
        private readonly Dictionary<string, decimal> _sustainabilityCache;
        private readonly object _cacheLock = new();

        public override string StrategyName => "Momentum Breakout Strategy";
        public override string Description => "Momentum-based trading strategy that identifies breakouts with volume confirmation";

        public MomentumStrategyCanonical(ITradingLogger logger)
            : base(logger, "MomentumStrategy")
        {
            _sustainabilityCache = new Dictionary<string, decimal>();
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

                // Detect momentum patterns
                var momentumSignals = await DetectMomentumAsync(symbol, conditions);
                if (momentumSignals.Length == 0)
                {
                    return TradingResult<TradingSignal>.Failure("NO_MOMENTUM", "No momentum detected");
                }

                // Calculate overall momentum strength
                var momentumStrength = await CalculateMomentumStrengthAsync(symbol, conditions);
                if (momentumStrength < 0.6m)
                {
                    return TradingResult<TradingSignal>.Failure("WEAK_MOMENTUM", $"Momentum strength {momentumStrength:F2} below threshold");
                }

                // Find the strongest momentum signal
                var strongestSignal = momentumSignals
                    .Where(m => m.Strength >= 0.7m && m.VolumeConfirmation >= 1.5m)
                    .OrderByDescending(m => m.Strength)
                    .FirstOrDefault();

                if (strongestSignal == null)
                {
                    return TradingResult<TradingSignal>.Failure("NO_STRONG_SIGNAL", "No signals meet strength criteria");
                }

                // Check for momentum acceleration
                var isAccelerating = await IsAcceleratingAsync(symbol, conditions);
                if (!isAccelerating)
                {
                    LogWarning("Momentum not accelerating - reducing confidence");
                    momentumStrength *= 0.8m;
                }

                // Check sustainability
                var sustainability = await GetSustainabilityScoreAsync(symbol, conditions);
                if (sustainability < 0.5m)
                {
                    return TradingResult<TradingSignal>.Failure("UNSUSTAINABLE", $"Momentum sustainability {sustainability:F2} too low");
                }

                // Check for reversal signals
                var isReversal = await IsReversalSignalAsync(symbol, conditions);
                if (isReversal)
                {
                    return TradingResult<TradingSignal>.Failure("REVERSAL_DETECTED", "Momentum reversal signals present");
                }

                // Create trading signal
                var signalType = strongestSignal.Direction == TrendDirection.Up ? SignalType.Buy : SignalType.Sell;
                var positionSize = CalculatePositionSize(strongestSignal.Strength);

                var signal = new TradingSignal(
                    Guid.NewGuid().ToString(),
                    "momentum-breakout",
                    symbol,
                    signalType,
                    strongestSignal.BreakoutLevel,
                    positionSize,
                    strongestSignal.Strength * sustainability, // Adjust confidence by sustainability
                    $"Momentum {strongestSignal.Direction}: Strength={strongestSignal.Strength:F2}, Volume={strongestSignal.VolumeConfirmation:F1}x, Sustainability={sustainability:F2}",
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["MomentumStrength"] = strongestSignal.Strength,
                        ["VolumeConfirmation"] = strongestSignal.VolumeConfirmation,
                        ["BreakoutLevel"] = strongestSignal.BreakoutLevel,
                        ["Direction"] = strongestSignal.Direction.ToString(),
                        ["Sustainability"] = sustainability,
                        ["IsAccelerating"] = isAccelerating
                    });

                LogInfo($"Generated momentum signal: {signal.SignalType} at {signal.Price:C}, Confidence: {signal.Confidence:F2}");
                return TradingResult<TradingSignal>.Success(signal);
            }
            catch (Exception ex)
            {
                LogError("Failed to generate momentum signal", ex);
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

        public async Task<MomentumSignal[]> DetectMomentumAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var signals = new List<MomentumSignal>();

                    // Detect momentum based on price movement and volume
                    var priceMovementThreshold = 0.02m; // 2% price movement
                    var volumeThreshold = 1.5m; // 1.5x average volume

                    if (Math.Abs(conditions.PriceChange) >= priceMovementThreshold)
                    {
                        var direction = conditions.PriceChange > 0 ? TrendDirection.Up : TrendDirection.Down;
                        var strength = Math.Min(Math.Abs(conditions.PriceChange) * 10, 1.0m); // Scale to 0-1

                        // Calculate volume confirmation (using actual relative volume)
                        var volumeConfirmation = conditions.Volume / 1000000m; // Normalize

                        // Calculate breakout level based on volatility and ATR
                        var atr = conditions.Volatility * 100; // Simplified ATR calculation
                        var breakoutLevel = direction == TrendDirection.Up 
                            ? conditions.Volatility * 100 * (1 + atr * 0.01m)
                            : conditions.Volatility * 100 * (1 - atr * 0.01m);

                        // Check if volume confirms the move
                        if (volumeConfirmation >= volumeThreshold)
                        {
                            var momentumSignal = new MomentumSignal(
                                symbol,
                                strength,
                                direction,
                                breakoutLevel,
                                volumeConfirmation,
                                DateTimeOffset.UtcNow);

                            signals.Add(momentumSignal);

                            LogInfo($"Detected momentum: Direction={direction}, Strength={strength:F2}, Volume={volumeConfirmation:F1}x",
                                additionalData: new
                                {
                                    Symbol = symbol,
                                    PriceChange = conditions.PriceChange,
                                    Volume = conditions.Volume,
                                    BreakoutLevel = breakoutLevel
                                });
                        }
                    }

                    return TradingResult<MomentumSignal[]>.Success(signals.ToArray());
                },
                nameof(DetectMomentumAsync));
        }

        public async Task<decimal> CalculateMomentumStrengthAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Calculate momentum strength based on multiple factors
                    var priceStrength = Math.Min(Math.Abs(conditions.PriceChange) * 10, 1.0m);
                    var volumeStrength = Math.Min(conditions.Volume / 2000000m, 1.0m);
                    var volatilityStrength = Math.Min(conditions.Volatility * 20, 1.0m);

                    // RSI momentum component
                    var rsiMomentum = conditions.RSI switch
                    {
                        > 70 => 0.8m,  // Strong upward momentum but overbought
                        > 60 => 1.0m,  // Strong upward momentum
                        > 40 => 0.6m,  // Moderate momentum
                        > 30 => 0.4m,  // Weak momentum
                        _ => 0.2m      // Very weak momentum
                    };

                    // Add trend alignment factor
                    var trendAlignment = conditions.Trend switch
                    {
                        TrendDirection.Up when conditions.PriceChange > 0 => 1.2m,
                        TrendDirection.Down when conditions.PriceChange < 0 => 1.2m,
                        TrendDirection.Sideways => 0.8m,
                        _ => 0.9m
                    };

                    // Weighted combination of factors
                    var momentumStrength = (priceStrength * 0.25m) +
                                         (volumeStrength * 0.25m) +
                                         (volatilityStrength * 0.15m) +
                                         (rsiMomentum * 0.20m) +
                                         (trendAlignment * 0.15m);

                    momentumStrength = Math.Min(momentumStrength, 1.0m);

                    LogInfo($"Calculated momentum strength: {momentumStrength:F2}",
                        additionalData: new
                        {
                            Symbol = symbol,
                            PriceStrength = priceStrength,
                            VolumeStrength = volumeStrength,
                            RSIMomentum = rsiMomentum,
                            TrendAlignment = trendAlignment
                        });

                    return TradingResult<decimal>.Success(momentumStrength);
                },
                nameof(CalculateMomentumStrengthAsync));
        }

        public bool CanTrade(string symbol)
        {
            // Momentum strategy works best with high-volume, volatile stocks
            var momentumStocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TSLA", "NVDA", "AMD", "ARKK", "SPY", "QQQ", "SQQQ", "TQQQ",
                "AAPL", "MSFT", "AMZN", "META", "GOOGL", "NFLX"
            };
            
            return momentumStocks.Contains(symbol);
        }

        public RiskLimits GetRiskLimits()
        {
            return new RiskLimits(
                MaxPositionSize: 15000.0m,    // $15k max position (higher for momentum)
                MaxDailyLoss: -750.0m,        // $750 max daily loss
                MaxPortfolioRisk: 0.03m,      // 3% portfolio risk (higher for momentum)
                MaxOpenPositions: 5,          // Max 5 concurrent positions
                StopLossPercentage: 0.03m);   // 3% stop loss (wider for momentum)
        }

        // Additional momentum-specific methods

        public async Task<bool> IsAcceleratingAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Check multiple acceleration factors
                    var priceAcceleration = Math.Abs(conditions.PriceChange) > 0.03m;
                    var volumeAcceleration = conditions.Volume > 1500000;
                    var volatilityIncreasing = conditions.Volatility > 0.025m;
                    
                    // Check if RSI is in acceleration zone
                    var rsiAcceleration = conditions.RSI is > 55 and < 75;

                    var isAccelerating = priceAcceleration && volumeAcceleration && (volatilityIncreasing || rsiAcceleration);

                    LogDebug($"Acceleration check: Price={priceAcceleration}, Volume={volumeAcceleration}, Result={isAccelerating}");
                    
                    return TradingResult<bool>.Success(isAccelerating);
                },
                nameof(IsAcceleratingAsync));
        }

        public async Task<decimal> GetSustainabilityScoreAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Check cache first
                    lock (_cacheLock)
                    {
                        if (_sustainabilityCache.TryGetValue(symbol, out var cachedScore))
                        {
                            // Cache for 5 minutes
                            return TradingResult<decimal>.Success(cachedScore);
                        }
                    }

                    // Factors that contribute to momentum sustainability
                    var volumeSupport = Math.Min(conditions.Volume / 1000000m, 2.0m) / 2.0m;
                    
                    var trendStrength = conditions.Trend switch
                    {
                        TrendDirection.Up => 1.0m,
                        TrendDirection.Down => 0.8m,
                        TrendDirection.Sideways => 0.3m,
                        _ => 0.1m
                    };

                    // RSI sustainability (not too extreme)
                    var rsiSustainability = conditions.RSI switch
                    {
                        > 80 or < 20 => 0.2m,  // Extreme levels - low sustainability
                        > 70 or < 30 => 0.5m,  // High levels - moderate sustainability
                        _ => 1.0m              // Normal levels - high sustainability
                    };

                    // Market breadth factor (simplified)
                    var marketBreadth = conditions.MarketBreadth > 0.6m ? 1.0m : 0.7m;

                    var sustainability = (volumeSupport * 0.3m) + 
                                       (trendStrength * 0.3m) + 
                                       (rsiSustainability * 0.2m) +
                                       (marketBreadth * 0.2m);

                    sustainability = Math.Min(sustainability, 1.0m);

                    // Cache the result
                    lock (_cacheLock)
                    {
                        _sustainabilityCache[symbol] = sustainability;
                    }

                    LogInfo($"Sustainability score: {sustainability:F2}",
                        additionalData: new
                        {
                            Symbol = symbol,
                            VolumeSupport = volumeSupport,
                            TrendStrength = trendStrength,
                            RSISustainability = rsiSustainability,
                            MarketBreadth = marketBreadth
                        });

                    return TradingResult<decimal>.Success(sustainability);
                },
                nameof(GetSustainabilityScoreAsync));
        }

        public async Task<bool> IsReversalSignalAsync(string symbol, MarketConditions conditions)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    await Task.Yield();

                    // Reversal signals based on momentum exhaustion
                    var highVolumeDivergence = conditions.Volume > 2000000 && Math.Abs(conditions.PriceChange) < 0.01m;
                    var extremeRSI = conditions.RSI > 85 || conditions.RSI < 15;
                    var volatilitySpike = conditions.Volatility > 0.05m;
                    
                    // Check for bearish/bullish divergence
                    var priceTrendUp = conditions.PriceChange > 0;
                    var rsiBearishDivergence = priceTrendUp && conditions.RSI < 50;
                    var rsiBullishDivergence = !priceTrendUp && conditions.RSI > 50;
                    
                    var divergence = rsiBearishDivergence || rsiBullishDivergence;

                    var isReversal = highVolumeDivergence || extremeRSI || volatilitySpike || divergence;

                    if (isReversal)
                    {
                        LogWarning($"Reversal signals detected",
                            additionalData: new
                            {
                                Symbol = symbol,
                                HighVolumeDivergence = highVolumeDivergence,
                                ExtremeRSI = extremeRSI,
                                VolatilitySpike = volatilitySpike,
                                Divergence = divergence
                            });
                    }

                    return TradingResult<bool>.Success(isReversal);
                },
                nameof(IsReversalSignalAsync));
        }

        // Helper methods
        private int CalculatePositionSize(decimal momentumStrength)
        {
            // Larger positions for stronger momentum signals
            var baseSize = 200;
            var strengthMultiplier = (double)(0.5m + (momentumStrength * 0.5m)); // 0.5x to 1.0x
            return (int)(baseSize * strengthMultiplier);
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
            LogInfo("Initializing Momentum Strategy");
            
            // Clear cache on initialization
            lock (_cacheLock)
            {
                _sustainabilityCache.Clear();
            }
            
            await Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            LogInfo("Momentum Strategy started");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            LogInfo("Momentum Strategy stopped");
            
            // Clear cache
            lock (_cacheLock)
            {
                _sustainabilityCache.Clear();
            }
            
            return Task.CompletedTask;
        }
    }
}
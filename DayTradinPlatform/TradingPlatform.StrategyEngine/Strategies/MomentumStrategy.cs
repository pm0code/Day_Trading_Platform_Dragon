using TradingPlatform.Core.Interfaces;
using TradingPlatform.StrategyEngine.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.StrategyEngine.Strategies;

/// <summary>
/// Momentum breakout trading strategy implementation
/// Identifies and trades momentum breakouts with volume confirmation
/// </summary>
public class MomentumStrategy : IMomentumStrategy
{
    private readonly ITradingLogger _logger;

    public string StrategyName => "Momentum Breakout Strategy";
    public string Description => "Momentum-based trading strategy that identifies breakouts with volume confirmation";

    public MomentumStrategy(ITradingLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TradingSignal[]> GenerateSignalsAsync(string symbol, MarketConditions conditions)
    {
        try
        {
            TradingLogOrchestrator.Instance.LogInfo($"Evaluating momentum signals for {symbol}");

            var momentumSignals = await DetectMomentumAsync(symbol, conditions);
            var momentumStrength = await CalculateMomentumStrengthAsync(symbol, conditions);

            if (momentumSignals.Length == 0 || momentumStrength < 0.6m)
            {
                return Array.Empty<TradingSignal>();
            }

            var signals = new List<TradingSignal>();

            foreach (var momentum in momentumSignals)
            {
                if (momentum.Strength >= 0.7m && momentum.VolumeConfirmation >= 1.5m)
                {
                    var signalType = momentum.Direction == TrendDirection.Up ? SignalType.Buy : SignalType.Sell;
                    
                    var signal = new TradingSignal(
                        Guid.NewGuid().ToString(),
                        "momentum-breakout",
                        symbol,
                        signalType,
                        momentum.BreakoutLevel,
                        CalculatePositionSize(momentum.Strength),
                        momentum.Strength,
                        $"Momentum {momentum.Direction}: Strength={momentum.Strength:F2}, Volume={momentum.VolumeConfirmation:F1}x",
                        DateTimeOffset.UtcNow,
                        new Dictionary<string, object>
                        {
                            ["MomentumStrength"] = momentum.Strength,
                            ["VolumeConfirmation"] = momentum.VolumeConfirmation,
                            ["BreakoutLevel"] = momentum.BreakoutLevel,
                            ["Direction"] = momentum.Direction.ToString()
                        });

                    signals.Add(signal);
                }
            }

            TradingLogOrchestrator.Instance.LogInfo($"Generated {signals.Count} momentum signals for {symbol}");
            return signals.ToArray();
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error generating momentum signals for {symbol}", ex);
            return Array.Empty<TradingSignal>();
        }
    }

    public async Task<MomentumSignal[]> DetectMomentumAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        var signals = new List<MomentumSignal>();

        try
        {
            // Detect momentum based on price movement and volume
            var priceMovementThreshold = 0.02m; // 2% price movement
            var volumeThreshold = 1.5m; // 1.5x average volume

            if (Math.Abs(conditions.PriceChange) >= priceMovementThreshold)
            {
                var direction = conditions.PriceChange > 0 ? TrendDirection.Up : TrendDirection.Down;
                var strength = Math.Min(Math.Abs(conditions.PriceChange) * 10, 1.0m); // Scale to 0-1
                
                // Calculate volume confirmation
                var volumeConfirmation = conditions.Volume / 1000000m; // Assuming average volume of 1M
                
                // Calculate breakout level (mock implementation)
                var breakoutLevel = conditions.Volatility * 100 * (1 + conditions.PriceChange);

                var momentumSignal = new MomentumSignal(
                    symbol,
                    strength,
                    direction,
                    breakoutLevel,
                    volumeConfirmation,
                    DateTimeOffset.UtcNow);

                signals.Add(momentumSignal);

                TradingLogOrchestrator.Instance.LogInfo($"Detected momentum for {symbol}: Direction={direction}, Strength={strength}, Volume={volumeConfirmation}x");
            }

            return signals.ToArray();
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error detecting momentum for {symbol}", ex);
            return Array.Empty<MomentumSignal>();
        }
    }

    public async Task<decimal> CalculateMomentumStrengthAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        try
        {
            // Calculate momentum strength based on multiple factors
            var priceStrength = Math.Min(Math.Abs(conditions.PriceChange) * 10, 1.0m);
            var volumeStrength = Math.Min(conditions.Volume / 2000000m, 1.0m); // Volume factor
            var volatilityStrength = Math.Min(conditions.Volatility * 20, 1.0m); // Volatility factor
            
            // RSI momentum component
            var rsiMomentum = conditions.RSI switch
            {
                > 70 => 0.8m, // Strong upward momentum but overbought
                > 60 => 1.0m, // Strong upward momentum
                > 40 => 0.6m, // Moderate momentum
                > 30 => 0.4m, // Weak momentum
                _ => 0.2m     // Very weak momentum
            };

            // Weighted combination of factors
            var momentumStrength = (priceStrength * 0.3m) + 
                                 (volumeStrength * 0.3m) + 
                                 (volatilityStrength * 0.2m) + 
                                 (rsiMomentum * 0.2m);

            TradingLogOrchestrator.Instance.LogInfo($"Calculated momentum strength for {symbol}: {momentumStrength} (Price={priceStrength}, Volume={volumeStrength}, RSI={rsiMomentum})");

            return Math.Min(momentumStrength, 1.0m);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error calculating momentum strength for {symbol}", ex);
            return 0.0m;
        }
    }

    public bool CanTrade(string symbol)
    {
        // Momentum strategy works best with high-volume, volatile stocks
        var momentumStocks = new[] { "TSLA", "NVDA", "AMD", "ARKK", "SPY", "QQQ", "SQQQ", "TQQQ" };
        return momentumStocks.Contains(symbol.ToUpperInvariant());
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

    /// <summary>
    /// Detect momentum acceleration patterns
    /// </summary>
    public async Task<bool> IsAcceleratingAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        // Mock implementation - would analyze price acceleration
        return Math.Abs(conditions.PriceChange) > 0.03m && conditions.Volume > 1500000;
    }

    /// <summary>
    /// Calculate momentum sustainability score
    /// </summary>
    public async Task<decimal> GetSustainabilityScoreAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        try
        {
            // Factors that contribute to momentum sustainability
            var volumeSupport = Math.Min(conditions.Volume / 1000000m, 2.0m) / 2.0m; // Volume support
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
                > 80 or < 20 => 0.2m, // Extreme levels - low sustainability
                > 70 or < 30 => 0.5m, // High levels - moderate sustainability
                _ => 1.0m             // Normal levels - high sustainability
            };

            var sustainability = (volumeSupport * 0.4m) + (trendStrength * 0.4m) + (rsiSustainability * 0.2m);

            return Math.Min(sustainability, 1.0m);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error calculating sustainability score for {symbol}", ex);
            return 0.0m;
        }
    }

    /// <summary>
    /// Identify momentum reversal signals
    /// </summary>
    public async Task<bool> IsReversalSignalAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        // Reversal signals based on momentum exhaustion
        var highVolumeDivergence = conditions.Volume > 2000000 && Math.Abs(conditions.PriceChange) < 0.01m;
        var extremeRSI = conditions.RSI > 85 || conditions.RSI < 15;
        var volatilitySpike = conditions.Volatility > 0.05m;

        return highVolumeDivergence || extremeRSI || volatilitySpike;
    }

    // Private helper methods
    private int CalculatePositionSize(decimal momentumStrength)
    {
        // Larger positions for stronger momentum signals
        var baseSize = 200;
        var strengthMultiplier = (double)(0.5m + (momentumStrength * 0.5m)); // 0.5x to 1.0x
        return (int)(baseSize * strengthMultiplier);
    }
}
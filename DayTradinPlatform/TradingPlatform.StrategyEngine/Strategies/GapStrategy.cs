using Microsoft.Extensions.Logging;
using TradingPlatform.StrategyEngine.Models;

namespace TradingPlatform.StrategyEngine.Strategies;

/// <summary>
/// Gap trading strategy implementation
/// Identifies and trades gap patterns with statistical probability analysis
/// </summary>
public class GapStrategy : IGapStrategy
{
    private readonly ILogger<GapStrategy> _logger;

    public string StrategyName => "Gap Trading Strategy";
    public string Description => "Gap-based trading strategy focusing on gap fill probabilities and reversal patterns";

    public GapStrategy(ILogger<GapStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TradingSignal[]> GenerateSignalsAsync(string symbol, MarketConditions conditions)
    {
        try
        {
            _logger.LogDebug("Evaluating gap signals for {Symbol}", symbol);

            var gaps = await DetectGapsAsync(symbol, conditions);
            
            if (gaps.Length == 0)
            {
                return Array.Empty<TradingSignal>();
            }

            var signals = new List<TradingSignal>();

            foreach (var gap in gaps)
            {
                var fillProbability = await AssessGapFillProbabilityAsync(symbol, gap);
                
                if (fillProbability >= 0.65m) // Minimum 65% fill probability
                {
                    var signal = await CreateGapSignalAsync(gap, fillProbability);
                    if (signal != null)
                    {
                        signals.Add(signal);
                    }
                }
            }

            _logger.LogInformation("Generated {SignalCount} gap signals for {Symbol}", signals.Count, symbol);
            return signals.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating gap signals for {Symbol}", symbol);
            return Array.Empty<TradingSignal>();
        }
    }

    public async Task<GapPattern[]> DetectGapsAsync(string symbol, MarketConditions conditions)
    {
        await Task.CompletedTask;

        try
        {
            var gaps = new List<GapPattern>();

            // Mock previous close (in real implementation, this would come from historical data)
            var mockPreviousClose = conditions.Volatility * 95; // Estimate previous close
            var currentOpen = conditions.Volatility * 100; // Current open price
            
            var gapSize = Math.Abs(currentOpen - mockPreviousClose);
            var gapPercentage = gapSize / mockPreviousClose;

            // Only consider significant gaps (>1%)
            if (gapPercentage >= 0.01m)
            {
                var gapType = DetermineGapType(currentOpen, mockPreviousClose, gapPercentage, conditions);
                var hasVolumeConfirmation = conditions.Volume > 1000000; // Volume threshold

                var gap = new GapPattern(
                    symbol,
                    gapType,
                    gapSize,
                    gapPercentage,
                    currentOpen,
                    mockPreviousClose,
                    hasVolumeConfirmation,
                    DateTimeOffset.UtcNow);

                gaps.Add(gap);

                _logger.LogDebug("Detected {GapType} gap for {Symbol}: {GapPercentage:P2} gap, Volume confirmation: {VolumeConfirmation}",
                    gapType, symbol, gapPercentage, hasVolumeConfirmation);
            }

            return gaps.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting gaps for {Symbol}", symbol);
            return Array.Empty<GapPattern>();
        }
    }

    public async Task<decimal> AssessGapFillProbabilityAsync(string symbol, GapPattern gap)
    {
        await Task.CompletedTask;

        try
        {
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

            // Market conditions adjustment
            var marketAdjustment = 0.0m;
            // In volatile markets, gaps are less likely to fill immediately
            if (gap.GapPercentage > 0.03m) // 3% or larger gap
            {
                marketAdjustment = -0.05m;
            }

            var finalProbability = baseProbability + sizeAdjustment + volumeAdjustment + marketAdjustment;
            finalProbability = Math.Max(0.0m, Math.Min(1.0m, finalProbability)); // Clamp to 0-1

            _logger.LogDebug("Gap fill probability for {Symbol}: {Probability:P1} (Base: {BaseProbability:P1}, Size: {SizeAdjustment:+0.00;-0.00}, Volume: {VolumeAdjustment:+0.00;-0.00})",
                symbol, finalProbability, baseProbability, sizeAdjustment, volumeAdjustment);

            return finalProbability;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing gap fill probability for {Symbol}", symbol);
            return 0.0m;
        }
    }

    public bool CanTrade(string symbol)
    {
        // Gap strategy works well with ETFs and large-cap stocks
        var gapTradingSymbols = new[] { "SPY", "QQQ", "IWM", "AAPL", "MSFT", "AMZN", "GOOGL", "TSLA" };
        return gapTradingSymbols.Contains(symbol.ToUpperInvariant());
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

    /// <summary>
    /// Calculate optimal entry point for gap trade
    /// </summary>
    public async Task<decimal> CalculateOptimalEntryAsync(GapPattern gap)
    {
        await Task.CompletedTask;

        // Optimal entry strategies based on gap type
        return gap.GapType switch
        {
            GapType.GapUp => gap.PreviousClose + (gap.GapSize * 0.25m), // Enter on partial retracement
            GapType.GapDown => gap.PreviousClose - (gap.GapSize * 0.25m), // Enter on partial bounce
            GapType.CommonGap => gap.OpenPrice, // Enter immediately for common gaps
            GapType.ExhaustionGap => gap.OpenPrice, // Enter immediately for exhaustion gaps
            GapType.BreakoutGap => gap.OpenPrice + (gap.GapSize * 0.1m), // Enter on confirmation
            _ => gap.OpenPrice
        };
    }

    /// <summary>
    /// Calculate stop loss level for gap trade
    /// </summary>
    public async Task<decimal> CalculateStopLossAsync(GapPattern gap, SignalType signalType)
    {
        await Task.CompletedTask;

        var stopLossPercent = gap.GapType switch
        {
            GapType.CommonGap => 0.015m,      // Tight stop for common gaps
            GapType.ExhaustionGap => 0.02m,   // Moderate stop for exhaustion gaps
            GapType.BreakoutGap => 0.035m,    // Wider stop for breakout gaps
            _ => 0.025m                       // Default stop loss
        };

        if (signalType == SignalType.Buy)
        {
            return gap.OpenPrice * (1 - stopLossPercent);
        }
        else
        {
            return gap.OpenPrice * (1 + stopLossPercent);
        }
    }

    /// <summary>
    /// Determine if gap is likely to be filled within the trading session
    /// </summary>
    public async Task<bool> IsIntraDayFillLikelyAsync(GapPattern gap)
    {
        await Task.CompletedTask;

        // Factors that increase intraday fill probability
        var smallGap = gap.GapPercentage < 0.03m; // Less than 3%
        var commonOrExhaustionGap = gap.GapType == GapType.CommonGap || gap.GapType == GapType.ExhaustionGap;
        var hasVolumeConfirmation = gap.HasVolumeConfirmation;

        var fillScore = 0;
        if (smallGap) fillScore += 2;
        if (commonOrExhaustionGap) fillScore += 2;
        if (hasVolumeConfirmation) fillScore += 1;

        return fillScore >= 3; // Need at least 3 points for likely intraday fill
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
            var positionSize = CalculatePositionSize(gap.GapPercentage, fillProbability);

            var signal = new TradingSignal(
                Guid.NewGuid().ToString(),
                "gap-reversal",
                gap.Symbol,
                signalType,
                entryPrice,
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
                    ["OpenPrice"] = gap.OpenPrice
                });

            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating gap signal for {Symbol}", gap.Symbol);
            return null;
        }
    }

    private GapType DetermineGapType(decimal currentOpen, decimal previousClose, decimal gapPercentage, MarketConditions conditions)
    {
        var isGapUp = currentOpen > previousClose;
        
        // Classify gap type based on size and market conditions
        return gapPercentage switch
        {
            >= 0.05m => isGapUp ? GapType.BreakoutGap : GapType.ExhaustionGap, // Large gaps (5%+)
            >= 0.03m => isGapUp ? GapType.GapUp : GapType.GapDown, // Medium gaps (3-5%)
            >= 0.01m => GapType.CommonGap, // Small gaps (1-3%)
            _ => GapType.CommonGap // Very small gaps
        };
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
            _ => 0.8         // Large gaps
        };

        return (int)(baseSize * probabilityMultiplier * gapMultiplier);
    }
}
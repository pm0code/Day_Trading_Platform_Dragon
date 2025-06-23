using TradingPlatform.PaperTrading.Models;

using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
namespace TradingPlatform.PaperTrading.Services;

public class SlippageCalculator : ISlippageCalculator
{
    private readonly ITradingLogger _logger;
    private readonly Dictionary<string, decimal> _averageDailyVolumes = new();

    public SlippageCalculator(ITradingLogger logger)
    {
        _logger = logger;
        InitializeVolumeData();
    }

    public decimal CalculateSlippage(decimal requestedPrice, decimal executedPrice, OrderSide side)
    {
        try
        {
            if (requestedPrice <= 0)
                return 0m;

            var priceDifference = side == OrderSide.Buy 
                ? executedPrice - requestedPrice
                : requestedPrice - executedPrice;

            return Math.Max(0, priceDifference / requestedPrice);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError("Error calculating slippage", ex);
            return 0m;
        }
    }

    public async Task<decimal> EstimateSlippageAsync(string symbol, OrderSide side, decimal quantity)
    {
        try
        {
            var adv = GetAverageDailyVolume(symbol);
            var participationRate = quantity / adv;

            // Square root market impact model
            var baseSlippage = 0.0001m; // 1 basis point base
            var impactCoefficient = 0.05m;
            var estimatedSlippage = baseSlippage + (impactCoefficient * (decimal)Math.Sqrt((double)participationRate));

            // Add bid-ask spread component
            var spreadComponent = GetTypicalSpread(symbol) / 2m;
            var totalSlippage = estimatedSlippage + spreadComponent;

            TradingLogOrchestrator.Instance.LogInfo($"Estimated slippage for {symbol}: {totalSlippage} (participation: {participationRate})");

            return await Task.FromResult(Math.Min(totalSlippage, 0.10m)); // Cap at 10%
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error estimating slippage for {symbol}", ex);
            return 0.001m; // Default 10 basis points
        }
    }

    public async Task<decimal> CalculateMarketImpactAsync(string symbol, decimal quantity, TimeSpan duration)
    {
        try
        {
            var adv = GetAverageDailyVolume(symbol);
            var dailyParticipation = quantity / adv;
            
            // Adjust for execution duration
            var durationHours = duration.TotalHours;
            var tradingHours = 6.5m; // Market hours
            var timeAdjustment = (decimal)Math.Sqrt((double)(tradingHours / (decimal)Math.Max(durationHours, 0.1)));
            
            var adjustedParticipation = dailyParticipation * timeAdjustment;

            // Almgren-Chriss model components
            var temporaryImpact = CalculateTemporaryImpact(adjustedParticipation);
            var permanentImpact = CalculatePermanentImpact(adjustedParticipation);
            
            var totalImpact = temporaryImpact + permanentImpact;

            TradingLogOrchestrator.Instance.LogInfo($"Market impact for {symbol}: {totalImpact} (temp: {temporaryImpact}, perm: {permanentImpact})");

            return await Task.FromResult(totalImpact);
        }
        catch (Exception ex)
        {
            TradingLogOrchestrator.Instance.LogError($"Error calculating market impact for {symbol}", ex);
            return 0.001m;
        }
    }

    private decimal CalculateTemporaryImpact(decimal participationRate)
    {
        // Temporary impact is proportional to square root of participation rate
        var sigma = 0.3m; // Volatility parameter
        var gamma = 0.5m; // Impact coefficient
        
        return gamma * sigma * (decimal)Math.Sqrt((double)participationRate);
    }

    private decimal CalculatePermanentImpact(decimal participationRate)
    {
        // Permanent impact is linear in participation rate
        var eta = 0.01m; // Permanent impact coefficient
        
        return eta * participationRate;
    }

    private decimal GetAverageDailyVolume(string symbol)
    {
        if (_averageDailyVolumes.TryGetValue(symbol, out var volume))
            return volume;

        // Generate realistic ADV based on symbol characteristics
        var symbolHash = Math.Abs(symbol.GetHashCode());
        var baseVolume = 1000000m + (symbolHash % 50000000); // 1M to 51M shares
        
        _averageDailyVolumes[symbol] = baseVolume;
        return baseVolume;
    }

    private decimal GetTypicalSpread(string symbol)
    {
        // Estimate typical bid-ask spread based on symbol characteristics
        var symbolCategory = GetSymbolCategory(symbol);
        
        return symbolCategory switch
        {
            "LargeCap" => 0.0001m,    // 1 basis point
            "MidCap" => 0.0005m,     // 5 basis points
            "SmallCap" => 0.002m,    // 20 basis points
            "ETF" => 0.0001m,        // 1 basis point
            _ => 0.001m              // 10 basis points default
        };
    }

    private string GetSymbolCategory(string symbol)
    {
        // Simplified categorization based on common symbols
        var largeCap = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "NVDA", "META", "BRK.B" };
        var etfs = new[] { "SPY", "QQQ", "IWM", "VTI", "VOO", "EFA", "EEM" };
        
        if (largeCap.Contains(symbol, StringComparer.OrdinalIgnoreCase))
            return "LargeCap";
        
        if (etfs.Contains(symbol, StringComparer.OrdinalIgnoreCase))
            return "ETF";
        
        // Use symbol length as proxy for market cap
        return symbol.Length <= 3 ? "MidCap" : "SmallCap";
    }

    private void InitializeVolumeData()
    {
        // Initialize with known volume data for common symbols
        var knownVolumes = new Dictionary<string, decimal>
        {
            ["AAPL"] = 50000000m,
            ["MSFT"] = 30000000m,
            ["GOOGL"] = 25000000m,
            ["AMZN"] = 35000000m,
            ["TSLA"] = 75000000m,
            ["NVDA"] = 40000000m,
            ["SPY"] = 100000000m,
            ["QQQ"] = 60000000m
        };

        foreach (var kvp in knownVolumes)
        {
            _averageDailyVolumes[kvp.Key] = kvp.Value;
        }
    }
}
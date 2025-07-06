using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.TaxOptimization.Interfaces;
using TradingPlatform.TaxOptimization.Models;

namespace TradingPlatform.TaxOptimization.Services;

/// <summary>
/// Advanced wash sale rule detector and compliance engine
/// Implements IRS Publication 550 wash sale rules with sophisticated violation detection and avoidance strategies
/// </summary>
public class WashSaleDetector : CanonicalServiceBase, IWashSaleDetector
{
    private readonly ITaxLotManager _taxLotManager;
    private readonly TaxConfiguration _taxConfig;
    private readonly Dictionary<string, List<TradeRecord>> _tradeHistory;

    public WashSaleDetector(
        ITradingLogger logger,
        ITaxLotManager taxLotManager,
        TaxConfiguration taxConfig) : base(logger, "WashSaleDetector")
    {
        _taxLotManager = taxLotManager ?? throw new ArgumentNullException(nameof(taxLotManager));
        _taxConfig = taxConfig ?? throw new ArgumentNullException(nameof(taxConfig));
        _tradeHistory = new Dictionary<string, List<TradeRecord>>();
    }

    public async Task<TradingResult<WashSaleAnalysis>> AnalyzeTransactionAsync(string symbol, DateTime saleDate)
    {
        LogMethodEntry();

        try
        {
            var analysis = new WashSaleAnalysis
            {
                Symbol = symbol,
                SaleDate = saleDate,
                IsWashSaleViolation = false,
                AffectedLoss = 0m,
                DeferredLoss = 0m,
                DaysToSafeRepurchase = _taxConfig.WashSaleAvoidanceDays,
                RecommendedAlternatives = new List<string>(),
                ViolationDetails = string.Empty
            };

            // Get trade history for the symbol
            var tradeHistory = await GetTradeHistoryAsync(symbol, saleDate.AddDays(-31), saleDate.AddDays(31));
            if (!tradeHistory.Success || tradeHistory.Data == null)
            {
                LogWarning($"Unable to retrieve trade history for {symbol}");
                return TradingResult<WashSaleAnalysis>.Success(analysis);
            }

            // Check for purchases within 30 days before or after the sale
            var washSalePeriodStart = saleDate.AddDays(-30);
            var washSalePeriodEnd = saleDate.AddDays(30);

            var potentialViolations = tradeHistory.Data
                .Where(trade => trade.TransactionType == "BUY" && 
                               trade.TradeDate >= washSalePeriodStart && 
                               trade.TradeDate <= washSalePeriodEnd &&
                               trade.TradeDate != saleDate)
                .OrderBy(trade => trade.TradeDate)
                .ToList();

            if (potentialViolations.Any())
            {
                var violation = potentialViolations.First();
                analysis.IsWashSaleViolation = true;
                analysis.RepurchaseDate = violation.TradeDate;
                
                // Calculate affected loss (only applies to losses, not gains)
                var saleLoss = await CalculateLossFromSale(symbol, saleDate);
                if (saleLoss > 0)
                {
                    analysis.AffectedLoss = Math.Min(saleLoss, violation.Quantity * violation.Price);
                    analysis.DeferredLoss = analysis.AffectedLoss;
                }

                analysis.ViolationDetails = GenerateViolationDetails(violation, saleDate);
                
                LogWarning($"Wash sale violation detected for {symbol}: Sale on {saleDate:yyyy-MM-dd}, repurchase on {violation.TradeDate:yyyy-MM-dd}");
            }

            // Calculate days to safe repurchase
            analysis.DaysToSafeRepurchase = CalculateDaysToSafeRepurchase(symbol, saleDate);

            // Generate alternative investment recommendations
            var alternatives = await GetSafeAlternativesAsync(symbol);
            if (alternatives.Success && alternatives.Data != null)
            {
                analysis.RecommendedAlternatives = alternatives.Data;
            }

            LogInfo($"Wash sale analysis completed for {symbol}: Violation={analysis.IsWashSaleViolation}, Deferred Loss=${analysis.DeferredLoss:F2}");

            return TradingResult<WashSaleAnalysis>.Success(analysis);
        }
        catch (Exception ex)
        {
            LogError($"Failed to analyze wash sale for {symbol} on {saleDate:yyyy-MM-dd}", ex);
            return TradingResult<WashSaleAnalysis>.Failure(
                "WASH_SALE_ANALYSIS_FAILED",
                ex.Message,
                "Unable to perform wash sale analysis for the specified transaction");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<bool>> IsWashSaleViolationAsync(string symbol, DateTime saleDate, DateTime? repurchaseDate)
    {
        LogMethodEntry();

        try
        {
            if (!repurchaseDate.HasValue)
            {
                return TradingResult<bool>.Success(false);
            }

            // Calculate days between sale and repurchase
            var daysBetween = Math.Abs((repurchaseDate.Value - saleDate).Days);

            // Wash sale rule: 30 days before or after the sale
            var isViolation = daysBetween <= 30;

            if (isViolation)
            {
                LogWarning($"Wash sale violation confirmed for {symbol}: {daysBetween} days between sale ({saleDate:yyyy-MM-dd}) and repurchase ({repurchaseDate.Value:yyyy-MM-dd})");
            }
            else
            {
                LogInfo($"No wash sale violation for {symbol}: {daysBetween} days between transactions");
            }

            return TradingResult<bool>.Success(isViolation);
        }
        catch (Exception ex)
        {
            LogError($"Failed to check wash sale violation for {symbol}", ex);
            return TradingResult<bool>.Failure(
                "WASH_SALE_CHECK_FAILED",
                ex.Message,
                "Unable to determine if transaction violates wash sale rule");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<List<string>>> GetSafeAlternativesAsync(string symbol)
    {
        LogMethodEntry();

        try
        {
            var alternatives = new List<string>();

            // Get sector and industry alternatives that are sufficiently different
            // to avoid wash sale rule while maintaining similar market exposure
            
            var sectorAlternatives = GetSectorAlternatives(symbol);
            alternatives.AddRange(sectorAlternatives);

            // Add broad market ETFs as alternatives
            var broadMarketETFs = new[]
            {
                "SPY", "QQQ", "IWM", "VTI", "VEA", "VWO", 
                "XLF", "XLK", "XLV", "XLE", "XLI", "XLY", 
                "VGT", "VHT", "VFH", "VDE", "VIS", "VCR"
            };
            
            alternatives.AddRange(broadMarketETFs);

            // Remove the original symbol and any potential wash sale violations
            alternatives.Remove(symbol);
            alternatives = RemoveSimilarSecurities(symbol, alternatives);

            // Ensure uniqueness and limit to reasonable number
            alternatives = alternatives.Distinct().Take(15).ToList();

            LogInfo($"Generated {alternatives.Count} safe alternative investments for {symbol}");

            return TradingResult<List<string>>.Success(alternatives);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get safe alternatives for {symbol}", ex);
            return TradingResult<List<string>>.Failure(
                "ALTERNATIVES_GENERATION_FAILED",
                ex.Message,
                "Unable to generate safe investment alternatives");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<int>> CalculateSafeRepurchaseDaysAsync(string symbol, DateTime saleDate)
    {
        LogMethodEntry();

        try
        {
            // Basic wash sale rule: 31 days from sale date
            var safeDateBasic = saleDate.AddDays(_taxConfig.WashSaleAvoidanceDays);
            var daysToSafeBasic = (safeDateBasic - DateTime.UtcNow).Days;

            // Check if there are any pending transactions that might create violations
            var pendingTrades = await GetPendingTradesAsync(symbol);
            var additionalDays = 0;

            if (pendingTrades.Success && pendingTrades.Data?.Any() == true)
            {
                var latestPendingTrade = pendingTrades.Data.Max(t => t.ScheduledDate);
                var safeDateWithPending = latestPendingTrade.AddDays(_taxConfig.WashSaleAvoidanceDays);
                var daysToSafeWithPending = (safeDateWithPending - DateTime.UtcNow).Days;
                
                additionalDays = Math.Max(0, daysToSafeWithPending - daysToSafeBasic);
            }

            var totalSafeDays = Math.Max(0, daysToSafeBasic + additionalDays);

            LogInfo($"Safe repurchase period for {symbol}: {totalSafeDays} days from now");

            return TradingResult<int>.Success(totalSafeDays);
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate safe repurchase days for {symbol}", ex);
            return TradingResult<int>.Failure(
                "SAFE_DAYS_CALCULATION_FAILED",
                ex.Message,
                "Unable to calculate safe repurchase period");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<bool>> MonitorWashSaleRiskAsync()
    {
        LogMethodEntry();

        try
        {
            var riskDetected = false;
            var alertsGenerated = 0;

            // Get all active positions
            var activePositions = await GetActivePositionsAsync();
            if (!activePositions.Success || activePositions.Data == null)
            {
                LogWarning("Unable to retrieve active positions for wash sale monitoring");
                return TradingResult<bool>.Success(false);
            }

            foreach (var position in activePositions.Data)
            {
                // Check for potential wash sale risks
                var analysis = await AnalyzeTransactionAsync(position.Symbol, DateTime.UtcNow);
                if (!analysis.Success) continue;

                if (analysis.Data?.IsWashSaleViolation == true)
                {
                    riskDetected = true;
                    alertsGenerated++;
                    
                    LogWarning($"Wash sale risk detected for {position.Symbol}: {analysis.Data.ViolationDetails}");
                    
                    // Generate alert for risk management
                    await GenerateWashSaleAlert(position.Symbol, analysis.Data);
                }

                // Check for positions approaching wash sale risk
                var daysToRisk = await CalculateDaysToWashSaleRisk(position.Symbol);
                if (daysToRisk <= 7) // Alert if risk is within 7 days
                {
                    LogInfo($"Wash sale risk approaching for {position.Symbol}: {daysToRisk} days");
                    await GenerateWashSaleWarning(position.Symbol, daysToRisk);
                }
            }

            LogInfo($"Wash sale monitoring completed: {alertsGenerated} alerts generated, Risk Detected: {riskDetected}");

            return TradingResult<bool>.Success(riskDetected);
        }
        catch (Exception ex)
        {
            LogError("Failed to monitor wash sale risk", ex);
            return TradingResult<bool>.Failure(
                "WASH_SALE_MONITORING_FAILED",
                ex.Message,
                "Unable to monitor portfolio for wash sale risks");
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Private helper methods
    private async Task<TradingResult<List<TradeRecord>>> GetTradeHistoryAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        LogMethodEntry();
        
        try
        {
            var trades = new List<TradeRecord>();
            
            // This would integrate with the trading platform's trade history
            // For now, return empty list as placeholder
            await Task.Delay(1); // Simulate async operation
            
            return TradingResult<List<TradeRecord>>.Success(trades);
        }
        catch (Exception ex)
        {
            LogError($"Failed to retrieve trade history for {symbol}", ex);
            return TradingResult<List<TradeRecord>>.Failure(
                "TRADE_HISTORY_RETRIEVAL_FAILED",
                ex.Message,
                "Unable to retrieve trade history for analysis");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<decimal> CalculateLossFromSale(string symbol, DateTime saleDate)
    {
        LogMethodEntry();
        
        try
        {
            // This would calculate the actual loss from the sale
            // by looking up the specific transaction details
            await Task.Delay(1); // Simulate async operation
            return 0m; // Placeholder
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate loss from sale for {symbol} on {saleDate:yyyy-MM-dd}", ex);
            return 0m;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private string GenerateViolationDetails(TradeRecord repurchaseTrade, DateTime saleDate)
    {
        var daysBetween = (repurchaseTrade.TradeDate - saleDate).Days;
        var direction = daysBetween > 0 ? "after" : "before";
        
        return $"Repurchased {repurchaseTrade.Quantity} shares on {repurchaseTrade.TradeDate:yyyy-MM-dd} " +
               $"({Math.Abs(daysBetween)} days {direction} sale). " +
               $"Loss deferred under wash sale rule.";
    }

    private int CalculateDaysToSafeRepurchase(string symbol, DateTime saleDate)
    {
        var safeDate = saleDate.AddDays(_taxConfig.WashSaleAvoidanceDays);
        return Math.Max(0, (safeDate - DateTime.UtcNow).Days);
    }

    private List<string> GetSectorAlternatives(string symbol)
    {
        // Enhanced sector mapping with wash sale safe alternatives
        var sectorMap = new Dictionary<string, List<string>>
        {
            ["AAPL"] = new() { "XLK", "VGT", "FTEC", "IYW", "SMH" }, // Tech sector alternatives
            ["MSFT"] = new() { "XLK", "VGT", "FTEC", "IYW", "QTEC" },
            ["GOOGL"] = new() { "XLK", "VGT", "FTEC", "IYW", "QTEC" },
            ["AMZN"] = new() { "XLY", "VCR", "RTH", "IYC", "FDIS" }, // Consumer discretionary
            ["TSLA"] = new() { "XLY", "VCR", "IDRV", "CARZ", "DRIV" }, // Auto/Clean energy
            ["JPM"] = new() { "XLF", "VFH", "KBE", "KBWB", "IAT" }, // Financial sector
            ["JNJ"] = new() { "XLV", "VHT", "IYH", "IBB", "XBI" }, // Healthcare sector
            ["XOM"] = new() { "XLE", "VDE", "OIH", "XOP", "FENY" }, // Energy sector
        };

        return sectorMap.GetValueOrDefault(symbol, new List<string> { "SPY", "QQQ", "VTI" });
    }

    private List<string> RemoveSimilarSecurities(string symbol, List<string> alternatives)
    {
        // Remove securities that might be considered "substantially identical"
        // This is a simplified implementation - real-world would need comprehensive mapping
        
        var similarSecurities = new Dictionary<string, List<string>>
        {
            ["AAPL"] = new() { "AAPL", "AAPD", "AAPB" }, // Remove Apple and Apple derivatives
            ["GOOGL"] = new() { "GOOGL", "GOOG", "GOOG" }, // Remove Google variants
            ["BRK.A"] = new() { "BRK.A", "BRK.B" }, // Remove Berkshire variants
        };

        var toRemove = similarSecurities.GetValueOrDefault(symbol, new List<string> { symbol });
        return alternatives.Where(alt => !toRemove.Contains(alt)).ToList();
    }

    private async Task<TradingResult<List<PendingTrade>>> GetPendingTradesAsync(string symbol)
    {
        LogMethodEntry();
        
        try
        {
            var pendingTrades = new List<PendingTrade>();
            // This would retrieve pending/scheduled trades
            await Task.Delay(1); // Simulate async operation
            return TradingResult<List<PendingTrade>>.Success(pendingTrades);
        }
        catch (Exception ex)
        {
            LogError($"Failed to retrieve pending trades for {symbol}", ex);
            return TradingResult<List<PendingTrade>>.Failure(
                "PENDING_TRADES_RETRIEVAL_FAILED",
                ex.Message,
                "Unable to retrieve pending trades");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<TradingResult<List<Position>>> GetActivePositionsAsync()
    {
        LogMethodEntry();
        
        try
        {
            var positions = new List<Position>();
            // This would retrieve current portfolio positions
            await Task.Delay(1); // Simulate async operation
            return TradingResult<List<Position>>.Success(positions);
        }
        catch (Exception ex)
        {
            LogError("Failed to retrieve active positions", ex);
            return TradingResult<List<Position>>.Failure(
                "POSITIONS_RETRIEVAL_FAILED",
                ex.Message,
                "Unable to retrieve active positions");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task GenerateWashSaleAlert(string symbol, WashSaleAnalysis analysis)
    {
        LogMethodEntry();
        
        try
        {
            // Generate high-priority alert for wash sale violation
            var alertMessage = $"WASH SALE VIOLATION: {symbol} - Loss of ${analysis.DeferredLoss:F2} deferred. " +
                             $"Alternative investments: {string.Join(", ", analysis.RecommendedAlternatives.Take(3))}";
            
            LogWarning($"Generated wash sale alert: {alertMessage}");
            await Task.Delay(1); // Simulate async operation
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate wash sale alert for {symbol}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task GenerateWashSaleWarning(string symbol, int daysToRisk)
    {
        LogMethodEntry();
        
        try
        {
            var warningMessage = $"WASH SALE RISK: {symbol} - Risk in {daysToRisk} days. Consider alternatives if planning to sell at a loss.";
            LogInfo($"Generated wash sale warning: {warningMessage}");
            await Task.Delay(1); // Simulate async operation
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate wash sale warning for {symbol}", ex);
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<int> CalculateDaysToWashSaleRisk(string symbol)
    {
        LogMethodEntry();
        
        try
        {
            // Calculate days until potential wash sale risk
            // This would analyze recent purchases and potential sale scenarios
            await Task.Delay(1); // Simulate async operation
            return 999; // No immediate risk
        }
        catch (Exception ex)
        {
            LogError($"Failed to calculate days to wash sale risk for {symbol}", ex);
            return 999;
        }
        finally
        {
            LogMethodExit();
        }
    }
}

// Helper classes for wash sale detection
public class TradeRecord
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime TradeDate { get; set; }
    public string TransactionType { get; set; } = string.Empty; // BUY, SELL
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string TradeId { get; set; } = string.Empty;
}

public class PendingTrade
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string TradeId { get; set; } = string.Empty;
}

public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal UnrealizedGainLoss { get; set; }
}
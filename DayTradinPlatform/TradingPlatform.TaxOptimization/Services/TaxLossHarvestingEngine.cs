using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Foundation.Models;
using TradingPlatform.TaxOptimization.Interfaces;
using TradingPlatform.TaxOptimization.Models;

namespace TradingPlatform.TaxOptimization.Services;

/// <summary>
/// Advanced tax loss harvesting engine for day traders
/// Implements sophisticated algorithms to minimize tax liability through strategic loss realization
/// </summary>
public class TaxLossHarvestingEngine : CanonicalServiceBase, ITaxLossHarvestingEngine
{
    private readonly ITaxLotManager _taxLotManager;
    private readonly IWashSaleDetector _washSaleDetector;
    private readonly ITaxCalculationEngine _taxCalculationEngine;
    private readonly TaxConfiguration _taxConfig;

    public TaxLossHarvestingEngine(
        ITradingLogger logger,
        ITaxLotManager taxLotManager,
        IWashSaleDetector washSaleDetector,
        ITaxCalculationEngine taxCalculationEngine,
        TaxConfiguration taxConfig) : base(logger, "TaxLossHarvestingEngine")
    {
        _taxLotManager = taxLotManager ?? throw new ArgumentNullException(nameof(taxLotManager));
        _washSaleDetector = washSaleDetector ?? throw new ArgumentNullException(nameof(washSaleDetector));
        _taxCalculationEngine = taxCalculationEngine ?? throw new ArgumentNullException(nameof(taxCalculationEngine));
        _taxConfig = taxConfig ?? throw new ArgumentNullException(nameof(taxConfig));
    }

    public async Task<TradingResult<List<TaxLossHarvestingOpportunity>>> IdentifyHarvestingOpportunitiesAsync()
    {
        LogMethodEntry();

        try
        {
            var opportunities = new List<TaxLossHarvestingOpportunity>();
            var allTaxLots = await GetAllOpenTaxLotsAsync();

            if (!allTaxLots.Success || allTaxLots.Data == null)
            {
                LogWarning("Failed to retrieve tax lots for harvesting analysis");
                return TradingResult<List<TaxLossHarvestingOpportunity>>.Success(opportunities);
            }

            // Group by symbol for analysis
            var lotsBySymbol = allTaxLots.Data.GroupBy(lot => lot.Symbol);

            foreach (var symbolGroup in lotsBySymbol)
            {
                var symbol = symbolGroup.Key;
                var currentPrice = await GetCurrentMarketPrice(symbol);
                
                if (currentPrice <= 0) continue;

                foreach (var lot in symbolGroup)
                {
                    var unrealizedLoss = CalculateUnrealizedLoss(lot, currentPrice);
                    
                    if (unrealizedLoss <= _taxConfig.MinimumLossHarvestAmount) continue;

                    // Check for wash sale risk
                    var washSaleAnalysis = await _washSaleDetector.AnalyzeTransactionAsync(symbol, DateTime.UtcNow);
                    if (!washSaleAnalysis.Success || washSaleAnalysis.Data?.IsWashSaleViolation == true)
                    {
                        LogInfo($"Skipping lot {lot.LotId} due to wash sale risk");
                        continue;
                    }

                    var taxSavings = await EstimateTaxSavingsAsync(new TaxLossHarvestingOpportunity
                    {
                        Symbol = symbol,
                        LotId = lot.LotId,
                        UnrealizedLoss = unrealizedLoss,
                        Quantity = lot.Quantity,
                        AcquisitionDate = lot.AcquisitionDate,
                        IsShortTerm = lot.IsShortTerm
                    });

                    if (!taxSavings.Success) continue;

                    var opportunity = new TaxLossHarvestingOpportunity
                    {
                        Symbol = symbol,
                        LotId = lot.LotId,
                        UnrealizedLoss = unrealizedLoss,
                        Quantity = lot.Quantity,
                        AcquisitionDate = lot.AcquisitionDate,
                        IsShortTerm = lot.IsShortTerm,
                        TaxSavings = taxSavings.Data,
                        Priority = await DeterminePriorityAsync(symbol, unrealizedLoss, lot.IsShortTerm),
                        RecommendedAction = await GenerateRecommendedAction(symbol, unrealizedLoss, lot.IsShortTerm),
                        RecommendationExpiry = CalculateRecommendationExpiry(lot.AcquisitionDate, lot.IsShortTerm)
                    };

                    opportunities.Add(opportunity);
                }
            }

            // Sort by tax savings potential (highest first)
            opportunities = opportunities.OrderByDescending(o => o.TaxSavings).ToList();

            LogInfo($"Identified {opportunities.Count} tax loss harvesting opportunities with total potential savings of ${opportunities.Sum(o => o.TaxSavings):F2}");

            return TradingResult<List<TaxLossHarvestingOpportunity>>.Success(opportunities);
        }
        catch (Exception ex)
        {
            LogError("Failed to identify tax loss harvesting opportunities", ex);
            return TradingResult<List<TaxLossHarvestingOpportunity>>.Failure(
                "HARVESTING_IDENTIFICATION_FAILED", 
                ex.Message, 
                "Unable to analyze portfolio for tax loss harvesting opportunities");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<decimal>> EstimateTaxSavingsAsync(TaxLossHarvestingOpportunity opportunity)
    {
        LogMethodEntry();

        try
        {
            decimal taxRate = opportunity.IsShortTerm ? 
                _taxConfig.ShortTermCapitalGainsRate : 
                _taxConfig.LongTermCapitalGainsRate;

            // Add Net Investment Income Tax if applicable
            if (opportunity.UnrealizedLoss > 0) // Actually a gain, not applicable for losses
            {
                taxRate += _taxConfig.NetInvestmentIncomeRate;
            }

            // Add state tax if configured
            taxRate += _taxConfig.StateIncomeTaxRate;

            var taxSavings = opportunity.UnrealizedLoss * taxRate;

            LogInfo($"Estimated tax savings for {opportunity.Symbol} lot {opportunity.LotId}: ${taxSavings:F2} (Rate: {taxRate:P2})");

            return TradingResult<decimal>.Success(taxSavings);
        }
        catch (Exception ex)
        {
            LogError($"Failed to estimate tax savings for opportunity {opportunity.LotId}", ex);
            return TradingResult<decimal>.Failure(
                "TAX_SAVINGS_CALCULATION_FAILED", 
                ex.Message, 
                "Unable to calculate tax savings for harvesting opportunity");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<bool>> ExecuteHarvestingAsync(string opportunityId)
    {
        LogMethodEntry();

        try
        {
            // Retrieve opportunity details
            var opportunity = await GetOpportunityById(opportunityId);
            if (opportunity == null)
            {
                LogWarning($"Opportunity {opportunityId} not found");
                return TradingResult<bool>.Failure(
                    "OPPORTUNITY_NOT_FOUND", 
                    "Opportunity not found", 
                    "The specified harvesting opportunity could not be located");
            }

            // Final wash sale check
            var washSaleCheck = await _washSaleDetector.AnalyzeTransactionAsync(opportunity.Symbol, DateTime.UtcNow);
            if (!washSaleCheck.Success || washSaleCheck.Data?.IsWashSaleViolation == true)
            {
                LogWarning($"Wash sale violation detected for {opportunity.Symbol}, canceling harvest");
                return TradingResult<bool>.Failure(
                    "WASH_SALE_VIOLATION", 
                    "Wash sale rule violation", 
                    "Cannot execute harvest due to wash sale rule violation");
            }

            // Execute the sale to realize the loss
            var realizationResult = await _taxLotManager.RealizeTaxLotAsync(
                opportunity.LotId, 
                opportunity.Quantity, 
                await GetCurrentMarketPrice(opportunity.Symbol), 
                DateTime.UtcNow);

            if (!realizationResult.Success)
            {
                LogError($"Failed to realize tax lot {opportunity.LotId}");
                return TradingResult<bool>.Failure(
                    "LOT_REALIZATION_FAILED", 
                    realizationResult.ErrorMessage ?? "Unknown error", 
                    "Failed to execute tax lot realization for harvesting");
            }

            // Generate alternative investment suggestions
            var alternatives = await GetAlternativeInvestmentsAsync(opportunity.Symbol);

            LogInfo($"Successfully executed tax loss harvesting for {opportunity.Symbol}: ${opportunity.UnrealizedLoss:F2} loss realized, ${opportunity.TaxSavings:F2} tax savings");

            if (alternatives.Success && alternatives.Data?.Any() == true)
            {
                LogInfo($"Suggested alternative investments: {string.Join(", ", alternatives.Data)}");
            }

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            LogError($"Failed to execute tax loss harvesting for opportunity {opportunityId}", ex);
            return TradingResult<bool>.Failure(
                "HARVESTING_EXECUTION_FAILED", 
                ex.Message, 
                "Unable to execute tax loss harvesting transaction");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<HarvestingPriority>> DeterminePriorityAsync(TaxLossHarvestingOpportunity opportunity)
    {
        LogMethodEntry();

        try
        {
            var priority = await DeterminePriorityAsync(opportunity.Symbol, opportunity.UnrealizedLoss, opportunity.IsShortTerm);
            return TradingResult<HarvestingPriority>.Success(priority);
        }
        catch (Exception ex)
        {
            LogError($"Failed to determine priority for opportunity {opportunity.LotId}", ex);
            return TradingResult<HarvestingPriority>.Failure(
                "PRIORITY_DETERMINATION_FAILED", 
                ex.Message, 
                "Unable to determine harvesting priority");
        }
        finally
        {
            LogMethodExit();
        }
    }

    public async Task<TradingResult<List<string>>> GetAlternativeInvestmentsAsync(string symbol)
    {
        LogMethodEntry();

        try
        {
            var alternatives = new List<string>();

            // For individual stocks, suggest:
            // 1. ETF in same sector
            // 2. Similar companies in same industry
            // 3. Broad market alternatives

            // Example logic for common symbols
            var sectorAlternatives = GetSectorAlternatives(symbol);
            alternatives.AddRange(sectorAlternatives);

            // Add broad market alternatives
            alternatives.AddRange(new[] { "SPY", "QQQ", "IWM", "VTI" });

            // Remove the original symbol to avoid wash sale
            alternatives.Remove(symbol);

            LogInfo($"Generated {alternatives.Count} alternative investments for {symbol}");

            return TradingResult<List<string>>.Success(alternatives.Take(10).ToList());
        }
        catch (Exception ex)
        {
            LogError($"Failed to get alternative investments for {symbol}", ex);
            return TradingResult<List<string>>.Failure(
                "ALTERNATIVES_GENERATION_FAILED", 
                ex.Message, 
                "Unable to generate alternative investment suggestions");
        }
        finally
        {
            LogMethodExit();
        }
    }

    // Private helper methods
    private async Task<TradingResult<List<TaxLot>>> GetAllOpenTaxLotsAsync()
    {
        LogMethodEntry();
        
        try
        {
            // This would typically query all open tax lots from the tax lot manager
            // For now, we'll return an empty list as a placeholder
            var allLots = new List<TaxLot>();
            
            // In a real implementation, this would iterate through all symbols
            // and retrieve their tax lots
            
            return TradingResult<List<TaxLot>>.Success(allLots);
        }
        catch (Exception ex)
        {
            LogError("Failed to retrieve all open tax lots", ex);
            return TradingResult<List<TaxLot>>.Failure(
                "TAX_LOTS_RETRIEVAL_FAILED", 
                ex.Message, 
                "Unable to retrieve tax lots for analysis");
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<decimal> GetCurrentMarketPrice(string symbol)
    {
        LogMethodEntry();
        
        try
        {
            // This would integrate with market data service to get current price
            // For now, return a placeholder
            await Task.Delay(1); // Simulate async operation
            return 100.0m; // Placeholder price
        }
        catch (Exception ex)
        {
            LogError($"Failed to get current market price for {symbol}", ex);
            return 0;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private decimal CalculateUnrealizedLoss(TaxLot lot, decimal currentPrice)
    {
        var currentValue = lot.Quantity * currentPrice;
        var costBasis = lot.CostBasis;
        var unrealizedGainLoss = currentValue - costBasis;
        
        // Return loss as positive number (negative gain)
        return unrealizedGainLoss < 0 ? Math.Abs(unrealizedGainLoss) : 0;
    }

    private async Task<HarvestingPriority> DeterminePriorityAsync(string symbol, decimal unrealizedLoss, bool isShortTerm)
    {
        LogMethodEntry();
        
        try
        {
            var daysToYearEnd = (new DateTime(DateTime.Now.Year, 12, 31) - DateTime.Now).Days;

            if (daysToYearEnd <= 30 && unrealizedLoss >= 5000m)
                return HarvestingPriority.Critical;

            if (unrealizedLoss >= 2000m && isShortTerm)
                return HarvestingPriority.High;

            if (unrealizedLoss >= 1000m)
                return HarvestingPriority.Medium;

            if (unrealizedLoss >= _taxConfig.MinimumLossHarvestAmount)
                return HarvestingPriority.Low;

            await Task.CompletedTask; // Ensure async signature
            return HarvestingPriority.Deferred;
        }
        catch (Exception ex)
        {
            LogError($"Failed to determine priority for {symbol}", ex);
            return HarvestingPriority.Low;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private async Task<string> GenerateRecommendedAction(string symbol, decimal unrealizedLoss, bool isShortTerm)
    {
        LogMethodEntry();
        
        try
        {
            var action = unrealizedLoss >= _taxConfig.MinimumLossHarvestAmount
                ? $"SELL {symbol} to realize ${unrealizedLoss:F2} loss"
                : $"MONITOR {symbol} for increased loss opportunity";

            if (isShortTerm)
                action += " (SHORT-TERM LOSS - Higher tax benefit)";

            await Task.CompletedTask; // Ensure async signature
            return action;
        }
        catch (Exception ex)
        {
            LogError($"Failed to generate recommended action for {symbol}", ex);
            return $"REVIEW {symbol} manually";
        }
        finally
        {
            LogMethodExit();
        }
    }

    private DateTime CalculateRecommendationExpiry(DateTime acquisitionDate, bool isShortTerm)
    {
        // If close to becoming long-term, prioritize before the one-year mark
        if (isShortTerm)
        {
            var oneYearMark = acquisitionDate.AddYears(1);
            var daysToLongTerm = (oneYearMark - DateTime.UtcNow).Days;
            
            if (daysToLongTerm <= 30)
                return oneYearMark.AddDays(-1); // Harvest before becoming long-term
        }

        // Default to year-end or 30 days, whichever is sooner
        var yearEnd = new DateTime(DateTime.Now.Year, 12, 31);
        var thirtyDaysOut = DateTime.UtcNow.AddDays(30);
        
        return yearEnd < thirtyDaysOut ? yearEnd : thirtyDaysOut;
    }

    private async Task<TaxLossHarvestingOpportunity?> GetOpportunityById(string opportunityId)
    {
        LogMethodEntry();
        
        try
        {
            // This would retrieve the opportunity from cache or database
            // For now, return null as placeholder
            await Task.Delay(1); // Simulate async operation
            return null;
        }
        catch (Exception ex)
        {
            LogError($"Failed to retrieve opportunity {opportunityId}", ex);
            return null;
        }
        finally
        {
            LogMethodExit();
        }
    }

    private List<string> GetSectorAlternatives(string symbol)
    {
        // Simplified sector mapping - in production, this would use
        // a comprehensive database of sector classifications
        var sectorMap = new Dictionary<string, List<string>>
        {
            ["AAPL"] = new() { "QQQ", "XLK", "VGT", "MSFT", "GOOGL" },
            ["MSFT"] = new() { "QQQ", "XLK", "VGT", "AAPL", "GOOGL" },
            ["GOOGL"] = new() { "QQQ", "XLK", "VGT", "AAPL", "MSFT" },
            ["TSLA"] = new() { "XLY", "VCR", "GM", "F", "NIO" },
            ["AMZN"] = new() { "XLY", "VCR", "WMT", "HD", "TGT" },
            ["JPM"] = new() { "XLF", "VFH", "BAC", "WFC", "C" },
            ["JNJ"] = new() { "XLV", "VHT", "PFE", "UNH", "ABBV" }
        };

        return sectorMap.GetValueOrDefault(symbol, new List<string>());
    }
}
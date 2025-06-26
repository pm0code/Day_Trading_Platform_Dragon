using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.ML.Common;

namespace TradingPlatform.ML.Algorithms.SARI
{
    /// <summary>
    /// Models how stress scenarios propagate through portfolios considering
    /// correlations, contagion effects, and feedback loops
    /// </summary>
    public class StressPropagationEngine : CanonicalServiceBase
    {
        private readonly IMarketDataService _marketDataService;
        private readonly Dictionary<string, ContagionModel> _contagionModels;
        private float[,] _baseCorrelationMatrix;
        private readonly object _correlationLock = new object();

        public StressPropagationEngine(
            IMarketDataService marketDataService,
            ICanonicalLogger logger) : base(logger)
        {
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _contagionModels = new Dictionary<string, ContagionModel>();
            InitializeContagionModels();
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Initializing stress propagation engine");
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogMethodExit();
            return Task.FromResult(TradingResult.Success());
        }

        /// <summary>
        /// Propagate stress scenario through portfolio considering all effects
        /// </summary>
        public async Task<TradingResult<StressPropagationResult>> PropagateStressAsync(
            StressScenario scenario,
            Portfolio portfolio,
            PropagationParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Propagating stress scenario: {scenario.Name} through portfolio with {portfolio.Holdings.Count} positions");

            try
            {
                var result = new StressPropagationResult
                {
                    ScenarioId = scenario.Id,
                    Timestamp = DateTime.UtcNow,
                    DirectImpacts = new Dictionary<string, AssetImpact>(),
                    ContagionEffects = new Dictionary<string, float>(),
                    FeedbackLoops = new List<FeedbackEffect>()
                };

                // Step 1: Calculate direct impacts
                LogDebug("Calculating direct impacts");
                CalculateDirectImpacts(scenario, portfolio, result);

                // Step 2: Model stressed correlations
                LogDebug("Modeling stressed correlations");
                var stressedCorrelations = await ModelStressedCorrelationsAsync(
                    scenario, 
                    portfolio, 
                    cancellationToken);

                // Step 3: Calculate contagion effects
                LogDebug("Calculating contagion effects");
                await CalculateContagionEffectsAsync(
                    scenario,
                    portfolio,
                    result,
                    stressedCorrelations,
                    parameters,
                    cancellationToken);

                // Step 4: Model feedback loops
                LogDebug("Modeling feedback loops");
                ModelFeedbackLoops(scenario, portfolio, result, parameters);

                // Step 5: Calculate liquidity spirals
                LogDebug("Calculating liquidity spirals");
                CalculateLiquiditySpirals(scenario, portfolio, result, parameters);

                // Step 6: Aggregate total impact
                AggregateImpacts(result);

                LogInfo($"Stress propagation completed. Total portfolio impact: {result.TotalPortfolioImpact:F4}");
                LogMethodExit();
                return TradingResult<StressPropagationResult>.Success(result);
            }
            catch (Exception ex)
            {
                LogError("Error propagating stress scenario", ex);
                return TradingResult<StressPropagationResult>.Failure($"Stress propagation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update base correlation matrix from market data
        /// </summary>
        public async Task<TradingResult> UpdateCorrelationMatrixAsync(
            List<string> assets,
            int lookbackDays,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Updating correlation matrix for {assets.Count} assets with {lookbackDays} days lookback");

            try
            {
                // Fetch historical returns
                var returns = await FetchHistoricalReturnsAsync(assets, lookbackDays, cancellationToken);
                
                // Calculate correlation matrix
                var correlationMatrix = CalculateCorrelationMatrix(returns);
                
                lock (_correlationLock)
                {
                    _baseCorrelationMatrix = correlationMatrix;
                }

                LogInfo("Correlation matrix updated successfully");
                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error updating correlation matrix", ex);
                return TradingResult.Failure($"Failed to update correlations: {ex.Message}");
            }
        }

        private void CalculateDirectImpacts(
            StressScenario scenario,
            Portfolio portfolio,
            StressPropagationResult result)
        {
            foreach (var holding in portfolio.Holdings)
            {
                var impact = new AssetImpact
                {
                    Symbol = holding.Key,
                    DirectShock = 0,
                    VolatilityImpact = 1.0f,
                    LiquidityImpact = 1.0f
                };

                // Apply asset class shock
                var assetClass = GetAssetClass(holding.Key);
                if (scenario.AssetClassShocks?.ContainsKey(assetClass) == true)
                {
                    var shock = scenario.AssetClassShocks[assetClass];
                    impact.DirectShock = shock.ShockPercent;
                    impact.VolatilityImpact = shock.VolatilityMultiplier;
                    
                    LogDebug($"Asset {holding.Key}: Direct shock {shock.ShockPercent:F4}, Vol multiplier {shock.VolatilityMultiplier:F2}");
                }

                // Apply sector-specific shock if available
                var sector = GetSector(holding.Key);
                if (scenario.SectorShocks?.ContainsKey(sector) == true)
                {
                    impact.DirectShock = Math.Min(impact.DirectShock, scenario.SectorShocks[sector]);
                    LogDebug($"Asset {holding.Key}: Sector shock {scenario.SectorShocks[sector]:F4}");
                }

                // Apply liquidity impact
                if (scenario.LiquidityImpact != null)
                {
                    impact.LiquidityImpact = scenario.LiquidityImpact.BidAskMultiplier;
                }

                result.DirectImpacts[holding.Key] = impact;
            }
        }

        private async Task<float[,]> ModelStressedCorrelationsAsync(
            StressScenario scenario,
            Portfolio portfolio,
            CancellationToken cancellationToken)
        {
            var assets = portfolio.Holdings.Keys.ToList();
            var n = assets.Count;
            var stressedCorr = new float[n, n];

            // Start with base correlations
            lock (_correlationLock)
            {
                if (_baseCorrelationMatrix != null && _baseCorrelationMatrix.GetLength(0) == n)
                {
                    Array.Copy(_baseCorrelationMatrix, stressedCorr, n * n);
                }
                else
                {
                    // Initialize with identity matrix if base not available
                    for (int i = 0; i < n; i++)
                    {
                        stressedCorr[i, i] = 1.0f;
                    }
                }
            }

            // Apply stress correlation adjustments
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var asset1Class = GetAssetClass(assets[i]);
                    var asset2Class = GetAssetClass(assets[j]);

                    // Check for correlation override in scenario
                    float? overrideCorr = null;
                    if (scenario.AssetClassShocks?.ContainsKey(asset1Class) == true)
                    {
                        overrideCorr = scenario.AssetClassShocks[asset1Class].CorrelationOverride;
                    }

                    if (overrideCorr.HasValue)
                    {
                        stressedCorr[i, j] = overrideCorr.Value;
                        stressedCorr[j, i] = overrideCorr.Value;
                        LogDebug($"Correlation override for {assets[i]}-{assets[j]}: {overrideCorr.Value:F4}");
                    }
                    else
                    {
                        // Increase correlations during stress (contagion effect)
                        float stressMultiplier = GetStressCorrelationMultiplier(scenario.Severity);
                        stressedCorr[i, j] = Math.Min(0.95f, stressedCorr[i, j] * stressMultiplier);
                        stressedCorr[j, i] = stressedCorr[i, j];
                    }
                }
            }

            return await Task.FromResult(stressedCorr);
        }

        private async Task CalculateContagionEffectsAsync(
            StressScenario scenario,
            Portfolio portfolio,
            StressPropagationResult result,
            float[,] stressedCorrelations,
            PropagationParameters parameters,
            CancellationToken cancellationToken)
        {
            var assets = portfolio.Holdings.Keys.ToList();
            var n = assets.Count;

            // Initialize contagion matrix
            var contagionMatrix = new float[n, n];

            // Apply contagion models
            foreach (var modelKvp in _contagionModels)
            {
                if (IsContagionModelApplicable(modelKvp.Key, scenario))
                {
                    LogDebug($"Applying contagion model: {modelKvp.Key}");
                    var model = modelKvp.Value;
                    
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i != j)
                            {
                                var contagion = model.CalculateContagion(
                                    assets[i],
                                    assets[j],
                                    result.DirectImpacts[assets[i]].DirectShock,
                                    stressedCorrelations[i, j],
                                    parameters);
                                
                                contagionMatrix[i, j] = contagion;
                            }
                        }
                    }
                }
            }

            // Calculate total contagion effect for each asset
            for (int i = 0; i < n; i++)
            {
                float totalContagion = 0;
                for (int j = 0; j < n; j++)
                {
                    if (i != j)
                    {
                        totalContagion += contagionMatrix[j, i] * Math.Abs(result.DirectImpacts[assets[j]].DirectShock);
                    }
                }
                
                result.ContagionEffects[assets[i]] = totalContagion;
                LogDebug($"Asset {assets[i]} total contagion effect: {totalContagion:F4}");
            }
        }

        private void ModelFeedbackLoops(
            StressScenario scenario,
            Portfolio portfolio,
            StressPropagationResult result,
            PropagationParameters parameters)
        {
            // Margin call feedback loop
            if (parameters.ModelMarginCalls)
            {
                var marginCallEffect = ModelMarginCallFeedback(portfolio, result, parameters);
                if (marginCallEffect != null)
                {
                    result.FeedbackLoops.Add(marginCallEffect);
                    LogInfo($"Margin call feedback: {marginCallEffect.ImpactPercent:F4} additional impact");
                }
            }

            // Risk limit breach feedback
            if (parameters.ModelRiskLimitBreaches)
            {
                var riskLimitEffect = ModelRiskLimitFeedback(portfolio, result, parameters);
                if (riskLimitEffect != null)
                {
                    result.FeedbackLoops.Add(riskLimitEffect);
                    LogInfo($"Risk limit feedback: {riskLimitEffect.ImpactPercent:F4} additional impact");
                }
            }

            // Volatility targeting feedback
            if (parameters.ModelVolatilityTargeting)
            {
                var volTargetEffect = ModelVolatilityTargetingFeedback(scenario, result, parameters);
                if (volTargetEffect != null)
                {
                    result.FeedbackLoops.Add(volTargetEffect);
                    LogInfo($"Volatility targeting feedback: {volTargetEffect.ImpactPercent:F4} additional impact");
                }
            }
        }

        private void CalculateLiquiditySpirals(
            StressScenario scenario,
            Portfolio portfolio,
            StressPropagationResult result,
            PropagationParameters parameters)
        {
            if (scenario.LiquidityImpact == null) return;

            var liquiditySpiral = new LiquiditySpiral
            {
                InitialShock = scenario.LiquidityImpact,
                Rounds = new List<LiquiditySpiralRound>()
            };

            // Model multiple rounds of liquidity deterioration
            float cumulativeImpact = 0;
            for (int round = 0; round < parameters.MaxLiquiditySpiralRounds; round++)
            {
                var roundImpact = CalculateLiquiditySpiralRound(
                    round,
                    scenario.LiquidityImpact,
                    portfolio,
                    result,
                    cumulativeImpact);

                if (roundImpact.ImpactPercent < parameters.LiquiditySpiralThreshold)
                {
                    LogDebug($"Liquidity spiral converged at round {round + 1}");
                    break;
                }

                liquiditySpiral.Rounds.Add(roundImpact);
                cumulativeImpact += roundImpact.ImpactPercent;
            }

            liquiditySpiral.TotalImpact = cumulativeImpact;
            result.LiquiditySpiral = liquiditySpiral;
            
            LogInfo($"Liquidity spiral total impact: {cumulativeImpact:F4} over {liquiditySpiral.Rounds.Count} rounds");
        }

        private void AggregateImpacts(StressPropagationResult result)
        {
            float totalImpact = 0;
            
            // Sum direct impacts
            foreach (var impact in result.DirectImpacts.Values)
            {
                totalImpact += impact.DirectShock;
            }

            // Add contagion effects
            totalImpact += result.ContagionEffects.Values.Sum();

            // Add feedback loops
            totalImpact += result.FeedbackLoops.Sum(f => f.ImpactPercent);

            // Add liquidity spiral
            if (result.LiquiditySpiral != null)
            {
                totalImpact += result.LiquiditySpiral.TotalImpact;
            }

            result.TotalPortfolioImpact = totalImpact;
            
            // Calculate impact breakdown
            result.ImpactBreakdown = new Dictionary<string, float>
            {
                ["Direct"] = result.DirectImpacts.Values.Sum(i => i.DirectShock),
                ["Contagion"] = result.ContagionEffects.Values.Sum(),
                ["Feedback"] = result.FeedbackLoops.Sum(f => f.ImpactPercent),
                ["Liquidity"] = result.LiquiditySpiral?.TotalImpact ?? 0
            };

            LogDebug($"Impact breakdown: Direct={result.ImpactBreakdown["Direct"]:F4}, " +
                    $"Contagion={result.ImpactBreakdown["Contagion"]:F4}, " +
                    $"Feedback={result.ImpactBreakdown["Feedback"]:F4}, " +
                    $"Liquidity={result.ImpactBreakdown["Liquidity"]:F4}");
        }

        private void InitializeContagionModels()
        {
            // Financial sector contagion
            _contagionModels["FINANCIAL_CONTAGION"] = new FinancialContagionModel();
            
            // Supply chain contagion
            _contagionModels["SUPPLY_CHAIN"] = new SupplyChainContagionModel();
            
            // Currency contagion
            _contagionModels["CURRENCY_CONTAGION"] = new CurrencyContagionModel();
            
            // Sentiment contagion
            _contagionModels["SENTIMENT_CONTAGION"] = new SentimentContagionModel();
        }

        private float GetStressCorrelationMultiplier(StressSeverity severity)
        {
            return severity switch
            {
                StressSeverity.Mild => 1.2f,
                StressSeverity.Moderate => 1.5f,
                StressSeverity.Severe => 2.0f,
                StressSeverity.Extreme => 3.0f,
                _ => 1.0f
            };
        }

        private bool IsContagionModelApplicable(string modelName, StressScenario scenario)
        {
            // Logic to determine which contagion models apply to scenario
            return modelName switch
            {
                "FINANCIAL_CONTAGION" => scenario.Category == ScenarioCategory.Historical ||
                                       scenario.Id.Contains("FINANCIAL") ||
                                       scenario.Id.Contains("CRISIS"),
                "SUPPLY_CHAIN" => scenario.Id.Contains("CHINA") ||
                                scenario.Id.Contains("GEOPOLITICAL"),
                "CURRENCY_CONTAGION" => scenario.Id.Contains("EMERGING") ||
                                      scenario.RegionShocks?.Any() == true,
                "SENTIMENT_CONTAGION" => true, // Always applies
                _ => false
            };
        }

        private FeedbackEffect ModelMarginCallFeedback(
            Portfolio portfolio,
            StressPropagationResult result,
            PropagationParameters parameters)
        {
            // Calculate portfolio loss
            float portfolioLoss = Math.Abs(result.DirectImpacts.Values.Sum(i => i.DirectShock));
            
            // Check if margin call triggered
            if (portfolioLoss > parameters.MarginCallThreshold)
            {
                float forcedSellingImpact = (portfolioLoss - parameters.MarginCallThreshold) * 
                                          parameters.ForcedSellingMultiplier;
                
                return new FeedbackEffect
                {
                    Type = FeedbackType.MarginCall,
                    ImpactPercent = forcedSellingImpact,
                    Description = $"Margin calls triggered at {portfolioLoss:F2} loss"
                };
            }

            return null;
        }

        private FeedbackEffect ModelRiskLimitFeedback(
            Portfolio portfolio,
            StressPropagationResult result,
            PropagationParameters parameters)
        {
            // Estimate new portfolio volatility under stress
            float stressVolatility = EstimateStressVolatility(result);
            
            if (stressVolatility > parameters.RiskLimitVolatility)
            {
                float deleveragingImpact = (stressVolatility - parameters.RiskLimitVolatility) * 
                                         parameters.DeleveragingMultiplier;
                
                return new FeedbackEffect
                {
                    Type = FeedbackType.RiskLimitBreach,
                    ImpactPercent = deleveragingImpact,
                    Description = $"Risk limits breached at {stressVolatility:F2} volatility"
                };
            }

            return null;
        }

        private FeedbackEffect ModelVolatilityTargetingFeedback(
            StressScenario scenario,
            StressPropagationResult result,
            PropagationParameters parameters)
        {
            // Average volatility multiplier from scenario
            float avgVolMultiplier = 1.0f;
            if (scenario.AssetClassShocks != null)
            {
                avgVolMultiplier = scenario.AssetClassShocks.Values
                    .Average(s => s.VolatilityMultiplier);
            }

            if (avgVolMultiplier > parameters.VolTargetingTrigger)
            {
                float rebalancingImpact = (avgVolMultiplier - 1.0f) * 
                                        parameters.VolTargetingMultiplier;
                
                return new FeedbackEffect
                {
                    Type = FeedbackType.VolatilityTargeting,
                    ImpactPercent = rebalancingImpact,
                    Description = $"Vol targeting triggered at {avgVolMultiplier:F2}x normal volatility"
                };
            }

            return null;
        }

        private LiquiditySpiralRound CalculateLiquiditySpiralRound(
            int round,
            LiquidityShock initialShock,
            Portfolio portfolio,
            StressPropagationResult result,
            float cumulativeImpact)
        {
            // Liquidity deteriorates with each round
            float roundMultiplier = (float)Math.Pow(0.7, round); // 30% decay per round
            
            float bidAskImpact = (initialShock.BidAskMultiplier - 1.0f) * roundMultiplier * 0.01f;
            float volumeImpact = initialShock.VolumeReduction * roundMultiplier * 0.02f;
            float depthImpact = initialShock.MarketDepthReduction * roundMultiplier * 0.03f;
            
            return new LiquiditySpiralRound
            {
                Round = round + 1,
                BidAskImpact = bidAskImpact,
                VolumeImpact = volumeImpact,
                DepthImpact = depthImpact,
                ImpactPercent = bidAskImpact + volumeImpact + depthImpact
            };
        }

        private float EstimateStressVolatility(StressPropagationResult result)
        {
            // Simplified volatility estimation
            float baseVol = 0.15f; // 15% annual
            float avgVolMultiplier = result.DirectImpacts.Values
                .Average(i => i.VolatilityImpact);
            
            return baseVol * avgVolMultiplier;
        }

        private async Task<Dictionary<string, float[]>> FetchHistoricalReturnsAsync(
            List<string> assets,
            int lookbackDays,
            CancellationToken cancellationToken)
        {
            // In practice, would fetch from market data service
            var returns = new Dictionary<string, float[]>();
            var random = new Random(42);
            
            foreach (var asset in assets)
            {
                var assetReturns = new float[lookbackDays];
                for (int i = 0; i < lookbackDays; i++)
                {
                    assetReturns[i] = (float)(random.NextDouble() * 0.04 - 0.02);
                }
                returns[asset] = assetReturns;
            }
            
            return returns;
        }

        private float[,] CalculateCorrelationMatrix(Dictionary<string, float[]> returns)
        {
            var assets = returns.Keys.ToList();
            int n = assets.Count;
            var correlation = new float[n, n];
            
            for (int i = 0; i < n; i++)
            {
                correlation[i, i] = 1.0f;
                
                for (int j = i + 1; j < n; j++)
                {
                    correlation[i, j] = CalculateCorrelation(
                        returns[assets[i]], 
                        returns[assets[j]]);
                    correlation[j, i] = correlation[i, j];
                }
            }
            
            return correlation;
        }

        private float CalculateCorrelation(float[] x, float[] y)
        {
            if (x.Length != y.Length || x.Length == 0) return 0;
            
            float meanX = x.Average();
            float meanY = y.Average();
            
            float covariance = 0;
            float varX = 0;
            float varY = 0;
            
            for (int i = 0; i < x.Length; i++)
            {
                float dx = x[i] - meanX;
                float dy = y[i] - meanY;
                covariance += dx * dy;
                varX += dx * dx;
                varY += dy * dy;
            }
            
            if (varX == 0 || varY == 0) return 0;
            
            return covariance / (float)Math.Sqrt(varX * varY);
        }

        // Helper methods for asset classification
        private string GetAssetClass(string symbol)
        {
            // Simplified - in practice would use proper asset classification
            if (symbol.EndsWith("BND") || symbol.EndsWith("AGG")) return "Bonds";
            if (symbol.EndsWith("GLD") || symbol.EndsWith("SLV")) return "Commodities";
            if (symbol.EndsWith("VNQ") || symbol.EndsWith("REM")) return "RealEstate";
            return "Equity";
        }

        private string GetSector(string symbol)
        {
            // Simplified - in practice would use proper sector classification
            var sectorMap = new Dictionary<string, string>
            {
                ["JPM"] = "Financials", ["BAC"] = "Financials", ["GS"] = "Financials",
                ["AAPL"] = "Technology", ["MSFT"] = "Technology", ["GOOGL"] = "Technology",
                ["XOM"] = "Energy", ["CVX"] = "Energy",
                ["JNJ"] = "Healthcare", ["UNH"] = "Healthcare",
                ["AMZN"] = "ConsumerDiscretionary", ["TSLA"] = "ConsumerDiscretionary",
                ["PG"] = "ConsumerStaples", ["KO"] = "ConsumerStaples"
            };
            
            return sectorMap.GetValueOrDefault(symbol, "Other");
        }
    }

    // Supporting classes
    public class StressPropagationResult
    {
        public string ScenarioId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, AssetImpact> DirectImpacts { get; set; }
        public Dictionary<string, float> ContagionEffects { get; set; }
        public List<FeedbackEffect> FeedbackLoops { get; set; }
        public LiquiditySpiral LiquiditySpiral { get; set; }
        public float TotalPortfolioImpact { get; set; }
        public Dictionary<string, float> ImpactBreakdown { get; set; }
    }

    public class AssetImpact
    {
        public string Symbol { get; set; }
        public float DirectShock { get; set; }
        public float VolatilityImpact { get; set; }
        public float LiquidityImpact { get; set; }
        public float TotalImpact => DirectShock * VolatilityImpact * LiquidityImpact;
    }

    public class FeedbackEffect
    {
        public FeedbackType Type { get; set; }
        public float ImpactPercent { get; set; }
        public string Description { get; set; }
    }

    public enum FeedbackType
    {
        MarginCall,
        RiskLimitBreach,
        VolatilityTargeting,
        Deleveraging,
        ForcedLiquidation
    }

    public class LiquiditySpiral
    {
        public LiquidityShock InitialShock { get; set; }
        public List<LiquiditySpiralRound> Rounds { get; set; }
        public float TotalImpact { get; set; }
    }

    public class LiquiditySpiralRound
    {
        public int Round { get; set; }
        public float BidAskImpact { get; set; }
        public float VolumeImpact { get; set; }
        public float DepthImpact { get; set; }
        public float ImpactPercent { get; set; }
    }

    public class PropagationParameters
    {
        // Contagion parameters
        public float ContagionThreshold { get; set; } = 0.1f;
        public float ContagionDecay { get; set; } = 0.5f;
        
        // Feedback loop parameters
        public bool ModelMarginCalls { get; set; } = true;
        public float MarginCallThreshold { get; set; } = 0.15f;
        public float ForcedSellingMultiplier { get; set; } = 2.0f;
        
        public bool ModelRiskLimitBreaches { get; set; } = true;
        public float RiskLimitVolatility { get; set; } = 0.25f;
        public float DeleveragingMultiplier { get; set; } = 1.5f;
        
        public bool ModelVolatilityTargeting { get; set; } = true;
        public float VolTargetingTrigger { get; set; } = 1.5f;
        public float VolTargetingMultiplier { get; set; } = 0.5f;
        
        // Liquidity spiral parameters
        public int MaxLiquiditySpiralRounds { get; set; } = 5;
        public float LiquiditySpiralThreshold { get; set; } = 0.001f;
    }

    // Contagion models
    public abstract class ContagionModel
    {
        public abstract float CalculateContagion(
            string sourceAsset,
            string targetAsset,
            float sourceShock,
            float correlation,
            PropagationParameters parameters);
    }

    public class FinancialContagionModel : ContagionModel
    {
        public override float CalculateContagion(
            string sourceAsset,
            string targetAsset,
            float sourceShock,
            float correlation,
            PropagationParameters parameters)
        {
            // Financial contagion stronger for same sector
            float sectorMultiplier = AreSameSector(sourceAsset, targetAsset) ? 1.5f : 1.0f;
            
            // Non-linear contagion effect
            float contagion = Math.Abs(sourceShock) * correlation * correlation * sectorMultiplier;
            
            // Apply threshold
            return contagion > parameters.ContagionThreshold ? contagion : 0;
        }

        private bool AreSameSector(string asset1, string asset2)
        {
            // Simplified check
            return (asset1.StartsWith("JP") && asset2.StartsWith("JP")) ||
                   (asset1.StartsWith("BA") && asset2.StartsWith("BA"));
        }
    }

    public class SupplyChainContagionModel : ContagionModel
    {
        public override float CalculateContagion(
            string sourceAsset,
            string targetAsset,
            float sourceShock,
            float correlation,
            PropagationParameters parameters)
        {
            // Supply chain effects decay with distance
            float supplyChainLink = GetSupplyChainLinkStrength(sourceAsset, targetAsset);
            return Math.Abs(sourceShock) * supplyChainLink * parameters.ContagionDecay;
        }

        private float GetSupplyChainLinkStrength(string asset1, string asset2)
        {
            // Simplified - would use actual supply chain data
            return 0.3f;
        }
    }

    public class CurrencyContagionModel : ContagionModel
    {
        public override float CalculateContagion(
            string sourceAsset,
            string targetAsset,
            float sourceShock,
            float correlation,
            PropagationParameters parameters)
        {
            // Currency contagion based on trade relationships
            float tradeIntensity = GetTradeIntensity(sourceAsset, targetAsset);
            return Math.Abs(sourceShock) * correlation * tradeIntensity;
        }

        private float GetTradeIntensity(string asset1, string asset2)
        {
            // Simplified - would use actual trade data
            return 0.2f;
        }
    }

    public class SentimentContagionModel : ContagionModel
    {
        public override float CalculateContagion(
            string sourceAsset,
            string targetAsset,
            float sourceShock,
            float correlation,
            PropagationParameters parameters)
        {
            // Sentiment spreads based on correlation and shock magnitude
            float sentimentSpread = (float)Math.Tanh(Math.Abs(sourceShock) * 3) * correlation;
            return sentimentSpread * 0.5f; // Sentiment has 50% impact of direct shock
        }
    }
}
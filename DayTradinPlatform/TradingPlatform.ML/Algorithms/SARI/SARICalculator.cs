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
    /// Core SARI (Stress-Adjusted Risk Index) calculator
    /// Aggregates stress scenario results into a single risk metric
    /// </summary>
    public class SARICalculator : CanonicalServiceBase
    {
        private readonly StressScenarioLibrary _scenarioLibrary;
        private readonly StressPropagationEngine _propagationEngine;
        private readonly IMarketDataService _marketDataService;
        private readonly Dictionary<string, float> _scenarioWeights;
        private readonly object _weightsLock = new object();

        public SARICalculator(
            StressScenarioLibrary scenarioLibrary,
            StressPropagationEngine propagationEngine,
            IMarketDataService marketDataService,
            ICanonicalLogger logger) : base(logger)
        {
            _scenarioLibrary = scenarioLibrary ?? throw new ArgumentNullException(nameof(scenarioLibrary));
            _propagationEngine = propagationEngine ?? throw new ArgumentNullException(nameof(propagationEngine));
            _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
            _scenarioWeights = new Dictionary<string, float>();
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
            LogInfo("Initializing SARI calculator");
            InitializeDefaultWeights();
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
        /// Calculate comprehensive SARI for portfolio
        /// </summary>
        public async Task<TradingResult<SARIResult>> CalculateSARIAsync(
            Portfolio portfolio,
            MarketContext marketContext,
            SARIParameters parameters = null,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Calculating SARI for portfolio with {portfolio.Holdings.Count} positions in {marketContext.MarketRegime} regime");

            try
            {
                parameters ??= new SARIParameters();
                var result = new SARIResult
                {
                    Timestamp = DateTime.UtcNow,
                    MarketRegime = marketContext.MarketRegime,
                    ScenarioResults = new List<ScenarioResult>(),
                    TimeHorizonResults = new Dictionary<TimeHorizon, float>()
                };

                // Get applicable scenarios for current regime
                var scenariosResult = _scenarioLibrary.GetScenariosForRegime(marketContext.MarketRegime);
                if (!scenariosResult.IsSuccess)
                {
                    return TradingResult<SARIResult>.Failure(scenariosResult.ErrorMessage);
                }

                var scenarios = scenariosResult.Data;
                LogDebug($"Processing {scenarios.Count} scenarios for {marketContext.MarketRegime} regime");

                // Update scenario weights based on market conditions
                await UpdateScenarioWeightsAsync(scenarios, marketContext, parameters, cancellationToken);

                // Process each scenario
                var scenarioTasks = scenarios.Select(async scenario =>
                {
                    return await ProcessScenarioAsync(scenario, portfolio, marketContext, parameters, cancellationToken);
                });

                var scenarioResults = await Task.WhenAll(scenarioTasks);

                // Aggregate results
                foreach (var scenarioResult in scenarioResults.Where(r => r != null))
                {
                    result.ScenarioResults.Add(scenarioResult);
                }

                // Calculate SARI index
                CalculateSARIIndex(result, parameters);

                // Calculate multi-horizon SARI
                if (parameters.CalculateMultiHorizon)
                {
                    await CalculateMultiHorizonSARIAsync(result, portfolio, marketContext, parameters, cancellationToken);
                }

                // Determine risk level and recommendations
                DetermineRiskLevel(result, parameters);
                GenerateRecommendations(result, portfolio, marketContext);

                LogInfo($"SARI calculation completed. Index: {result.SARIIndex:F4}, Risk Level: {result.RiskLevel}");
                LogMethodExit();
                return TradingResult<SARIResult>.Success(result);
            }
            catch (Exception ex)
            {
                LogError("Error calculating SARI", ex);
                return TradingResult<SARIResult>.Failure($"SARI calculation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update scenario weights dynamically based on market conditions
        /// </summary>
        public async Task<TradingResult> UpdateScenarioWeightsAsync(
            List<StressScenario> scenarios,
            MarketContext marketContext,
            SARIParameters parameters,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogDebug("Updating scenario weights based on market conditions");

            try
            {
                foreach (var scenario in scenarios)
                {
                    float baseWeight = GetBaseWeight(scenario);
                    
                    // Apply regime multiplier
                    float regimeMultiplier = GetRegimeMultiplier(scenario, marketContext.MarketRegime);
                    
                    // Apply recency factor (recent similar events increase probability)
                    float recencyFactor = await CalculateRecencyFactorAsync(scenario, cancellationToken);
                    
                    // Apply market signal adjustments
                    float signalAdjustment = CalculateSignalAdjustment(scenario, marketContext);
                    
                    // Calculate final weight
                    float finalWeight = baseWeight * regimeMultiplier * recencyFactor * signalAdjustment;
                    
                    lock (_weightsLock)
                    {
                        _scenarioWeights[scenario.Id] = finalWeight;
                    }
                    
                    LogDebug($"Scenario {scenario.Id} weight: {baseWeight:F4} -> {finalWeight:F4} " +
                            $"(regime: {regimeMultiplier:F2}, recency: {recencyFactor:F2}, signal: {signalAdjustment:F2})");
                }

                // Normalize weights to sum to 1
                NormalizeWeights();

                LogMethodExit();
                return TradingResult.Success();
            }
            catch (Exception ex)
            {
                LogError("Error updating scenario weights", ex);
                return TradingResult.Failure($"Failed to update weights: {ex.Message}");
            }
        }

        private async Task<ScenarioResult> ProcessScenarioAsync(
            StressScenario scenario,
            Portfolio portfolio,
            MarketContext marketContext,
            SARIParameters parameters,
            CancellationToken cancellationToken)
        {
            LogDebug($"Processing scenario: {scenario.Name}");

            try
            {
                // Get propagation parameters
                var propagationParams = CreatePropagationParameters(scenario, parameters);

                // Propagate stress through portfolio
                var propagationResult = await _propagationEngine.PropagateStressAsync(
                    scenario,
                    portfolio,
                    propagationParams,
                    cancellationToken);

                if (!propagationResult.IsSuccess)
                {
                    LogWarning($"Failed to propagate scenario {scenario.Id}: {propagationResult.ErrorMessage}");
                    return null;
                }

                // Get scenario weight
                float weight = 0;
                lock (_weightsLock)
                {
                    weight = _scenarioWeights.GetValueOrDefault(scenario.Id, scenario.Probability);
                }

                // Calculate time to recovery
                float timeToRecovery = EstimateTimeToRecovery(scenario, propagationResult.Data);

                // Create scenario result
                var result = new ScenarioResult
                {
                    ScenarioId = scenario.Id,
                    ScenarioName = scenario.Name,
                    Probability = scenario.Probability,
                    Weight = weight,
                    StressLoss = propagationResult.Data.TotalPortfolioImpact,
                    TimeToRecovery = timeToRecovery,
                    ImpactBreakdown = propagationResult.Data.ImpactBreakdown,
                    WeightedImpact = weight * propagationResult.Data.TotalPortfolioImpact
                };

                LogDebug($"Scenario {scenario.Id} result: Loss={result.StressLoss:F4}, " +
                        $"Weight={weight:F4}, WeightedImpact={result.WeightedImpact:F4}");

                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error processing scenario {scenario.Id}", ex);
                return null;
            }
        }

        private void CalculateSARIIndex(SARIResult result, SARIParameters parameters)
        {
            LogDebug("Calculating SARI index");

            // Base SARI: weighted sum of scenario impacts
            float baseSARI = result.ScenarioResults.Sum(s => s.WeightedImpact);

            // Apply liquidity adjustment
            float liquidityMultiplier = CalculateLiquidityMultiplier(result, parameters);
            
            // Apply time horizon adjustment
            float timeHorizonMultiplier = CalculateTimeHorizonMultiplier(result, parameters);

            // Apply tail risk adjustment
            float tailRiskAdjustment = CalculateTailRiskAdjustment(result, parameters);

            // Final SARI calculation
            result.SARIIndex = baseSARI * liquidityMultiplier * timeHorizonMultiplier + tailRiskAdjustment;
            
            // Calculate component contributions
            result.ComponentContributions = new Dictionary<string, float>
            {
                ["BaseStress"] = baseSARI,
                ["Liquidity"] = baseSARI * (liquidityMultiplier - 1),
                ["TimeHorizon"] = baseSARI * liquidityMultiplier * (timeHorizonMultiplier - 1),
                ["TailRisk"] = tailRiskAdjustment
            };

            LogInfo($"SARI components: Base={baseSARI:F4}, Liquidity={liquidityMultiplier:F2}, " +
                   $"TimeHorizon={timeHorizonMultiplier:F2}, TailRisk={tailRiskAdjustment:F4}");
        }

        private async Task CalculateMultiHorizonSARIAsync(
            SARIResult result,
            Portfolio portfolio,
            MarketContext marketContext,
            SARIParameters parameters,
            CancellationToken cancellationToken)
        {
            LogDebug("Calculating multi-horizon SARI");

            var horizons = new[] 
            { 
                TimeHorizon.OneDay, 
                TimeHorizon.OneWeek, 
                TimeHorizon.OneMonth, 
                TimeHorizon.ThreeMonth 
            };

            foreach (var horizon in horizons)
            {
                float horizonSARI = 0;
                
                // Filter scenarios by time horizon
                var horizonScenarios = result.ScenarioResults
                    .Where(s => GetScenarioHorizon(s.ScenarioId) <= horizon)
                    .ToList();

                if (horizonScenarios.Any())
                {
                    // Apply horizon-specific decay factors
                    float decayFactor = GetHorizonDecayFactor(horizon);
                    horizonSARI = horizonScenarios.Sum(s => s.WeightedImpact * decayFactor);
                }

                result.TimeHorizonResults[horizon] = horizonSARI;
                LogDebug($"SARI for {horizon}: {horizonSARI:F4}");
            }
        }

        private void DetermineRiskLevel(SARIResult result, SARIParameters parameters)
        {
            result.RiskLevel = result.SARIIndex switch
            {
                < 0.10f => RiskLevel.Low,
                < 0.20f => RiskLevel.Medium,
                < 0.35f => RiskLevel.High,
                < 0.50f => RiskLevel.VeryHigh,
                _ => RiskLevel.Critical
            };

            // Additional checks for critical scenarios
            if (result.ScenarioResults.Any(s => s.StressLoss > parameters.CriticalLossThreshold))
            {
                result.RiskLevel = RiskLevel.Critical;
                LogWarning($"Critical risk level due to scenario exceeding {parameters.CriticalLossThreshold:P} loss threshold");
            }
        }

        private void GenerateRecommendations(SARIResult result, Portfolio portfolio, MarketContext marketContext)
        {
            result.Recommendations = new List<SARIRecommendation>();

            // Risk level based recommendations
            switch (result.RiskLevel)
            {
                case RiskLevel.Critical:
                    result.Recommendations.Add(new SARIRecommendation
                    {
                        Priority = RecommendationPriority.Urgent,
                        Action = "Immediate de-risking required",
                        Details = "Reduce portfolio exposure by 50% or implement protective hedges"
                    });
                    break;

                case RiskLevel.VeryHigh:
                    result.Recommendations.Add(new SARIRecommendation
                    {
                        Priority = RecommendationPriority.High,
                        Action = "Significant risk reduction needed",
                        Details = "Consider reducing exposure by 25-30% and increasing hedges"
                    });
                    break;

                case RiskLevel.High:
                    result.Recommendations.Add(new SARIRecommendation
                    {
                        Priority = RecommendationPriority.Medium,
                        Action = "Review and adjust portfolio risk",
                        Details = "Consider selective position reduction and hedge optimization"
                    });
                    break;
            }

            // Scenario-specific recommendations
            var topScenarios = result.ScenarioResults
                .OrderByDescending(s => s.WeightedImpact)
                .Take(3);

            foreach (var scenario in topScenarios)
            {
                result.Recommendations.Add(GenerateScenarioRecommendation(scenario));
            }

            // Liquidity recommendations
            if (result.ComponentContributions["Liquidity"] > 0.05f)
            {
                result.Recommendations.Add(new SARIRecommendation
                {
                    Priority = RecommendationPriority.High,
                    Action = "Improve portfolio liquidity",
                    Details = "Shift allocation toward more liquid assets"
                });
            }
        }

        private SARIRecommendation GenerateScenarioRecommendation(ScenarioResult scenario)
        {
            var recommendation = new SARIRecommendation
            {
                Priority = scenario.WeightedImpact > 0.1f ? RecommendationPriority.High : RecommendationPriority.Medium,
                Action = $"Hedge against {scenario.ScenarioName}",
                Details = GetScenarioHedgeStrategy(scenario.ScenarioId)
            };

            return recommendation;
        }

        private string GetScenarioHedgeStrategy(string scenarioId)
        {
            return scenarioId switch
            {
                "2008_FINANCIAL_CRISIS" => "Increase cash allocation, buy put options on equity indices",
                "TECH_BUBBLE_2" => "Reduce tech exposure, rotate to defensive sectors",
                "RATE_SHOCK" => "Shorten duration, consider floating rate instruments",
                "GEOPOLITICAL_CRISIS" => "Increase gold allocation, buy oil futures",
                "CYBER_ATTACK" => "Increase cyber insurance, reduce financial sector exposure",
                _ => "Review scenario-specific exposures and implement appropriate hedges"
            };
        }

        private void InitializeDefaultWeights()
        {
            // Initialize with equal weights that will be adjusted dynamically
            lock (_weightsLock)
            {
                _scenarioWeights["2008_FINANCIAL_CRISIS"] = 0.15f;
                _scenarioWeights["2020_COVID_CRASH"] = 0.10f;
                _scenarioWeights["TECH_BUBBLE_2"] = 0.15f;
                _scenarioWeights["RATE_SHOCK"] = 0.20f;
                _scenarioWeights["CHINA_HARD_LANDING"] = 0.10f;
                _scenarioWeights["CYBER_ATTACK"] = 0.10f;
                _scenarioWeights["GEOPOLITICAL_CRISIS"] = 0.10f;
                _scenarioWeights["1987_BLACK_MONDAY"] = 0.05f;
                _scenarioWeights["2018_VOLMAGEDDON"] = 0.05f;
            }
        }

        private float GetBaseWeight(StressScenario scenario)
        {
            // Combine probability and severity for base weight
            float severityWeight = scenario.Severity switch
            {
                StressSeverity.Mild => 0.25f,
                StressSeverity.Moderate => 0.50f,
                StressSeverity.Severe => 0.75f,
                StressSeverity.Extreme => 1.00f,
                _ => 0.5f
            };

            return scenario.Probability * severityWeight;
        }

        private float GetRegimeMultiplier(StressScenario scenario, MarketRegime regime)
        {
            // Adjust scenario weights based on market regime
            if (regime == MarketRegime.Crisis)
            {
                return scenario.Severity == StressSeverity.Extreme ? 2.0f : 1.5f;
            }
            else if (regime == MarketRegime.Volatile)
            {
                return 1.3f;
            }
            else if (regime == MarketRegime.Stable)
            {
                return 0.7f;
            }

            return 1.0f;
        }

        private async Task<float> CalculateRecencyFactorAsync(StressScenario scenario, CancellationToken cancellationToken)
        {
            // Check if similar events occurred recently
            // In practice, would check historical data
            await Task.Delay(1, cancellationToken); // Simulate async work

            // Default recency factor
            return 1.0f;
        }

        private float CalculateSignalAdjustment(StressScenario scenario, MarketContext marketContext)
        {
            float adjustment = 1.0f;

            // Volatility signal
            if (marketContext.MarketVolatility > 0.30f && scenario.Category == ScenarioCategory.Historical)
            {
                adjustment *= 1.5f;
            }

            // Rate environment signal
            if (scenario.Id == "RATE_SHOCK" && marketContext.EconomicIndicators?.GetValueOrDefault("10YearYield", 0) > 4.0f)
            {
                adjustment *= 1.3f;
            }

            // Tech valuation signal
            if (scenario.Id == "TECH_BUBBLE_2" && marketContext.EconomicIndicators?.GetValueOrDefault("NasdaqPE", 0) > 30f)
            {
                adjustment *= 1.4f;
            }

            return adjustment;
        }

        private void NormalizeWeights()
        {
            lock (_weightsLock)
            {
                float sum = _scenarioWeights.Values.Sum();
                if (sum > 0)
                {
                    var keys = _scenarioWeights.Keys.ToList();
                    foreach (var key in keys)
                    {
                        _scenarioWeights[key] /= sum;
                    }
                }
            }
        }

        private PropagationParameters CreatePropagationParameters(StressScenario scenario, SARIParameters parameters)
        {
            return new PropagationParameters
            {
                ContagionThreshold = parameters.ContagionThreshold,
                ContagionDecay = parameters.ContagionDecay,
                ModelMarginCalls = parameters.ModelMarginCalls,
                MarginCallThreshold = parameters.MarginCallThreshold,
                ModelRiskLimitBreaches = parameters.ModelRiskLimitBreaches,
                ModelVolatilityTargeting = parameters.ModelVolatilityTargeting,
                MaxLiquiditySpiralRounds = parameters.MaxLiquiditySpiralRounds
            };
        }

        private float EstimateTimeToRecovery(StressScenario scenario, StressPropagationResult propagation)
        {
            // Estimate based on scenario severity and historical patterns
            float baseDays = scenario.Severity switch
            {
                StressSeverity.Mild => 30,
                StressSeverity.Moderate => 90,
                StressSeverity.Severe => 180,
                StressSeverity.Extreme => 365,
                _ => 60
            };

            // Adjust for liquidity impact
            if (propagation.LiquiditySpiral != null)
            {
                baseDays *= (1 + propagation.LiquiditySpiral.TotalImpact);
            }

            return baseDays;
        }

        private float CalculateLiquidityMultiplier(SARIResult result, SARIParameters parameters)
        {
            // Average liquidity impact across scenarios
            float avgLiquidityImpact = result.ScenarioResults
                .Where(s => s.ImpactBreakdown.ContainsKey("Liquidity"))
                .Select(s => s.ImpactBreakdown["Liquidity"])
                .DefaultIfEmpty(0)
                .Average();

            return 1.0f + (avgLiquidityImpact * parameters.LiquiditySensitivity);
        }

        private float CalculateTimeHorizonMultiplier(SARIResult result, SARIParameters parameters)
        {
            // Weight longer-term scenarios more heavily
            float longTermWeight = result.ScenarioResults
                .Where(s => GetScenarioHorizon(s.ScenarioId) >= TimeHorizon.ThreeMonth)
                .Sum(s => s.Weight);

            return 1.0f + (longTermWeight * parameters.TimeHorizonSensitivity);
        }

        private float CalculateTailRiskAdjustment(SARIResult result, SARIParameters parameters)
        {
            // Add extra penalty for extreme tail events
            var tailScenarios = result.ScenarioResults
                .Where(s => s.StressLoss > parameters.TailRiskThreshold)
                .ToList();

            if (!tailScenarios.Any()) return 0;

            float tailRisk = tailScenarios.Sum(s => (s.StressLoss - parameters.TailRiskThreshold) * s.Weight);
            return tailRisk * parameters.TailRiskMultiplier;
        }

        private TimeHorizon GetScenarioHorizon(string scenarioId)
        {
            // Map scenarios to their typical time horizons
            return scenarioId switch
            {
                "1987_BLACK_MONDAY" => TimeHorizon.OneDay,
                "2018_VOLMAGEDDON" => TimeHorizon.OneDay,
                "CYBER_ATTACK" => TimeHorizon.OneWeek,
                "RATE_SHOCK" => TimeHorizon.OneMonth,
                "2020_COVID_CRASH" => TimeHorizon.OneMonth,
                "TECH_BUBBLE_2" => TimeHorizon.ThreeMonth,
                "2008_FINANCIAL_CRISIS" => TimeHorizon.ThreeMonth,
                "CHINA_HARD_LANDING" => TimeHorizon.SixMonth,
                _ => TimeHorizon.OneMonth
            };
        }

        private float GetHorizonDecayFactor(TimeHorizon horizon)
        {
            return horizon switch
            {
                TimeHorizon.OneDay => 1.0f,
                TimeHorizon.OneWeek => 0.9f,
                TimeHorizon.OneMonth => 0.8f,
                TimeHorizon.ThreeMonth => 0.7f,
                TimeHorizon.SixMonth => 0.6f,
                _ => 0.5f
            };
        }
    }

    // Supporting classes
    public class SARIResult
    {
        public DateTime Timestamp { get; set; }
        public float SARIIndex { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public MarketRegime MarketRegime { get; set; }
        public List<ScenarioResult> ScenarioResults { get; set; }
        public Dictionary<TimeHorizon, float> TimeHorizonResults { get; set; }
        public Dictionary<string, float> ComponentContributions { get; set; }
        public List<SARIRecommendation> Recommendations { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioId { get; set; }
        public string ScenarioName { get; set; }
        public float Probability { get; set; }
        public float Weight { get; set; }
        public float StressLoss { get; set; }
        public float TimeToRecovery { get; set; }
        public Dictionary<string, float> ImpactBreakdown { get; set; }
        public float WeightedImpact { get; set; }
    }

    public class SARIParameters
    {
        // Core parameters
        public float ContagionThreshold { get; set; } = 0.1f;
        public float ContagionDecay { get; set; } = 0.5f;
        
        // Feedback modeling
        public bool ModelMarginCalls { get; set; } = true;
        public float MarginCallThreshold { get; set; } = 0.15f;
        public bool ModelRiskLimitBreaches { get; set; } = true;
        public bool ModelVolatilityTargeting { get; set; } = true;
        
        // Liquidity parameters
        public int MaxLiquiditySpiralRounds { get; set; } = 5;
        public float LiquiditySensitivity { get; set; } = 0.5f;
        
        // Risk thresholds
        public float CriticalLossThreshold { get; set; } = 0.25f;
        public float TailRiskThreshold { get; set; } = 0.20f;
        public float TailRiskMultiplier { get; set; } = 2.0f;
        
        // Time horizon
        public bool CalculateMultiHorizon { get; set; } = true;
        public float TimeHorizonSensitivity { get; set; } = 0.3f;
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        VeryHigh,
        Critical
    }

    public class SARIRecommendation
    {
        public RecommendationPriority Priority { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }

    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
}
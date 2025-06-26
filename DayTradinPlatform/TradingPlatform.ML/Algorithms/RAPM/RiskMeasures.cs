using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Core.Common;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Canonical;
using TradingPlatform.ML.Common;

namespace TradingPlatform.ML.Algorithms.RAPM
{
    public class RiskMeasures : CanonicalServiceBase
    {
        public RiskMeasures(ICanonicalLogger logger) : base(logger)
        {
        }

        protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
        {
            LogMethodEntry();
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

        public TradingResult<float> CalculateVaR(
            float[] returns,
            float confidenceLevel = 0.95f,
            VaRMethod method = VaRMethod.Historical)
        {
            LogMethodEntry();
            LogDebug($"Calculating VaR with method: {method}, confidence: {confidenceLevel}, returns count: {returns?.Length ?? 0}");
            
            try
            {
                if (returns == null || returns.Length == 0)
                {
                    LogWarning("Returns array is null or empty");
                    return TradingResult<float>.Failure("Returns array is null or empty");
                }

                if (confidenceLevel <= 0 || confidenceLevel >= 1)
                {
                    LogWarning($"Invalid confidence level: {confidenceLevel}");
                    return TradingResult<float>.Failure("Confidence level must be between 0 and 1");
                }

                LogDebug($"Starting VaR calculation with {returns.Length} data points");
                
                float var = method switch
                {
                    VaRMethod.Historical => CalculateHistoricalVaR(returns, confidenceLevel),
                    VaRMethod.Parametric => CalculateParametricVaR(returns, confidenceLevel),
                    VaRMethod.MonteCarlo => CalculateMonteCarloVaR(returns, confidenceLevel),
                    _ => throw new ArgumentException($"Unknown VaR method: {method}")
                };

                LogInfo($"VaR calculated successfully: {var:F4} at {confidenceLevel:P0} confidence using {method} method");
                LogMethodExit();
                return TradingResult<float>.Success(var);
            }
            catch (Exception ex)
            {
                LogError("Error calculating VaR", ex);
                return TradingResult<float>.Failure($"Failed to calculate VaR: {ex.Message}");
            }
        }

        public TradingResult<float> CalculateCVaR(
            float[] returns,
            float confidenceLevel = 0.95f,
            VaRMethod method = VaRMethod.Historical)
        {
            LogMethodEntry();
            
            try
            {
                var varResult = CalculateVaR(returns, confidenceLevel, method);
                if (!varResult.IsSuccess)
                {
                    return TradingResult<float>.Failure(varResult.ErrorMessage);
                }

                float var = varResult.Data;
                float cvar = method switch
                {
                    VaRMethod.Historical => CalculateHistoricalCVaR(returns, var),
                    VaRMethod.Parametric => CalculateParametricCVaR(returns, var, confidenceLevel),
                    VaRMethod.MonteCarlo => CalculateMonteCarloVaR(returns, confidenceLevel) * 1.2f, // Approximation
                    _ => throw new ArgumentException($"Unknown CVaR method: {method}")
                };

                LogMethodExit();
                return TradingResult<float>.Success(cvar);
            }
            catch (Exception ex)
            {
                LogError("Error calculating CVaR", ex);
                return TradingResult<float>.Failure($"Failed to calculate CVaR: {ex.Message}");
            }
        }

        public TradingResult<float> CalculateMaxDrawdown(float[] prices)
        {
            LogMethodEntry();
            
            try
            {
                if (prices == null || prices.Length < 2)
                {
                    return TradingResult<float>.Failure("Insufficient price data");
                }

                float maxDrawdown = 0;
                float peak = prices[0];

                for (int i = 1; i < prices.Length; i++)
                {
                    if (prices[i] > peak)
                    {
                        peak = prices[i];
                    }
                    else
                    {
                        float drawdown = (peak - prices[i]) / peak;
                        if (drawdown > maxDrawdown)
                        {
                            maxDrawdown = drawdown;
                        }
                    }
                }

                LogMethodExit();
                return TradingResult<float>.Success(maxDrawdown);
            }
            catch (Exception ex)
            {
                LogError("Error calculating max drawdown", ex);
                return TradingResult<float>.Failure($"Failed to calculate max drawdown: {ex.Message}");
            }
        }

        public TradingResult<float> CalculateVolatilityAdjustedRisk(
            float[] returns,
            float skewPenalty = 0.5f,
            float kurtosisPenalty = 0.25f)
        {
            LogMethodEntry();
            
            try
            {
                if (returns == null || returns.Length < 4)
                {
                    return TradingResult<float>.Failure("Insufficient return data");
                }

                // Calculate moments
                float mean = returns.Average();
                float variance = returns.Select(r => (r - mean) * (r - mean)).Average();
                float stdDev = (float)Math.Sqrt(variance);

                // Standardized moments
                float[] standardized = returns.Select(r => (r - mean) / stdDev).ToArray();
                float skewness = standardized.Select(z => z * z * z).Average();
                float kurtosis = standardized.Select(z => z * z * z * z).Average();

                // Adjusted volatility
                float adjustment = 1 + skewPenalty * skewness * skewness + 
                                  kurtosisPenalty * Math.Max(0, kurtosis - 3);
                float adjustedVol = stdDev * (float)Math.Sqrt(adjustment);

                LogMethodExit();
                return TradingResult<float>.Success(adjustedVol);
            }
            catch (Exception ex)
            {
                LogError("Error calculating volatility-adjusted risk", ex);
                return TradingResult<float>.Failure($"Failed to calculate adjusted risk: {ex.Message}");
            }
        }

        public TradingResult<float> CalculateCompositeRisk(
            RiskComponents components,
            RiskWeights weights)
        {
            LogMethodEntry();
            
            try
            {
                // Validate weights sum to 1
                float weightSum = weights.CVaRWeight + weights.VolatilityWeight + 
                                 weights.DrawdownWeight + weights.ConcentrationWeight;
                
                if (Math.Abs(weightSum - 1.0f) > 0.001f)
                {
                    return TradingResult<float>.Failure($"Risk weights must sum to 1, got {weightSum}");
                }

                float compositeRisk = 
                    weights.CVaRWeight * components.CVaR +
                    weights.VolatilityWeight * components.Volatility +
                    weights.DrawdownWeight * components.MaxDrawdown +
                    weights.ConcentrationWeight * components.ConcentrationRisk;

                LogMethodExit();
                return TradingResult<float>.Success(compositeRisk);
            }
            catch (Exception ex)
            {
                LogError("Error calculating composite risk", ex);
                return TradingResult<float>.Failure($"Failed to calculate composite risk: {ex.Message}");
            }
        }

        public TradingResult<float> CalculateConcentrationRisk(float[] weights)
        {
            LogMethodEntry();
            
            try
            {
                if (weights == null || weights.Length == 0)
                {
                    return TradingResult<float>.Failure("Weights array is null or empty");
                }

                // Herfindahl-Hirschman Index
                float hhi = weights.Sum(w => w * w);
                
                // Normalize to [0, 1] range
                float minHHI = 1.0f / weights.Length;
                float maxHHI = 1.0f;
                float normalizedHHI = (hhi - minHHI) / (maxHHI - minHHI);

                LogMethodExit();
                return TradingResult<float>.Success(normalizedHHI);
            }
            catch (Exception ex)
            {
                LogError("Error calculating concentration risk", ex);
                return TradingResult<float>.Failure($"Failed to calculate concentration risk: {ex.Message}");
            }
        }

        public async Task<TradingResult<StressTestResult>> RunStressTestAsync(
            float[] portfolioWeights,
            float[,] assetReturns,
            List<StressScenario> scenarios,
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            LogInfo($"Running stress test with {scenarios.Count} scenarios, {portfolioWeights.Length} assets");
            
            try
            {
                var results = new StressTestResult
                {
                    ScenarioResults = new List<ScenarioResult>(),
                    WorstCaseLoss = float.MinValue,
                    AverageStressLoss = 0
                };
                
                LogDebug($"Portfolio weights: [{string.Join(", ", portfolioWeights.Select(w => w.ToString("F4")))}]");

                foreach (var scenario in scenarios)
                {
                    LogDebug($"Evaluating scenario: {scenario.Name}");
                    
                    var scenarioResult = await EvaluateScenarioAsync(
                        portfolioWeights,
                        assetReturns,
                        scenario,
                        cancellationToken);

                    results.ScenarioResults.Add(scenarioResult);
                    
                    LogDebug($"Scenario '{scenario.Name}' result: Loss = {scenarioResult.PortfolioLoss:F4}");
                    
                    if (scenarioResult.PortfolioLoss > results.WorstCaseLoss)
                    {
                        results.WorstCaseLoss = scenarioResult.PortfolioLoss;
                        results.WorstScenario = scenario.Name;
                        LogInfo($"New worst-case scenario: {scenario.Name} with loss {scenarioResult.PortfolioLoss:F4}");
                    }
                }

                results.AverageStressLoss = results.ScenarioResults.Average(r => r.PortfolioLoss);
                
                LogInfo($"Stress test completed. Worst case: {results.WorstScenario} ({results.WorstCaseLoss:F4}), Average loss: {results.AverageStressLoss:F4}");
                LogMethodExit();
                return TradingResult<StressTestResult>.Success(results);
            }
            catch (Exception ex)
            {
                LogError("Error running stress test", ex);
                return TradingResult<StressTestResult>.Failure($"Failed to run stress test: {ex.Message}");
            }
        }

        private float CalculateHistoricalVaR(float[] returns, float confidenceLevel)
        {
            LogDebug($"Calculating Historical VaR with {returns.Length} returns");
            
            var sortedReturns = returns.OrderBy(r => r).ToArray();
            int index = (int)Math.Floor((1 - confidenceLevel) * returns.Length);
            
            LogDebug($"VaR index: {index}, sorted returns range: [{sortedReturns.First():F4}, {sortedReturns.Last():F4}]");
            
            float var = -sortedReturns[index];
            LogDebug($"Historical VaR result: {var:F4}");
            
            return var;
        }

        private float CalculateParametricVaR(float[] returns, float confidenceLevel)
        {
            float mean = returns.Average();
            float stdDev = CalculateStandardDeviation(returns);
            
            // Z-score for confidence level (assuming normal distribution)
            float zScore = GetZScore(confidenceLevel);
            
            return -(mean - zScore * stdDev);
        }

        private float CalculateMonteCarloVaR(float[] returns, float confidenceLevel, int simulations = 10000)
        {
            float mean = returns.Average();
            float stdDev = CalculateStandardDeviation(returns);
            
            var random = new Random();
            var simulatedReturns = new float[simulations];
            
            for (int i = 0; i < simulations; i++)
            {
                // Box-Muller transform for normal distribution
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                
                simulatedReturns[i] = mean + stdDev * (float)normal;
            }
            
            return CalculateHistoricalVaR(simulatedReturns, confidenceLevel);
        }

        private float CalculateHistoricalCVaR(float[] returns, float var)
        {
            var lossesBeyo ndVaR = returns.Where(r => r < -var).ToArray();
            return lossesBeyo ndVaR.Length > 0 ? -lossesBeyo ndVaR.Average() : var;
        }

        private float CalculateParametricCVaR(float[] returns, float var, float confidenceLevel)
        {
            float mean = returns.Average();
            float stdDev = CalculateStandardDeviation(returns);
            float zScore = GetZScore(confidenceLevel);
            
            // For normal distribution, CVaR has closed-form solution
            float phi = (float)(Math.Exp(-zScore * zScore / 2) / Math.Sqrt(2 * Math.PI));
            float cvar = mean + stdDev * phi / (1 - confidenceLevel);
            
            return -cvar;
        }

        private float CalculateStandardDeviation(float[] values)
        {
            float mean = values.Average();
            float variance = values.Select(v => (v - mean) * (v - mean)).Average();
            return (float)Math.Sqrt(variance);
        }

        private float GetZScore(float confidenceLevel)
        {
            // Approximate inverse normal CDF using Beasley-Springer-Moro algorithm
            float a0 = 2.50662823884f;
            float a1 = -18.61500062529f;
            float a2 = 41.39119773534f;
            float a3 = -25.44106049637f;
            
            float b0 = -8.47351093090f;
            float b1 = 23.08336743743f;
            float b2 = -21.06224101826f;
            float b3 = 3.13082909833f;
            
            float c0 = 0.3374754822726147f;
            float c1 = 0.9761690190917186f;
            float c2 = 0.1607979714918209f;
            float c3 = 0.0276438810333863f;
            float c4 = 0.0038405729373609f;
            float c5 = 0.0003951896511919f;
            float c6 = 0.0000321767881768f;
            float c7 = 0.0000002888167364f;
            float c8 = 0.0000003960315187f;
            
            float y = confidenceLevel - 0.5f;
            
            if (Math.Abs(y) < 0.42f)
            {
                float r = y * y;
                return y * (((a3 * r + a2) * r + a1) * r + a0) / ((((b3 * r + b2) * r + b1) * r + b0) * r + 1);
            }
            else
            {
                float r = confidenceLevel > 0.5f ? 1 - confidenceLevel : confidenceLevel;
                r = (float)Math.Log(-Math.Log(r));
                float x = c0 + r * (c1 + r * (c2 + r * (c3 + r * (c4 + r * (c5 + r * (c6 + r * (c7 + r * c8)))))));
                
                return confidenceLevel > 0.5f ? x : -x;
            }
        }

        private async Task<ScenarioResult> EvaluateScenarioAsync(
            float[] portfolioWeights,
            float[,] assetReturns,
            StressScenario scenario,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var result = new ScenarioResult
                {
                    ScenarioName = scenario.Name,
                    AssetLosses = new float[portfolioWeights.Length]
                };

                // Apply scenario shocks
                for (int i = 0; i < portfolioWeights.Length; i++)
                {
                    float shock = scenario.AssetShocks?.Length > i ? scenario.AssetShocks[i] : scenario.MarketShock;
                    result.AssetLosses[i] = shock;
                }

                // Calculate portfolio loss
                result.PortfolioLoss = 0;
                for (int i = 0; i < portfolioWeights.Length; i++)
                {
                    result.PortfolioLoss += portfolioWeights[i] * result.AssetLosses[i];
                }

                // Calculate risk metrics under stress
                var stressedReturns = ApplyStressToReturns(assetReturns, scenario);
                var varResult = CalculateVaR(stressedReturns, 0.95f);
                result.StressedVaR = varResult.IsSuccess ? varResult.Data : 0;

                var cvarResult = CalculateCVaR(stressedReturns, 0.95f);
                result.StressedCVaR = cvarResult.IsSuccess ? cvarResult.Data : 0;

                return result;
            }, cancellationToken);
        }

        private float[] ApplyStressToReturns(float[,] assetReturns, StressScenario scenario)
        {
            int periods = assetReturns.GetLength(0);
            int assets = assetReturns.GetLength(1);
            var stressedReturns = new float[periods];

            for (int t = 0; t < periods; t++)
            {
                stressedReturns[t] = 0;
                for (int i = 0; i < assets; i++)
                {
                    float shock = scenario.AssetShocks?.Length > i ? scenario.AssetShocks[i] : scenario.MarketShock;
                    float stressMultiplier = 1 + shock;
                    stressedReturns[t] += assetReturns[t, i] * stressMultiplier;
                }
                stressedReturns[t] /= assets;
            }

            return stressedReturns;
        }
    }

    public enum VaRMethod
    {
        Historical,
        Parametric,
        MonteCarlo
    }

    public class RiskComponents
    {
        public float CVaR { get; set; }
        public float Volatility { get; set; }
        public float MaxDrawdown { get; set; }
        public float ConcentrationRisk { get; set; }
    }

    public class RiskWeights
    {
        public float CVaRWeight { get; set; } = 0.4f;
        public float VolatilityWeight { get; set; } = 0.3f;
        public float DrawdownWeight { get; set; } = 0.2f;
        public float ConcentrationWeight { get; set; } = 0.1f;
    }

    public class StressScenario
    {
        public string Name { get; set; }
        public float MarketShock { get; set; }
        public float[] AssetShocks { get; set; }
        public float Probability { get; set; }
        public string Description { get; set; }
    }

    public class StressTestResult
    {
        public List<ScenarioResult> ScenarioResults { get; set; }
        public float WorstCaseLoss { get; set; }
        public string WorstScenario { get; set; }
        public float AverageStressLoss { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioName { get; set; }
        public float PortfolioLoss { get; set; }
        public float[] AssetLosses { get; set; }
        public float StressedVaR { get; set; }
        public float StressedCVaR { get; set; }
    }
}
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

        public TradingResult<decimal> CalculateVaR(
            decimal[] returns,
            decimal confidenceLevel = 0.95m,
            VaRMethod method = VaRMethod.Historical)
        {
            LogMethodEntry();
            LogDebug($"Calculating VaR with method: {method}, confidence: {confidenceLevel}, returns count: {returns?.Length ?? 0}");
            
            try
            {
                if (returns == null || returns.Length == 0)
                {
                    LogWarning("Returns array is null or empty");
                    return TradingResult<decimal>.Failure("Returns array is null or empty");
                }

                if (confidenceLevel <= 0 || confidenceLevel >= 1)
                {
                    LogWarning($"Invalid confidence level: {confidenceLevel}");
                    return TradingResult<decimal>.Failure("Confidence level must be between 0 and 1");
                }

                LogDebug($"Starting VaR calculation with {returns.Length} data points");
                
                decimal var = method switch
                {
                    VaRMethod.Historical => CalculateHistoricalVaR(returns, confidenceLevel),
                    VaRMethod.Parametric => CalculateParametricVaR(returns, confidenceLevel),
                    VaRMethod.MonteCarlo => CalculateMonteCarloVaR(returns, confidenceLevel),
                    _ => throw new ArgumentException($"Unknown VaR method: {method}")
                };

                LogInfo($"VaR calculated successfully: {var:F4} at {confidenceLevel:P0} confidence using {method} method");
                LogMethodExit();
                return TradingResult<decimal>.Success(var);
            }
            catch (Exception ex)
            {
                LogError("Error calculating VaR", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate VaR: {ex.Message}");
            }
        }

        public TradingResult<decimal> CalculateCVaR(
            decimal[] returns,
            decimal confidenceLevel = 0.95m,
            VaRMethod method = VaRMethod.Historical)
        {
            LogMethodEntry();
            
            try
            {
                var varResult = CalculateVaR(returns, confidenceLevel, method);
                if (!varResult.IsSuccess)
                {
                    return TradingResult<decimal>.Failure(varResult.ErrorMessage);
                }

                decimal var = varResult.Data;
                decimal cvar = method switch
                {
                    VaRMethod.Historical => CalculateHistoricalCVaR(returns, var),
                    VaRMethod.Parametric => CalculateParametricCVaR(returns, var, confidenceLevel),
                    VaRMethod.MonteCarlo => CalculateMonteCarloVaR(returns, confidenceLevel) * 1.2m, // Approximation
                    _ => throw new ArgumentException($"Unknown CVaR method: {method}")
                };

                LogMethodExit();
                return TradingResult<decimal>.Success(cvar);
            }
            catch (Exception ex)
            {
                LogError("Error calculating CVaR", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate CVaR: {ex.Message}");
            }
        }

        public TradingResult<decimal> CalculateMaxDrawdown(decimal[] prices)
        {
            LogMethodEntry();
            
            try
            {
                if (prices == null || prices.Length < 2)
                {
                    return TradingResult<decimal>.Failure("Insufficient price data");
                }

                decimal maxDrawdown = 0m;
                decimal peak = prices[0];

                for (int i = 1; i < prices.Length; i++)
                {
                    if (prices[i] > peak)
                    {
                        peak = prices[i];
                    }
                    else
                    {
                        decimal drawdown = (peak - prices[i]) / peak;
                        if (drawdown > maxDrawdown)
                        {
                            maxDrawdown = drawdown;
                        }
                    }
                }

                LogMethodExit();
                return TradingResult<decimal>.Success(maxDrawdown);
            }
            catch (Exception ex)
            {
                LogError("Error calculating max drawdown", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate max drawdown: {ex.Message}");
            }
        }

        public TradingResult<decimal> CalculateVolatilityAdjustedRisk(
            decimal[] returns,
            decimal skewPenalty = 0.5m,
            decimal kurtosisPenalty = 0.25m)
        {
            LogMethodEntry();
            
            try
            {
                if (returns == null || returns.Length < 4)
                {
                    return TradingResult<decimal>.Failure("Insufficient return data");
                }

                // Calculate moments
                decimal mean = returns.Average();
                decimal variance = returns.Select(r => (r - mean) * (r - mean)).Average();
                decimal stdDev = DecimalMath.Sqrt(variance);

                // Standardized moments
                decimal[] standardized = returns.Select(r => (r - mean) / stdDev).ToArray();
                decimal skewness = standardized.Select(z => z * z * z).Average();
                decimal kurtosis = standardized.Select(z => z * z * z * z).Average();

                // Adjusted volatility
                decimal adjustment = 1m + skewPenalty * skewness * skewness + 
                                  kurtosisPenalty * Math.Max(0m, kurtosis - 3m);
                decimal adjustedVol = stdDev * DecimalMath.Sqrt(adjustment);

                LogMethodExit();
                return TradingResult<decimal>.Success(adjustedVol);
            }
            catch (Exception ex)
            {
                LogError("Error calculating volatility-adjusted risk", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate adjusted risk: {ex.Message}");
            }
        }

        public TradingResult<decimal> CalculateCompositeRisk(
            RiskComponents components,
            RiskWeights weights)
        {
            LogMethodEntry();
            
            try
            {
                // Validate weights sum to 1
                decimal weightSum = weights.CVaRWeight + weights.VolatilityWeight + 
                                 weights.DrawdownWeight + weights.ConcentrationWeight;
                
                if (Math.Abs(weightSum - 1.0m) > 0.001m)
                {
                    return TradingResult<decimal>.Failure($"Risk weights must sum to 1, got {weightSum}");
                }

                decimal compositeRisk = 
                    weights.CVaRWeight * components.CVaR +
                    weights.VolatilityWeight * components.Volatility +
                    weights.DrawdownWeight * components.MaxDrawdown +
                    weights.ConcentrationWeight * components.ConcentrationRisk;

                LogMethodExit();
                return TradingResult<decimal>.Success(compositeRisk);
            }
            catch (Exception ex)
            {
                LogError("Error calculating composite risk", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate composite risk: {ex.Message}");
            }
        }

        public TradingResult<decimal> CalculateConcentrationRisk(decimal[] weights)
        {
            LogMethodEntry();
            
            try
            {
                if (weights == null || weights.Length == 0)
                {
                    return TradingResult<decimal>.Failure("Weights array is null or empty");
                }

                // Herfindahl-Hirschman Index
                decimal hhi = weights.Sum(w => w * w);
                
                // Normalize to [0, 1] range
                decimal minHHI = 1.0m / weights.Length;
                decimal maxHHI = 1.0m;
                decimal normalizedHHI = (hhi - minHHI) / (maxHHI - minHHI);

                LogMethodExit();
                return TradingResult<decimal>.Success(normalizedHHI);
            }
            catch (Exception ex)
            {
                LogError("Error calculating concentration risk", ex);
                return TradingResult<decimal>.Failure($"Failed to calculate concentration risk: {ex.Message}");
            }
        }

        public async Task<TradingResult<StressTestResult>> RunStressTestAsync(
            decimal[] portfolioWeights,
            decimal[,] assetReturns,
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
                    WorstCaseLoss = decimal.MinValue,
                    AverageStressLoss = 0m
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

        private decimal CalculateHistoricalVaR(decimal[] returns, decimal confidenceLevel)
        {
            LogDebug($"Calculating Historical VaR with {returns.Length} returns");
            
            var sortedReturns = returns.OrderBy(r => r).ToArray();
            int index = (int)Math.Floor((1 - confidenceLevel) * returns.Length);
            
            LogDebug($"VaR index: {index}, sorted returns range: [{sortedReturns.First():F4}, {sortedReturns.Last():F4}]");
            
            decimal var = -sortedReturns[index];
            LogDebug($"Historical VaR result: {var:F4}");
            
            return var;
        }

        private decimal CalculateParametricVaR(decimal[] returns, decimal confidenceLevel)
        {
            decimal mean = returns.Average();
            decimal stdDev = CalculateStandardDeviation(returns);
            
            // Z-score for confidence level (assuming normal distribution)
            decimal zScore = GetZScore(confidenceLevel);
            
            return -(mean - zScore * stdDev);
        }

        private decimal CalculateMonteCarloVaR(decimal[] returns, decimal confidenceLevel, int simulations = 10000)
        {
            decimal mean = returns.Average();
            decimal stdDev = CalculateStandardDeviation(returns);
            
            var random = new Random();
            var simulatedReturns = new decimal[simulations];
            
            for (int i = 0; i < simulations; i++)
            {
                // Box-Muller transform for normal distribution
                decimal u1 = 1.0m - (decimal)random.NextDouble();
                decimal u2 = 1.0m - (decimal)random.NextDouble();
                decimal normal = DecimalMath.Sqrt(-2.0m * DecimalMath.Log(u1)) * DecimalMath.Sin(2.0m * DecimalMath.PI * u2);
                
                simulatedReturns[i] = mean + stdDev * (decimal)normal;
            }
            
            return CalculateHistoricalVaR(simulatedReturns, confidenceLevel);
        }

        private decimal CalculateHistoricalCVaR(decimal[] returns, decimal var)
        {
            var lossesBeyo ndVaR = returns.Where(r => r < -var).ToArray();
            return lossesBeyo ndVaR.Length > 0 ? -lossesBeyo ndVaR.Average() : var;
        }

        private decimal CalculateParametricCVaR(decimal[] returns, decimal var, decimal confidenceLevel)
        {
            decimal mean = returns.Average();
            decimal stdDev = CalculateStandardDeviation(returns);
            decimal zScore = GetZScore(confidenceLevel);
            
            // For normal distribution, CVaR has closed-form solution
            decimal phi = DecimalMath.Exp(-zScore * zScore / 2) / DecimalMath.Sqrt(2 * DecimalMath.PI);
            decimal cvar = mean + stdDev * phi / (1 - confidenceLevel);
            
            return -cvar;
        }

        private decimal CalculateStandardDeviation(decimal[] values)
        {
            decimal mean = values.Average();
            decimal variance = values.Select(v => (v - mean) * (v - mean)).Average();
            return DecimalMath.Sqrt(variance);
        }

        private decimal GetZScore(decimal confidenceLevel)
        {
            // Approximate inverse normal CDF using Beasley-Springer-Moro algorithm
            decimal a0 = 2.50662823884m;
            decimal a1 = -18.61500062529m;
            decimal a2 = 41.39119773534m;
            decimal a3 = -25.44106049637m;
            
            decimal b0 = -8.47351093090m;
            decimal b1 = 23.08336743743m;
            decimal b2 = -21.06224101826m;
            decimal b3 = 3.13082909833m;
            
            decimal c0 = 0.3374754822726147m;
            decimal c1 = 0.9761690190917186m;
            decimal c2 = 0.1607979714918209m;
            decimal c3 = 0.0276438810333863m;
            decimal c4 = 0.0038405729373609m;
            decimal c5 = 0.0003951896511919m;
            decimal c6 = 0.0000321767881768m;
            decimal c7 = 0.0000002888167364m;
            decimal c8 = 0.0000003960315187m;
            
            decimal y = confidenceLevel - 0.5m;
            
            if (Math.Abs(y) < 0.42m)
            {
                decimal r = y * y;
                return y * (((a3 * r + a2) * r + a1) * r + a0) / ((((b3 * r + b2) * r + b1) * r + b0) * r + 1);
            }
            else
            {
                decimal r = confidenceLevel > 0.5m ? 1 - confidenceLevel : confidenceLevel;
                r = DecimalMath.Log(-DecimalMath.Log(r));
                decimal x = c0 + r * (c1 + r * (c2 + r * (c3 + r * (c4 + r * (c5 + r * (c6 + r * (c7 + r * c8)))))));
                
                return confidenceLevel > 0.5m ? x : -x;
            }
        }

        private async Task<ScenarioResult> EvaluateScenarioAsync(
            decimal[] portfolioWeights,
            decimal[,] assetReturns,
            StressScenario scenario,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var result = new ScenarioResult
                {
                    ScenarioName = scenario.Name,
                    AssetLosses = new decimal[portfolioWeights.Length]
                };

                // Apply scenario shocks
                for (int i = 0; i < portfolioWeights.Length; i++)
                {
                    decimal shock = scenario.AssetShocks?.Length > i ? scenario.AssetShocks[i] : scenario.MarketShock;
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
                var varResult = CalculateVaR(stressedReturns, 0.95m);
                result.StressedVaR = varResult.IsSuccess ? varResult.Data : 0;

                var cvarResult = CalculateCVaR(stressedReturns, 0.95m);
                result.StressedCVaR = cvarResult.IsSuccess ? cvarResult.Data : 0;

                return result;
            }, cancellationToken);
        }

        private decimal[] ApplyStressToReturns(decimal[,] assetReturns, StressScenario scenario)
        {
            int periods = assetReturns.GetLength(0);
            int assets = assetReturns.GetLength(1);
            var stressedReturns = new decimal[periods];

            for (int t = 0; t < periods; t++)
            {
                stressedReturns[t] = 0;
                for (int i = 0; i < assets; i++)
                {
                    decimal shock = scenario.AssetShocks?.Length > i ? scenario.AssetShocks[i] : scenario.MarketShock;
                    decimal stressMultiplier = 1 + shock;
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
        public decimal CVaR { get; set; }
        public decimal Volatility { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal ConcentrationRisk { get; set; }
    }

    public class RiskWeights
    {
        public decimal CVaRWeight { get; set; } = 0.4m;
        public decimal VolatilityWeight { get; set; } = 0.3m;
        public decimal DrawdownWeight { get; set; } = 0.2m;
        public decimal ConcentrationWeight { get; set; } = 0.1m;
    }

    public class StressScenario
    {
        public string Name { get; set; }
        public decimal MarketShock { get; set; }
        public decimal[] AssetShocks { get; set; }
        public decimal Probability { get; set; }
        public string Description { get; set; }
    }

    public class StressTestResult
    {
        public List<ScenarioResult> ScenarioResults { get; set; }
        public decimal WorstCaseLoss { get; set; }
        public string WorstScenario { get; set; }
        public decimal AverageStressLoss { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioName { get; set; }
        public decimal PortfolioLoss { get; set; }
        public decimal[] AssetLosses { get; set; }
        public decimal StressedVaR { get; set; }
        public decimal StressedCVaR { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.Optimization;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.FinancialCalculations.Interfaces;

namespace TradingPlatform.ML.RiskManagement
{
    /// <summary>
    /// Entropic Value at Risk (EVaR) calculator - a coherent risk measure that provides
    /// a tight upper bound for both VaR and CVaR while being computationally efficient
    /// </summary>
    public class EVaRCalculator : CanonicalServiceBaseEnhanced, IEVaRCalculator
    {
        private readonly IDecimalMathService _mathService;
        private readonly GpuContext? _gpuContext;
        private readonly EVaRConfiguration _config;

        public EVaRCalculator(
            IDecimalMathService mathService,
            EVaRConfiguration config,
            GpuContext? gpuContext = null,
            ITradingLogger? logger = null)
            : base(logger, "EVaRCalculator")
        {
            _mathService = mathService ?? throw new ArgumentNullException(nameof(mathService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpuContext = gpuContext;
        }

        public async Task<EVaRResult> CalculateEVaRAsync(
            decimal[] returns,
            decimal confidenceLevel = 0.95m,
            EVaRMethod method = EVaRMethod.DualRepresentation)
        {
            return await TrackOperationAsync("CalculateEVaR", async () =>
            {
                ValidateInputs(returns, confidenceLevel);
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Calculate EVaR using specified method
                var evarValue = method switch
                {
                    EVaRMethod.DualRepresentation => await CalculateEVaRDualAsync(returns, confidenceLevel),
                    EVaRMethod.DirectOptimization => await CalculateEVaRDirectAsync(returns, confidenceLevel),
                    EVaRMethod.MonteCarloSimulation => await CalculateEVaRMonteCarloAsync(returns, confidenceLevel),
                    _ => await CalculateEVaRDualAsync(returns, confidenceLevel)
                };
                
                // Calculate related risk measures for comparison
                var riskMeasures = await CalculateRelatedRiskMeasuresAsync(returns, confidenceLevel);
                
                // Calculate conditional moments
                var conditionalMoments = CalculateConditionalMoments(returns, confidenceLevel);
                
                stopwatch.Stop();
                
                var result = new EVaRResult
                {
                    EVaR = evarValue,
                    ConfidenceLevel = confidenceLevel,
                    VaR = riskMeasures.VaR,
                    CVaR = riskMeasures.CVaR,
                    ExpectedShortfall = riskMeasures.ExpectedShortfall,
                    MaximumLoss = returns.Min(),
                    SampleSize = returns.Length,
                    CalculationTimeMs = stopwatch.ElapsedMilliseconds,
                    Method = method,
                    ConditionalMoments = conditionalMoments,
                    RiskHierarchy = new RiskHierarchy
                    {
                        VaR = riskMeasures.VaR,
                        CVaR = riskMeasures.CVaR,
                        EVaR = evarValue
                    },
                    Timestamp = DateTime.UtcNow
                };
                
                LogInfo("EVAR_CALCULATION_COMPLETE", "EVaR calculation completed",
                    additionalData: new
                    {
                        EVaR = evarValue,
                        VaR = riskMeasures.VaR,
                        CVaR = riskMeasures.CVaR,
                        ConfidenceLevel = confidenceLevel,
                        SampleSize = returns.Length,
                        CalculationTimeMs = stopwatch.ElapsedMilliseconds,
                        Method = method.ToString()
                    });
                
                return result;
            });
        }

        public async Task<PortfolioEVaRResult> CalculatePortfolioEVaRAsync(
            Dictionary<string, decimal[]> assetReturns,
            Dictionary<string, decimal> weights,
            decimal confidenceLevel = 0.95m)
        {
            return await TrackOperationAsync("CalculatePortfolioEVaR", async () =>
            {
                ValidatePortfolioInputs(assetReturns, weights);
                
                // Calculate portfolio returns
                var portfolioReturns = CalculatePortfolioReturns(assetReturns, weights);
                
                // Calculate portfolio EVaR
                var portfolioEVaR = await CalculateEVaRAsync(portfolioReturns, confidenceLevel);
                
                // Calculate marginal EVaR for each asset
                var marginalEVaRs = await CalculateMarginalEVaRAsync(assetReturns, weights, confidenceLevel);
                
                // Calculate component EVaR
                var componentEVaRs = CalculateComponentEVaR(marginalEVaRs, weights, portfolioEVaR.EVaR);
                
                // Calculate diversification benefit
                var undiversifiedEVaR = await CalculateUndiversifiedEVaRAsync(assetReturns, weights, confidenceLevel);
                var diversificationBenefit = undiversifiedEVaR - portfolioEVaR.EVaR;
                
                return new PortfolioEVaRResult
                {
                    PortfolioEVaR = portfolioEVaR,
                    MarginalEVaRs = marginalEVaRs,
                    ComponentEVaRs = componentEVaRs,
                    UndiversifiedEVaR = undiversifiedEVaR,
                    DiversificationBenefit = diversificationBenefit,
                    DiversificationRatio = undiversifiedEVaR > 0 ? diversificationBenefit / undiversifiedEVaR : 0m,
                    Timestamp = DateTime.UtcNow
                };
            });
        }

        public async Task<IntraDayEVaRResult> CalculateIntraDayEVaRAsync(
            decimal[] returns,
            TimeSpan timeHorizon,
            decimal confidenceLevel = 0.95m)
        {
            return await TrackOperationAsync("CalculateIntraDayEVaR", async () =>
            {
                // Calculate base EVaR (typically 1-minute or 5-minute returns)
                var baseEVaR = await CalculateEVaRAsync(returns, confidenceLevel);
                
                // Scale to requested time horizon using square root of time scaling
                var scalingFactor = (decimal)Math.Sqrt(timeHorizon.TotalMinutes / _config.BaseTimeHorizonMinutes);
                var scaledEVaR = baseEVaR.EVaR * scalingFactor;
                
                // Calculate time-dependent adjustments for intraday patterns
                var timeDependentAdjustment = CalculateTimeDependentAdjustment(timeHorizon);
                var adjustedEVaR = scaledEVaR * timeDependentAdjustment;
                
                // Calculate microstructure adjustments for very short horizons
                var microstructureAdjustment = CalculateMicrostructureAdjustment(timeHorizon, returns);
                var finalEVaR = adjustedEVaR * microstructureAdjustment;
                
                return new IntraDayEVaRResult
                {
                    BaseEVaR = baseEVaR.EVaR,
                    ScaledEVaR = scaledEVaR,
                    TimeDependentEVaR = adjustedEVaR,
                    FinalEVaR = finalEVaR,
                    TimeHorizon = timeHorizon,
                    ScalingFactor = scalingFactor,
                    TimeDependentAdjustment = timeDependentAdjustment,
                    MicrostructureAdjustment = microstructureAdjustment,
                    ConfidenceLevel = confidenceLevel,
                    Timestamp = DateTime.UtcNow
                };
            });
        }

        public async Task<DynamicEVaRResult> CalculateDynamicEVaRAsync(
            decimal[] returns,
            int rollingWindow = 252,
            decimal confidenceLevel = 0.95m)
        {
            return await TrackOperationAsync("CalculateDynamicEVaR", async () =>
            {
                if (returns.Length < rollingWindow)
                    throw new ArgumentException($"Returns array too short for rolling window of {rollingWindow}");
                
                var evarTimeSeries = new List<TimeSeriesEVaR>();
                
                // Calculate rolling EVaR
                for (int i = rollingWindow; i <= returns.Length; i++)
                {
                    var windowReturns = returns.Skip(i - rollingWindow).Take(rollingWindow).ToArray();
                    var windowEVaR = await CalculateEVaRAsync(windowReturns, confidenceLevel);
                    
                    evarTimeSeries.Add(new TimeSeriesEVaR
                    {
                        Date = DateTime.UtcNow.AddDays(-(returns.Length - i)),
                        EVaR = windowEVaR.EVaR,
                        VaR = windowEVaR.VaR,
                        CVaR = windowEVaR.CVaR
                    });
                }
                
                // Calculate trend and volatility of EVaR
                var evarValues = evarTimeSeries.Select(e => e.EVaR).ToArray();
                var evarTrend = CalculateTrend(evarValues);
                var evarVolatility = CalculateStandardDeviation(evarValues);
                
                // Detect regime changes in risk
                var regimeChanges = DetectRiskRegimeChanges(evarValues);
                
                return new DynamicEVaRResult
                {
                    EVaRTimeSeries = evarTimeSeries,
                    CurrentEVaR = evarTimeSeries.Last().EVaR,
                    EVaRTrend = evarTrend,
                    EVaRVolatility = evarVolatility,
                    RollingWindow = rollingWindow,
                    RegimeChangePoints = regimeChanges,
                    Timestamp = DateTime.UtcNow
                };
            });
        }

        private async Task<decimal> CalculateEVaRDualAsync(decimal[] returns, decimal confidenceLevel)
        {
            // EVaR dual representation: EVaR_α(X) = inf_{z>0} {z * ln(E[e^(-X/z)]) + z * ln(1/α)}
            
            if (_gpuContext?.IsGpuAvailable == true && returns.Length > 1000)
            {
                return await CalculateEVaRDualGpuAsync(returns, confidenceLevel);
            }
            
            return await Task.Run(() =>
            {
                // Use numerical optimization to find optimal z
                var objective = new Func<double, double>(z =>
                {
                    if (z <= 0) return double.MaxValue;
                    
                    var exponentialSum = 0.0;
                    foreach (var ret in returns)
                    {
                        exponentialSum += Math.Exp(-(double)ret / z);
                    }
                    
                    var expectedExponential = exponentialSum / returns.Length;
                    return z * Math.Log(expectedExponential) + z * Math.Log(1.0 / (double)confidenceLevel);
                });
                
                // Use golden section search for optimization
                var result = FindMinimum.OfScalarFunction(objective, 0.001, 10.0);
                return (decimal)result;
            });
        }

        private async Task<decimal> CalculateEVaRDualGpuAsync(decimal[] returns, decimal confidenceLevel)
        {
            // GPU-accelerated version for large datasets
            // Implementation would use ILGPU kernels for parallel computation of exponentials
            // For now, fallback to CPU version
            return await CalculateEVaRDualAsync(returns, confidenceLevel);
        }

        private async Task<decimal> CalculateEVaRDirectAsync(decimal[] returns, decimal confidenceLevel)
        {
            // Direct optimization approach using the primal formulation
            return await Task.Run(() =>
            {
                var sortedReturns = returns.OrderBy(r => r).ToArray();
                var alpha = confidenceLevel;
                var n = returns.Length;
                
                // Find the optimal lambda using bisection method
                var lambda = FindOptimalLambda(sortedReturns, alpha);
                
                // Calculate EVaR using the found lambda
                var evar = 0.0m;
                foreach (var ret in returns)
                {
                    evar += (decimal)Math.Exp((double)(-ret) * (double)lambda);
                }
                evar = -(decimal)Math.Log((double)(evar / n)) / lambda;
                
                return evar;
            });
        }

        private async Task<decimal> CalculateEVaRMonteCarloAsync(decimal[] returns, decimal confidenceLevel)
        {
            // Monte Carlo simulation approach for EVaR calculation
            return await Task.Run(() =>
            {
                var random = new Random(_config.RandomSeed);
                var simulations = _config.MonteCarloSimulations;
                var evarEstimates = new decimal[simulations];
                
                for (int sim = 0; sim < simulations; sim++)
                {
                    // Bootstrap sample
                    var bootstrapReturns = new decimal[returns.Length];
                    for (int i = 0; i < returns.Length; i++)
                    {
                        bootstrapReturns[i] = returns[random.Next(returns.Length)];
                    }
                    
                    // Calculate EVaR for this bootstrap sample
                    evarEstimates[sim] = CalculateEVaRDualAsync(bootstrapReturns, confidenceLevel).Result;
                }
                
                return evarEstimates.Average();
            });
        }

        private async Task<RiskMeasures> CalculateRelatedRiskMeasuresAsync(decimal[] returns, decimal confidenceLevel)
        {
            return await Task.Run(() =>
            {
                var sortedReturns = returns.OrderBy(r => r).ToArray();
                var alpha = confidenceLevel;
                var n = returns.Length;
                var tailIndex = (int)Math.Ceiling((1 - alpha) * n);
                
                // Value at Risk (VaR)
                var var = sortedReturns[tailIndex - 1];
                
                // Conditional Value at Risk (CVaR)
                var cvar = sortedReturns.Take(tailIndex).Average();
                
                // Expected Shortfall (same as CVaR for continuous distributions)
                var expectedShortfall = cvar;
                
                return new RiskMeasures
                {
                    VaR = var,
                    CVaR = cvar,
                    ExpectedShortfall = expectedShortfall
                };
            });
        }

        private ConditionalMoments CalculateConditionalMoments(decimal[] returns, decimal confidenceLevel)
        {
            var sortedReturns = returns.OrderBy(r => r).ToArray();
            var tailIndex = (int)Math.Ceiling((1 - confidenceLevel) * returns.Length);
            var tailReturns = sortedReturns.Take(tailIndex).ToArray();
            
            if (tailReturns.Length == 0)
                return new ConditionalMoments();
            
            var mean = tailReturns.Average();
            var variance = tailReturns.Select(r => (r - mean) * (r - mean)).Average();
            var std = (decimal)Math.Sqrt((double)variance);
            
            // Skewness and kurtosis
            var skewness = tailReturns.Select(r => Math.Pow((double)(r - mean) / (double)std, 3)).Average();
            var kurtosis = tailReturns.Select(r => Math.Pow((double)(r - mean) / (double)std, 4)).Average() - 3;
            
            return new ConditionalMoments
            {
                Mean = mean,
                StandardDeviation = std,
                Skewness = (decimal)skewness,
                Kurtosis = (decimal)kurtosis,
                SampleSize = tailReturns.Length
            };
        }

        private decimal[] CalculatePortfolioReturns(
            Dictionary<string, decimal[]> assetReturns,
            Dictionary<string, decimal> weights)
        {
            var assets = assetReturns.Keys.ToArray();
            var minLength = assetReturns.Values.Min(r => r.Length);
            var portfolioReturns = new decimal[minLength];
            
            for (int i = 0; i < minLength; i++)
            {
                portfolioReturns[i] = assets.Sum(asset => 
                    weights.ContainsKey(asset) ? weights[asset] * assetReturns[asset][i] : 0m);
            }
            
            return portfolioReturns;
        }

        private async Task<Dictionary<string, decimal>> CalculateMarginalEVaRAsync(
            Dictionary<string, decimal[]> assetReturns,
            Dictionary<string, decimal> weights,
            decimal confidenceLevel)
        {
            var marginalEVaRs = new Dictionary<string, decimal>();
            const decimal delta = 0.01m; // 1% change for numerical differentiation
            
            var portfolioReturns = CalculatePortfolioReturns(assetReturns, weights);
            var baseEVaR = await CalculateEVaRAsync(portfolioReturns, confidenceLevel);
            
            foreach (var asset in weights.Keys)
            {
                // Increase weight by delta
                var adjustedWeights = new Dictionary<string, decimal>(weights);
                adjustedWeights[asset] += delta;
                
                // Renormalize weights
                var totalWeight = adjustedWeights.Values.Sum();
                foreach (var key in adjustedWeights.Keys.ToList())
                {
                    adjustedWeights[key] /= totalWeight;
                }
                
                // Calculate new portfolio EVaR
                var adjustedReturns = CalculatePortfolioReturns(assetReturns, adjustedWeights);
                var adjustedEVaR = await CalculateEVaRAsync(adjustedReturns, confidenceLevel);
                
                // Calculate marginal EVaR
                marginalEVaRs[asset] = (adjustedEVaR.EVaR - baseEVaR.EVaR) / delta;
            }
            
            return marginalEVaRs;
        }

        private Dictionary<string, decimal> CalculateComponentEVaR(
            Dictionary<string, decimal> marginalEVaRs,
            Dictionary<string, decimal> weights,
            decimal portfolioEVaR)
        {
            var componentEVaRs = new Dictionary<string, decimal>();
            
            foreach (var asset in weights.Keys)
            {
                componentEVaRs[asset] = weights[asset] * marginalEVaRs[asset];
            }
            
            return componentEVaRs;
        }

        private async Task<decimal> CalculateUndiversifiedEVaRAsync(
            Dictionary<string, decimal[]> assetReturns,
            Dictionary<string, decimal> weights,
            decimal confidenceLevel)
        {
            var undiversifiedEVaR = 0m;
            
            foreach (var asset in weights.Keys)
            {
                var assetEVaR = await CalculateEVaRAsync(assetReturns[asset], confidenceLevel);
                undiversifiedEVaR += weights[asset] * assetEVaR.EVaR;
            }
            
            return undiversifiedEVaR;
        }

        private decimal FindOptimalLambda(decimal[] sortedReturns, decimal alpha)
        {
            // Bisection method to find optimal lambda
            var low = 0.001;
            var high = 100.0;
            var tolerance = 1e-6;
            
            while (high - low > tolerance)
            {
                var mid = (low + high) / 2.0;
                var constraint = EvaluateLambdaConstraint(sortedReturns, alpha, (decimal)mid);
                
                if (constraint > 0)
                    low = mid;
                else
                    high = mid;
            }
            
            return (decimal)((low + high) / 2.0);
        }

        private double EvaluateLambdaConstraint(decimal[] returns, decimal alpha, decimal lambda)
        {
            var sum = 0.0;
            foreach (var ret in returns)
            {
                sum += Math.Exp((double)(-ret * lambda));
            }
            
            return (double)alpha - sum / returns.Length;
        }

        private decimal CalculateTimeDependentAdjustment(TimeSpan timeHorizon)
        {
            // Adjust for intraday volatility patterns
            var hour = DateTime.Now.Hour;
            
            // Higher volatility at market open/close
            if (hour <= 10 || hour >= 15) // 9:30-10:30 AM or 3:00-4:00 PM
            {
                return 1.2m; // 20% increase
            }
            else if (hour >= 11 && hour <= 14) // 11:00 AM - 2:00 PM (lunch lull)
            {
                return 0.8m; // 20% decrease
            }
            
            return 1.0m; // No adjustment
        }

        private decimal CalculateMicrostructureAdjustment(TimeSpan timeHorizon, decimal[] returns)
        {
            // For very short horizons (< 5 minutes), adjust for microstructure noise
            if (timeHorizon.TotalMinutes < 5)
            {
                var bidAskSpreadEffect = 1.1m; // 10% increase for bid-ask bounce
                var inventoryEffect = 1.05m; // 5% increase for inventory effects
                return bidAskSpreadEffect * inventoryEffect;
            }
            
            return 1.0m;
        }

        private decimal CalculateTrend(decimal[] values)
        {
            if (values.Length < 2) return 0m;
            
            // Simple linear trend calculation
            var n = values.Length;
            var sumX = n * (n - 1) / 2m;
            var sumY = values.Sum();
            var sumXY = values.Select((v, i) => v * i).Sum();
            var sumX2 = n * (n - 1) * (2 * n - 1) / 6m;
            
            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        private decimal CalculateStandardDeviation(decimal[] values)
        {
            if (values.Length == 0) return 0m;
            
            var mean = values.Average();
            var variance = values.Select(v => (v - mean) * (v - mean)).Average();
            return (decimal)Math.Sqrt((double)variance);
        }

        private List<int> DetectRiskRegimeChanges(decimal[] evarValues)
        {
            var changePoints = new List<int>();
            var threshold = CalculateStandardDeviation(evarValues) * 2; // 2 standard deviations
            
            for (int i = 1; i < evarValues.Length; i++)
            {
                if (Math.Abs(evarValues[i] - evarValues[i - 1]) > threshold)
                {
                    changePoints.Add(i);
                }
            }
            
            return changePoints;
        }

        private void ValidateInputs(decimal[] returns, decimal confidenceLevel)
        {
            if (returns == null || returns.Length == 0)
                throw new ArgumentException("Returns array cannot be null or empty");
            
            if (confidenceLevel <= 0m || confidenceLevel >= 1m)
                throw new ArgumentException("Confidence level must be between 0 and 1");
            
            if (returns.Length < _config.MinimumSampleSize)
                throw new ArgumentException($"Minimum sample size is {_config.MinimumSampleSize}");
        }

        private void ValidatePortfolioInputs(
            Dictionary<string, decimal[]> assetReturns,
            Dictionary<string, decimal> weights)
        {
            if (assetReturns == null || assetReturns.Count == 0)
                throw new ArgumentException("Asset returns cannot be null or empty");
            
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("Weights cannot be null or empty");
            
            if (Math.Abs(weights.Values.Sum() - 1m) > 0.001m)
                throw new ArgumentException("Weights must sum to 1");
            
            foreach (var asset in weights.Keys)
            {
                if (!assetReturns.ContainsKey(asset))
                    throw new ArgumentException($"Returns not found for asset: {asset}");
            }
        }
    }

    // Supporting classes and interfaces
    public interface IEVaRCalculator
    {
        Task<EVaRResult> CalculateEVaRAsync(decimal[] returns, decimal confidenceLevel = 0.95m, EVaRMethod method = EVaRMethod.DualRepresentation);
        Task<PortfolioEVaRResult> CalculatePortfolioEVaRAsync(Dictionary<string, decimal[]> assetReturns, Dictionary<string, decimal> weights, decimal confidenceLevel = 0.95m);
        Task<IntraDayEVaRResult> CalculateIntraDayEVaRAsync(decimal[] returns, TimeSpan timeHorizon, decimal confidenceLevel = 0.95m);
        Task<DynamicEVaRResult> CalculateDynamicEVaRAsync(decimal[] returns, int rollingWindow = 252, decimal confidenceLevel = 0.95m);
    }

    public class EVaRConfiguration
    {
        public int MinimumSampleSize { get; set; } = 30;
        public int MonteCarloSimulations { get; set; } = 10000;
        public int RandomSeed { get; set; } = 42;
        public double BaseTimeHorizonMinutes { get; set; } = 1.0;
        public double OptimizationTolerance { get; set; } = 1e-6;
        public int MaxOptimizationIterations { get; set; } = 1000;
    }

    public enum EVaRMethod
    {
        DualRepresentation,
        DirectOptimization,
        MonteCarloSimulation
    }

    public class EVaRResult
    {
        public decimal EVaR { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public decimal VaR { get; set; }
        public decimal CVaR { get; set; }
        public decimal ExpectedShortfall { get; set; }
        public decimal MaximumLoss { get; set; }
        public int SampleSize { get; set; }
        public long CalculationTimeMs { get; set; }
        public EVaRMethod Method { get; set; }
        public ConditionalMoments ConditionalMoments { get; set; } = new();
        public RiskHierarchy RiskHierarchy { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class PortfolioEVaRResult
    {
        public EVaRResult PortfolioEVaR { get; set; } = new();
        public Dictionary<string, decimal> MarginalEVaRs { get; set; } = new();
        public Dictionary<string, decimal> ComponentEVaRs { get; set; } = new();
        public decimal UndiversifiedEVaR { get; set; }
        public decimal DiversificationBenefit { get; set; }
        public decimal DiversificationRatio { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class IntraDayEVaRResult
    {
        public decimal BaseEVaR { get; set; }
        public decimal ScaledEVaR { get; set; }
        public decimal TimeDependentEVaR { get; set; }
        public decimal FinalEVaR { get; set; }
        public TimeSpan TimeHorizon { get; set; }
        public decimal ScalingFactor { get; set; }
        public decimal TimeDependentAdjustment { get; set; }
        public decimal MicrostructureAdjustment { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DynamicEVaRResult
    {
        public List<TimeSeriesEVaR> EVaRTimeSeries { get; set; } = new();
        public decimal CurrentEVaR { get; set; }
        public decimal EVaRTrend { get; set; }
        public decimal EVaRVolatility { get; set; }
        public int RollingWindow { get; set; }
        public List<int> RegimeChangePoints { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class TimeSeriesEVaR
    {
        public DateTime Date { get; set; }
        public decimal EVaR { get; set; }
        public decimal VaR { get; set; }
        public decimal CVaR { get; set; }
    }

    public class RiskMeasures
    {
        public decimal VaR { get; set; }
        public decimal CVaR { get; set; }
        public decimal ExpectedShortfall { get; set; }
    }

    public class ConditionalMoments
    {
        public decimal Mean { get; set; }
        public decimal StandardDeviation { get; set; }
        public decimal Skewness { get; set; }
        public decimal Kurtosis { get; set; }
        public int SampleSize { get; set; }
    }

    public class RiskHierarchy
    {
        public decimal VaR { get; set; }
        public decimal CVaR { get; set; }
        public decimal EVaR { get; set; }
        
        public bool IsCoherent => VaR <= CVaR && CVaR <= EVaR;
    }
}
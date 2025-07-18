// File: TradingPlatform.RiskManagement.Services\RiskCalculatorCanonical.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;
using TradingPlatform.RiskManagement.Models;

namespace TradingPlatform.RiskManagement.Services
{
    /// <summary>
    /// Canonical implementation of risk calculation services providing VaR, Expected Shortfall,
    /// Maximum Drawdown, and other risk metrics with comprehensive monitoring.
    /// </summary>
    public class RiskCalculatorCanonical : CanonicalRiskEvaluator<RiskCalculationContext>, IRiskCalculator
    {
        private const decimal DEFAULT_CONFIDENCE_LEVEL = 0.95m;
        private const decimal HIGH_RISK_THRESHOLD = 0.15m; // 15% loss threshold
        private const decimal EXTREME_RISK_THRESHOLD = 0.25m; // 25% loss threshold

        protected override string RiskType => "Market";

        public RiskCalculatorCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "RiskCalculator")
        {
        }

        /// <summary>
        /// Calculate Value at Risk (VaR) with canonical monitoring
        /// </summary>
        public async Task<decimal> CalculateVaRAsync(IEnumerable<decimal> returns, decimal confidenceLevel = DEFAULT_CONFIDENCE_LEVEL)
        {
            if (returns == null) throw new ArgumentNullException(nameof(returns));
            
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var returnsList = returns.ToList();
                    if (returnsList.Count == 0)
                    {
                        return TradingResult<decimal>.Success(0m);
                    }

                    var var = CalculateVaR(returnsList, confidenceLevel);
                    
                    // Record metrics
                    RecordRiskMetric("VaR", var);
                    RecordRiskMetric("VaR.ConfidenceLevel", confidenceLevel);
                    RecordRiskMetric("VaR.DataPoints", returnsList.Count);

                    // Check risk levels
                    if (var > EXTREME_RISK_THRESHOLD)
                    {
                        LogWarning($"Extreme VaR detected: {var:P2} at {confidenceLevel:P0} confidence", 
                            additionalData: new { VaR = var, ConfidenceLevel = confidenceLevel, DataPoints = returnsList.Count });
                    }
                    else if (var > HIGH_RISK_THRESHOLD)
                    {
                        LogInfo($"High VaR detected: {var:P2} at {confidenceLevel:P0} confidence",
                            additionalData: new { VaR = var, ConfidenceLevel = confidenceLevel });
                    }

                    return await Task.FromResult(TradingResult<decimal>.Success(var));
                },
                nameof(CalculateVaRAsync));

            return result.IsSuccess ? result.Value : 0m;
        }

        /// <summary>
        /// Calculate Expected Shortfall (CVaR) with canonical monitoring
        /// </summary>
        public async Task<decimal> CalculateExpectedShortfallAsync(IEnumerable<decimal> returns, decimal confidenceLevel = DEFAULT_CONFIDENCE_LEVEL)
        {
            if (returns == null) throw new ArgumentNullException(nameof(returns));
            
            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var returnsList = returns.ToList();
                    if (returnsList.Count == 0)
                    {
                        return TradingResult<decimal>.Success(0m);
                    }

                    var es = CalculateExpectedShortfall(returnsList, confidenceLevel);
                    
                    // Record metrics
                    RecordRiskMetric("ExpectedShortfall", es);
                    RecordRiskMetric("ES.ConfidenceLevel", confidenceLevel);
                    
                    // Compare to VaR
                    var var = CalculateVaR(returnsList, confidenceLevel);
                    var esVarRatio = var > 0 ? es / var : 0m;
                    RecordRiskMetric("ES.VaRRatio", esVarRatio);

                    LogInfo($"Expected Shortfall calculated: {es:P2} (ES/VaR ratio: {esVarRatio:F2})",
                        additionalData: new { ExpectedShortfall = es, VaR = var, Ratio = esVarRatio });

                    return await Task.FromResult(TradingResult<decimal>.Success(es));
                },
                nameof(CalculateExpectedShortfallAsync));

            return result.IsSuccess ? result.Value : 0m;
        }

        /// <summary>
        /// Calculate Maximum Drawdown with canonical monitoring
        /// </summary>
        public async Task<decimal> CalculateMaxDrawdownAsync(IEnumerable<decimal> portfolioValues)
        {
            if (portfolioValues == null) throw new ArgumentNullException(nameof(portfolioValues));

            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var values = portfolioValues.ToList();
                    if (values.Count < 2)
                    {
                        return TradingResult<decimal>.Success(0m);
                    }

                    decimal maxValue = values[0];
                    decimal maxDrawdown = 0m;
                    int peakIndex = 0;
                    int troughIndex = 0;

                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] > maxValue)
                        {
                            maxValue = values[i];
                            peakIndex = i;
                        }

                        decimal drawdown = maxValue > 0 ? (maxValue - values[i]) / maxValue : 0m;
                        if (drawdown > maxDrawdown)
                        {
                            maxDrawdown = drawdown;
                            troughIndex = i;
                        }
                    }

                    // Record metrics
                    RecordRiskMetric("MaxDrawdown", maxDrawdown);
                    RecordRiskMetric("MaxDrawdown.PeakIndex", peakIndex);
                    RecordRiskMetric("MaxDrawdown.TroughIndex", troughIndex);
                    RecordRiskMetric("MaxDrawdown.Duration", troughIndex - peakIndex);

                    // Check severity
                    if (maxDrawdown > 0.5m)
                    {
                        LogError($"Severe drawdown detected: {maxDrawdown:P2}", null,
                            "Risk calculation", "Severe drawdown may trigger risk limits", "Review position sizing and stop losses",
                            new { MaxDrawdown = maxDrawdown, PeakIndex = peakIndex, TroughIndex = troughIndex });
                    }
                    else if (maxDrawdown > 0.3m)
                    {
                        LogWarning($"Significant drawdown detected: {maxDrawdown:P2}",
                            additionalData: new { MaxDrawdown = maxDrawdown, Duration = troughIndex - peakIndex });
                    }

                    return await Task.FromResult(TradingResult<decimal>.Success(maxDrawdown));
                },
                nameof(CalculateMaxDrawdownAsync));

            return result.IsSuccess ? result.Value : 0m;
        }

        /// <summary>
        /// Calculate Sharpe Ratio with canonical monitoring
        /// </summary>
        public async Task<decimal> CalculateSharpeRatioAsync(IEnumerable<decimal> returns, decimal riskFreeRate)
        {
            if (returns == null) throw new ArgumentNullException(nameof(returns));

            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var returnsList = returns.ToList();
                    if (returnsList.Count == 0)
                    {
                        return TradingResult<decimal>.Success(0m);
                    }

                    var avgReturn = returnsList.Average();
                    var stdDev = CalculateStandardDeviation(returnsList);
                    var sharpeRatio = CalculateSharpeRatio(avgReturn, riskFreeRate, stdDev);

                    // Record metrics
                    RecordRiskMetric("SharpeRatio", sharpeRatio);
                    RecordRiskMetric("SharpeRatio.AvgReturn", avgReturn);
                    RecordRiskMetric("SharpeRatio.StdDev", stdDev);
                    RecordRiskMetric("SharpeRatio.RiskFreeRate", riskFreeRate);

                    // Evaluate quality
                    string quality = sharpeRatio switch
                    {
                        >= 3m => "Excellent",
                        >= 2m => "Very Good",
                        >= 1m => "Good",
                        >= 0.5m => "Acceptable",
                        >= 0m => "Poor",
                        _ => "Negative"
                    };

                    LogInfo($"Sharpe Ratio: {sharpeRatio:F2} ({quality})",
                        additionalData: new { SharpeRatio = sharpeRatio, Quality = quality, AvgReturn = avgReturn, StdDev = stdDev });

                    return await Task.FromResult(TradingResult<decimal>.Success(sharpeRatio));
                },
                nameof(CalculateSharpeRatioAsync));

            return result.IsSuccess ? result.Value : 0m;
        }

        /// <summary>
        /// Calculate portfolio beta with canonical monitoring
        /// </summary>
        public async Task<decimal> CalculateBetaAsync(IEnumerable<decimal> assetReturns, IEnumerable<decimal> marketReturns)
        {
            if (assetReturns == null) throw new ArgumentNullException(nameof(assetReturns));

            var result = await ExecuteServiceOperationAsync(
                async () =>
                {
                    var assetList = assetReturns.ToList();
                    var marketList = marketReturns.ToList();

                    if (assetList.Count != marketList.Count || assetList.Count < 2)
                    {
                        return TradingResult<decimal>.Failure(new TradingError("INVALID_DATA", "Invalid data: asset and market returns must have same length and at least 2 data points"));
                    }

                    var covariance = CalculateCovariance(assetList, marketList);
                    var marketVariance = CalculateVariance(marketList);
                    
                    var beta = marketVariance > 0 ? covariance / marketVariance : 0m;

                    // Record metrics
                    RecordRiskMetric("Beta", beta);
                    RecordRiskMetric("Beta.Covariance", covariance);
                    RecordRiskMetric("Beta.MarketVariance", marketVariance);

                    // Classify beta
                    string classification = beta switch
                    {
                        > 1.5m => "High Beta (Aggressive)",
                        > 1m => "Above Market",
                        1m => "Market Neutral",
                        > 0.5m => "Below Market",
                        > 0m => "Low Beta (Defensive)",
                        0m => "Zero Beta",
                        _ => "Negative Beta (Inverse)"
                    };

                    LogInfo($"Beta calculated: {beta:F2} - {classification}",
                        additionalData: new { Beta = beta, Classification = classification });

                    return await Task.FromResult(TradingResult<decimal>.Success(beta));
                },
                nameof(CalculateBetaAsync));

            return result.IsSuccess ? result.Value : 0m;
        }

        protected override async Task<TradingResult<RiskAssessment>> EvaluateRiskCoreAsync(
            RiskCalculationContext context,
            RiskAssessment assessment,
            CancellationToken cancellationToken)
        {
            try
            {
                // Calculate various risk metrics
                var var = await CalculateVaRAsync(context.Returns, context.ConfidenceLevel);
                var es = await CalculateExpectedShortfallAsync(context.Returns, context.ConfidenceLevel);
                var maxDrawdown = await CalculateMaxDrawdownAsync(context.PortfolioValues);
                var sharpeRatio = await CalculateSharpeRatioAsync(context.Returns, context.RiskFreeRate);

                // Aggregate risk score (0-1 scale, higher is riskier)
                decimal riskScore = 0m;
                decimal weightSum = 0m;

                // VaR component (40% weight)
                decimal varComponent = Math.Min(var / EXTREME_RISK_THRESHOLD, 1m) * 0.4m;
                riskScore += varComponent;
                weightSum += 0.4m;

                // Expected Shortfall component (30% weight)
                decimal esComponent = Math.Min(es / (EXTREME_RISK_THRESHOLD * 1.5m), 1m) * 0.3m;
                riskScore += esComponent;
                weightSum += 0.3m;

                // Max Drawdown component (20% weight)
                decimal ddComponent = Math.Min(maxDrawdown / 0.5m, 1m) * 0.2m;
                riskScore += ddComponent;
                weightSum += 0.2m;

                // Sharpe Ratio component (10% weight, inverted - lower is riskier)
                decimal sharpeComponent = sharpeRatio <= 0 ? 0.1m : Math.Max(0, (1m - sharpeRatio / 3m)) * 0.1m;
                riskScore += sharpeComponent;
                weightSum += 0.1m;

                // Normalize
                assessment.RiskScore = weightSum > 0 ? riskScore / weightSum : riskScore;
                assessment.IsAcceptable = assessment.RiskScore <= MaxAcceptableRiskScore;

                // Populate metrics
                assessment.Metrics["VaR"] = var;
                assessment.Metrics["ExpectedShortfall"] = es;
                assessment.Metrics["MaxDrawdown"] = maxDrawdown;
                assessment.Metrics["SharpeRatio"] = sharpeRatio;
                assessment.Metrics["VaRComponent"] = varComponent;
                assessment.Metrics["ESComponent"] = esComponent;
                assessment.Metrics["DrawdownComponent"] = ddComponent;
                assessment.Metrics["SharpeComponent"] = sharpeComponent;

                // Generate reason
                assessment.Reason = GenerateRiskAssessmentReason(assessment);

                return TradingResult<RiskAssessment>.Success(assessment);
            }
            catch (Exception ex)
            {
                LogError("Risk evaluation failed", ex);
                return TradingResult<RiskAssessment>.Failure(TradingError.System(ex));
            }
        }

        private string GenerateRiskAssessmentReason(RiskAssessment assessment)
        {
            var reasons = new List<string>();

            if (assessment.Metrics.TryGetValue("VaR", out var varValue) && varValue > HIGH_RISK_THRESHOLD)
            {
                reasons.Add($"High VaR: {varValue:P2}");
            }

            if (assessment.Metrics.TryGetValue("MaxDrawdown", out var ddValue) && ddValue > 0.3m)
            {
                reasons.Add($"Significant drawdown: {ddValue:P2}");
            }

            if (assessment.Metrics.TryGetValue("SharpeRatio", out var sharpeValue) && sharpeValue < 0.5m)
            {
                reasons.Add($"Low Sharpe ratio: {sharpeValue:F2}");
            }

            if (reasons.Count > 0)
            {
                return $"Risk factors: {string.Join(", ", reasons)}";
            }

            return assessment.IsAcceptable ? "Risk within acceptable limits" : "Elevated risk levels detected";
        }

        private decimal CalculateStandardDeviation(List<decimal> values)
        {
            if (values.Count < 2) return 0m;

            var avg = values.Average();
            var sumSquares = values.Sum(v => (v - avg) * (v - avg));
            return (decimal)Math.Sqrt((double)(sumSquares / (values.Count - 1)));
        }

        private decimal CalculateVariance(List<decimal> values)
        {
            if (values.Count < 2) return 0m;

            var avg = values.Average();
            return values.Sum(v => (v - avg) * (v - avg)) / (values.Count - 1);
        }

        private decimal CalculateCovariance(List<decimal> x, List<decimal> y)
        {
            if (x.Count != y.Count || x.Count < 2) return 0m;

            var avgX = x.Average();
            var avgY = y.Average();

            decimal sum = 0m;
            for (int i = 0; i < x.Count; i++)
            {
                sum += (x[i] - avgX) * (y[i] - avgY);
            }

            return sum / (x.Count - 1);
        }

        // IRiskCalculator interface implementations (synchronous wrappers)
        public new decimal CalculateVaR(IEnumerable<decimal> returns, decimal confidenceLevel = DEFAULT_CONFIDENCE_LEVEL)
        {
            return CalculateVaRAsync(returns, confidenceLevel).GetAwaiter().GetResult();
        }

        public new decimal CalculateExpectedShortfall(IEnumerable<decimal> returns, decimal confidenceLevel = DEFAULT_CONFIDENCE_LEVEL)
        {
            return CalculateExpectedShortfallAsync(returns, confidenceLevel).GetAwaiter().GetResult();
        }

        public decimal CalculateMaxDrawdown(IEnumerable<decimal> portfolioValues)
        {
            return CalculateMaxDrawdownAsync(portfolioValues).GetAwaiter().GetResult();
        }

        public decimal CalculateSharpeRatio(IEnumerable<decimal> returns, decimal riskFreeRate)
        {
            return CalculateSharpeRatioAsync(returns, riskFreeRate).GetAwaiter().GetResult();
        }

        public decimal CalculateBeta(IEnumerable<decimal> assetReturns, IEnumerable<decimal> marketReturns)
        {
            return CalculateBetaAsync(assetReturns, marketReturns).GetAwaiter().GetResult();
        }

        public decimal CalculatePositionSize(decimal accountBalance, decimal riskPerTrade, decimal stopLoss)
        {
            var task = ExecuteServiceOperationAsync(
                async () =>
                {
                    if (accountBalance <= 0 || riskPerTrade <= 0 || stopLoss <= 0)
                    {
                        return TradingResult<decimal>.Success(0m);
                    }

                    // Kelly Criterion adjusted position size
                    decimal riskAmount = accountBalance * riskPerTrade;
                    decimal positionSize = riskAmount / stopLoss;

                    // Apply maximum position size limit (10% of account)
                    decimal maxPositionSize = accountBalance * 0.1m;
                    positionSize = Math.Min(positionSize, maxPositionSize);

                    RecordRiskMetric("PositionSize", positionSize);
                    RecordRiskMetric("PositionSize.RiskAmount", riskAmount);
                    RecordRiskMetric("PositionSize.AccountBalance", accountBalance);

                    LogInfo($"Position size calculated: {positionSize:C} (Risk: {riskPerTrade:P2}, Stop: {stopLoss:C})",
                        additionalData: new { PositionSize = positionSize, RiskPerTrade = riskPerTrade, StopLoss = stopLoss });

                    return await Task.FromResult(TradingResult<decimal>.Success(positionSize));
                },
                nameof(CalculatePositionSize));

            var result = task.GetAwaiter().GetResult();
            return result.IsSuccess ? result.Value : 0m;
        }

        public RiskMetrics CalculatePortfolioRisk(IEnumerable<Position> positions)
        {
            var task = ExecuteServiceOperationAsync(
                async () =>
                {
                    var positionsList = positions.ToList();
                    var metrics = new RiskMetrics(
                        VaR95: 0m,
                        VaR99: 0m,
                        ExpectedShortfall: 0m,
                        SharpeRatio: 0m,
                        MaxDrawdown: 0m,
                        Beta: 0m,
                        PortfolioVolatility: 0m,
                        CalculatedAt: DateTime.UtcNow
                    );

                    if (positionsList.Count == 0)
                    {
                        return TradingResult<RiskMetrics>.Success(metrics);
                    }

                    // Calculate total exposure
                    var totalExposure = positionsList.Sum(p => Math.Abs(p.Quantity * p.CurrentPrice));
                    
                    // Calculate unrealized P&L
                    var totalUnrealizedPnL = positionsList.Sum(p => p.UnrealizedPnL);
                    
                    // Calculate concentration risk
                    var largestPosition = positionsList.OrderByDescending(p => Math.Abs(p.Quantity * p.CurrentPrice)).First();
                    var concentrationRisk = totalExposure > 0 
                        ? Math.Abs(largestPosition.Quantity * largestPosition.CurrentPrice) / totalExposure 
                        : 0m;

                    // Calculate portfolio returns for risk metrics
                    var returns = CalculatePositionReturns(positionsList);
                    if (returns.Count > 0)
                    {
                        metrics = metrics with
                        {
                            VaR95 = await CalculateVaRAsync(returns, 0.95m),
                            ExpectedShortfall = await CalculateExpectedShortfallAsync(returns, 0.95m)
                        };
                    }

                    RecordRiskMetric("Portfolio.TotalExposure", totalExposure);
                    RecordRiskMetric("Portfolio.ConcentrationRisk", concentrationRisk);
                    RecordRiskMetric("Portfolio.Positions", positionsList.Count);

                    LogInfo($"Portfolio risk calculated: {positionsList.Count} positions, ${totalExposure:N2} exposure",
                        additionalData: new { Metrics = metrics });

                    return await Task.FromResult(TradingResult<RiskMetrics>.Success(metrics));
                },
                nameof(CalculatePortfolioRisk));

            var result = task.GetAwaiter().GetResult();
            return result.IsSuccess ? result.Value : new RiskMetrics(0m, 0m, 0m, 0m, 0m, 0m, 0m, DateTime.UtcNow);
        }

        private List<decimal> CalculatePositionReturns(List<Position> positions)
        {
            return positions
                .Where(p => p.AveragePrice > 0)
                .Select(p => (p.CurrentPrice - p.AveragePrice) / p.AveragePrice)
                .ToList();
        }
    }

    /// <summary>
    /// Context for risk calculations
    /// </summary>
    public class RiskCalculationContext
    {
        public List<decimal> Returns { get; set; } = new();
        public List<decimal> PortfolioValues { get; set; } = new();
        public decimal ConfidenceLevel { get; set; } = 0.95m;
        public decimal RiskFreeRate { get; set; } = 0.02m; // 2% default
        public string PortfolioId { get; set; } = "";
        public DateTime CalculationDate { get; set; } = DateTime.UtcNow;
    }
}
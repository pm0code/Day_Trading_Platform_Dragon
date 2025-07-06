using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.FinancialCalculations.Interfaces;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.PortfolioOptimization
{
    /// <summary>
    /// Black-Litterman portfolio optimizer enhanced with LSTM-generated market views
    /// Combines Bayesian inference with deep learning predictions for superior portfolio construction
    /// </summary>
    public class BlackLittermanLSTMOptimizer : CanonicalServiceBaseEnhanced, IPortfolioOptimizer
    {
        private readonly IMLInferenceService _mlService;
        private readonly IDecimalMathService _mathService;
        private readonly GpuContext? _gpuContext;
        private readonly BlackLittermanConfiguration _config;
        
        // Black-Litterman parameters
        private readonly decimal _tau; // Uncertainty in prior (typically 0.025 - 0.05)
        private readonly decimal _riskAversion; // Market risk aversion coefficient
        private readonly int _predictionHorizon; // Trading days for prediction
        
        // Model names
        private const string PRICE_PREDICTION_MODEL = "PricePredictionLSTM";
        private const string REGIME_DETECTION_MODEL = "RegimeDetectionHMM";
        private const string VOLATILITY_MODEL = "VolatilityGARCH";

        public BlackLittermanLSTMOptimizer(
            IMLInferenceService mlService,
            IDecimalMathService mathService,
            BlackLittermanConfiguration config,
            GpuContext? gpuContext = null,
            ITradingLogger? logger = null)
            : base(logger, "BlackLittermanLSTMOptimizer")
        {
            _mlService = mlService ?? throw new ArgumentNullException(nameof(mlService));
            _mathService = mathService ?? throw new ArgumentNullException(nameof(mathService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpuContext = gpuContext;
            
            _tau = config.Tau;
            _riskAversion = config.RiskAversion;
            _predictionHorizon = config.PredictionHorizon;
        }

        public async Task<OptimizationResult> OptimizeAsync(
            List<Asset> assets,
            MarketData marketData,
            InvestorViews? investorViews = null,
            OptimizationConstraints? constraints = null)
        {
            return await TrackOperationAsync("BlackLittermanLSTMOptimization", async () =>
            {
                ValidateInputs(assets, marketData);
                
                // Step 1: Calculate market equilibrium returns (prior)
                var equilibriumReturns = await CalculateEquilibriumReturnsAsync(assets, marketData);
                
                // Step 2: Generate LSTM-based market views
                var lstmViews = await GenerateLSTMViewsAsync(assets, marketData);
                
                // Step 3: Combine with investor views if provided
                var combinedViews = investorViews != null 
                    ? MergeViews(investorViews, lstmViews) 
                    : lstmViews;
                
                // Step 4: Apply Black-Litterman formula
                var posteriorReturns = await ApplyBlackLittermanAsync(
                    equilibriumReturns, 
                    marketData.CovarianceMatrix, 
                    combinedViews);
                
                // Step 5: Optimize portfolio with posterior returns
                var optimalWeights = await OptimizePortfolioAsync(
                    posteriorReturns, 
                    marketData.CovarianceMatrix, 
                    constraints);
                
                // Step 6: Calculate performance metrics
                var metrics = await CalculatePortfolioMetricsAsync(
                    optimalWeights, 
                    posteriorReturns, 
                    marketData.CovarianceMatrix);
                
                LogInfo("BL_LSTM_OPTIMIZATION_COMPLETE", "Portfolio optimization completed",
                    additionalData: new
                    {
                        AssetCount = assets.Count,
                        ViewCount = combinedViews.Views.Count,
                        ExpectedReturn = metrics.ExpectedReturn,
                        Volatility = metrics.Volatility,
                        SharpeRatio = metrics.SharpeRatio
                    });
                
                return new OptimizationResult
                {
                    Weights = optimalWeights,
                    ExpectedReturn = metrics.ExpectedReturn,
                    Volatility = metrics.Volatility,
                    SharpeRatio = metrics.SharpeRatio,
                    OptimizationType = "BlackLittermanLSTM",
                    Timestamp = DateTime.UtcNow,
                    AdditionalMetrics = new Dictionary<string, decimal>
                    {
                        { "Tau", _tau },
                        { "RiskAversion", _riskAversion },
                        { "ViewConfidence", combinedViews.AverageConfidence },
                        { "LSTMViewCount", lstmViews.Views.Count },
                        { "InvestorViewCount", investorViews?.Views.Count ?? 0 }
                    }
                };
            });
        }

        private async Task<decimal[]> CalculateEquilibriumReturnsAsync(
            List<Asset> assets, 
            MarketData marketData)
        {
            return await Task.Run(() =>
            {
                // π = δ * Σ * w_mkt
                // where δ is risk aversion, Σ is covariance matrix, w_mkt is market cap weights
                
                var totalMarketCap = assets.Sum(a => a.MarketCap);
                var marketWeights = assets.Select(a => (double)(a.MarketCap / totalMarketCap)).ToArray();
                
                var covMatrix = DenseMatrix.OfArray(marketData.CovarianceMatrix);
                var weightsVector = DenseVector.OfArray(marketWeights);
                
                var equilibriumVector = (double)_riskAversion * covMatrix * weightsVector;
                
                return equilibriumVector.Select(r => (decimal)r).ToArray();
            });
        }

        private async Task<MarketViews> GenerateLSTMViewsAsync(
            List<Asset> assets, 
            MarketData marketData)
        {
            var views = new List<View>();
            
            // Generate views for each asset using LSTM predictions
            var tasks = assets.Select(async (asset, index) =>
            {
                try
                {
                    // Prepare features for LSTM
                    var features = ExtractAssetFeatures(asset, marketData, index);
                    
                    // Get price prediction
                    var pricePrediction = await _mlService.PredictAsync(
                        PRICE_PREDICTION_MODEL,
                        features.PriceFeatures,
                        new[] { 1, features.PriceFeatures.Length });
                    
                    if (!pricePrediction.IsSuccess)
                        return null;
                    
                    // Calculate expected return from price prediction
                    var currentPrice = asset.CurrentPrice;
                    var predictedPrice = (decimal)pricePrediction.Data.Predictions[0];
                    var expectedReturn = (predictedPrice - currentPrice) / currentPrice;
                    
                    // Get confidence from volatility prediction
                    var volPrediction = await _mlService.PredictAsync(
                        VOLATILITY_MODEL,
                        features.VolatilityFeatures,
                        new[] { 1, features.VolatilityFeatures.Length });
                    
                    var predictedVolatility = volPrediction.IsSuccess 
                        ? (decimal)volPrediction.Data.Predictions[0] 
                        : marketData.HistoricalVolatilities[index];
                    
                    // Higher volatility = lower confidence
                    var confidence = CalculateViewConfidence(predictedVolatility, expectedReturn);
                    
                    return new View
                    {
                        Assets = new[] { index },
                        ExpectedReturn = expectedReturn,
                        Confidence = confidence,
                        ViewType = ViewType.Absolute,
                        Source = "LSTM"
                    };
                }
                catch (Exception ex)
                {
                    LogError("LSTM_VIEW_GENERATION_ERROR", 
                        $"Failed to generate LSTM view for {asset.Symbol}", ex);
                    return null;
                }
            }).ToArray();
            
            var generatedViews = (await Task.WhenAll(tasks))
                .Where(v => v != null)
                .ToList();
            
            // Add relative views based on regime detection
            var regimeViews = await GenerateRegimeBasedViewsAsync(assets, marketData);
            generatedViews.AddRange(regimeViews);
            
            return new MarketViews
            {
                Views = generatedViews!,
                ViewMatrix = BuildViewMatrix(generatedViews!, assets.Count),
                ExpectedReturns = generatedViews!.Select(v => v.ExpectedReturn).ToArray(),
                Confidences = generatedViews.Select(v => v.Confidence).ToArray()
            };
        }

        private async Task<List<View>> GenerateRegimeBasedViewsAsync(
            List<Asset> assets, 
            MarketData marketData)
        {
            var views = new List<View>();
            
            try
            {
                // Prepare market-wide features for regime detection
                var marketFeatures = ExtractMarketFeatures(marketData);
                
                var regimePrediction = await _mlService.PredictAsync(
                    REGIME_DETECTION_MODEL,
                    marketFeatures,
                    new[] { 1, marketFeatures.Length });
                
                if (!regimePrediction.IsSuccess)
                    return views;
                
                var regime = InterpretRegime(regimePrediction.Data.Predictions);
                
                // Generate relative views based on detected regime
                switch (regime)
                {
                    case MarketRegime.Bullish:
                        views.AddRange(GenerateBullishViews(assets));
                        break;
                    case MarketRegime.Bearish:
                        views.AddRange(GenerateBearishViews(assets));
                        break;
                    case MarketRegime.HighVolatility:
                        views.AddRange(GenerateDefensiveViews(assets));
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError("REGIME_VIEW_GENERATION_ERROR", "Failed to generate regime-based views", ex);
            }
            
            return views;
        }

        private MarketViews MergeViews(InvestorViews investorViews, MarketViews lstmViews)
        {
            var mergedViews = new List<View>();
            
            // Add all LSTM views
            mergedViews.AddRange(lstmViews.Views);
            
            // Add investor views with potentially adjusted confidence
            foreach (var investorView in investorViews.Views)
            {
                // Check if there's a conflicting LSTM view
                var conflictingView = lstmViews.Views.FirstOrDefault(v => 
                    v.Assets.SequenceEqual(investorView.Assets) && 
                    Math.Sign(v.ExpectedReturn) != Math.Sign(investorView.ExpectedReturn));
                
                if (conflictingView != null)
                {
                    // Reduce confidence when views conflict
                    var adjustedView = new View
                    {
                        Assets = investorView.Assets,
                        ExpectedReturn = investorView.ExpectedReturn,
                        Confidence = investorView.Confidence * 0.7m, // Reduce confidence by 30%
                        ViewType = investorView.ViewType,
                        Source = "Investor (Adjusted)"
                    };
                    mergedViews.Add(adjustedView);
                    
                    LogWarning("VIEW_CONFLICT", 
                        $"Investor view conflicts with LSTM view for assets {string.Join(",", investorView.Assets)}");
                }
                else
                {
                    mergedViews.Add(investorView);
                }
            }
            
            return new MarketViews
            {
                Views = mergedViews,
                ViewMatrix = BuildViewMatrix(mergedViews, lstmViews.ViewMatrix.GetLength(1)),
                ExpectedReturns = mergedViews.Select(v => v.ExpectedReturn).ToArray(),
                Confidences = mergedViews.Select(v => v.Confidence).ToArray()
            };
        }

        private async Task<decimal[]> ApplyBlackLittermanAsync(
            decimal[] equilibriumReturns,
            double[,] covarianceMatrix,
            MarketViews views)
        {
            return await Task.Run(() =>
            {
                // Black-Litterman formula:
                // E[R] = [(τΣ)^(-1) + P'Ω^(-1)P]^(-1)[(τΣ)^(-1)π + P'Ω^(-1)Q]
                
                var n = equilibriumReturns.Length;
                var k = views.Views.Count;
                
                if (k == 0)
                    return equilibriumReturns; // No views, return prior
                
                // Convert to matrices
                var sigma = DenseMatrix.OfArray(covarianceMatrix);
                var pi = DenseVector.OfArray(equilibriumReturns.Select(r => (double)r).ToArray());
                var P = DenseMatrix.OfArray(views.ViewMatrix);
                var Q = DenseVector.OfArray(views.ExpectedReturns.Select(r => (double)r).ToArray());
                
                // Calculate Omega (confidence matrix)
                var omega = CalculateOmegaMatrix(P, sigma, views.Confidences);
                
                // Calculate posterior expected returns
                var tauSigma = (double)_tau * sigma;
                var tauSigmaInv = tauSigma.Inverse();
                var omegaInv = omega.Inverse();
                
                var term1 = tauSigmaInv + P.TransposeThisAndMultiply(omegaInv).Multiply(P);
                var term2 = tauSigmaInv.Multiply(pi) + P.TransposeThisAndMultiply(omegaInv).Multiply(Q);
                
                var posteriorReturns = term1.Inverse().Multiply(term2);
                
                return posteriorReturns.Select(r => (decimal)r).ToArray();
            });
        }

        private Matrix<double> CalculateOmegaMatrix(
            Matrix<double> P, 
            Matrix<double> sigma, 
            decimal[] confidences)
        {
            // Ω = diag(P * τ * Σ * P') / confidence
            // Higher confidence = smaller uncertainty
            
            var tauSigma = (double)_tau * sigma;
            var uncertainty = P.Multiply(tauSigma).Multiply(P.Transpose());
            
            var omega = DenseMatrix.CreateDiagonal(
                confidences.Length, 
                confidences.Length,
                i => uncertainty[i, i] / (double)confidences[i]);
            
            return omega;
        }

        private async Task<Dictionary<string, decimal>> OptimizePortfolioAsync(
            decimal[] expectedReturns,
            double[,] covarianceMatrix,
            OptimizationConstraints? constraints)
        {
            // Use GPU-accelerated quadratic programming if available
            if (_gpuContext?.IsGpuAvailable == true)
            {
                return await OptimizeWithGpuAsync(expectedReturns, covarianceMatrix, constraints);
            }
            
            // Fallback to CPU optimization
            return await OptimizeWithCpuAsync(expectedReturns, covarianceMatrix, constraints);
        }

        private async Task<Dictionary<string, decimal>> OptimizeWithGpuAsync(
            decimal[] expectedReturns,
            double[,] covarianceMatrix,
            OptimizationConstraints? constraints)
        {
            // GPU-accelerated mean-variance optimization
            // Maximize: w'μ - (λ/2)w'Σw
            // Subject to: Σw = 1, w ≥ 0 (if long-only)
            
            return await Task.Run(() =>
            {
                // Implementation would use ILGPU kernels for matrix operations
                // For now, delegate to CPU version
                return OptimizeWithCpuAsync(expectedReturns, covarianceMatrix, constraints).Result;
            });
        }

        private async Task<Dictionary<string, decimal>> OptimizeWithCpuAsync(
            decimal[] expectedReturns,
            double[,] covarianceMatrix,
            OptimizationConstraints? constraints)
        {
            return await Task.Run(() =>
            {
                var n = expectedReturns.Length;
                var mu = DenseVector.OfArray(expectedReturns.Select(r => (double)r).ToArray());
                var sigma = DenseMatrix.OfArray(covarianceMatrix);
                
                // Analytical solution for unconstrained case
                if (constraints == null || !constraints.HasConstraints)
                {
                    var ones = DenseVector.Create(n, 1.0);
                    var sigmaInv = sigma.Inverse();
                    
                    var a = ones.DotProduct(sigmaInv * mu);
                    var b = mu.DotProduct(sigmaInv * mu);
                    var c = ones.DotProduct(sigmaInv * ones);
                    
                    var lambda = (b - a * (double)_config.TargetReturn) / 
                                (a * a / c - b);
                    var gamma = (a / c - (double)_config.TargetReturn) / 
                               (a * a / c - b);
                    
                    var weights = sigmaInv * (lambda * ones + gamma * mu);
                    
                    // Normalize weights
                    var sumWeights = weights.Sum();
                    weights = weights / sumWeights;
                    
                    return CreateWeightsDictionary(weights.ToArray());
                }
                
                // For constrained case, use numerical optimization
                return SolveConstrainedOptimization(mu, sigma, constraints);
            });
        }

        private PortfolioMetrics CalculatePortfolioMetricsAsync(
            Dictionary<string, decimal> weights,
            decimal[] expectedReturns,
            double[,] covarianceMatrix)
        {
            var weightsArray = weights.Values.ToArray();
            var portfolioReturn = weightsArray.Zip(expectedReturns, (w, r) => w * r).Sum();
            
            // Portfolio variance: w'Σw
            var weightsVector = DenseVector.OfArray(weightsArray.Select(w => (double)w).ToArray());
            var sigma = DenseMatrix.OfArray(covarianceMatrix);
            var portfolioVariance = weightsVector.DotProduct(sigma * weightsVector);
            var portfolioVolatility = (decimal)Math.Sqrt(portfolioVariance);
            
            // Sharpe ratio (assuming risk-free rate from config)
            var sharpeRatio = portfolioVolatility > 0 
                ? (portfolioReturn - _config.RiskFreeRate) / portfolioVolatility 
                : 0;
            
            return new PortfolioMetrics
            {
                ExpectedReturn = portfolioReturn,
                Volatility = portfolioVolatility,
                SharpeRatio = sharpeRatio,
                MaxDrawdown = EstimateMaxDrawdown(portfolioVolatility),
                ValueAtRisk95 = CalculateVaR(portfolioReturn, portfolioVolatility, 0.95m)
            };
        }

        private AssetFeatures ExtractAssetFeatures(Asset asset, MarketData marketData, int assetIndex)
        {
            var priceHistory = marketData.PriceHistories[asset.Symbol];
            var volumeHistory = marketData.VolumeHistories[asset.Symbol];
            
            // Price features for LSTM
            var priceFeatures = new List<float>();
            
            // Returns at different horizons
            for (int i = 1; i <= 20; i++)
            {
                if (i < priceHistory.Length)
                {
                    var ret = (float)((priceHistory[^1] - priceHistory[^(i+1)]) / priceHistory[^(i+1)]);
                    priceFeatures.Add(ret);
                }
                else
                {
                    priceFeatures.Add(0f);
                }
            }
            
            // Technical indicators
            priceFeatures.Add((float)CalculateRSI(priceHistory, 14));
            priceFeatures.Add((float)CalculateSMA(priceHistory, 20));
            priceFeatures.Add((float)CalculateSMA(priceHistory, 50));
            priceFeatures.Add((float)CalculateBollingerBand(priceHistory, 20));
            
            // Volume features
            priceFeatures.Add((float)CalculateVolumeRatio(volumeHistory));
            
            // Market relative features
            priceFeatures.Add((float)asset.Beta);
            priceFeatures.Add((float)(asset.MarketCap / 1e9m)); // In billions
            
            // Volatility features for GARCH model
            var returns = CalculateReturns(priceHistory);
            var volFeatures = new List<float>();
            
            // Historical volatilities at different windows
            volFeatures.Add((float)CalculateVolatility(returns, 5));
            volFeatures.Add((float)CalculateVolatility(returns, 10));
            volFeatures.Add((float)CalculateVolatility(returns, 20));
            volFeatures.Add((float)CalculateVolatility(returns, 60));
            
            // GARCH-style features
            volFeatures.Add((float)Math.Pow((double)returns.Last(), 2)); // Squared return
            volFeatures.Add((float)marketData.HistoricalVolatilities[assetIndex]); // Previous volatility
            
            return new AssetFeatures
            {
                PriceFeatures = priceFeatures.ToArray(),
                VolatilityFeatures = volFeatures.ToArray()
            };
        }

        private float[] ExtractMarketFeatures(MarketData marketData)
        {
            var features = new List<float>();
            
            // Market-wide indicators
            features.Add((float)marketData.MarketVolatility);
            features.Add((float)marketData.VixIndex);
            features.Add((float)marketData.MarketBreadth); // Advance/Decline ratio
            features.Add((float)marketData.PutCallRatio);
            
            // Term structure
            features.Add((float)marketData.YieldCurve10Y2Y); // 10Y-2Y spread
            features.Add((float)marketData.CreditSpread); // IG spread
            
            // Cross-asset features
            features.Add((float)marketData.DollarIndex);
            features.Add((float)marketData.GoldPrice);
            features.Add((float)marketData.OilPrice);
            
            // Correlation metrics
            features.Add((float)marketData.AverageCorrelation);
            features.Add((float)marketData.DispersionIndex);
            
            return features.ToArray();
        }

        private void ValidateInputs(List<Asset> assets, MarketData marketData)
        {
            if (assets == null || assets.Count == 0)
                throw new ArgumentException("Assets list cannot be null or empty");
            
            if (marketData == null)
                throw new ArgumentException("Market data cannot be null");
            
            if (marketData.CovarianceMatrix == null || 
                marketData.CovarianceMatrix.GetLength(0) != assets.Count ||
                marketData.CovarianceMatrix.GetLength(1) != assets.Count)
                throw new ArgumentException("Invalid covariance matrix dimensions");
        }

        // Helper methods
        private decimal CalculateViewConfidence(decimal volatility, decimal expectedReturn)
        {
            // Base confidence inversely proportional to volatility
            var baseConfidence = 1m / (1m + volatility * 10m);
            
            // Adjust for return magnitude (extreme predictions get lower confidence)
            var returnAdjustment = 1m - Math.Min(Math.Abs(expectedReturn) * 2m, 0.5m);
            
            return Math.Max(0.1m, Math.Min(0.9m, baseConfidence * returnAdjustment));
        }

        private double[,] BuildViewMatrix(List<View> views, int numAssets)
        {
            var P = new double[views.Count, numAssets];
            
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view.ViewType == ViewType.Absolute)
                {
                    foreach (var assetIndex in view.Assets)
                    {
                        P[i, assetIndex] = 1.0;
                    }
                }
                else // Relative view
                {
                    // First asset positive, second negative for relative view
                    P[i, view.Assets[0]] = 1.0;
                    if (view.Assets.Length > 1)
                        P[i, view.Assets[1]] = -1.0;
                }
            }
            
            return P;
        }

        private Dictionary<string, decimal> CreateWeightsDictionary(double[] weights)
        {
            var result = new Dictionary<string, decimal>();
            for (int i = 0; i < weights.Length; i++)
            {
                result[$"Asset_{i}"] = (decimal)weights[i];
            }
            return result;
        }

        private Dictionary<string, decimal> SolveConstrainedOptimization(
            Vector<double> mu,
            Matrix<double> sigma,
            OptimizationConstraints constraints)
        {
            // Placeholder for constrained optimization solver
            // In production, use a proper QP solver like CVXPY or similar
            throw new NotImplementedException("Constrained optimization solver not yet implemented");
        }

        // Technical indicator calculations
        private decimal CalculateRSI(decimal[] prices, int period)
        {
            if (prices.Length < period + 1) return 50m;
            
            var gains = new List<decimal>();
            var losses = new List<decimal>();
            
            for (int i = prices.Length - period; i < prices.Length; i++)
            {
                var change = prices[i] - prices[i - 1];
                if (change > 0)
                    gains.Add(change);
                else
                    losses.Add(-change);
            }
            
            var avgGain = gains.Count > 0 ? gains.Average() : 0m;
            var avgLoss = losses.Count > 0 ? losses.Average() : 0.01m;
            
            var rs = avgGain / avgLoss;
            return 100m - (100m / (1m + rs));
        }

        private decimal CalculateSMA(decimal[] prices, int period)
        {
            if (prices.Length < period) return prices.Last();
            return prices.Skip(prices.Length - period).Average();
        }

        private decimal CalculateBollingerBand(decimal[] prices, int period)
        {
            if (prices.Length < period) return 0m;
            
            var sma = CalculateSMA(prices, period);
            var std = CalculateStandardDeviation(prices.Skip(prices.Length - period).ToArray());
            
            var currentPrice = prices.Last();
            var upperBand = sma + 2m * std;
            var lowerBand = sma - 2m * std;
            
            // Return position within bands (-1 to 1)
            return (currentPrice - lowerBand) / (upperBand - lowerBand) * 2m - 1m;
        }

        private decimal CalculateVolumeRatio(decimal[] volumes)
        {
            if (volumes.Length < 20) return 1m;
            
            var recentVolume = volumes.Skip(volumes.Length - 5).Average();
            var historicalVolume = volumes.Skip(volumes.Length - 20).Average();
            
            return historicalVolume > 0 ? recentVolume / historicalVolume : 1m;
        }

        private decimal[] CalculateReturns(decimal[] prices)
        {
            var returns = new decimal[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
            {
                returns[i - 1] = (prices[i] - prices[i - 1]) / prices[i - 1];
            }
            return returns;
        }

        private decimal CalculateVolatility(decimal[] returns, int window)
        {
            if (returns.Length < window) return 0.02m; // Default 2% volatility
            
            var windowReturns = returns.Skip(returns.Length - window).ToArray();
            return CalculateStandardDeviation(windowReturns) * (decimal)Math.Sqrt(252); // Annualized
        }

        private decimal CalculateStandardDeviation(decimal[] values)
        {
            if (values.Length == 0) return 0m;
            
            var mean = values.Average();
            var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
            return (decimal)Math.Sqrt((double)(sumOfSquares / values.Length));
        }

        private MarketRegime InterpretRegime(float[] predictions)
        {
            // Assuming model outputs probabilities for each regime
            var maxIndex = Array.IndexOf(predictions, predictions.Max());
            return (MarketRegime)maxIndex;
        }

        private List<View> GenerateBullishViews(List<Asset> assets)
        {
            var views = new List<View>();
            
            // In bullish regime, favor growth stocks and cyclicals
            var growthStocks = assets.Where(a => a.Sector == "Technology" || a.Sector == "Consumer Discretionary")
                                   .Select(a => assets.IndexOf(a))
                                   .ToArray();
            
            if (growthStocks.Length > 0)
            {
                views.Add(new View
                {
                    Assets = growthStocks,
                    ExpectedReturn = 0.15m, // 15% expected return
                    Confidence = 0.7m,
                    ViewType = ViewType.Absolute,
                    Source = "RegimeBullish"
                });
            }
            
            return views;
        }

        private List<View> GenerateBearishViews(List<Asset> assets)
        {
            var views = new List<View>();
            
            // In bearish regime, favor defensive stocks and utilities
            var defensiveStocks = assets.Where(a => a.Sector == "Utilities" || a.Sector == "Consumer Staples")
                                      .Select(a => assets.IndexOf(a))
                                      .ToArray();
            
            if (defensiveStocks.Length > 0)
            {
                views.Add(new View
                {
                    Assets = defensiveStocks,
                    ExpectedReturn = 0.05m, // 5% expected return (defensive)
                    Confidence = 0.8m,
                    ViewType = ViewType.Absolute,
                    Source = "RegimeBearish"
                });
            }
            
            return views;
        }

        private List<View> GenerateDefensiveViews(List<Asset> assets)
        {
            var views = new List<View>();
            
            // In high volatility, reduce overall exposure and favor low-beta stocks
            var lowBetaStocks = assets.Where(a => a.Beta < 0.8m)
                                    .Select(a => assets.IndexOf(a))
                                    .ToArray();
            
            if (lowBetaStocks.Length > 0)
            {
                views.Add(new View
                {
                    Assets = lowBetaStocks,
                    ExpectedReturn = 0.02m, // 2% expected return (capital preservation)
                    Confidence = 0.9m,
                    ViewType = ViewType.Absolute,
                    Source = "RegimeDefensive"
                });
            }
            
            return views;
        }

        private decimal EstimateMaxDrawdown(decimal volatility)
        {
            // Rough estimate: MaxDD ≈ 2.5 * annual volatility
            return Math.Min(volatility * 2.5m, 0.5m); // Cap at 50%
        }

        private decimal CalculateVaR(decimal expectedReturn, decimal volatility, decimal confidence)
        {
            // Parametric VaR assuming normal distribution
            var zScore = confidence switch
            {
                0.95m => 1.645m,
                0.99m => 2.326m,
                _ => 1.645m
            };
            
            return expectedReturn - zScore * volatility;
        }
    }

    // Supporting classes
    public class BlackLittermanConfiguration
    {
        public decimal Tau { get; set; } = 0.05m;
        public decimal RiskAversion { get; set; } = 2.5m;
        public int PredictionHorizon { get; set; } = 21; // Trading days
        public decimal TargetReturn { get; set; } = 0.10m; // 10% annual
        public decimal RiskFreeRate { get; set; } = 0.04m; // 4% risk-free rate
    }

    public class Asset
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal MarketCap { get; set; }
        public decimal Beta { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    public class MarketData
    {
        public double[,] CovarianceMatrix { get; set; } = new double[0, 0];
        public decimal[] HistoricalVolatilities { get; set; } = Array.Empty<decimal>();
        public Dictionary<string, decimal[]> PriceHistories { get; set; } = new();
        public Dictionary<string, decimal[]> VolumeHistories { get; set; } = new();
        
        // Market-wide indicators
        public decimal MarketVolatility { get; set; }
        public decimal VixIndex { get; set; }
        public decimal MarketBreadth { get; set; }
        public decimal PutCallRatio { get; set; }
        public decimal YieldCurve10Y2Y { get; set; }
        public decimal CreditSpread { get; set; }
        public decimal DollarIndex { get; set; }
        public decimal GoldPrice { get; set; }
        public decimal OilPrice { get; set; }
        public decimal AverageCorrelation { get; set; }
        public decimal DispersionIndex { get; set; }
    }

    public class InvestorViews
    {
        public List<View> Views { get; set; } = new();
    }

    public class MarketViews
    {
        public List<View> Views { get; set; } = new();
        public double[,] ViewMatrix { get; set; } = new double[0, 0];
        public decimal[] ExpectedReturns { get; set; } = Array.Empty<decimal>();
        public decimal[] Confidences { get; set; } = Array.Empty<decimal>();
        
        public decimal AverageConfidence => Confidences.Length > 0 ? Confidences.Average() : 0m;
    }

    public class View
    {
        public int[] Assets { get; set; } = Array.Empty<int>(); // Asset indices
        public decimal ExpectedReturn { get; set; }
        public decimal Confidence { get; set; } // 0 to 1
        public ViewType ViewType { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public enum ViewType
    {
        Absolute, // View on absolute return
        Relative  // View on relative performance
    }

    public class OptimizationConstraints
    {
        public decimal MinWeight { get; set; } = 0m;
        public decimal MaxWeight { get; set; } = 1m;
        public bool LongOnly { get; set; } = true;
        public Dictionary<string, decimal> SectorLimits { get; set; } = new();
        public decimal MaxLeverage { get; set; } = 1m;
        
        public bool HasConstraints => 
            MinWeight > 0 || MaxWeight < 1 || LongOnly || 
            SectorLimits.Count > 0 || MaxLeverage != 1m;
    }

    public class OptimizationResult
    {
        public Dictionary<string, decimal> Weights { get; set; } = new();
        public decimal ExpectedReturn { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public string OptimizationType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, decimal> AdditionalMetrics { get; set; } = new();
    }

    public class PortfolioMetrics
    {
        public decimal ExpectedReturn { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal ValueAtRisk95 { get; set; }
    }

    public enum MarketRegime
    {
        Bullish = 0,
        Bearish = 1,
        HighVolatility = 2,
        LowVolatility = 3,
        Transitional = 4
    }

    public class AssetFeatures
    {
        public float[] PriceFeatures { get; set; } = Array.Empty<float>();
        public float[] VolatilityFeatures { get; set; } = Array.Empty<float>();
    }

    public interface IPortfolioOptimizer
    {
        Task<OptimizationResult> OptimizeAsync(
            List<Asset> assets,
            MarketData marketData,
            InvestorViews? investorViews = null,
            OptimizationConstraints? constraints = null);
    }
}
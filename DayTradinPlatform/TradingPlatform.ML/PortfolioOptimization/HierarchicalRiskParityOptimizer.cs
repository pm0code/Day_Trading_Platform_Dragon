using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.GPU.Infrastructure;
using TradingPlatform.ML.Interfaces;

namespace TradingPlatform.ML.PortfolioOptimization
{
    /// <summary>
    /// Hierarchical Risk Parity (HRP) portfolio optimizer
    /// Implements Marcos López de Prado's HRP algorithm for robust portfolio construction
    /// </summary>
    public class HierarchicalRiskParityOptimizer : CanonicalServiceBaseEnhanced, IPortfolioOptimizer
    {
        private readonly GpuContext? _gpuContext;
        private readonly HRPConfiguration _config;
        private readonly IMLInferenceService? _mlService;

        public HierarchicalRiskParityOptimizer(
            HRPConfiguration config,
            GpuContext? gpuContext = null,
            IMLInferenceService? mlService = null,
            ITradingLogger? logger = null)
            : base(logger, "HierarchicalRiskParityOptimizer")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpuContext = gpuContext;
            _mlService = mlService;
        }

        public async Task<OptimizationResult> OptimizeAsync(
            List<Asset> assets,
            MarketData marketData,
            InvestorViews? investorViews = null,
            OptimizationConstraints? constraints = null)
        {
            return await TrackOperationAsync("HRPOptimization", async () =>
            {
                ValidateInputs(assets, marketData);
                
                // Step 1: Calculate returns correlation matrix
                var returns = ExtractReturnsMatrix(marketData, assets);
                var correlationMatrix = await CalculateCorrelationMatrixAsync(returns);
                
                // Step 2: Perform hierarchical clustering
                var distanceMatrix = CorrelationToDistance(correlationMatrix);
                var linkageMatrix = await PerformHierarchicalClusteringAsync(distanceMatrix);
                
                // Step 3: Quasi-diagonalize correlation matrix
                var sortedIndices = QuasiDiagonalize(linkageMatrix, correlationMatrix.RowCount);
                var sortedCorrelation = ReorderMatrix(correlationMatrix, sortedIndices);
                
                // Step 4: Calculate variance using sorted covariance
                var sortedCovariance = CorrelationToCovariance(sortedCorrelation, marketData.HistoricalVolatilities, sortedIndices);
                
                // Step 5: Recursive bisection to allocate weights
                var hrpWeights = RecursiveBisection(sortedCovariance, sortedIndices);
                
                // Step 6: Apply ML-based adjustments if available
                if (_mlService != null && _config.UseMLEnhancement)
                {
                    hrpWeights = await ApplyMLAdjustmentsAsync(hrpWeights, assets, marketData);
                }
                
                // Step 7: Apply constraints if provided
                if (constraints != null && constraints.HasConstraints)
                {
                    hrpWeights = ApplyConstraints(hrpWeights, constraints);
                }
                
                // Step 8: Calculate portfolio metrics
                var metrics = CalculatePortfolioMetrics(hrpWeights, marketData);
                
                LogInfo("HRP_OPTIMIZATION_COMPLETE", "HRP optimization completed",
                    additionalData: new
                    {
                        AssetCount = assets.Count,
                        ExpectedReturn = metrics.ExpectedReturn,
                        Volatility = metrics.Volatility,
                        SharpeRatio = metrics.SharpeRatio,
                        DiversificationRatio = metrics.DiversificationRatio
                    });
                
                return new OptimizationResult
                {
                    Weights = CreateWeightsDictionary(hrpWeights, assets),
                    ExpectedReturn = metrics.ExpectedReturn,
                    Volatility = metrics.Volatility,
                    SharpeRatio = metrics.SharpeRatio,
                    OptimizationType = "HierarchicalRiskParity",
                    Timestamp = DateTime.UtcNow,
                    AdditionalMetrics = new Dictionary<string, decimal>
                    {
                        { "DiversificationRatio", metrics.DiversificationRatio },
                        { "EffectiveNumberOfAssets", metrics.EffectiveNumberOfAssets },
                        { "MaxWeight", (decimal)hrpWeights.Max() },
                        { "MinWeight", (decimal)hrpWeights.Min() },
                        { "ConcentrationRatio", metrics.ConcentrationRatio }
                    }
                };
            });
        }

        private async Task<Matrix<double>> CalculateCorrelationMatrixAsync(Matrix<double> returns)
        {
            if (_gpuContext?.IsGpuAvailable == true && returns.RowCount > 100)
            {
                return await CalculateCorrelationGpuAsync(returns);
            }
            
            return await Task.Run(() => CalculateCorrelationCpu(returns));
        }

        private Matrix<double> CalculateCorrelationCpu(Matrix<double> returns)
        {
            var n = returns.ColumnCount;
            var correlation = DenseMatrix.Create(n, n, 0.0);
            
            // Calculate means and standard deviations
            var means = new double[n];
            var stds = new double[n];
            
            for (int i = 0; i < n; i++)
            {
                var column = returns.Column(i);
                means[i] = column.Average();
                stds[i] = Math.Sqrt(column.Select(r => Math.Pow(r - means[i], 2)).Average());
            }
            
            // Calculate correlations
            for (int i = 0; i < n; i++)
            {
                correlation[i, i] = 1.0;
                
                for (int j = i + 1; j < n; j++)
                {
                    var cov = 0.0;
                    for (int k = 0; k < returns.RowCount; k++)
                    {
                        cov += (returns[k, i] - means[i]) * (returns[k, j] - means[j]);
                    }
                    cov /= returns.RowCount;
                    
                    var corr = cov / (stds[i] * stds[j]);
                    correlation[i, j] = corr;
                    correlation[j, i] = corr;
                }
            }
            
            return correlation;
        }

        private async Task<Matrix<double>> CalculateCorrelationGpuAsync(Matrix<double> returns)
        {
            // GPU acceleration implementation would go here
            // For now, fallback to CPU
            return await Task.Run(() => CalculateCorrelationCpu(returns));
        }

        private Matrix<double> CorrelationToDistance(Matrix<double> correlation)
        {
            var n = correlation.RowCount;
            var distance = DenseMatrix.Create(n, n, 0.0);
            
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    // Distance = sqrt(0.5 * (1 - correlation))
                    distance[i, j] = Math.Sqrt(0.5 * (1 - correlation[i, j]));
                }
            }
            
            return distance;
        }

        private async Task<LinkageMatrix> PerformHierarchicalClusteringAsync(Matrix<double> distanceMatrix)
        {
            return await Task.Run(() => PerformHierarchicalClustering(distanceMatrix));
        }

        private LinkageMatrix PerformHierarchicalClustering(Matrix<double> distanceMatrix)
        {
            var n = distanceMatrix.RowCount;
            var clusters = new List<Cluster>();
            var linkage = new LinkageMatrix();
            
            // Initialize clusters (each asset is its own cluster)
            for (int i = 0; i < n; i++)
            {
                clusters.Add(new Cluster { Id = i, Members = new List<int> { i } });
            }
            
            // Hierarchical clustering using single linkage
            while (clusters.Count > 1)
            {
                // Find closest pair of clusters
                double minDistance = double.MaxValue;
                int cluster1Index = -1;
                int cluster2Index = -1;
                
                for (int i = 0; i < clusters.Count; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        var dist = CalculateClusterDistance(clusters[i], clusters[j], distanceMatrix, _config.LinkageMethod);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            cluster1Index = i;
                            cluster2Index = j;
                        }
                    }
                }
                
                // Merge clusters
                var newCluster = new Cluster
                {
                    Id = n + linkage.Steps.Count,
                    Members = clusters[cluster1Index].Members.Concat(clusters[cluster2Index].Members).ToList()
                };
                
                linkage.Steps.Add(new LinkageStep
                {
                    Cluster1 = clusters[cluster1Index].Id,
                    Cluster2 = clusters[cluster2Index].Id,
                    Distance = minDistance,
                    NewClusterId = newCluster.Id
                });
                
                // Remove old clusters and add new one
                clusters.RemoveAt(Math.Max(cluster1Index, cluster2Index));
                clusters.RemoveAt(Math.Min(cluster1Index, cluster2Index));
                clusters.Add(newCluster);
            }
            
            return linkage;
        }

        private double CalculateClusterDistance(Cluster c1, Cluster c2, Matrix<double> distanceMatrix, LinkageMethod method)
        {
            var distances = new List<double>();
            
            foreach (var i in c1.Members)
            {
                foreach (var j in c2.Members)
                {
                    distances.Add(distanceMatrix[i, j]);
                }
            }
            
            return method switch
            {
                LinkageMethod.Single => distances.Min(),
                LinkageMethod.Complete => distances.Max(),
                LinkageMethod.Average => distances.Average(),
                LinkageMethod.Ward => CalculateWardDistance(c1, c2, distanceMatrix),
                _ => distances.Min()
            };
        }

        private double CalculateWardDistance(Cluster c1, Cluster c2, Matrix<double> distanceMatrix)
        {
            // Ward's method minimizes within-cluster variance
            var n1 = c1.Members.Count;
            var n2 = c2.Members.Count;
            
            // Calculate centroid distance with size weighting
            var avgDist = 0.0;
            foreach (var i in c1.Members)
            {
                foreach (var j in c2.Members)
                {
                    avgDist += distanceMatrix[i, j] * distanceMatrix[i, j];
                }
            }
            
            return Math.Sqrt(avgDist * n1 * n2 / (n1 + n2));
        }

        private int[] QuasiDiagonalize(LinkageMatrix linkage, int numAssets)
        {
            // Reorder assets to place similar ones together
            var sortedIndices = new List<int>();
            var dendrogramOrder = ExtractDendrogramOrder(linkage, numAssets);
            
            foreach (var node in dendrogramOrder)
            {
                if (node < numAssets) // Leaf node (original asset)
                {
                    sortedIndices.Add(node);
                }
            }
            
            return sortedIndices.ToArray();
        }

        private List<int> ExtractDendrogramOrder(LinkageMatrix linkage, int numAssets)
        {
            // Extract order from dendrogram using depth-first traversal
            var order = new List<int>();
            var visited = new HashSet<int>();
            
            if (linkage.Steps.Count > 0)
            {
                var rootCluster = linkage.Steps.Last().NewClusterId;
                TraverseDendrogram(rootCluster, linkage, numAssets, order, visited);
            }
            else
            {
                // No clustering performed, return original order
                for (int i = 0; i < numAssets; i++)
                {
                    order.Add(i);
                }
            }
            
            return order;
        }

        private void TraverseDendrogram(int clusterId, LinkageMatrix linkage, int numAssets, List<int> order, HashSet<int> visited)
        {
            if (visited.Contains(clusterId))
                return;
            
            visited.Add(clusterId);
            
            if (clusterId < numAssets)
            {
                // Leaf node
                order.Add(clusterId);
            }
            else
            {
                // Internal node - find the step that created this cluster
                var step = linkage.Steps.FirstOrDefault(s => s.NewClusterId == clusterId);
                if (step != null)
                {
                    TraverseDendrogram(step.Cluster1, linkage, numAssets, order, visited);
                    TraverseDendrogram(step.Cluster2, linkage, numAssets, order, visited);
                }
            }
        }

        private Matrix<double> ReorderMatrix(Matrix<double> matrix, int[] newOrder)
        {
            var n = matrix.RowCount;
            var reordered = DenseMatrix.Create(n, n, 0.0);
            
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    reordered[i, j] = matrix[newOrder[i], newOrder[j]];
                }
            }
            
            return reordered;
        }

        private Matrix<double> CorrelationToCovariance(Matrix<double> correlation, decimal[] volatilities, int[] sortedIndices)
        {
            var n = correlation.RowCount;
            var covariance = DenseMatrix.Create(n, n, 0.0);
            
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    var vol_i = (double)volatilities[sortedIndices[i]];
                    var vol_j = (double)volatilities[sortedIndices[j]];
                    covariance[i, j] = correlation[i, j] * vol_i * vol_j;
                }
            }
            
            return covariance;
        }

        private double[] RecursiveBisection(Matrix<double> covariance, int[] sortedIndices)
        {
            var n = covariance.RowCount;
            var weights = new double[n];
            
            // Initialize all weights to 1
            for (int i = 0; i < n; i++)
            {
                weights[i] = 1.0;
            }
            
            // Build cluster tree
            var items = Enumerable.Range(0, n).ToList();
            RecursiveBisectionInternal(covariance, weights, items);
            
            // Normalize weights
            var sumWeights = weights.Sum();
            for (int i = 0; i < n; i++)
            {
                weights[i] /= sumWeights;
            }
            
            // Reorder weights back to original order
            var finalWeights = new double[n];
            for (int i = 0; i < n; i++)
            {
                finalWeights[sortedIndices[i]] = weights[i];
            }
            
            return finalWeights;
        }

        private void RecursiveBisectionInternal(Matrix<double> covariance, double[] weights, List<int> items)
        {
            if (items.Count <= 1)
                return;
            
            // Split items into two clusters
            var midpoint = items.Count / 2;
            var cluster1 = items.Take(midpoint).ToList();
            var cluster2 = items.Skip(midpoint).ToList();
            
            // Calculate cluster variances
            var var1 = CalculateClusterVariance(covariance, weights, cluster1);
            var var2 = CalculateClusterVariance(covariance, weights, cluster2);
            
            // Calculate allocation factor based on inverse variance
            var alpha = 1.0 - var1 / (var1 + var2);
            
            // Update weights
            foreach (var i in cluster1)
            {
                weights[i] *= alpha;
            }
            
            foreach (var i in cluster2)
            {
                weights[i] *= (1 - alpha);
            }
            
            // Recursively bisect each cluster
            RecursiveBisectionInternal(covariance, weights, cluster1);
            RecursiveBisectionInternal(covariance, weights, cluster2);
        }

        private double CalculateClusterVariance(Matrix<double> covariance, double[] weights, List<int> items)
        {
            var variance = 0.0;
            
            foreach (var i in items)
            {
                foreach (var j in items)
                {
                    variance += weights[i] * weights[j] * covariance[i, j];
                }
            }
            
            return variance;
        }

        private async Task<double[]> ApplyMLAdjustmentsAsync(double[] hrpWeights, List<Asset> assets, MarketData marketData)
        {
            try
            {
                // Use ML to predict regime and adjust weights accordingly
                var marketFeatures = ExtractMarketFeatures(marketData);
                
                var regimePrediction = await _mlService!.PredictAsync(
                    "MarketRegimeClassifier",
                    marketFeatures,
                    new[] { 1, marketFeatures.Length });
                
                if (!regimePrediction.IsSuccess)
                    return hrpWeights;
                
                var regime = InterpretRegime(regimePrediction.Data.Predictions);
                
                // Adjust weights based on regime
                return AdjustWeightsForRegime(hrpWeights, assets, regime);
            }
            catch (Exception ex)
            {
                LogWarning("ML_ADJUSTMENT_FAILED", "Failed to apply ML adjustments", ex);
                return hrpWeights;
            }
        }

        private float[] ExtractMarketFeatures(MarketData marketData)
        {
            return new[]
            {
                (float)marketData.MarketVolatility,
                (float)marketData.VixIndex,
                (float)marketData.MarketBreadth,
                (float)marketData.PutCallRatio,
                (float)marketData.YieldCurve10Y2Y,
                (float)marketData.CreditSpread,
                (float)marketData.AverageCorrelation,
                (float)marketData.DispersionIndex
            };
        }

        private MarketRegime InterpretRegime(float[] predictions)
        {
            var maxIndex = Array.IndexOf(predictions, predictions.Max());
            return (MarketRegime)maxIndex;
        }

        private double[] AdjustWeightsForRegime(double[] weights, List<Asset> assets, MarketRegime regime)
        {
            var adjustedWeights = (double[])weights.Clone();
            
            switch (regime)
            {
                case MarketRegime.HighVolatility:
                    // In high volatility, increase weights of low-beta assets
                    for (int i = 0; i < assets.Count; i++)
                    {
                        if (assets[i].Beta < 0.8m)
                        {
                            adjustedWeights[i] *= 1.2; // 20% increase
                        }
                        else if (assets[i].Beta > 1.2m)
                        {
                            adjustedWeights[i] *= 0.8; // 20% decrease
                        }
                    }
                    break;
                    
                case MarketRegime.Bullish:
                    // In bullish regime, slight tilt towards growth
                    for (int i = 0; i < assets.Count; i++)
                    {
                        if (assets[i].Sector == "Technology" || assets[i].Sector == "Consumer Discretionary")
                        {
                            adjustedWeights[i] *= 1.1; // 10% increase
                        }
                    }
                    break;
                    
                case MarketRegime.Bearish:
                    // In bearish regime, defensive tilt
                    for (int i = 0; i < assets.Count; i++)
                    {
                        if (assets[i].Sector == "Utilities" || assets[i].Sector == "Consumer Staples")
                        {
                            adjustedWeights[i] *= 1.15; // 15% increase
                        }
                    }
                    break;
            }
            
            // Renormalize
            var sum = adjustedWeights.Sum();
            for (int i = 0; i < adjustedWeights.Length; i++)
            {
                adjustedWeights[i] /= sum;
            }
            
            return adjustedWeights;
        }

        private double[] ApplyConstraints(double[] weights, OptimizationConstraints constraints)
        {
            var constrainedWeights = (double[])weights.Clone();
            
            // Apply min/max weight constraints
            for (int i = 0; i < constrainedWeights.Length; i++)
            {
                constrainedWeights[i] = Math.Max((double)constraints.MinWeight, constrainedWeights[i]);
                constrainedWeights[i] = Math.Min((double)constraints.MaxWeight, constrainedWeights[i]);
            }
            
            // Renormalize
            var sum = constrainedWeights.Sum();
            for (int i = 0; i < constrainedWeights.Length; i++)
            {
                constrainedWeights[i] /= sum;
            }
            
            return constrainedWeights;
        }

        private HRPMetrics CalculatePortfolioMetrics(double[] weights, MarketData marketData)
        {
            var n = weights.Length;
            
            // Portfolio variance
            var portfolioVariance = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    portfolioVariance += weights[i] * weights[j] * marketData.CovarianceMatrix[i, j];
                }
            }
            
            var portfolioVolatility = Math.Sqrt(portfolioVariance);
            
            // Expected returns (using historical average for HRP)
            var expectedReturns = marketData.PriceHistories.Values
                .Select(prices => CalculateAverageReturn(prices))
                .ToArray();
            
            var portfolioReturn = weights.Zip(expectedReturns, (w, r) => w * (double)r).Sum();
            
            // Diversification ratio
            var weightedVolatilities = 0.0;
            for (int i = 0; i < n; i++)
            {
                weightedVolatilities += weights[i] * (double)marketData.HistoricalVolatilities[i];
            }
            
            var diversificationRatio = weightedVolatilities / portfolioVolatility;
            
            // Effective number of assets (inverse HHI)
            var hhi = weights.Sum(w => w * w);
            var effectiveN = 1.0 / hhi;
            
            // Concentration ratio (top 5 assets)
            var top5Weight = weights.OrderByDescending(w => w).Take(5).Sum();
            
            return new HRPMetrics
            {
                ExpectedReturn = (decimal)portfolioReturn,
                Volatility = (decimal)portfolioVolatility,
                SharpeRatio = portfolioVolatility > 0 ? 
                    (decimal)((portfolioReturn - (double)_config.RiskFreeRate) / portfolioVolatility) : 0,
                DiversificationRatio = (decimal)diversificationRatio,
                EffectiveNumberOfAssets = (decimal)effectiveN,
                ConcentrationRatio = (decimal)top5Weight
            };
        }

        private decimal CalculateAverageReturn(decimal[] prices)
        {
            if (prices.Length < 2)
                return 0;
            
            var returns = new decimal[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
            {
                returns[i - 1] = (prices[i] - prices[i - 1]) / prices[i - 1];
            }
            
            return returns.Average() * 252; // Annualized
        }

        private Matrix<double> ExtractReturnsMatrix(MarketData marketData, List<Asset> assets)
        {
            var priceHistories = assets.Select(a => marketData.PriceHistories[a.Symbol]).ToList();
            var minLength = priceHistories.Min(p => p.Length);
            
            // Calculate returns
            var returnsMatrix = DenseMatrix.Create(minLength - 1, assets.Count, 0.0);
            
            for (int j = 0; j < assets.Count; j++)
            {
                var prices = priceHistories[j];
                for (int i = 1; i < minLength; i++)
                {
                    returnsMatrix[i - 1, j] = (double)((prices[i] - prices[i - 1]) / prices[i - 1]);
                }
            }
            
            return returnsMatrix;
        }

        private Dictionary<string, decimal> CreateWeightsDictionary(double[] weights, List<Asset> assets)
        {
            var result = new Dictionary<string, decimal>();
            for (int i = 0; i < weights.Length; i++)
            {
                result[assets[i].Symbol] = (decimal)weights[i];
            }
            return result;
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
            
            foreach (var asset in assets)
            {
                if (!marketData.PriceHistories.ContainsKey(asset.Symbol))
                    throw new ArgumentException($"Price history missing for asset {asset.Symbol}");
            }
        }
    }

    // Supporting classes
    public class HRPConfiguration
    {
        public LinkageMethod LinkageMethod { get; set; } = LinkageMethod.Single;
        public bool UseMLEnhancement { get; set; } = true;
        public decimal RiskFreeRate { get; set; } = 0.04m;
        public int MinClusterSize { get; set; } = 1;
        public double DistanceThreshold { get; set; } = 0.5;
    }

    public enum LinkageMethod
    {
        Single,    // Minimum distance
        Complete,  // Maximum distance
        Average,   // Average distance
        Ward       // Minimize within-cluster variance
    }

    public class LinkageMatrix
    {
        public List<LinkageStep> Steps { get; set; } = new();
    }

    public class LinkageStep
    {
        public int Cluster1 { get; set; }
        public int Cluster2 { get; set; }
        public double Distance { get; set; }
        public int NewClusterId { get; set; }
    }

    public class Cluster
    {
        public int Id { get; set; }
        public List<int> Members { get; set; } = new();
    }

    public class HRPMetrics : PortfolioMetrics
    {
        public decimal DiversificationRatio { get; set; }
        public decimal EffectiveNumberOfAssets { get; set; }
        public decimal ConcentrationRatio { get; set; }
    }
}
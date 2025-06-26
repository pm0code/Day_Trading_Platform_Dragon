// File: TradingPlatform.ML/Models/RandomForestRankingModel.cs

using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Ranking;

namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// Random Forest model for multi-factor stock ranking
    /// </summary>
    public class RandomForestRankingModel : CanonicalServiceBase, 
        IPredictiveModel<RankingFactors, RankingPrediction>
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private DataViewSchema? _modelSchema;
        private readonly object _modelLock = new object();
        private ModelMetadata? _metadata;
        
        // Model configuration
        private const int NumberOfTrees = 100;
        private const int NumberOfLeaves = 50;
        private const int MinimumExampleCountPerLeaf = 10;
        private const double LearningRate = 0.2;
        private const double FeatureFraction = 0.8;
        private const double BaggingFraction = 0.8;
        
        public RandomForestRankingModel(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            MLContext mlContext)
            : base(serviceProvider, logger, "RandomForestRankingModel")
        {
            _mlContext = mlContext;
        }
        
        /// <summary>
        /// Train the Random Forest ranking model
        /// </summary>
        public async Task<TradingResult<ModelTrainingResult>> TrainAsync(
            RankingDataset dataset,
            RankingTrainingOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Training Random Forest model with {dataset.Count} samples");
                    var startTime = DateTime.UtcNow;
                    
                    // Convert to ML.NET format
                    var mlData = ConvertToMLData(dataset);
                    
                    // Split data
                    var split = _mlContext.Data.TrainTestSplit(mlData, testFraction: options.ValidationSplit);
                    
                    // Define pipeline
                    var pipeline = BuildPipeline(options);
                    
                    // Train model
                    ITransformer trainedModel;
                    lock (_modelLock)
                    {
                        LogInfo("Starting Random Forest training...");
                        trainedModel = pipeline.Fit(split.TrainSet);
                        _model = trainedModel;
                        _modelSchema = split.TrainSet.Schema;
                    }
                    
                    // Evaluate on validation set
                    var predictions = trainedModel.Transform(split.TestSet);
                    var metrics = _mlContext.Regression.Evaluate(predictions);
                    
                    // Calculate additional metrics
                    var rankingMetrics = await CalculateRankingMetricsAsync(
                        trainedModel, split.TestSet, cancellationToken);
                    
                    // Extract feature importance
                    var featureImportance = ExtractFeatureImportance(trainedModel);
                    
                    var result = new ModelTrainingResult
                    {
                        ModelId = Guid.NewGuid().ToString(),
                        TrainingStartTime = startTime,
                        TrainingEndTime = DateTime.UtcNow,
                        Metrics = new Dictionary<string, double>
                        {
                            ["RMSE"] = metrics.RootMeanSquaredError,
                            ["MAE"] = metrics.MeanAbsoluteError,
                            ["R2"] = metrics.RSquared,
                            ["SpearmanCorrelation"] = rankingMetrics.SpearmanCorrelation,
                            ["KendallTau"] = rankingMetrics.KendallTau,
                            ["NDCG@10"] = rankingMetrics.NDCG10,
                            ["NDCG@20"] = rankingMetrics.NDCG20,
                            ["TopKAccuracy"] = rankingMetrics.TopKAccuracy
                        },
                        FeatureImportance = featureImportance
                    };
                    
                    // Update metadata
                    _metadata = new ModelMetadata
                    {
                        ModelType = "RandomForest_Ranking",
                        Version = "1.0",
                        CreatedAt = DateTime.UtcNow,
                        LastTrainedAt = DateTime.UtcNow,
                        TrainingMetrics = result.Metrics,
                        FeatureNames = GetFeatureNames(),
                        Parameters = new Dictionary<string, object>
                        {
                            ["NumberOfTrees"] = options.NumberOfTrees,
                            ["NumberOfLeaves"] = options.NumberOfLeaves,
                            ["LearningRate"] = options.LearningRate,
                            ["FeatureFraction"] = options.FeatureFraction
                        }
                    };
                    
                    LogInfo("Random Forest training completed", additionalData: result.Metrics);
                    RecordServiceMetric("ModelTrained", 1);
                    
                    return TradingResult<ModelTrainingResult>.Success(result);
                },
                nameof(TrainAsync));
        }
        
        /// <summary>
        /// Predict ranking score for a stock
        /// </summary>
        public async Task<TradingResult<RankingPrediction>> PredictAsync(
            RankingFactors input,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not loaded");
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    lock (_modelLock)
                    {
                        // Convert input to features
                        var features = ConvertToFeatures(input);
                        var predictionEngine = _mlContext.Model.CreatePredictionEngine<RankingFeatures, RankingScore>(_model);
                        
                        // Make prediction
                        var score = predictionEngine.Predict(features);
                        
                        // Create ranking prediction
                        var prediction = new RankingPrediction
                        {
                            Symbol = input.Symbol,
                            RankingScore = score.Score,
                            Confidence = CalculateConfidence(score, input),
                            FactorContributions = CalculateFactorContributions(features, score),
                            PredictionTime = DateTime.UtcNow,
                            ModelVersion = _metadata?.Version ?? "Unknown"
                        };
                        
                        // Determine recommendation
                        prediction.Recommendation = DetermineRecommendation(prediction.RankingScore);
                        prediction.StrengthLevel = DetermineStrength(prediction.RankingScore, prediction.Confidence);
                        
                        stopwatch.Stop();
                        RecordServiceMetric("PredictionLatency", stopwatch.ElapsedMilliseconds);
                        
                        return TradingResult<RankingPrediction>.Success(prediction);
                    }
                },
                nameof(PredictAsync));
        }
        
        /// <summary>
        /// Batch prediction for multiple stocks
        /// </summary>
        public async Task<TradingResult<List<RankingPrediction>>> PredictBatchAsync(
            List<RankingFactors> inputs,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not loaded");
                    
                    var predictions = new List<RankingPrediction>();
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Convert all inputs to features
                    var featuresList = inputs.Select(ConvertToFeatures).ToList();
                    var dataView = _mlContext.Data.LoadFromEnumerable(featuresList);
                    
                    lock (_modelLock)
                    {
                        // Transform batch
                        var transformedData = _model.Transform(dataView);
                        var scores = _mlContext.Data.CreateEnumerable<RankingScore>(
                            transformedData, reuseRowObject: false).ToList();
                        
                        // Create predictions
                        for (int i = 0; i < inputs.Count && i < scores.Count; i++)
                        {
                            var prediction = new RankingPrediction
                            {
                                Symbol = inputs[i].Symbol,
                                RankingScore = scores[i].Score,
                                Confidence = CalculateConfidence(scores[i], inputs[i]),
                                FactorContributions = CalculateFactorContributions(featuresList[i], scores[i]),
                                PredictionTime = DateTime.UtcNow,
                                ModelVersion = _metadata?.Version ?? "Unknown",
                                Recommendation = DetermineRecommendation(scores[i].Score),
                                StrengthLevel = DetermineStrength(scores[i].Score, 0.7f)
                            };
                            
                            predictions.Add(prediction);
                        }
                    }
                    
                    // Sort by ranking score
                    predictions = predictions.OrderByDescending(p => p.RankingScore).ToList();
                    
                    // Assign percentile ranks
                    for (int i = 0; i < predictions.Count; i++)
                    {
                        predictions[i].PercentileRank = (double)(predictions.Count - i) / predictions.Count * 100;
                    }
                    
                    stopwatch.Stop();
                    RecordServiceMetric("BatchPredictionLatency", stopwatch.ElapsedMilliseconds);
                    RecordServiceMetric("BatchPredictionSize", predictions.Count);
                    
                    return TradingResult<List<RankingPrediction>>.Success(predictions);
                },
                nameof(PredictBatchAsync));
        }
        
        /// <summary>
        /// Get feature importance rankings
        /// </summary>
        public async Task<TradingResult<FeatureImportanceReport>> GetFeatureImportanceAsync(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null)
                        throw new InvalidOperationException("Model not loaded");
                    
                    var importance = ExtractFeatureImportance(_model);
                    
                    var report = new FeatureImportanceReport
                    {
                        ModelId = _metadata?.ModelId ?? "Unknown",
                        Timestamp = DateTime.UtcNow,
                        FeatureImportance = importance
                            .OrderByDescending(kvp => kvp.Value)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        TopFeatures = importance
                            .OrderByDescending(kvp => kvp.Value)
                            .Take(10)
                            .Select(kvp => new FeatureInfo
                            {
                                Name = kvp.Key,
                                Importance = kvp.Value,
                                Category = GetFeatureCategory(kvp.Key),
                                Description = GetFeatureDescription(kvp.Key)
                            })
                            .ToList(),
                        FeatureCategories = importance
                            .GroupBy(kvp => GetFeatureCategory(kvp.Key))
                            .Select(g => new CategoryImportance
                            {
                                Category = g.Key,
                                TotalImportance = g.Sum(kvp => kvp.Value),
                                FeatureCount = g.Count()
                            })
                            .OrderByDescending(c => c.TotalImportance)
                            .ToList()
                    };
                    
                    return TradingResult<FeatureImportanceReport>.Success(report);
                },
                nameof(GetFeatureImportanceAsync));
        }
        
        /// <summary>
        /// Perform cross-validation
        /// </summary>
        public async Task<TradingResult<CrossValidationResult>> CrossValidateAsync(
            RankingDataset dataset,
            int numberOfFolds = 5,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    LogInfo($"Starting {numberOfFolds}-fold cross-validation");
                    
                    var mlData = ConvertToMLData(dataset);
                    var pipeline = BuildPipeline(new RankingTrainingOptions());
                    
                    // Perform cross-validation
                    var cvResults = _mlContext.Regression.CrossValidate(
                        mlData, 
                        pipeline, 
                        numberOfFolds: numberOfFolds);
                    
                    // Calculate metrics
                    var avgMetrics = new Dictionary<string, double>
                    {
                        ["RMSE"] = cvResults.Average(r => r.Metrics.RootMeanSquaredError),
                        ["MAE"] = cvResults.Average(r => r.Metrics.MeanAbsoluteError),
                        ["R2"] = cvResults.Average(r => r.Metrics.RSquared)
                    };
                    
                    var stdMetrics = new Dictionary<string, double>
                    {
                        ["RMSE_Std"] = CalculateStandardDeviation(cvResults.Select(r => r.Metrics.RootMeanSquaredError)),
                        ["MAE_Std"] = CalculateStandardDeviation(cvResults.Select(r => r.Metrics.MeanAbsoluteError)),
                        ["R2_Std"] = CalculateStandardDeviation(cvResults.Select(r => r.Metrics.RSquared))
                    };
                    
                    var result = new CrossValidationResult
                    {
                        NumberOfFolds = numberOfFolds,
                        AverageMetrics = avgMetrics,
                        StandardDeviations = stdMetrics,
                        FoldResults = cvResults.Select((r, i) => new FoldResult
                        {
                            FoldIndex = i,
                            Metrics = new Dictionary<string, double>
                            {
                                ["RMSE"] = r.Metrics.RootMeanSquaredError,
                                ["MAE"] = r.Metrics.MeanAbsoluteError,
                                ["R2"] = r.Metrics.RSquared
                            }
                        }).ToList()
                    };
                    
                    LogInfo("Cross-validation completed", additionalData: avgMetrics);
                    
                    return TradingResult<CrossValidationResult>.Success(result);
                },
                nameof(CrossValidateAsync));
        }
        
        /// <summary>
        /// Save model
        /// </summary>
        public async Task<TradingResult<bool>> SaveAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    if (_model == null || _modelSchema == null)
                        throw new InvalidOperationException("No model to save");
                    
                    lock (_modelLock)
                    {
                        // Save ML.NET model
                        _mlContext.Model.Save(_model, _modelSchema, path);
                        
                        // Save metadata
                        if (_metadata != null)
                        {
                            var metadataPath = Path.ChangeExtension(path, ".metadata.json");
                            var json = System.Text.Json.JsonSerializer.Serialize(_metadata);
                            File.WriteAllText(metadataPath, json);
                        }
                    }
                    
                    LogInfo($"Model saved to {path}");
                    return TradingResult<bool>.Success(true);
                },
                nameof(SaveAsync));
        }
        
        /// <summary>
        /// Load model
        /// </summary>
        public async Task<TradingResult<bool>> LoadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    lock (_modelLock)
                    {
                        // Load ML.NET model
                        _model = _mlContext.Model.Load(path, out _modelSchema);
                        
                        // Load metadata
                        var metadataPath = Path.ChangeExtension(path, ".metadata.json");
                        if (File.Exists(metadataPath))
                        {
                            var json = File.ReadAllText(metadataPath);
                            _metadata = System.Text.Json.JsonSerializer.Deserialize<ModelMetadata>(json);
                        }
                    }
                    
                    LogInfo($"Model loaded from {path}");
                    return TradingResult<bool>.Success(true);
                },
                nameof(LoadAsync));
        }
        
        // Helper methods
        
        private IEstimator<ITransformer> BuildPipeline(RankingTrainingOptions options)
        {
            // Feature concatenation
            var featureColumns = GetFeatureColumns();
            
            var pipeline = _mlContext.Transforms.Concatenate("Features", featureColumns)
                // Normalize features
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                // Random Forest trainer
                .Append(_mlContext.Regression.Trainers.FastForest(
                    new FastForestRegressionTrainer.Options
                    {
                        NumberOfTrees = options.NumberOfTrees,
                        NumberOfLeaves = options.NumberOfLeaves,
                        MinimumExampleCountPerLeaf = options.MinimumExampleCountPerLeaf,
                        LearningRate = options.LearningRate,
                        FeatureFraction = options.FeatureFraction,
                        BaggingFraction = options.BaggingFraction,
                        BaggingExampleCount = options.BaggingExampleCount,
                        FeatureSelectionSeed = 123,
                        LabelColumnName = "Label",
                        FeatureColumnName = "Features"
                    }));
            
            return pipeline;
        }
        
        private IDataView ConvertToMLData(RankingDataset dataset)
        {
            var features = dataset.Samples.Select(s => ConvertToFeatures(s.Factors)).ToList();
            
            // Add labels (ranking scores)
            for (int i = 0; i < features.Count && i < dataset.Samples.Count; i++)
            {
                features[i].Label = dataset.Samples[i].TargetScore;
            }
            
            return _mlContext.Data.LoadFromEnumerable(features);
        }
        
        private RankingFeatures ConvertToFeatures(RankingFactors factors)
        {
            var features = new RankingFeatures();
            
            // Technical factors
            if (factors.TechnicalFactors != null)
            {
                var tech = factors.TechnicalFactors;
                features.Momentum1M = (float)tech.Momentum1M;
                features.Momentum3M = (float)tech.Momentum3M;
                features.Momentum6M = (float)tech.Momentum6M;
                features.Volatility20D = (float)tech.Volatility20D;
                features.RSI = (float)tech.RSI;
                features.TrendStrength = (float)tech.TrendStrength;
                features.VolumeRatio = (float)tech.VolumeRatio;
            }
            
            // Fundamental factors
            if (factors.FundamentalFactors != null)
            {
                var fund = factors.FundamentalFactors;
                features.PriceToEarnings = (float)fund.PriceToEarnings;
                features.PriceToBook = (float)fund.PriceToBook;
                features.ROE = (float)fund.ROE;
                features.RevenueGrowth = (float)fund.RevenueGrowth;
                features.DebtToEquity = (float)fund.DebtToEquity;
                features.FreeCashFlowYield = (float)fund.FreeCashFlowYield;
            }
            
            // Sentiment factors
            if (factors.SentimentFactors != null)
            {
                var sent = factors.SentimentFactors;
                features.NewsScore = (float)sent.NewsScore;
                features.SocialScore = (float)sent.SocialScore;
                features.AnalystRating = (float)sent.AnalystRating;
                features.InsiderBuyRatio = (float)sent.InsiderBuyRatio;
            }
            
            // Quality factors
            if (factors.QualityFactors != null)
            {
                var qual = factors.QualityFactors;
                features.EarningsQuality = (float)qual.EarningsQuality;
                features.GrowthStability = (float)qual.GrowthStability;
                features.CapitalAllocationScore = (float)qual.CapitalAllocationScore;
            }
            
            // Risk factors
            if (factors.RiskFactors != null)
            {
                var risk = factors.RiskFactors;
                features.Beta = (float)risk.Beta;
                features.ValueAtRisk = (float)risk.ValueAtRisk;
                features.MaxDrawdown = (float)risk.MaxDrawdown;
            }
            
            return features;
        }
        
        private string[] GetFeatureColumns()
        {
            return new[]
            {
                // Technical
                nameof(RankingFeatures.Momentum1M),
                nameof(RankingFeatures.Momentum3M),
                nameof(RankingFeatures.Momentum6M),
                nameof(RankingFeatures.Volatility20D),
                nameof(RankingFeatures.RSI),
                nameof(RankingFeatures.TrendStrength),
                nameof(RankingFeatures.VolumeRatio),
                
                // Fundamental
                nameof(RankingFeatures.PriceToEarnings),
                nameof(RankingFeatures.PriceToBook),
                nameof(RankingFeatures.ROE),
                nameof(RankingFeatures.RevenueGrowth),
                nameof(RankingFeatures.DebtToEquity),
                nameof(RankingFeatures.FreeCashFlowYield),
                
                // Sentiment
                nameof(RankingFeatures.NewsScore),
                nameof(RankingFeatures.SocialScore),
                nameof(RankingFeatures.AnalystRating),
                nameof(RankingFeatures.InsiderBuyRatio),
                
                // Quality
                nameof(RankingFeatures.EarningsQuality),
                nameof(RankingFeatures.GrowthStability),
                nameof(RankingFeatures.CapitalAllocationScore),
                
                // Risk
                nameof(RankingFeatures.Beta),
                nameof(RankingFeatures.ValueAtRisk),
                nameof(RankingFeatures.MaxDrawdown)
            };
        }
        
        private List<string> GetFeatureNames()
        {
            return GetFeatureColumns().ToList();
        }
        
        private Dictionary<string, double> ExtractFeatureImportance(ITransformer model)
        {
            var importance = new Dictionary<string, double>();
            
            // Extract feature importance from Random Forest
            var treePredictor = model as ISingleFeaturePredictionTransformer<object>;
            if (treePredictor?.Model is FastForestRegressionModelParameters forestModel)
            {
                var featureNames = GetFeatureNames();
                var gains = forestModel.GetFeatureGains();
                
                for (int i = 0; i < featureNames.Count && i < gains.Length; i++)
                {
                    importance[featureNames[i]] = gains[i];
                }
                
                // Normalize to sum to 1
                var total = importance.Values.Sum();
                if (total > 0)
                {
                    var normalized = importance.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value / total);
                    importance = normalized;
                }
            }
            
            return importance;
        }
        
        private async Task<RankingMetrics> CalculateRankingMetricsAsync(
            ITransformer model,
            IDataView testData,
            CancellationToken cancellationToken)
        {
            var predictions = model.Transform(testData);
            
            var actualScores = _mlContext.Data.CreateEnumerable<RankingScore>(
                testData, reuseRowObject: false)
                .Select(r => r.Label)
                .ToList();
            
            var predictedScores = _mlContext.Data.CreateEnumerable<RankingScore>(
                predictions, reuseRowObject: false)
                .Select(r => r.Score)
                .ToList();
            
            return new RankingMetrics
            {
                SpearmanCorrelation = CalculateSpearmanCorrelation(actualScores, predictedScores),
                KendallTau = CalculateKendallTau(actualScores, predictedScores),
                NDCG10 = CalculateNDCG(actualScores, predictedScores, 10),
                NDCG20 = CalculateNDCG(actualScores, predictedScores, 20),
                TopKAccuracy = CalculateTopKAccuracy(actualScores, predictedScores, 10)
            };
        }
        
        private float CalculateConfidence(RankingScore score, RankingFactors factors)
        {
            // Base confidence on score magnitude and factor quality
            var scoreConfidence = Math.Min(Math.Abs(score.Score) / 100f, 1f);
            
            // Adjust for data completeness
            var dataCompleteness = 0f;
            var factorCount = 0;
            
            if (factors.TechnicalFactors != null) { dataCompleteness += 1; factorCount++; }
            if (factors.FundamentalFactors != null) { dataCompleteness += 1; factorCount++; }
            if (factors.SentimentFactors != null) { dataCompleteness += 1; factorCount++; }
            if (factors.QualityFactors != null) { dataCompleteness += 1; factorCount++; }
            
            dataCompleteness = factorCount > 0 ? dataCompleteness / 4f : 0;
            
            return (scoreConfidence + dataCompleteness) / 2f;
        }
        
        private Dictionary<string, double> CalculateFactorContributions(
            RankingFeatures features,
            RankingScore score)
        {
            // Simplified - in production would use SHAP values or similar
            var contributions = new Dictionary<string, double>();
            
            if (_metadata?.FeatureImportance != null)
            {
                foreach (var (feature, importance) in _metadata.FeatureImportance)
                {
                    contributions[feature] = importance * score.Score;
                }
            }
            
            return contributions;
        }
        
        private string DetermineRecommendation(float score)
        {
            return score switch
            {
                > 80 => "Strong Buy",
                > 60 => "Buy",
                > 40 => "Hold",
                > 20 => "Sell",
                _ => "Strong Sell"
            };
        }
        
        private string DetermineStrength(float score, float confidence)
        {
            var strength = score * confidence / 100f;
            
            return strength switch
            {
                > 0.8f => "Very Strong",
                > 0.6f => "Strong",
                > 0.4f => "Moderate",
                > 0.2f => "Weak",
                _ => "Very Weak"
            };
        }
        
        private string GetFeatureCategory(string featureName)
        {
            if (featureName.Contains("Momentum") || featureName.Contains("RSI") || 
                featureName.Contains("Trend") || featureName.Contains("Volume"))
                return "Technical";
            
            if (featureName.Contains("Price") || featureName.Contains("ROE") || 
                featureName.Contains("Revenue") || featureName.Contains("Debt"))
                return "Fundamental";
            
            if (featureName.Contains("News") || featureName.Contains("Social") || 
                featureName.Contains("Analyst") || featureName.Contains("Insider"))
                return "Sentiment";
            
            if (featureName.Contains("Earnings") || featureName.Contains("Growth") || 
                featureName.Contains("Capital"))
                return "Quality";
            
            if (featureName.Contains("Beta") || featureName.Contains("VaR") || 
                featureName.Contains("Drawdown"))
                return "Risk";
            
            return "Other";
        }
        
        private string GetFeatureDescription(string featureName)
        {
            var descriptions = new Dictionary<string, string>
            {
                ["Momentum1M"] = "1-month price momentum",
                ["Momentum3M"] = "3-month price momentum",
                ["RSI"] = "Relative Strength Index",
                ["PriceToEarnings"] = "Price to Earnings ratio",
                ["ROE"] = "Return on Equity",
                ["NewsScore"] = "Aggregate news sentiment score",
                ["Beta"] = "Market beta (systematic risk)",
                ["EarningsQuality"] = "Quality of reported earnings"
            };
            
            return descriptions.GetValueOrDefault(featureName, featureName);
        }
        
        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;
            
            var mean = list.Average();
            var sumSquares = list.Sum(v => Math.Pow(v - mean, 2));
            
            return Math.Sqrt(sumSquares / (list.Count - 1));
        }
        
        private double CalculateSpearmanCorrelation(List<float> actual, List<float> predicted)
        {
            // Simplified Spearman correlation
            var n = Math.Min(actual.Count, predicted.Count);
            if (n < 2) return 0;
            
            var actualRanks = GetRanks(actual);
            var predictedRanks = GetRanks(predicted);
            
            var sumSquaredDiff = 0.0;
            for (int i = 0; i < n; i++)
            {
                var diff = actualRanks[i] - predictedRanks[i];
                sumSquaredDiff += diff * diff;
            }
            
            return 1 - (6 * sumSquaredDiff) / (n * (n * n - 1));
        }
        
        private List<double> GetRanks(List<float> values)
        {
            var indexed = values.Select((v, i) => new { Value = v, Index = i })
                .OrderByDescending(x => x.Value)
                .Select((x, rank) => new { x.Index, Rank = rank + 1.0 })
                .OrderBy(x => x.Index)
                .Select(x => x.Rank)
                .ToList();
            
            return indexed;
        }
        
        private double CalculateKendallTau(List<float> actual, List<float> predicted)
        {
            // Simplified Kendall's Tau
            var n = Math.Min(actual.Count, predicted.Count);
            if (n < 2) return 0;
            
            var concordant = 0;
            var discordant = 0;
            
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var actualDiff = actual[i] - actual[j];
                    var predictedDiff = predicted[i] - predicted[j];
                    
                    if (actualDiff * predictedDiff > 0)
                        concordant++;
                    else if (actualDiff * predictedDiff < 0)
                        discordant++;
                }
            }
            
            var total = n * (n - 1) / 2;
            return (concordant - discordant) / (double)total;
        }
        
        private double CalculateNDCG(List<float> actual, List<float> predicted, int k)
        {
            // Normalized Discounted Cumulative Gain
            var n = Math.Min(actual.Count, predicted.Count);
            k = Math.Min(k, n);
            
            // Get top k indices by predicted scores
            var topKIndices = predicted
                .Select((score, index) => new { Score = score, Index = index })
                .OrderByDescending(x => x.Score)
                .Take(k)
                .Select(x => x.Index)
                .ToList();
            
            // Calculate DCG
            var dcg = 0.0;
            for (int i = 0; i < topKIndices.Count; i++)
            {
                var relevance = actual[topKIndices[i]];
                dcg += relevance / Math.Log(i + 2, 2);
            }
            
            // Calculate ideal DCG
            var idealScores = actual.OrderByDescending(x => x).Take(k).ToList();
            var idcg = 0.0;
            for (int i = 0; i < idealScores.Count; i++)
            {
                idcg += idealScores[i] / Math.Log(i + 2, 2);
            }
            
            return idcg > 0 ? dcg / idcg : 0;
        }
        
        private double CalculateTopKAccuracy(List<float> actual, List<float> predicted, int k)
        {
            // Percentage of true top-k items that appear in predicted top-k
            var n = Math.Min(actual.Count, predicted.Count);
            k = Math.Min(k, n);
            
            var actualTopK = actual
                .Select((score, index) => new { Score = score, Index = index })
                .OrderByDescending(x => x.Score)
                .Take(k)
                .Select(x => x.Index)
                .ToHashSet();
            
            var predictedTopK = predicted
                .Select((score, index) => new { Score = score, Index = index })
                .OrderByDescending(x => x.Score)
                .Take(k)
                .Select(x => x.Index)
                .ToHashSet();
            
            var overlap = actualTopK.Intersect(predictedTopK).Count();
            return (double)overlap / k;
        }
    }
    
    // Supporting classes
    
    public class RankingFeatures
    {
        [LoadColumn(0)]
        public float Label { get; set; }
        
        // Technical factors
        [LoadColumn(1)]
        public float Momentum1M { get; set; }
        
        [LoadColumn(2)]
        public float Momentum3M { get; set; }
        
        [LoadColumn(3)]
        public float Momentum6M { get; set; }
        
        [LoadColumn(4)]
        public float Volatility20D { get; set; }
        
        [LoadColumn(5)]
        public float RSI { get; set; }
        
        [LoadColumn(6)]
        public float TrendStrength { get; set; }
        
        [LoadColumn(7)]
        public float VolumeRatio { get; set; }
        
        // Fundamental factors
        [LoadColumn(8)]
        public float PriceToEarnings { get; set; }
        
        [LoadColumn(9)]
        public float PriceToBook { get; set; }
        
        [LoadColumn(10)]
        public float ROE { get; set; }
        
        [LoadColumn(11)]
        public float RevenueGrowth { get; set; }
        
        [LoadColumn(12)]
        public float DebtToEquity { get; set; }
        
        [LoadColumn(13)]
        public float FreeCashFlowYield { get; set; }
        
        // Sentiment factors
        [LoadColumn(14)]
        public float NewsScore { get; set; }
        
        [LoadColumn(15)]
        public float SocialScore { get; set; }
        
        [LoadColumn(16)]
        public float AnalystRating { get; set; }
        
        [LoadColumn(17)]
        public float InsiderBuyRatio { get; set; }
        
        // Quality factors
        [LoadColumn(18)]
        public float EarningsQuality { get; set; }
        
        [LoadColumn(19)]
        public float GrowthStability { get; set; }
        
        [LoadColumn(20)]
        public float CapitalAllocationScore { get; set; }
        
        // Risk factors
        [LoadColumn(21)]
        public float Beta { get; set; }
        
        [LoadColumn(22)]
        public float ValueAtRisk { get; set; }
        
        [LoadColumn(23)]
        public float MaxDrawdown { get; set; }
    }
    
    public class RankingScore
    {
        [ColumnName("Score")]
        public float Score { get; set; }
        
        [ColumnName("Label")]
        public float Label { get; set; }
    }
    
    public class RankingPrediction
    {
        public string Symbol { get; set; } = string.Empty;
        public float RankingScore { get; set; }
        public float Confidence { get; set; }
        public double PercentileRank { get; set; }
        public Dictionary<string, double> FactorContributions { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
        public string StrengthLevel { get; set; } = string.Empty;
        public DateTime PredictionTime { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
    }
    
    public class RankingDataset
    {
        public List<RankingSample> Samples { get; set; } = new();
        public int Count => Samples.Count;
    }
    
    public class RankingSample
    {
        public RankingFactors Factors { get; set; } = null!;
        public float TargetScore { get; set; }
    }
    
    public class RankingTrainingOptions
    {
        public int NumberOfTrees { get; set; } = 100;
        public int NumberOfLeaves { get; set; } = 50;
        public int MinimumExampleCountPerLeaf { get; set; } = 10;
        public double LearningRate { get; set; } = 0.2;
        public double FeatureFraction { get; set; } = 0.8;
        public double BaggingFraction { get; set; } = 0.8;
        public int BaggingExampleCount { get; set; } = 100;
        public float ValidationSplit { get; set; } = 0.2f;
    }
    
    public class RankingMetrics
    {
        public double SpearmanCorrelation { get; set; }
        public double KendallTau { get; set; }
        public double NDCG10 { get; set; }
        public double NDCG20 { get; set; }
        public double TopKAccuracy { get; set; }
    }
    
    public class FeatureImportanceReport
    {
        public string ModelId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> FeatureImportance { get; set; } = new();
        public List<FeatureInfo> TopFeatures { get; set; } = new();
        public List<CategoryImportance> FeatureCategories { get; set; } = new();
    }
    
    public class FeatureInfo
    {
        public string Name { get; set; } = string.Empty;
        public double Importance { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
    
    public class CategoryImportance
    {
        public string Category { get; set; } = string.Empty;
        public double TotalImportance { get; set; }
        public int FeatureCount { get; set; }
    }
    
    public class CrossValidationResult
    {
        public int NumberOfFolds { get; set; }
        public Dictionary<string, double> AverageMetrics { get; set; } = new();
        public Dictionary<string, double> StandardDeviations { get; set; } = new();
        public List<FoldResult> FoldResults { get; set; } = new();
    }
    
    public class FoldResult
    {
        public int FoldIndex { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
    }
}
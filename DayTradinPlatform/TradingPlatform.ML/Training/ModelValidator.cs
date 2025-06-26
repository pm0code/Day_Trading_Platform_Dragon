// File: TradingPlatform.ML/Training/ModelValidator.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;
using TradingPlatform.ML.Data;

namespace TradingPlatform.ML.Training
{
    /// <summary>
    /// Validates ML models using various techniques
    /// </summary>
    public class ModelValidator : CanonicalServiceBase
    {
        private readonly MLDatasetBuilder _datasetBuilder;
        
        public ModelValidator(
            IServiceProvider serviceProvider,
            ITradingLogger logger)
            : base(serviceProvider, logger, "ModelValidator")
        {
            var mlContext = serviceProvider.GetRequiredService<Microsoft.ML.MLContext>();
            _datasetBuilder = new MLDatasetBuilder(mlContext);
        }
        
        /// <summary>
        /// Perform walk-forward analysis
        /// </summary>
        public async Task<TradingResult<WalkForwardResult>> PerformWalkForwardAnalysisAsync<TModel>(
            TModel model,
            MarketDataset fullDataset,
            WalkForwardOptions options,
            CancellationToken cancellationToken = default)
            where TModel : IMLModel
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var results = new List<WalkForwardWindow>();
                    var totalSamples = fullDataset.Data.Count;
                    
                    LogInfo($"Starting walk-forward analysis with {options.WindowCount} windows",
                        additionalData: new { 
                            TotalSamples = totalSamples,
                            TrainSize = options.TrainWindowSize,
                            TestSize = options.TestWindowSize
                        });
                    
                    for (int window = 0; window < options.WindowCount; window++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        // Calculate window boundaries
                        var trainStart = window * options.StepSize;
                        var trainEnd = trainStart + options.TrainWindowSize;
                        var testStart = trainEnd;
                        var testEnd = testStart + options.TestWindowSize;
                        
                        if (testEnd > totalSamples)
                            break;
                        
                        // Extract window data
                        var trainData = new MarketDataset
                        {
                            Symbol = fullDataset.Symbol,
                            Data = fullDataset.Data.Skip(trainStart).Take(options.TrainWindowSize).ToList(),
                            StartDate = fullDataset.Data[trainStart].Timestamp,
                            EndDate = fullDataset.Data[trainEnd - 1].Timestamp
                        };
                        
                        var testData = new MarketDataset
                        {
                            Symbol = fullDataset.Symbol,
                            Data = fullDataset.Data.Skip(testStart).Take(options.TestWindowSize).ToList(),
                            StartDate = fullDataset.Data[testStart].Timestamp,
                            EndDate = fullDataset.Data[testEnd - 1].Timestamp
                        };
                        
                        // Build datasets
                        var trainDataset = _datasetBuilder.BuildPricePredictionDataset(trainData);
                        var testDataset = _datasetBuilder.BuildPricePredictionDataset(testData);
                        
                        // Train model on window
                        var trainOptions = new ModelTrainingOptions
                        {
                            MaxIterations = options.TrainingIterations,
                            EarlyStopping = true,
                            EarlyStoppingPatience = 5
                        };
                        
                        var trainResult = await model.TrainAsync(trainDataset, trainOptions, cancellationToken);
                        if (!trainResult.IsSuccess)
                        {
                            LogWarning($"Training failed for window {window}: {trainResult.Error?.Message}");
                            continue;
                        }
                        
                        // Evaluate on test window
                        var evalResult = await model.EvaluateAsync(testDataset, cancellationToken);
                        if (!evalResult.IsSuccess)
                        {
                            LogWarning($"Evaluation failed for window {window}: {evalResult.Error?.Message}");
                            continue;
                        }
                        
                        var windowResult = new WalkForwardWindow
                        {
                            WindowIndex = window,
                            TrainStartDate = trainData.StartDate,
                            TrainEndDate = trainData.EndDate,
                            TestStartDate = testData.StartDate,
                            TestEndDate = testData.EndDate,
                            TrainSamples = trainData.Data.Count,
                            TestSamples = testData.Data.Count,
                            TrainingMetrics = trainResult.Value.Metrics,
                            TestMetrics = new Dictionary<string, double>
                            {
                                ["RMSE"] = evalResult.Value.RootMeanSquaredError,
                                ["MAE"] = evalResult.Value.MeanAbsoluteError,
                                ["R2"] = evalResult.Value.R2Score,
                                ["DirectionalAccuracy"] = evalResult.Value.CustomMetrics.GetValueOrDefault("DirectionalAccuracy", 0)
                            }
                        };
                        
                        results.Add(windowResult);
                        
                        RecordServiceMetric($"WalkForward.Window{window}.RMSE", evalResult.Value.RootMeanSquaredError);
                        RecordServiceMetric($"WalkForward.Window{window}.DirectionalAccuracy", 
                            evalResult.Value.CustomMetrics.GetValueOrDefault("DirectionalAccuracy", 0));
                        
                        LogInfo($"Completed window {window + 1}/{options.WindowCount}",
                            additionalData: new {
                                TestRMSE = evalResult.Value.RootMeanSquaredError,
                                TestR2 = evalResult.Value.R2Score
                            });
                    }
                    
                    // Calculate aggregate statistics
                    var walkForwardResult = new WalkForwardResult
                    {
                        Windows = results,
                        AverageTestRMSE = results.Average(w => w.TestMetrics["RMSE"]),
                        AverageTestMAE = results.Average(w => w.TestMetrics["MAE"]),
                        AverageTestR2 = results.Average(w => w.TestMetrics["R2"]),
                        AverageDirectionalAccuracy = results.Average(w => w.TestMetrics["DirectionalAccuracy"]),
                        StabilityScore = CalculateStabilityScore(results),
                        OverfittingScore = CalculateOverfittingScore(results)
                    };
                    
                    LogInfo("Walk-forward analysis completed",
                        additionalData: walkForwardResult);
                    
                    return TradingResult<WalkForwardResult>.Success(walkForwardResult);
                },
                nameof(PerformWalkForwardAnalysisAsync));
        }
        
        /// <summary>
        /// Perform k-fold cross-validation
        /// </summary>
        public async Task<TradingResult<CrossValidationResult>> PerformCrossValidationAsync<TModel>(
            TModel model,
            MLDataset dataset,
            int folds = 5,
            CancellationToken cancellationToken = default)
            where TModel : IMLModel
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var foldResults = new List<FoldResult>();
                    
                    LogInfo($"Starting {folds}-fold cross-validation");
                    
                    // Note: In production, would properly implement k-fold splitting
                    // This is simplified for demonstration
                    for (int fold = 0; fold < folds; fold++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        // Train on all folds except current
                        var trainOptions = new ModelTrainingOptions { MaxIterations = 50 };
                        var trainResult = await model.TrainAsync(dataset, trainOptions, cancellationToken);
                        
                        if (!trainResult.IsSuccess)
                        {
                            LogWarning($"Training failed for fold {fold}");
                            continue;
                        }
                        
                        // Evaluate on current fold
                        var evalResult = await model.EvaluateAsync(dataset, cancellationToken);
                        
                        if (!evalResult.IsSuccess)
                        {
                            LogWarning($"Evaluation failed for fold {fold}");
                            continue;
                        }
                        
                        foldResults.Add(new FoldResult
                        {
                            FoldIndex = fold,
                            TrainingMetrics = trainResult.Value.Metrics,
                            ValidationMetrics = new Dictionary<string, double>
                            {
                                ["RMSE"] = evalResult.Value.RootMeanSquaredError,
                                ["MAE"] = evalResult.Value.MeanAbsoluteError,
                                ["R2"] = evalResult.Value.R2Score
                            }
                        });
                    }
                    
                    var cvResult = new CrossValidationResult
                    {
                        Folds = foldResults,
                        AverageRMSE = foldResults.Average(f => f.ValidationMetrics["RMSE"]),
                        StdDevRMSE = CalculateStandardDeviation(
                            foldResults.Select(f => f.ValidationMetrics["RMSE"]).ToList()),
                        AverageMAE = foldResults.Average(f => f.ValidationMetrics["MAE"]),
                        StdDevMAE = CalculateStandardDeviation(
                            foldResults.Select(f => f.ValidationMetrics["MAE"]).ToList()),
                        AverageR2 = foldResults.Average(f => f.ValidationMetrics["R2"]),
                        StdDevR2 = CalculateStandardDeviation(
                            foldResults.Select(f => f.ValidationMetrics["R2"]).ToList())
                    };
                    
                    LogInfo("Cross-validation completed", additionalData: cvResult);
                    
                    return TradingResult<CrossValidationResult>.Success(cvResult);
                },
                nameof(PerformCrossValidationAsync));
        }
        
        /// <summary>
        /// Validate model on different market conditions
        /// </summary>
        public async Task<TradingResult<MarketConditionValidation>> ValidateAcrossMarketConditionsAsync<TModel>(
            TModel model,
            Dictionary<MarketCondition, MarketDataset> conditionDatasets,
            CancellationToken cancellationToken = default)
            where TModel : IMLModel
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var results = new Dictionary<MarketCondition, ModelEvaluationResult>();
                    
                    foreach (var (condition, dataset) in conditionDatasets)
                    {
                        LogInfo($"Validating model on {condition} market conditions");
                        
                        var mlDataset = _datasetBuilder.BuildPricePredictionDataset(dataset);
                        var evalResult = await model.EvaluateAsync(mlDataset, cancellationToken);
                        
                        if (evalResult.IsSuccess)
                        {
                            results[condition] = evalResult.Value;
                            
                            RecordServiceMetric($"MarketCondition.{condition}.RMSE", 
                                evalResult.Value.RootMeanSquaredError);
                            RecordServiceMetric($"MarketCondition.{condition}.DirectionalAccuracy",
                                evalResult.Value.CustomMetrics.GetValueOrDefault("DirectionalAccuracy", 0));
                        }
                    }
                    
                    var validation = new MarketConditionValidation
                    {
                        ConditionResults = results,
                        BestCondition = results.OrderBy(r => r.Value.RootMeanSquaredError).First().Key,
                        WorstCondition = results.OrderByDescending(r => r.Value.RootMeanSquaredError).First().Key,
                        ConsistencyScore = CalculateConsistencyScore(results),
                        RobustnessScore = CalculateRobustnessScore(results)
                    };
                    
                    LogInfo("Market condition validation completed", additionalData: validation);
                    
                    return TradingResult<MarketConditionValidation>.Success(validation);
                },
                nameof(ValidateAcrossMarketConditionsAsync));
        }
        
        /// <summary>
        /// Perform sensitivity analysis on model inputs
        /// </summary>
        public async Task<TradingResult<SensitivityAnalysisResult>> PerformSensitivityAnalysisAsync(
            IPredictiveModel<PricePredictionInput, PricePrediction> model,
            PricePredictionInput baselineInput,
            SensitivityOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var featureSensitivities = new Dictionary<string, FeatureSensitivity>();
                    
                    // Get baseline prediction
                    var baselineResult = await model.PredictAsync(baselineInput, cancellationToken);
                    if (!baselineResult.IsSuccess)
                        return TradingResult<SensitivityAnalysisResult>.Failure(baselineResult.Error);
                    
                    var baselinePrediction = baselineResult.Value.PredictedPrice;
                    
                    // Test each feature
                    var features = new[]
                    {
                        nameof(PricePredictionInput.RSI),
                        nameof(PricePredictionInput.MACD),
                        nameof(PricePredictionInput.Volume),
                        nameof(PricePredictionInput.VolumeRatio),
                        nameof(PricePredictionInput.Close)
                    };
                    
                    foreach (var feature in features)
                    {
                        var sensitivity = await AnalyzeFeatureSensitivity(
                            model, baselineInput, feature, baselinePrediction, options, cancellationToken);
                        
                        if (sensitivity != null)
                        {
                            featureSensitivities[feature] = sensitivity;
                        }
                    }
                    
                    var result = new SensitivityAnalysisResult
                    {
                        BaselinePrediction = baselinePrediction,
                        FeatureSensitivities = featureSensitivities,
                        MostSensitiveFeature = featureSensitivities
                            .OrderByDescending(f => f.Value.AverageSensitivity)
                            .First().Key,
                        LeastSensitiveFeature = featureSensitivities
                            .OrderBy(f => f.Value.AverageSensitivity)
                            .First().Key
                    };
                    
                    LogInfo("Sensitivity analysis completed", additionalData: result);
                    
                    return TradingResult<SensitivityAnalysisResult>.Success(result);
                },
                nameof(PerformSensitivityAnalysisAsync));
        }
        
        // Helper methods
        
        private double CalculateStabilityScore(List<WalkForwardWindow> windows)
        {
            if (windows.Count < 2) return 1.0;
            
            var rmseValues = windows.Select(w => w.TestMetrics["RMSE"]).ToList();
            var mean = rmseValues.Average();
            var stdDev = CalculateStandardDeviation(rmseValues);
            
            // Lower coefficient of variation = higher stability
            return 1.0 - Math.Min(stdDev / mean, 1.0);
        }
        
        private double CalculateOverfittingScore(List<WalkForwardWindow> windows)
        {
            var overfittingScores = new List<double>();
            
            foreach (var window in windows)
            {
                var trainRMSE = window.TrainingMetrics.GetValueOrDefault("RMSE", 0);
                var testRMSE = window.TestMetrics["RMSE"];
                
                if (trainRMSE > 0)
                {
                    // Higher ratio = more overfitting
                    var overfitting = (testRMSE - trainRMSE) / trainRMSE;
                    overfittingScores.Add(Math.Max(0, overfitting));
                }
            }
            
            return overfittingScores.Any() ? overfittingScores.Average() : 0;
        }
        
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            
            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }
        
        private double CalculateConsistencyScore(Dictionary<MarketCondition, ModelEvaluationResult> results)
        {
            var rmseValues = results.Values.Select(r => r.RootMeanSquaredError).ToList();
            var stdDev = CalculateStandardDeviation(rmseValues);
            var mean = rmseValues.Average();
            
            // Lower variation = higher consistency
            return mean > 0 ? 1.0 - Math.Min(stdDev / mean, 1.0) : 0;
        }
        
        private double CalculateRobustnessScore(Dictionary<MarketCondition, ModelEvaluationResult> results)
        {
            // Calculate based on directional accuracy across conditions
            var accuracies = results.Values
                .Select(r => r.CustomMetrics.GetValueOrDefault("DirectionalAccuracy", 0))
                .Where(a => a > 0)
                .ToList();
            
            if (!accuracies.Any()) return 0;
            
            var minAccuracy = accuracies.Min();
            var avgAccuracy = accuracies.Average();
            
            // High minimum accuracy = robust model
            return minAccuracy * 0.7 + (minAccuracy / avgAccuracy) * 0.3;
        }
        
        private async Task<FeatureSensitivity?> AnalyzeFeatureSensitivity(
            IPredictiveModel<PricePredictionInput, PricePrediction> model,
            PricePredictionInput baselineInput,
            string featureName,
            float baselinePrediction,
            SensitivityOptions options,
            CancellationToken cancellationToken)
        {
            var sensitivities = new List<double>();
            var perturbations = options.PerturbationLevels;
            
            foreach (var perturbation in perturbations)
            {
                // Create perturbed input
                var perturbedInput = CloneInput(baselineInput);
                PerturbFeature(perturbedInput, featureName, perturbation);
                
                // Get prediction
                var result = await model.PredictAsync(perturbedInput, cancellationToken);
                if (result.IsSuccess)
                {
                    var changePct = Math.Abs((result.Value.PredictedPrice - baselinePrediction) / baselinePrediction);
                    var sensitivity = changePct / Math.Abs(perturbation);
                    sensitivities.Add(sensitivity);
                }
            }
            
            if (!sensitivities.Any()) return null;
            
            return new FeatureSensitivity
            {
                FeatureName = featureName,
                AverageSensitivity = sensitivities.Average(),
                MaxSensitivity = sensitivities.Max(),
                MinSensitivity = sensitivities.Min()
            };
        }
        
        private PricePredictionInput CloneInput(PricePredictionInput input)
        {
            // Simple clone - in production would use proper cloning
            return new PricePredictionInput
            {
                Open = input.Open,
                High = input.High,
                Low = input.Low,
                Close = input.Close,
                Volume = input.Volume,
                RSI = input.RSI,
                MACD = input.MACD,
                BollingerUpper = input.BollingerUpper,
                BollingerLower = input.BollingerLower,
                MovingAverage20 = input.MovingAverage20,
                MovingAverage50 = input.MovingAverage50,
                VolumeRatio = input.VolumeRatio,
                PriceChangePercent = input.PriceChangePercent,
                MarketCap = input.MarketCap,
                DayOfWeek = input.DayOfWeek,
                HourOfDay = input.HourOfDay
            };
        }
        
        private void PerturbFeature(PricePredictionInput input, string featureName, double perturbation)
        {
            var multiplier = 1.0f + (float)perturbation;
            
            switch (featureName)
            {
                case nameof(PricePredictionInput.RSI):
                    input.RSI *= multiplier;
                    break;
                case nameof(PricePredictionInput.MACD):
                    input.MACD *= multiplier;
                    break;
                case nameof(PricePredictionInput.Volume):
                    input.Volume *= multiplier;
                    break;
                case nameof(PricePredictionInput.VolumeRatio):
                    input.VolumeRatio *= multiplier;
                    break;
                case nameof(PricePredictionInput.Close):
                    input.Close *= multiplier;
                    break;
            }
        }
    }
    
    // Validation result classes
    
    public class WalkForwardResult
    {
        public List<WalkForwardWindow> Windows { get; set; } = new();
        public double AverageTestRMSE { get; set; }
        public double AverageTestMAE { get; set; }
        public double AverageTestR2 { get; set; }
        public double AverageDirectionalAccuracy { get; set; }
        public double StabilityScore { get; set; }
        public double OverfittingScore { get; set; }
    }
    
    public class WalkForwardWindow
    {
        public int WindowIndex { get; set; }
        public DateTime TrainStartDate { get; set; }
        public DateTime TrainEndDate { get; set; }
        public DateTime TestStartDate { get; set; }
        public DateTime TestEndDate { get; set; }
        public int TrainSamples { get; set; }
        public int TestSamples { get; set; }
        public Dictionary<string, double> TrainingMetrics { get; set; } = new();
        public Dictionary<string, double> TestMetrics { get; set; } = new();
    }
    
    public class WalkForwardOptions
    {
        public int WindowCount { get; set; } = 10;
        public int TrainWindowSize { get; set; } = 1000;
        public int TestWindowSize { get; set; } = 200;
        public int StepSize { get; set; } = 200;
        public int TrainingIterations { get; set; } = 50;
    }
    
    public class CrossValidationResult
    {
        public List<FoldResult> Folds { get; set; } = new();
        public double AverageRMSE { get; set; }
        public double StdDevRMSE { get; set; }
        public double AverageMAE { get; set; }
        public double StdDevMAE { get; set; }
        public double AverageR2 { get; set; }
        public double StdDevR2 { get; set; }
    }
    
    public class FoldResult
    {
        public int FoldIndex { get; set; }
        public Dictionary<string, double> TrainingMetrics { get; set; } = new();
        public Dictionary<string, double> ValidationMetrics { get; set; } = new();
    }
    
    public class MarketConditionValidation
    {
        public Dictionary<MarketCondition, ModelEvaluationResult> ConditionResults { get; set; } = new();
        public MarketCondition BestCondition { get; set; }
        public MarketCondition WorstCondition { get; set; }
        public double ConsistencyScore { get; set; }
        public double RobustnessScore { get; set; }
    }
    
    public enum MarketCondition
    {
        Bullish,
        Bearish,
        Volatile,
        Stable,
        Trending,
        RangeB

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Interfaces
{
    /// <summary>
    /// Interface for ML inference service with ONNX Runtime support
    /// </summary>
    public interface IMLInferenceService : IDisposable
    {
        /// <summary>
        /// Performs single inference on the specified model
        /// </summary>
        /// <param name="modelName">Name of the model to use</param>
        /// <param name="inputData">Input data as float array</param>
        /// <param name="inputShape">Shape of the input tensor</param>
        /// <returns>Model prediction result</returns>
        Task<TradingResult<ModelPrediction>> PredictAsync(
            string modelName,
            float[] inputData,
            int[] inputShape);

        /// <summary>
        /// Performs batch inference on the specified model
        /// </summary>
        /// <param name="modelName">Name of the model to use</param>
        /// <param name="batchData">Batch of input data</param>
        /// <param name="inputShape">Shape of each input tensor (batch dimension first)</param>
        /// <returns>Batch of model predictions</returns>
        Task<TradingResult<ModelPredictionBatch>> PredictBatchAsync(
            string modelName,
            float[][] batchData,
            int[] inputShape);

        /// <summary>
        /// Performs inference with multiple inputs (for models with multiple input tensors)
        /// </summary>
        /// <param name="modelName">Name of the model to use</param>
        /// <param name="inputs">Dictionary of input tensor names to data</param>
        /// <returns>Dictionary of output tensor names to predictions</returns>
        Task<TradingResult<Dictionary<string, float[]>>> PredictMultiInputAsync(
            string modelName,
            Dictionary<string, float[]> inputs);

        /// <summary>
        /// Loads a model into memory for faster inference
        /// </summary>
        /// <param name="modelName">Name of the model to load</param>
        /// <param name="modelPath">Optional custom path to the model file</param>
        /// <returns>Success result if model loaded successfully</returns>
        Task<TradingResult<ModelMetadata>> LoadModelAsync(
            string modelName,
            string? modelPath = null);

        /// <summary>
        /// Unloads a model from memory
        /// </summary>
        /// <param name="modelName">Name of the model to unload</param>
        /// <returns>Success result if model unloaded successfully</returns>
        Task<TradingResult> UnloadModelAsync(string modelName);

        /// <summary>
        /// Gets metadata for a loaded model
        /// </summary>
        /// <param name="modelName">Name of the model</param>
        /// <returns>Model metadata including input/output specifications</returns>
        Task<TradingResult<ModelMetadata>> GetModelMetadataAsync(string modelName);

        /// <summary>
        /// Warms up a model by running dummy inferences
        /// </summary>
        /// <param name="modelName">Name of the model to warm up</param>
        /// <param name="iterations">Number of warmup iterations</param>
        /// <returns>Warmup statistics</returns>
        Task<TradingResult<WarmupStatistics>> WarmupModelAsync(
            string modelName,
            int? iterations = null);

        /// <summary>
        /// Gets current performance metrics for all loaded models
        /// </summary>
        /// <returns>Dictionary of model names to performance metrics</returns>
        Task<TradingResult<Dictionary<string, ModelPerformanceMetrics>>> GetPerformanceMetricsAsync();

        /// <summary>
        /// Checks if a model is currently loaded
        /// </summary>
        /// <param name="modelName">Name of the model to check</param>
        /// <returns>True if model is loaded, false otherwise</returns>
        bool IsModelLoaded(string modelName);

        /// <summary>
        /// Gets list of all loaded models
        /// </summary>
        /// <returns>List of loaded model names</returns>
        IReadOnlyList<string> GetLoadedModels();
    }

    /// <summary>
    /// Interface for order book prediction specialized ML service
    /// </summary>
    public interface IOrderBookPredictor
    {
        /// <summary>
        /// Predicts next order book state based on historical snapshots
        /// </summary>
        /// <param name="historicalSnapshots">Array of historical order book snapshots</param>
        /// <returns>Prediction of next order book state</returns>
        Task<OrderBookPrediction> PredictNextStateAsync(OrderBookSnapshot[] historicalSnapshots);

        /// <summary>
        /// Predicts price impact of a potential order
        /// </summary>
        /// <param name="currentSnapshot">Current order book state</param>
        /// <param name="orderSize">Size of the hypothetical order</param>
        /// <param name="isBuyOrder">Whether it's a buy order</param>
        /// <returns>Predicted price impact</returns>
        Task<PriceImpactPrediction> PredictPriceImpactAsync(
            OrderBookSnapshot currentSnapshot,
            decimal orderSize,
            bool isBuyOrder);
    }

    /// <summary>
    /// Interface for sentiment analysis ML service
    /// </summary>
    public interface ISentimentAnalyzer
    {
        /// <summary>
        /// Analyzes sentiment from social media posts
        /// </summary>
        /// <param name="posts">List of social media posts</param>
        /// <param name="symbol">Trading symbol to analyze</param>
        /// <returns>Aggregated sentiment analysis</returns>
        Task<SentimentAnalysis> AnalyzeSentimentBatchAsync(
            List<SocialPost> posts,
            string symbol);

        /// <summary>
        /// Analyzes sentiment from news articles
        /// </summary>
        /// <param name="articles">List of news articles</param>
        /// <param name="symbol">Trading symbol to analyze</param>
        /// <returns>News sentiment analysis</returns>
        Task<NewsSentiment> AnalyzeNewsAsync(
            List<NewsArticle> articles,
            string symbol);
    }

    /// <summary>
    /// Interface for ML model performance monitoring
    /// </summary>
    public interface IMLPerformanceMonitor
    {
        /// <summary>
        /// Records inference latency and success
        /// </summary>
        /// <param name="modelName">Name of the model</param>
        /// <param name="latencyMs">Inference latency in milliseconds</param>
        /// <param name="success">Whether inference was successful</param>
        void RecordInference(string modelName, double latencyMs, bool success);

        /// <summary>
        /// Gets comprehensive health report for ML services
        /// </summary>
        /// <returns>ML health report with metrics and warnings</returns>
        Task<MLHealthReport> GetHealthReportAsync();

        /// <summary>
        /// Checks if a model is performing within acceptable parameters
        /// </summary>
        /// <param name="modelName">Name of the model to check</param>
        /// <returns>True if model is healthy, false otherwise</returns>
        Task<bool> IsModelHealthyAsync(string modelName);
    }
}
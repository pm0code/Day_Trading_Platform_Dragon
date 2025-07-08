using MarketAnalyzer.Domain.Entities;
using MarketAnalyzer.Foundation;
using MarketAnalyzer.Infrastructure.AI.Models;

namespace MarketAnalyzer.Infrastructure.AI.Services;

/// <summary>
/// Defines the contract for ML inference services.
/// Supports ONNX Runtime, ML.NET, and TorchSharp models.
/// </summary>
public interface IMLInferenceService
{
    /// <summary>
    /// Performs inference using the specified ONNX model.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    /// <param name="inputData">The input data array</param>
    /// <param name="inputShape">The shape of the input tensor</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing the prediction</returns>
    Task<TradingResult<ModelPrediction>> PredictAsync(
        string modelName, 
        float[] inputData, 
        int[] inputShape,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs batch inference for multiple inputs.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    /// <param name="batchInputs">The batch of inputs</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing batch predictions</returns>
    Task<TradingResult<BatchPrediction>> PredictBatchAsync(
        string modelName,
        BatchInput batchInputs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Predicts price movement for a stock using time series model.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="historicalPrices">Historical price data</param>
    /// <param name="horizon">Prediction horizon in minutes</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing price predictions</returns>
    Task<TradingResult<PricePrediction>> PredictPriceMovementAsync(
        string symbol,
        decimal[] historicalPrices,
        int horizon,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes market sentiment using NLP model.
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing sentiment scores</returns>
    Task<TradingResult<SentimentAnalysis>> AnalyzeSentimentAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects patterns in market data using computer vision model.
    /// </summary>
    /// <param name="priceData">Price chart data as 2D array</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing detected patterns</returns>
    Task<TradingResult<PatternDetection>> DetectPatternsAsync(
        float[,] priceData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates trading signals using ensemble model.
    /// </summary>
    /// <param name="marketData">Current market data</param>
    /// <param name="technicalIndicators">Technical indicators</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing trading signals</returns>
    Task<TradingResult<TradingSignalPrediction>> GenerateTradingSignalsAsync(
        MarketQuote marketData,
        Dictionary<string, decimal> technicalIndicators,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates risk score using ML model.
    /// </summary>
    /// <param name="position">The trading position</param>
    /// <param name="marketConditions">Current market conditions</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result containing risk assessment</returns>
    Task<TradingResult<RiskAssessment>> AssessRiskAsync(
        TradingPosition position,
        MarketConditions marketConditions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a model into memory for faster inference.
    /// </summary>
    /// <param name="modelName">The name of the model to load</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A trading result indicating success</returns>
    Task<TradingResult<bool>> PreloadModelAsync(
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a model from memory.
    /// </summary>
    /// <param name="modelName">The name of the model to unload</param>
    /// <returns>A trading result indicating success</returns>
    TradingResult<bool> UnloadModel(string modelName);

    /// <summary>
    /// Gets model metadata and performance statistics.
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    /// <returns>A trading result containing model info</returns>
    TradingResult<ModelInfo> GetModelInfo(string modelName);

    /// <summary>
    /// Gets the health status of the ML inference service.
    /// </summary>
    /// <returns>A trading result containing health status</returns>
    Task<TradingResult<MLHealthStatus>> GetMLHealthAsync();
}
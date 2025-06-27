// File: TradingPlatform.ML/Interfaces/IMLModel.cs

using TradingPlatform.Foundation.Models;

namespace TradingPlatform.ML.Interfaces
{
    /// <summary>
    /// Base interface for all machine learning models in the trading platform
    /// </summary>
    public interface IMLModel
    {
        /// <summary>
        /// Unique identifier for the model
        /// </summary>
        string ModelId { get; }
        
        /// <summary>
        /// Model version for tracking iterations
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Model type (e.g., XGBoost, LSTM, RandomForest)
        /// </summary>
        string ModelType { get; }
        
        /// <summary>
        /// When the model was last trained
        /// </summary>
        DateTime LastTrainedAt { get; }
        
        /// <summary>
        /// Model performance metrics
        /// </summary>
        Dictionary<string, decimal> Metrics { get; }
        
        /// <summary>
        /// Train the model with the provided data
        /// </summary>
        Task<TradingResult<ModelTrainingResult>> TrainAsync(
            IMLDataset dataset,
            ModelTrainingOptions options,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Evaluate model performance on test data
        /// </summary>
        Task<TradingResult<ModelEvaluationResult>> EvaluateAsync(
            IMLDataset testData,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Save the model to persistent storage
        /// </summary>
        Task<TradingResult> SaveAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Load the model from persistent storage
        /// </summary>
        Task<TradingResult> LoadAsync(string path, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Export model to ONNX format for interoperability
        /// </summary>
        Task<TradingResult<byte[]>> ExportToOnnxAsync(CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface for models that support prediction
    /// </summary>
    public interface IPredictiveModel<TInput, TOutput> : IMLModel
        where TInput : class
        where TOutput : class
    {
        /// <summary>
        /// Make a single prediction
        /// </summary>
        Task<TradingResult<TOutput>> PredictAsync(
            TInput input,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Make batch predictions
        /// </summary>
        Task<TradingResult<IEnumerable<TOutput>>> PredictBatchAsync(
            IEnumerable<TInput> inputs,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get prediction confidence/probability
        /// </summary>
        Task<TradingResult<PredictionConfidence>> GetConfidenceAsync(
            TInput input,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface for explainable models
    /// </summary>
    public interface IExplainableModel : IMLModel
    {
        /// <summary>
        /// Get feature importance scores
        /// </summary>
        Task<TradingResult<Dictionary<string, decimal>>> GetFeatureImportanceAsync(
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get SHAP values for a prediction
        /// </summary>
        Task<TradingResult<ShapExplanation>> ExplainPredictionAsync(
            object input,
            CancellationToken cancellationToken = default);
    }
}
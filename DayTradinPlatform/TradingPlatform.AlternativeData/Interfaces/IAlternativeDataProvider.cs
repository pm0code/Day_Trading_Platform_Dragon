using TradingPlatform.AlternativeData.Models;
using TradingPlatform.Core.Models;

namespace TradingPlatform.AlternativeData.Interfaces;

public interface IAlternativeDataProvider
{
    string ProviderId { get; }
    AlternativeDataType DataType { get; }
    bool IsActive { get; }
    
    Task<TradingResult<AlternativeDataResponse>> GetDataAsync(
        AlternativeDataRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<DataProviderHealth>> GetHealthAsync(
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> EstimateCostAsync(
        AlternativeDataRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> ValidateConfigurationAsync(
        CancellationToken cancellationToken = default);
}

public interface ISatelliteDataProvider : IAlternativeDataProvider
{
    Task<TradingResult<List<SatelliteDataPoint>>> GetSatelliteImageryAsync(
        decimal latitude,
        decimal longitude,
        decimal radius,
        DateTime startTime,
        DateTime endTime,
        ImageQuality minQuality = ImageQuality.Medium,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<SatelliteAnalysisResult>> AnalyzeEconomicActivityAsync(
        SatelliteDataPoint satelliteData,
        List<string> targetSymbols,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<AlternativeDataSignal>>> DetectActivityChangesAsync(
        List<SatelliteDataPoint> historicalData,
        SatelliteDataPoint currentData,
        CancellationToken cancellationToken = default);
}

public interface ISocialMediaProvider : IAlternativeDataProvider
{
    Task<TradingResult<List<SocialMediaPost>>> GetPostsAsync(
        List<string> symbols,
        DateTime startTime,
        DateTime endTime,
        int maxPosts = 1000,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<SentimentAnalysisResult>> AnalyzeSentimentAsync(
        SocialMediaPost post,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<AlternativeDataSignal>>> AggregateSymbolSentimentAsync(
        string symbol,
        List<SentimentAnalysisResult> sentimentResults,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateInfluenceScoreAsync(
        SocialMediaPost post,
        CancellationToken cancellationToken = default);
}

public interface IEconomicDataProvider : IAlternativeDataProvider
{
    Task<TradingResult<List<EconomicIndicatorData>>> GetEconomicIndicatorsAsync(
        List<string> indicatorIds,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<AlternativeDataSignal>>> AnalyzeIndicatorImpactAsync(
        EconomicIndicatorData indicator,
        List<string> targetSymbols,
        CancellationToken cancellationToken = default);
}

public interface IAIModelService
{
    string ModelName { get; }
    string ModelType { get; }
    bool RequiresGPU { get; }
    
    Task<TradingResult<bool>> InitializeAsync(
        AIModelConfig config,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<Dictionary<string, object>>> ProcessAsync(
        byte[] inputData,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<Dictionary<string, object>>>> ProcessBatchAsync(
        List<byte[]> inputDataBatch,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> ValidateModelAsync(
        CancellationToken cancellationToken = default);
    
    Task DisposeAsync();
}

public interface IProphetTimeSeriesService : IAIModelService
{
    Task<TradingResult<List<decimal>>> ForecastAsync(
        List<(DateTime timestamp, decimal value)> timeSeries,
        int periodsAhead,
        bool includeSeasonality = true,
        bool includeHolidays = true,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<Dictionary<string, decimal>>> DetectAnomaliesAsync(
        List<(DateTime timestamp, decimal value)> timeSeries,
        decimal threshold = 0.95m,
        CancellationToken cancellationToken = default);
}

public interface INeuralProphetService : IAIModelService
{
    Task<TradingResult<List<decimal>>> ForecastWithCovariatesAsync(
        List<(DateTime timestamp, decimal value)> timeSeries,
        List<(DateTime timestamp, Dictionary<string, decimal> covariates)> externalData,
        int periodsAhead,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<Dictionary<string, decimal>>> AnalyzeComponentsAsync(
        List<(DateTime timestamp, decimal value)> timeSeries,
        CancellationToken cancellationToken = default);
}

public interface IFinRLTradingService : IAIModelService
{
    Task<TradingResult<List<(string action, decimal confidence)>>> GetTradingSignalsAsync(
        Dictionary<string, List<decimal>> marketData,
        Dictionary<string, List<decimal>> alternativeData,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> TrainModelAsync(
        Dictionary<string, List<decimal>> historicalMarketData,
        Dictionary<string, List<decimal>> historicalAlternativeData,
        Dictionary<string, List<decimal>> historicalReturns,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> BacktestStrategyAsync(
        Dictionary<string, List<decimal>> testMarketData,
        Dictionary<string, List<decimal>> testAlternativeData,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

public interface IAlternativeDataHub
{
    Task<TradingResult<AlternativeDataResponse>> RequestDataAsync(
        AlternativeDataRequest request,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<AlternativeDataSignal>>> GetActiveSignalsAsync(
        List<string>? symbols = null,
        List<AlternativeDataType>? dataTypes = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<AlternativeDataMetrics>> GetMetricsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<DataProviderHealth>>> GetProviderHealthAsync(
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> RegisterProviderAsync(
        IAlternativeDataProvider provider,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> UnregisterProviderAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}

public interface ISignalValidationService
{
    Task<TradingResult<SignalValidationResult>> ValidateSignalAsync(
        AlternativeDataSignal signal,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<SignalValidationResult>>> ValidateSignalsAsync(
        List<AlternativeDataSignal> signals,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<decimal>> CalculateSignalQualityScoreAsync(
        AlternativeDataSignal signal,
        List<BacktestResult>? historicalResults = null,
        CancellationToken cancellationToken = default);
}

public interface IDataProcessingPipeline
{
    Task<TradingResult<DataProcessingTask>> SubmitTaskAsync(
        AlternativeDataType dataType,
        string aiModelName,
        byte[] inputData,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<DataProcessingTask>> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<List<DataProcessingTask>>> GetQueueStatusAsync(
        CancellationToken cancellationToken = default);
    
    Task<TradingResult<bool>> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
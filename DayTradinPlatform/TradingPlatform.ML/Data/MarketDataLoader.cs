// File: TradingPlatform.ML/Data/MarketDataLoader.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Data
{
    /// <summary>
    /// Loads and prepares market data for ML training
    /// </summary>
    public class MarketDataLoader
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMarketDataService _marketDataService;
        
        public MarketDataLoader(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _marketDataService = serviceProvider.GetRequiredService<IMarketDataService>();
        }
        
        /// <summary>
        /// Load historical market data for training
        /// </summary>
        public async Task<TradingResult<MarketDataset>> LoadHistoricalDataAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            TimeSpan? interval = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Default to 1-minute intervals for day trading
                var dataInterval = interval ?? TimeSpan.FromMinutes(1);
                
                // Load market data
                var result = await _marketDataService.GetHistoricalDataAsync(
                    symbol, startDate, endDate, cancellationToken);
                
                if (!result.IsSuccess)
                {
                    return TradingResult<MarketDataset>.Failure(result.Error);
                }
                
                var marketData = result.Value.ToList();
                
                // Validate data quality
                var validationResult = ValidateMarketData(marketData);
                if (!validationResult.IsValid)
                {
                    return TradingResult<MarketDataset>.Failure(
                        new TradingError("ML002", $"Data validation failed: {validationResult.Message}"));
                }
                
                // Handle missing data
                var cleanedData = HandleMissingData(marketData);
                
                // Create dataset
                var dataset = new MarketDataset
                {
                    Symbol = symbol,
                    StartDate = startDate,
                    EndDate = endDate,
                    Interval = dataInterval,
                    Data = cleanedData,
                    SampleCount = cleanedData.Count,
                    CreatedAt = DateTime.UtcNow
                };
                
                return TradingResult<MarketDataset>.Success(dataset);
            }
            catch (Exception ex)
            {
                return TradingResult<MarketDataset>.Failure(TradingError.System(ex));
            }
        }
        
        /// <summary>
        /// Load multiple symbols for cross-sectional analysis
        /// </summary>
        public async Task<TradingResult<MultiSymbolDataset>> LoadMultiSymbolDataAsync(
            IList<string> symbols,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var datasets = new Dictionary<string, MarketDataset>();
            var errors = new List<string>();
            
            // Load data for each symbol in parallel
            var tasks = symbols.Select(async symbol =>
            {
                var result = await LoadHistoricalDataAsync(symbol, startDate, endDate, cancellationToken: cancellationToken);
                return (symbol, result);
            }).ToList();
            
            var results = await Task.WhenAll(tasks);
            
            foreach (var (symbol, result) in results)
            {
                if (result.IsSuccess)
                {
                    datasets[symbol] = result.Value;
                }
                else
                {
                    errors.Add($"{symbol}: {result.Error?.Message}");
                }
            }
            
            if (datasets.Count == 0)
            {
                return TradingResult<MultiSymbolDataset>.Failure(
                    new TradingError("ML003", $"Failed to load any symbols. Errors: {string.Join("; ", errors)}"));
            }
            
            var multiDataset = new MultiSymbolDataset
            {
                Symbols = datasets.Keys.ToList(),
                Datasets = datasets,
                StartDate = startDate,
                EndDate = endDate,
                CreatedAt = DateTime.UtcNow
            };
            
            return TradingResult<MultiSymbolDataset>.Success(multiDataset);
        }
        
        /// <summary>
        /// Load real-time streaming data for inference
        /// </summary>
        public async Task<IAsyncEnumerable<MarketDataSnapshot>> StreamRealTimeDataAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            await foreach (var snapshot in _marketDataService.StreamRealTimeDataAsync(symbol, cancellationToken))
            {
                yield return snapshot;
            }
        }
        
        /// <summary>
        /// Split dataset for training and validation
        /// </summary>
        public (MarketDataset train, MarketDataset validation, MarketDataset test) SplitDataset(
            MarketDataset dataset,
            decimal trainRatio = 0.7m,
            decimal validationRatio = 0.15m)
        {
            var totalSamples = dataset.Data.Count;
            var trainSize = (int)(totalSamples * trainRatio);
            var validationSize = (int)(totalSamples * validationRatio);
            var testSize = totalSamples - trainSize - validationSize;
            
            var trainData = dataset.Data.Take(trainSize).ToList();
            var validationData = dataset.Data.Skip(trainSize).Take(validationSize).ToList();
            var testData = dataset.Data.Skip(trainSize + validationSize).ToList();
            
            return (
                new MarketDataset 
                { 
                    Symbol = dataset.Symbol,
                    Data = trainData,
                    SampleCount = trainData.Count,
                    StartDate = trainData.First().Timestamp,
                    EndDate = trainData.Last().Timestamp,
                    Interval = dataset.Interval,
                    CreatedAt = DateTime.UtcNow
                },
                new MarketDataset 
                { 
                    Symbol = dataset.Symbol,
                    Data = validationData,
                    SampleCount = validationData.Count,
                    StartDate = validationData.First().Timestamp,
                    EndDate = validationData.Last().Timestamp,
                    Interval = dataset.Interval,
                    CreatedAt = DateTime.UtcNow
                },
                new MarketDataset 
                { 
                    Symbol = dataset.Symbol,
                    Data = testData,
                    SampleCount = testData.Count,
                    StartDate = testData.First().Timestamp,
                    EndDate = testData.Last().Timestamp,
                    Interval = dataset.Interval,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
        
        /// <summary>
        /// Create time series windows for sequential models
        /// </summary>
        public IList<TimeSeriesWindow> CreateTimeSeriesWindows(
            MarketDataset dataset,
            int windowSize,
            int stepSize = 1,
            int predictionHorizon = 1)
        {
            var windows = new List<TimeSeriesWindow>();
            
            for (int i = 0; i <= dataset.Data.Count - windowSize - predictionHorizon; i += stepSize)
            {
                var inputData = dataset.Data.Skip(i).Take(windowSize).ToList();
                var targetData = dataset.Data.Skip(i + windowSize).Take(predictionHorizon).ToList();
                
                windows.Add(new TimeSeriesWindow
                {
                    WindowId = i,
                    InputSequence = inputData,
                    TargetSequence = targetData,
                    StartTime = inputData.First().Timestamp,
                    EndTime = targetData.Last().Timestamp
                });
            }
            
            return windows;
        }
        
        /// <summary>
        /// Validate market data quality
        /// </summary>
        private DataValidationResult ValidateMarketData(IList<MarketDataSnapshot> data)
        {
            if (data.Count < 100)
            {
                return new DataValidationResult 
                { 
                    IsValid = false, 
                    Message = "Insufficient data points (minimum 100 required)" 
                };
            }
            
            // Check for data gaps
            var gaps = 0;
            for (int i = 1; i < data.Count; i++)
            {
                var timeDiff = data[i].Timestamp - data[i-1].Timestamp;
                if (timeDiff > TimeSpan.FromMinutes(5)) // More than 5 minutes gap
                {
                    gaps++;
                }
            }
            
            if (gaps > data.Count * 0.05) // More than 5% gaps
            {
                return new DataValidationResult 
                { 
                    IsValid = false, 
                    Message = $"Too many data gaps ({gaps} gaps found)" 
                };
            }
            
            // Check for invalid values
            var invalidCount = data.Count(d => 
                d.Open <= 0 || d.High <= 0 || d.Low <= 0 || d.Close <= 0 || 
                d.Volume < 0 || d.High < d.Low || 
                d.High < d.Open || d.High < d.Close ||
                d.Low > d.Open || d.Low > d.Close);
            
            if (invalidCount > 0)
            {
                return new DataValidationResult 
                { 
                    IsValid = false, 
                    Message = $"Invalid price/volume values found ({invalidCount} records)" 
                };
            }
            
            return new DataValidationResult { IsValid = true };
        }
        
        /// <summary>
        /// Handle missing data points
        /// </summary>
        private List<MarketDataSnapshot> HandleMissingData(IList<MarketDataSnapshot> data)
        {
            var cleanedData = new List<MarketDataSnapshot>();
            
            for (int i = 0; i < data.Count; i++)
            {
                var current = data[i];
                
                // Handle missing bid/ask
                if (!current.Bid.HasValue && i > 0)
                {
                    current.Bid = data[i-1].Bid ?? current.Close * 0.9999m;
                }
                
                if (!current.Ask.HasValue && i > 0)
                {
                    current.Ask = data[i-1].Ask ?? current.Close * 1.0001m;
                }
                
                // Handle missing sizes
                if (!current.BidSize.HasValue)
                {
                    current.BidSize = 100; // Default size
                }
                
                if (!current.AskSize.HasValue)
                {
                    current.AskSize = 100; // Default size
                }
                
                cleanedData.Add(current);
            }
            
            return cleanedData;
        }
    }
    
    /// <summary>
    /// Market dataset for ML training
    /// </summary>
    public class MarketDataset : IMLDataset
    {
        public string Symbol { get; set; } = string.Empty;
        public List<MarketDataSnapshot> Data { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Interval { get; set; }
        
        // IMLDataset implementation
        public string Name => $"{Symbol}_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}";
        public int SampleCount { get; set; }
        public DateTime CreatedAt { get; set; }
        
        public IDataView GetDataView()
        {
            // This will be implemented when integrating with ML.NET
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Multi-symbol dataset for cross-sectional analysis
    /// </summary>
    public class MultiSymbolDataset
    {
        public List<string> Symbols { get; set; } = new();
        public Dictionary<string, MarketDataset> Datasets { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    /// <summary>
    /// Time series window for sequential models
    /// </summary>
    public class TimeSeriesWindow
    {
        public int WindowId { get; set; }
        public List<MarketDataSnapshot> InputSequence { get; set; } = new();
        public List<MarketDataSnapshot> TargetSequence { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
    
    /// <summary>
    /// Data validation result
    /// </summary>
    public class DataValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
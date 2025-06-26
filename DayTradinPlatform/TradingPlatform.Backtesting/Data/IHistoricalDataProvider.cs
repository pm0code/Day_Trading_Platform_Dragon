using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.Foundation.Models;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Backtesting.Data
{
    /// <summary>
    /// Interface for comprehensive historical market data management
    /// </summary>
    public interface IHistoricalDataProvider
    {
        /// <summary>
        /// Gets historical price bars for a symbol within a date range
        /// </summary>
        Task<TradingResult<IEnumerable<PriceBar>>> GetHistoricalBarsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            bool adjustForCorporateActions = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets historical tick data for a symbol within a date range
        /// </summary>
        Task<TradingResult<IAsyncEnumerable<MarketTick>>> GetHistoricalTicksAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            TickType tickType = TickType.Trade,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets historical data for multiple symbols aligned by timestamp
        /// </summary>
        Task<TradingResult<IAsyncEnumerable<AlignedMarketData>>> GetAlignedDataAsync(
            IEnumerable<string> symbols,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            DataAlignmentMethod alignmentMethod = DataAlignmentMethod.Forward,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets corporate actions for a symbol within a date range
        /// </summary>
        Task<TradingResult<IEnumerable<CorporateAction>>> GetCorporateActionsAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates data quality for a symbol and date range
        /// </summary>
        Task<TradingResult<DataQualityReport>> ValidateDataQualityAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Preloads data into cache for faster access
        /// </summary>
        Task<TradingResult> PreloadDataAsync(
            IEnumerable<string> symbols,
            DateTime startDate,
            DateTime endDate,
            BarTimeframe timeframe,
            IProgress<DataLoadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available date range for a symbol
        /// </summary>
        Task<TradingResult<DateRange>> GetAvailableDateRangeAsync(
            string symbol,
            BarTimeframe timeframe,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available symbols for a specific date
        /// </summary>
        Task<TradingResult<IEnumerable<string>>> GetAvailableSymbolsAsync(
            DateTime date,
            string? exchange = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears cached data to free memory
        /// </summary>
        Task<TradingResult> ClearCacheAsync(
            IEnumerable<string>? symbols = null,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Gets data provider statistics
        /// </summary>
        Task<TradingResult<DataProviderStatistics>> GetStatisticsAsync();
    }

    /// <summary>
    /// Historical price bar data
    /// </summary>
    public class PriceBar
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public decimal? AdjustedClose { get; set; }
        public bool IsAdjusted { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public BarTimeframe Timeframe { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Market tick data
    /// </summary>
    public class MarketTick
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long Size { get; set; }
        public TickType Type { get; set; }
        public string? Exchange { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Ask { get; set; }
        public long? BidSize { get; set; }
        public long? AskSize { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Aligned market data across multiple symbols
    /// </summary>
    public class AlignedMarketData
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, PriceBar> SymbolData { get; set; } = new();
        public DataAlignmentInfo AlignmentInfo { get; set; } = new();
    }

    /// <summary>
    /// Data alignment information
    /// </summary>
    public class DataAlignmentInfo
    {
        public int MissingSymbols { get; set; }
        public List<string> ForwardFilledSymbols { get; set; } = new();
        public List<string> BackFilledSymbols { get; set; } = new();
        public bool IsMarketOpen { get; set; }
    }

    /// <summary>
    /// Corporate action data
    /// </summary>
    public class CorporateAction
    {
        public DateTime ExDate { get; set; }
        public DateTime? RecordDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public CorporateActionType Type { get; set; }
        public decimal Factor { get; set; }
        public decimal? Amount { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Data quality report
    /// </summary>
    public class DataQualityReport
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalRecords { get; set; }
        public int MissingRecords { get; set; }
        public int DuplicateRecords { get; set; }
        public int OutlierRecords { get; set; }
        public decimal DataCompleteness { get; set; }
        public List<DataQualityIssue> Issues { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    /// <summary>
    /// Data quality issue
    /// </summary>
    public class DataQualityIssue
    {
        public DateTime Timestamp { get; set; }
        public DataQualityIssueType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public object? AffectedData { get; set; }
        public string? SuggestedAction { get; set; }
    }

    /// <summary>
    /// Data load progress
    /// </summary>
    public class DataLoadProgress
    {
        public int TotalSymbols { get; set; }
        public int CompletedSymbols { get; set; }
        public string CurrentSymbol { get; set; } = string.Empty;
        public decimal PercentComplete { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public long BytesLoaded { get; set; }
        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// Date range
    /// </summary>
    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int TradingDays { get; set; }
        public int CalendarDays => (End - Start).Days;
    }

    /// <summary>
    /// Data provider statistics
    /// </summary>
    public class DataProviderStatistics
    {
        public long TotalRecords { get; set; }
        public long CachedRecords { get; set; }
        public long CacheSizeBytes { get; set; }
        public int AvailableSymbols { get; set; }
        public DateRange AvailableDateRange { get; set; } = new();
        public Dictionary<string, long> RecordsByTimeframe { get; set; } = new();
        public Dictionary<string, long> RecordsBySymbol { get; set; } = new();
        public TimeSpan AverageQueryTime { get; set; }
        public decimal CacheHitRate { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Bar timeframe enumeration
    /// </summary>
    public enum BarTimeframe
    {
        Tick,
        OneSecond,
        FiveSeconds,
        TenSeconds,
        FifteenSeconds,
        ThirtySeconds,
        OneMinute,
        FiveMinutes,
        TenMinutes,
        FifteenMinutes,
        ThirtyMinutes,
        OneHour,
        FourHours,
        Daily,
        Weekly,
        Monthly
    }

    /// <summary>
    /// Tick type enumeration
    /// </summary>
    public enum TickType
    {
        Trade,
        Quote,
        BidAsk,
        All
    }

    /// <summary>
    /// Data alignment method
    /// </summary>
    public enum DataAlignmentMethod
    {
        None,
        Forward,
        Backward,
        Linear,
        Previous
    }

    /// <summary>
    /// Corporate action type
    /// </summary>
    public enum CorporateActionType
    {
        Dividend,
        StockSplit,
        Merger,
        Acquisition,
        SpinOff,
        SpecialDividend,
        RightsOffering,
        SymbolChange,
        Delisting,
        Other
    }

    /// <summary>
    /// Data quality issue type
    /// </summary>
    public enum DataQualityIssueType
    {
        MissingData,
        DuplicateData,
        PriceOutlier,
        VolumeOutlier,
        NegativePrice,
        ZeroVolume,
        GapInData,
        InconsistentData,
        StaleData,
        CorruptedData
    }
}
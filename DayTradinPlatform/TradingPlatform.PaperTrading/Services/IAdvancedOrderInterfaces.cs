using TradingPlatform.PaperTrading.Models;

namespace TradingPlatform.PaperTrading.Services
{
    /// <summary>
    /// Extended interfaces for advanced order execution functionality
    /// </summary>
    
    /// <summary>
    /// Extended order execution engine with advanced order support
    /// </summary>
    public interface IOrderExecutionEngineExtended : IOrderExecutionEngine
    {
        // Add support for submitting orders with result
        Task<OrderResult> SubmitOrderAsync(OrderRequest orderRequest);
        
        // Add support for getting order status
        Task<OrderStatus> GetOrderStatusAsync(string orderId);
        
        // Add support for getting order details
        Task<Order?> GetOrderAsync(string orderId);
        
        // Add support for cancelling orders
        Task<bool> CancelOrderAsync(string orderId);
        
        // Add support for getting execution details
        Task<Execution?> GetExecutionDetailsAsync(string orderId);
    }

    /// <summary>
    /// Market data service for real-time market information
    /// </summary>
    public interface IMarketDataService
    {
        // Get current price for a symbol
        Task<decimal> GetCurrentPriceAsync(string symbol);
        
        // Get current volume for a symbol
        Task<decimal> GetCurrentVolumeAsync(string symbol);
        
        // Get VWAP for a symbol
        Task<decimal> GetVwapAsync(string symbol);
        
        // Get TWAP for a symbol over a time period
        Task<decimal> GetTwapAsync(string symbol, DateTime startTime, DateTime endTime);
        
        // Get bid/ask spread
        Task<(decimal bid, decimal ask)> GetBidAskAsync(string symbol);
        
        // Get market depth
        Task<OrderBook> GetMarketDepthAsync(string symbol, int levels = 5);
        
        // Subscribe to real-time updates
        Task SubscribeToMarketDataAsync(string symbol, Action<MarketDataUpdate> callback);
        
        // Unsubscribe from real-time updates
        Task UnsubscribeFromMarketDataAsync(string symbol);
    }

    /// <summary>
    /// Market data update event
    /// </summary>
    public record MarketDataUpdate(
        string Symbol,
        decimal Price,
        decimal Volume,
        decimal Bid,
        decimal Ask,
        DateTime Timestamp
    );

    /// <summary>
    /// Volume analysis service for VWAP calculations
    /// </summary>
    public interface IVolumeAnalysisServiceExtended : IVolumeAnalysisService
    {
        // Get typical volume pattern for a symbol
        Task<VolumePattern> GetTypicalVolumePatternAsync(string symbol);
        
        // Get volume forecast for upcoming period
        Task<VolumeForecast> GetVolumeForecastAsync(string symbol, TimeSpan period);
        
        // Calculate optimal participation rate
        Task<decimal> CalculateOptimalParticipationRateAsync(
            string symbol, 
            decimal totalQuantity, 
            TimeSpan duration);
    }

    /// <summary>
    /// Volume pattern analysis
    /// </summary>
    public record VolumePattern(
        string Symbol,
        Dictionary<TimeOnly, decimal> IntradayPattern, // Time of day -> typical volume %
        Dictionary<DayOfWeek, decimal> WeeklyPattern,  // Day of week -> volume multiplier
        decimal AverageDailyVolume,
        decimal VolatilityOfVolume
    );

    /// <summary>
    /// Volume forecast
    /// </summary>
    public record VolumeForecast(
        string Symbol,
        DateTime StartTime,
        DateTime EndTime,
        decimal ExpectedVolume,
        decimal ConfidenceLevel,
        List<VolumeProfile> HourlyForecast
    );

    /// <summary>
    /// Advanced order factory for creating complex orders
    /// </summary>
    public interface IAdvancedOrderFactory
    {
        // Create TWAP order with optimal parameters
        TwapOrder CreateOptimalTwapOrder(
            string symbol,
            OrderSide side,
            decimal quantity,
            DateTime startTime,
            DateTime endTime,
            decimal? limitPrice = null);
        
        // Create VWAP order with optimal parameters
        VwapOrder CreateOptimalVwapOrder(
            string symbol,
            OrderSide side,
            decimal quantity,
            DateTime startTime,
            DateTime endTime,
            decimal? limitPrice = null);
        
        // Create Iceberg order with optimal parameters
        IcebergOrder CreateOptimalIcebergOrder(
            string symbol,
            OrderSide side,
            decimal quantity,
            OrderType underlyingType,
            decimal? limitPrice = null);
        
        // Suggest best order type for given parameters
        AdvancedOrderType SuggestOrderType(
            string symbol,
            decimal quantity,
            decimal averageDailyVolume,
            TimeSpan executionWindow,
            decimal urgency); // 0 = patient, 1 = urgent
    }

    /// <summary>
    /// Order analytics service for performance tracking
    /// </summary>
    public interface IAdvancedOrderAnalytics
    {
        // Get execution performance vs benchmark
        Task<ExecutionPerformance> GetExecutionPerformanceAsync(string orderId);
        
        // Get slippage analysis
        Task<SlippageAnalysis> GetSlippageAnalysisAsync(string orderId);
        
        // Get market impact analysis
        Task<MarketImpactAnalysis> GetMarketImpactAnalysisAsync(string orderId);
        
        // Get execution cost analysis
        Task<ExecutionCostAnalysis> GetExecutionCostAnalysisAsync(string orderId);
        
        // Compare different execution strategies
        Task<StrategyComparison> CompareExecutionStrategiesAsync(
            string symbol,
            decimal quantity,
            List<AdvancedOrderType> strategies);
    }

    /// <summary>
    /// Execution performance metrics
    /// </summary>
    public record ExecutionPerformance(
        string OrderId,
        AdvancedOrderType OrderType,
        decimal AverageExecutionPrice,
        decimal BenchmarkPrice, // TWAP or VWAP
        decimal PerformanceVsBenchmark, // basis points
        decimal ImplementationShortfall,
        TimeSpan ExecutionDuration,
        int NumberOfFills
    );

    /// <summary>
    /// Detailed slippage analysis
    /// </summary>
    public record SlippageAnalysis(
        string OrderId,
        decimal TotalSlippage,
        decimal AverageSlippage,
        decimal MaxSlippage,
        decimal MinSlippage,
        Dictionary<string, decimal> SlippageByVenue,
        List<SlippageEvent> SlippageEvents
    );

    /// <summary>
    /// Individual slippage event
    /// </summary>
    public record SlippageEvent(
        DateTime Timestamp,
        decimal Quantity,
        decimal ExpectedPrice,
        decimal ExecutedPrice,
        decimal Slippage,
        string Venue,
        string Reason
    );

    /// <summary>
    /// Market impact analysis
    /// </summary>
    public record MarketImpactAnalysis(
        string OrderId,
        decimal TemporaryImpact,
        decimal PermanentImpact,
        decimal TotalImpact,
        decimal PreTradeSpread,
        decimal PostTradeSpread,
        decimal VolumeParticipation,
        TimeSpan RecoveryTime
    );

    /// <summary>
    /// Execution cost analysis
    /// </summary>
    public record ExecutionCostAnalysis(
        string OrderId,
        decimal ExplicitCosts, // Commissions, fees
        decimal ImplicitCosts, // Spread, market impact
        decimal OpportunityCost, // Delay cost
        decimal TotalCost,
        decimal CostPerShare,
        decimal CostBasisPoints
    );

    /// <summary>
    /// Strategy comparison results
    /// </summary>
    public record StrategyComparison(
        string Symbol,
        decimal Quantity,
        Dictionary<AdvancedOrderType, StrategyEstimate> Estimates,
        AdvancedOrderType RecommendedStrategy,
        string RecommendationReason
    );

    /// <summary>
    /// Strategy execution estimate
    /// </summary>
    public record StrategyEstimate(
        AdvancedOrderType Strategy,
        decimal EstimatedCost,
        decimal EstimatedSlippage,
        decimal EstimatedMarketImpact,
        TimeSpan EstimatedDuration,
        decimal ConfidenceLevel
    );

    /// <summary>
    /// Smart order router for intelligent venue selection
    /// </summary>
    public interface ISmartOrderRouter
    {
        // Route order slice to best venue
        Task<string> SelectVenueAsync(
            string symbol,
            OrderSide side,
            decimal quantity,
            OrderType orderType);
        
        // Get venue rankings for a symbol
        Task<List<VenueRanking>> GetVenueRankingsAsync(string symbol);
        
        // Update venue statistics after execution
        Task UpdateVenueStatisticsAsync(
            string venue,
            string symbol,
            Execution execution);
    }

    /// <summary>
    /// Venue ranking information
    /// </summary>
    public record VenueRanking(
        string VenueId,
        string VenueName,
        decimal FillRate,
        TimeSpan AverageLatency,
        decimal AverageSlippage,
        decimal LiquidityScore,
        decimal CostScore,
        decimal OverallScore
    );
}
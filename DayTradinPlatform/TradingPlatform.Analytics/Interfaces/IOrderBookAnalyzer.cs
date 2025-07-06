using System.Collections.Generic;
using System.Threading.Tasks;
using TradingPlatform.Analytics.OrderBook;

namespace TradingPlatform.Analytics.Interfaces
{
    /// <summary>
    /// Interface for deep order book analytics and microstructure analysis
    /// </summary>
    public interface IOrderBookAnalyzer
    {
        /// <summary>
        /// Performs comprehensive order book analysis including liquidity, price impact, and pattern detection
        /// </summary>
        Task<OrderBookAnalysis> AnalyzeOrderBookAsync(string symbol, OrderBookSnapshot snapshot);

        /// <summary>
        /// Analyzes liquidity characteristics and quality metrics
        /// </summary>
        Task<LiquidityAnalysis> AnalyzeLiquidityAsync(OrderBookSnapshot snapshot, OrderBookFeatures features);

        /// <summary>
        /// Analyzes price impact for various order sizes
        /// </summary>
        Task<PriceImpactAnalysis> AnalyzePriceImpactAsync(OrderBookSnapshot snapshot, OrderBookFeatures features);

        /// <summary>
        /// Detects microstructure patterns like iceberg orders, layering, and spoofing
        /// </summary>
        Task<List<MicrostructurePattern>> DetectMicrostructurePatternsAsync(string symbol, OrderBookSnapshot snapshot);

        /// <summary>
        /// Identifies trading opportunities based on order book inefficiencies
        /// </summary>
        Task<List<TradingOpportunity>> IdentifyTradingOpportunitiesAsync(OrderBookSnapshot snapshot, OrderBookFeatures features);

        /// <summary>
        /// Analyzes order flow characteristics and information content
        /// </summary>
        Task<OrderFlowAnalysis> AnalyzeOrderFlowAsync(string symbol, OrderBookSnapshot snapshot);

        /// <summary>
        /// Detects market anomalies and unusual patterns
        /// </summary>
        Task<List<MarketAnomaly>> DetectAnomaliesAsync(OrderBookSnapshot snapshot, OrderBookFeatures features);

        /// <summary>
        /// Calculates real-time market quality metrics
        /// </summary>
        Task<MarketQualityMetrics> CalculateMarketQualityAsync(string symbol, OrderBookSnapshot snapshot);

        /// <summary>
        /// Estimates optimal execution strategies based on current market state
        /// </summary>
        Task<ExecutionRecommendation> GetExecutionRecommendationAsync(
            string symbol, 
            decimal orderSize, 
            bool isBuyOrder, 
            ExecutionObjective objective);
    }

    /// <summary>
    /// Interface for specialized liquidity analysis
    /// </summary>
    public interface ILiquidityAnalyzer
    {
        Task<LiquidityMetrics> CalculateLiquidityMetricsAsync(OrderBookSnapshot snapshot);
        Task<LiquidityProvisionAnalysis> AnalyzeLiquidityProvidersAsync(string symbol);
        Task<List<LiquidityEvent>> DetectLiquidityEventsAsync(string symbol, OrderBookSnapshot snapshot);
        Task<LiquidityForecast> ForecastLiquidityAsync(string symbol, TimeSpan horizon);
    }

    /// <summary>
    /// Interface for price impact modeling
    /// </summary>
    public interface IPriceImpactModeler
    {
        Task<PriceImpactModel> BuildImpactModelAsync(string symbol, List<OrderBookSnapshot> history);
        Task<ImpactPrediction> PredictImpactAsync(string symbol, decimal orderSize, bool isBuyOrder);
        Task<OptimalSizingRecommendation> GetOptimalSizingAsync(string symbol, decimal targetSize);
        Task<ImpactCostAnalysis> AnalyzeImpactCostsAsync(string symbol, ExecutionPlan plan);
    }

    /// <summary>
    /// Interface for microstructure pattern detection
    /// </summary>
    public interface IMicrostructurePatternDetector
    {
        Task<List<IcebergDetection>> DetectIcebergOrdersAsync(string symbol, OrderBookSnapshot snapshot);
        Task<List<LayeringPattern>> DetectLayeringAsync(string symbol, OrderBookSnapshot snapshot);
        Task<List<SpoofingIndicator>> DetectSpoofingAsync(string symbol, OrderBookSnapshot snapshot);
        Task<List<ManipulationPattern>> DetectManipulationAsync(string symbol, OrderBookSnapshot snapshot);
        Task<PatternConfidence> ValidatePatternAsync(MicrostructurePattern pattern, List<OrderBookSnapshot> context);
    }
}

namespace TradingPlatform.Analytics.OrderBook
{
    // Supporting types for interfaces
    public class MarketQualityMetrics
    {
        public decimal BidAskSpreadQuality { get; set; }
        public decimal DepthQuality { get; set; }
        public decimal ResilienceQuality { get; set; }
        public decimal InformationEfficiency { get; set; }
        public decimal OverallQualityScore { get; set; }
        public Dictionary<string, decimal> DetailedMetrics { get; set; } = new();
    }

    public class ExecutionRecommendation
    {
        public ExecutionStrategy RecommendedStrategy { get; set; }
        public decimal OptimalOrderSize { get; set; }
        public TimeSpan RecommendedTimeHorizon { get; set; }
        public decimal ExpectedCost { get; set; }
        public decimal ExpectedSlippage { get; set; }
        public List<ExecutionStep> ExecutionSteps { get; set; } = new();
        public RiskAssessment RiskAssessment { get; set; } = new();
    }

    public class LiquidityMetrics
    {
        public decimal SpreadTightness { get; set; }
        public decimal MarketDepth { get; set; }
        public decimal Immediacy { get; set; }
        public decimal Resilience { get; set; }
        public decimal LiquidityScore { get; set; }
    }

    public class LiquidityProvisionAnalysis
    {
        public List<LiquidityProvider> ActiveProviders { get; set; } = new();
        public decimal ProvisionIntensity { get; set; }
        public decimal ProviderConcentration { get; set; }
        public Dictionary<string, ProviderMetrics> ProviderMetrics { get; set; } = new();
    }

    public class LiquidityEvent
    {
        public LiquidityEventType Type { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal Magnitude { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Impact { get; set; }
    }

    public class LiquidityForecast
    {
        public TimeSpan Horizon { get; set; }
        public decimal PredictedLiquidity { get; set; }
        public decimal Confidence { get; set; }
        public List<LiquidityScenario> Scenarios { get; set; } = new();
    }

    public class PriceImpactModel
    {
        public ModelType Type { get; set; }
        public Dictionary<string, decimal> Parameters { get; set; } = new();
        public decimal GoodnessOfFit { get; set; }
        public DateTime LastCalibrated { get; set; }
        public decimal PredictivePower { get; set; }
    }

    public class ImpactPrediction
    {
        public decimal PredictedImpactBps { get; set; }
        public decimal Confidence { get; set; }
        public decimal[] ConfidenceInterval { get; set; } = new decimal[2];
        public Dictionary<string, decimal> ComponentBreakdown { get; set; } = new();
    }

    public class OptimalSizingRecommendation
    {
        public List<OrderSlice> RecommendedSlices { get; set; } = new();
        public decimal TotalExpectedCost { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public decimal RiskAdjustedReturn { get; set; }
    }

    public class ImpactCostAnalysis
    {
        public decimal TemporaryImpact { get; set; }
        public decimal PermanentImpact { get; set; }
        public decimal TotalCost { get; set; }
        public decimal OpportunityCost { get; set; }
        public CostBreakdown Breakdown { get; set; } = new();
    }

    public class IcebergDetection
    {
        public decimal Price { get; set; }
        public decimal EstimatedHiddenSize { get; set; }
        public decimal Confidence { get; set; }
        public RefreshPattern Pattern { get; set; } = new();
    }

    public class LayeringPattern
    {
        public List<decimal> LayerPrices { get; set; } = new();
        public decimal Confidence { get; set; }
        public LayeringType Type { get; set; }
        public decimal EstimatedIntent { get; set; }
    }

    public class SpoofingIndicator
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public TimeSpan Duration { get; set; }
        public decimal Confidence { get; set; }
        public SpoofingType Type { get; set; }
    }

    public class ManipulationPattern
    {
        public ManipulationType Type { get; set; }
        public decimal Confidence { get; set; }
        public Dictionary<string, object> Evidence { get; set; } = new();
        public decimal EstimatedImpact { get; set; }
    }

    public class PatternConfidence
    {
        public decimal OverallConfidence { get; set; }
        public Dictionary<string, decimal> ComponentConfidences { get; set; } = new();
        public List<ValidationResult> ValidationResults { get; set; } = new();
    }

    // Enums and supporting types
    public enum ExecutionObjective { MinimizeImpact, MinimizeCost, MaximizeSpeed, BalancedExecution }
    public enum ExecutionStrategy { TWAP, VWAP, Implementation, Opportunistic, Aggressive }
    public enum LiquidityEventType { Withdrawal, Addition, Shock, Recovery }
    public enum ModelType { Linear, PowerLaw, SquareRoot, Almgren_Chriss, Custom }
    public enum LayeringType { Horizontal, Vertical, Mixed }
    public enum SpoofingType { BidSpoof, AskSpoof, Sandwich, FrontRunning }
    public enum ManipulationType { PriceManipulation, VolumeManipulation, FlowToxicity, Collusion }

    // Additional supporting classes
    public class ExecutionStep { public string Action { get; set; } = string.Empty; }
    public class RiskAssessment { public decimal RiskScore { get; set; } }
    public class LiquidityProvider { public string Id { get; set; } = string.Empty; }
    public class ProviderMetrics { public decimal ContributionRatio { get; set; } }
    public class LiquidityScenario { public decimal Probability { get; set; } }
    public class OrderSlice { public decimal Size { get; set; } }
    public class CostBreakdown { public decimal MarketImpact { get; set; } }
    public class RefreshPattern { public decimal RefreshRate { get; set; } }
    public class ValidationResult { public bool IsValid { get; set; } }
    public class ExecutionPlan { public List<ExecutionStep> Steps { get; set; } = new(); }
}
using System;
using System.Collections.Generic;

namespace TradingPlatform.Analytics.OrderBook
{
    /// <summary>
    /// Comprehensive order book snapshot with enhanced metadata
    /// </summary>
    public class OrderBookSnapshot
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long SequenceNumber { get; set; }
        
        // Best prices
        public decimal BestBid { get; set; }
        public decimal BestAsk { get; set; }
        
        // Order book levels
        public List<OrderBookLevel> Bids { get; set; } = new();
        public List<OrderBookLevel> Asks { get; set; } = new();
        
        // Volume and trade data
        public decimal LastTradePrice { get; set; }
        public decimal LastTradeQuantity { get; set; }
        public DateTime LastTradeTime { get; set; }
        public decimal DayVolume { get; set; }
        public decimal DayVWAP { get; set; }
        
        // Market data
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        
        // Metadata
        public string Exchange { get; set; } = string.Empty;
        public string DataProvider { get; set; } = string.Empty;
        public int Latency { get; set; } // Microseconds
        public bool IsComplete { get; set; } = true;
    }

    /// <summary>
    /// Individual price level in the order book
    /// </summary>
    public class OrderBookLevel
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public int OrderCount { get; set; }
        public DateTime LastUpdateTime { get; set; }
        
        // Enhanced metadata
        public decimal ImpliedValue => Price * Quantity;
        public string LevelId { get; set; } = string.Empty;
        public LevelType Type { get; set; }
        public bool IsIceberg { get; set; }
        public decimal HiddenQuantity { get; set; }
    }

    /// <summary>
    /// Order book analysis features for ML and analytics
    /// </summary>
    public class OrderBookFeatures
    {
        // Basic spread and price features
        public decimal BidAskSpread { get; set; }
        public decimal RelativeSpread { get; set; }
        public decimal MidPrice { get; set; }
        public decimal WeightedBidPrice { get; set; }
        public decimal WeightedAskPrice { get; set; }
        
        // Depth features
        public decimal BidDepth { get; set; }
        public decimal AskDepth { get; set; }
        public decimal TotalDepth { get; set; }
        public decimal DepthImbalance { get; set; }
        
        // Level features
        public int BidLevels { get; set; }
        public int AskLevels { get; set; }
        public int TotalLevels { get; set; }
        
        // Time features
        public TimeSpan TimeSinceLastUpdate { get; set; }
        public decimal UpdateFrequency { get; set; }
        
        // Market microstructure features
        public decimal VolatilityIndex { get; set; }
        public decimal MomentumIndex { get; set; }
        public decimal EffectiveTickSize { get; set; }
        public decimal LiquidityConcentration { get; set; }
        public decimal[] OrderSizeDistribution { get; set; } = new decimal[0];
        
        // Advanced features
        public decimal BidAskCorrelation { get; set; }
        public decimal PricePressure { get; set; }
        public decimal FlowToxicity { get; set; }
        public decimal MicropriceReturn { get; set; }
        public decimal OrderBookSlope { get; set; }
    }

    /// <summary>
    /// Comprehensive order book analysis result
    /// </summary>
    public class OrderBookAnalysis
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        
        // Core analysis components
        public LiquidityAnalysis LiquidityAnalysis { get; set; } = new();
        public PriceImpactAnalysis PriceImpactAnalysis { get; set; } = new();
        public List<MicrostructurePattern> MicrostructurePatterns { get; set; } = new();
        public List<TradingOpportunity> TradingOpportunities { get; set; } = new();
        public OrderFlowAnalysis OrderFlowAnalysis { get; set; } = new();
        public List<MarketAnomaly> Anomalies { get; set; } = new();
        
        // Aggregate metrics
        public AggregateMetrics AggregateMetrics { get; set; } = new();
        public OrderBookFeatures Features { get; set; } = new();
        
        // Quality indicators
        public decimal AnalysisQuality { get; set; }
        public Dictionary<string, decimal> QualityMetrics { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Liquidity analysis with detailed metrics
    /// </summary>
    public class LiquidityAnalysis
    {
        public decimal LiquidityScore { get; set; }
        public SpreadMetrics SpreadMetrics { get; set; } = new();
        public DepthAnalysis DepthAnalysis { get; set; } = new();
        public LiquidityConcentration Concentration { get; set; } = new();
        public MarketDepthResilience Resilience { get; set; } = new();
        public decimal EffectiveSpread { get; set; }
        public LiquidityLayerAnalysis LayerAnalysis { get; set; } = new();
        public Dictionary<string, decimal> QualityIndicators { get; set; } = new();
    }

    /// <summary>
    /// Price impact analysis for different order sizes
    /// </summary>
    public class PriceImpactAnalysis
    {
        public List<PriceImpactProfile> ImpactProfiles { get; set; } = new();
        public ImpactElasticity Elasticity { get; set; } = new();
        public List<ImpactAnomaly> Anomalies { get; set; } = new();
        public decimal LinearityIndex { get; set; }
        public decimal OptimalOrderSize { get; set; }
        public ImpactPersistence ImpactPersistence { get; set; } = new();
    }

    /// <summary>
    /// Detected microstructure pattern
    /// </summary>
    public class MicrostructurePattern
    {
        public PatternType Type { get; set; }
        public decimal Price { get; set; }
        public decimal Confidence { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public decimal PatternStrength { get; set; }
        public decimal EstimatedHiddenSize { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Trading opportunity identification
    /// </summary>
    public class TradingOpportunity
    {
        public OpportunityType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal ExpectedProfit { get; set; }
        public decimal Confidence { get; set; }
        public TimeSpan TimeHorizon { get; set; }
        public decimal MaxPosition { get; set; }
        public decimal Score { get; set; }
        public decimal RiskAdjustedScore { get; set; }
        public Dictionary<string, decimal> RiskMetrics { get; set; } = new();
    }

    /// <summary>
    /// Order flow analysis
    /// </summary>
    public class OrderFlowAnalysis
    {
        public decimal OrderFlowImbalance { get; set; }
        public decimal AggressiveRatio { get; set; }
        public decimal PassiveRatio { get; set; }
        public Dictionary<OrderType, decimal> OrderTypeDistribution { get; set; } = new();
        public decimal FlowToxicity { get; set; }
        public decimal InformationContent { get; set; }
        public List<FlowEvent> SignificantEvents { get; set; } = new();
    }

    /// <summary>
    /// Market anomaly detection
    /// </summary>
    public class MarketAnomaly
    {
        public AnomalyType Type { get; set; }
        public decimal Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public decimal Confidence { get; set; }
        public Dictionary<string, object> Evidence { get; set; } = new();
        public bool RequiresAttention { get; set; }
    }

    // Supporting classes
    public class SpreadMetrics
    {
        public decimal AbsoluteSpread { get; set; }
        public decimal RelativeSpread { get; set; }
        public decimal PercentageSpread { get; set; }
        public decimal SpreadVolatility { get; set; }
        public decimal EffectiveSpread { get; set; }
        public decimal RealizedSpread { get; set; }
        public decimal QuotedSpread { get; set; }
    }

    public class DepthAnalysis
    {
        public decimal BidDepthMean { get; set; }
        public decimal AskDepthMean { get; set; }
        public decimal BidDepthStd { get; set; }
        public decimal AskDepthStd { get; set; }
        public decimal DepthSkewness { get; set; }
        public decimal DepthKurtosis { get; set; }
        public decimal MaxBidDepth { get; set; }
        public decimal MaxAskDepth { get; set; }
        public decimal DepthConcentration { get; set; }
    }

    public class LiquidityConcentration
    {
        public decimal Top3LevelsRatio { get; set; }
        public decimal Top5LevelsRatio { get; set; }
        public decimal HerfindahlIndex { get; set; }
        public decimal GiniCoefficient { get; set; }
        public decimal ConcentrationScore { get; set; }
    }

    public class PriceImpactProfile
    {
        public decimal OrderSize { get; set; }
        public ImpactResult BuyImpact { get; set; } = new();
        public ImpactResult SellImpact { get; set; } = new();
        public decimal AsymmetryRatio { get; set; }
    }

    public class ImpactResult
    {
        public decimal ImpactBps { get; set; }
        public decimal ExecutableQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public int LevelsConsumed { get; set; }
        public bool LiquidityAdequate { get; set; }
        public decimal SlippageCost { get; set; }
        public decimal MarketImpactCost { get; set; }
    }

    public class AggregateMetrics
    {
        public decimal OverallLiquidityScore { get; set; }
        public decimal MarketQualityIndex { get; set; }
        public decimal TradabilityScore { get; set; }
        public decimal RiskScore { get; set; }
        public decimal OpportunityScore { get; set; }
        public decimal EfficiencyRatio { get; set; }
    }

    // State management
    public class OrderBookState
    {
        public OrderBookSnapshot? LastSnapshot { get; set; }
        public long UpdateCount { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public Dictionary<string, object> StateMetrics { get; set; } = new();
    }

    // Configuration
    public class OrderBookAnalyticsConfiguration
    {
        public int MaxHistorySnapshots { get; set; } = 1000;
        public int MinHistoryForPatternDetection { get; set; } = 50;
        public decimal[] ImpactAnalysisSizes { get; set; } = { 1000m, 5000m, 10000m, 25000m, 50000m };
        public decimal MinimumOpportunityScore { get; set; } = 50m;
        public int MaxOpportunitiesReturned { get; set; } = 20;
        public TimeSpan MaxAnalysisLatency { get; set; } = TimeSpan.FromMilliseconds(100);
        public bool EnableMLPatternDetection { get; set; } = true;
        public bool EnableGpuAcceleration { get; set; } = true;
    }

    // Enums
    public enum LevelType { Bid, Ask }
    public enum PatternType { IcebergOrder, Layering, Spoofing, MomentumIgnition, LiquidityProvision, Manipulation }
    public enum OpportunityType { Arbitrage, LiquidityGap, OrderImbalance, MeanReversion, Momentum, Statistical }
    public enum AnomalyType { PriceAnomaly, VolumeAnomaly, SpreadAnomaly, PatternAnomaly, FlowAnomaly }
    public enum OrderType { Market, Limit, Stop, Hidden, Iceberg }

    // Additional supporting classes would be defined here...
    public class MarketDepthResilience { public decimal ResilienceScore { get; set; } }
    public class LiquidityLayerAnalysis { public decimal LayerScore { get; set; } }
    public class ImpactElasticity { public decimal ElasticityCoefficient { get; set; } }
    public class ImpactAnomaly { public string Description { get; set; } = string.Empty; }
    public class ImpactPersistence { public decimal PersistenceScore { get; set; } }
    public class FlowEvent { public string EventType { get; set; } = string.Empty; }

    // Pattern analysis helpers
    public class OrderRefreshPattern
    {
        public bool IsIcebergCandidate { get; set; }
        public decimal Confidence { get; set; }
        public decimal EstimatedHiddenSize { get; set; }
        public decimal PatternStrength { get; set; }
    }
}
namespace TradingPlatform.ML.Models
{
    /// <summary>
    /// Types of ML models used in the trading platform
    /// </summary>
    public enum ModelType
    {
        // Time Series Models
        /// <summary>
        /// LSTM model for next price prediction
        /// </summary>
        PricePredictionLSTM,
        
        /// <summary>
        /// GARCH model for volatility forecasting
        /// </summary>
        VolatilityGARCH,
        
        /// <summary>
        /// Hidden Markov Model for market regime detection
        /// </summary>
        RegimeDetectionHMM,
        
        /// <summary>
        /// Transformer model for multi-horizon forecasting
        /// </summary>
        MultiHorizonTransformer,

        // Classification Models
        /// <summary>
        /// Model for buy/sell/hold signal classification
        /// </summary>
        SignalClassifier,
        
        /// <summary>
        /// CNN model for chart pattern recognition
        /// </summary>
        PatternRecognition,
        
        /// <summary>
        /// Autoencoder for anomaly detection
        /// </summary>
        AnomalyDetection,
        
        /// <summary>
        /// Model for trend direction classification
        /// </summary>
        TrendClassifier,

        // Risk Models
        /// <summary>
        /// Value at Risk estimation model
        /// </summary>
        VaREstimator,
        
        /// <summary>
        /// Maximum drawdown prediction model
        /// </summary>
        DrawdownPredictor,
        
        /// <summary>
        /// Liquidity risk assessment model
        /// </summary>
        LiquidityClassifier,
        
        /// <summary>
        /// Conditional Value at Risk (CVaR) model
        /// </summary>
        CVaREstimator,
        
        /// <summary>
        /// Entropic Value at Risk (EVaR) model
        /// </summary>
        EVaRCalculator,

        // Portfolio Optimization Models
        /// <summary>
        /// Black-Litterman with LSTM views
        /// </summary>
        BlackLittermanLSTM,
        
        /// <summary>
        /// Hierarchical Risk Parity optimizer
        /// </summary>
        HierarchicalRiskParity,
        
        /// <summary>
        /// Mean-variance optimization with ML constraints
        /// </summary>
        MLConstrainedMVO,
        
        /// <summary>
        /// Multi-objective portfolio optimizer
        /// </summary>
        MultiObjectiveOptimizer,

        // Alternative Data Models
        /// <summary>
        /// BERT-based social media sentiment analyzer
        /// </summary>
        SentimentAnalyzer,
        
        /// <summary>
        /// News impact prediction model
        /// </summary>
        NewsImpactPredictor,
        
        /// <summary>
        /// Satellite image analysis for economic activity
        /// </summary>
        SatelliteImageAnalyzer,
        
        /// <summary>
        /// Web scraping sentiment aggregator
        /// </summary>
        WebSentimentAggregator,

        // Market Microstructure Models
        /// <summary>
        /// Deep order book analytics model
        /// </summary>
        OrderBookPredictor,
        
        /// <summary>
        /// Trade flow imbalance predictor
        /// </summary>
        TradeFlowAnalyzer,
        
        /// <summary>
        /// Price impact estimation model
        /// </summary>
        PriceImpactEstimator,
        
        /// <summary>
        /// Market maker behavior predictor
        /// </summary>
        MarketMakerPredictor,

        // High-Frequency Trading Models
        /// <summary>
        /// Ultra-low latency signal generator
        /// </summary>
        HFTSignalGenerator,
        
        /// <summary>
        /// Arbitrage opportunity detector
        /// </summary>
        ArbitrageDetector,
        
        /// <summary>
        /// Order execution optimizer
        /// </summary>
        ExecutionOptimizer,
        
        /// <summary>
        /// Latency-aware routing model
        /// </summary>
        LatencyRouter,

        // Ensemble Models
        /// <summary>
        /// Ensemble of multiple price predictors
        /// </summary>
        PriceEnsemble,
        
        /// <summary>
        /// Risk model ensemble
        /// </summary>
        RiskEnsemble,
        
        /// <summary>
        /// Meta-learning model selector
        /// </summary>
        MetaLearner,
        
        /// <summary>
        /// Adaptive ensemble weighting
        /// </summary>
        AdaptiveEnsemble
    }

    /// <summary>
    /// Model optimization settings for deployment
    /// </summary>
    public class OptimizationSettings
    {
        /// <summary>
        /// Gets or sets whether to enable quantization
        /// </summary>
        public bool EnableQuantization { get; set; } = true;

        /// <summary>
        /// Gets or sets the quantization type
        /// </summary>
        public QuantizationType QuantizationType { get; set; } = QuantizationType.FP16;

        /// <summary>
        /// Gets or sets whether to enable graph optimization
        /// </summary>
        public bool EnableGraphOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets the target execution provider
        /// </summary>
        public ExecutionProvider TargetProvider { get; set; } = ExecutionProvider.CUDA;

        /// <summary>
        /// Gets or sets whether to enable kernel fusion
        /// </summary>
        public bool EnableKernelFusion { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable memory optimization
        /// </summary>
        public bool EnableMemoryOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets the optimization level (1-5)
        /// </summary>
        public int OptimizationLevel { get; set; } = 3;
    }

    /// <summary>
    /// Quantization types for model optimization
    /// </summary>
    public enum QuantizationType
    {
        /// <summary>
        /// No quantization (FP32)
        /// </summary>
        None,
        
        /// <summary>
        /// Half precision (FP16)
        /// </summary>
        FP16,
        
        /// <summary>
        /// Dynamic INT8 quantization
        /// </summary>
        INT8Dynamic,
        
        /// <summary>
        /// Static INT8 quantization with calibration
        /// </summary>
        INT8Static,
        
        /// <summary>
        /// Mixed precision (FP16 + INT8)
        /// </summary>
        Mixed
    }

    /// <summary>
    /// Optimized model information
    /// </summary>
    public class OptimizedModel
    {
        /// <summary>
        /// Gets or sets the optimized model path
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original model path
        /// </summary>
        public string OriginalPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optimization settings used
        /// </summary>
        public OptimizationSettings Settings { get; set; } = new();

        /// <summary>
        /// Gets or sets the size reduction percentage
        /// </summary>
        public double SizeReductionPercent { get; set; }

        /// <summary>
        /// Gets or sets the speedup factor
        /// </summary>
        public double SpeedupFactor { get; set; }

        /// <summary>
        /// Gets or sets accuracy metrics after optimization
        /// </summary>
        public Dictionary<string, double> AccuracyMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets when the optimization was performed
        /// </summary>
        public DateTime OptimizedAt { get; set; }
    }
}
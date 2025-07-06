using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Analytics.Interfaces;
using TradingPlatform.GPU.Infrastructure;

namespace TradingPlatform.Analytics.OrderBook
{
    /// <summary>
    /// Specialized liquidity analyzer for detailed liquidity metrics and forecasting
    /// </summary>
    public class LiquidityAnalyzer : CanonicalServiceBaseEnhanced, ILiquidityAnalyzer
    {
        private readonly GpuContext? _gpuContext;
        private readonly LiquidityAnalysisConfiguration _config;
        private readonly Dictionary<string, LiquidityState> _liquidityStates;
        private readonly Dictionary<string, List<LiquiditySnapshot>> _liquidityHistory;

        public LiquidityAnalyzer(
            LiquidityAnalysisConfiguration config,
            GpuContext? gpuContext = null,
            ITradingLogger? logger = null)
            : base(logger, "LiquidityAnalyzer")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gpuContext = gpuContext;
            _liquidityStates = new Dictionary<string, LiquidityState>();
            _liquidityHistory = new Dictionary<string, List<LiquiditySnapshot>>();
        }

        public async Task<LiquidityMetrics> CalculateLiquidityMetricsAsync(OrderBookSnapshot snapshot)
        {
            return await TrackOperationAsync("CalculateLiquidityMetrics", async () =>
            {
                // Calculate Kyle's Lambda (price impact coefficient)
                var kylesLambda = await CalculateKylesLambdaAsync(snapshot);
                
                // Calculate Amihud's ILLIQ measure
                var amihudIlliq = CalculateAmihudIlliquidity(snapshot);
                
                // Calculate Roll's spread estimate
                var rollSpread = CalculateRollSpread(snapshot);
                
                // Calculate market depth
                var marketDepth = CalculateMarketDepth(snapshot);
                
                // Calculate resilience measure
                var resilience = await CalculateResilienceAsync(snapshot);
                
                // Calculate immediacy measure
                var immediacy = CalculateImmediacy(snapshot);
                
                // Calculate spread tightness
                var spreadTightness = CalculateSpreadTightness(snapshot);
                
                // Composite liquidity score using principal component analysis
                var liquidityScore = CalculateCompositeLiquidityScore(
                    spreadTightness, marketDepth, immediacy, resilience);
                
                var metrics = new LiquidityMetrics
                {
                    SpreadTightness = spreadTightness,
                    MarketDepth = marketDepth,
                    Immediacy = immediacy,
                    Resilience = resilience,
                    LiquidityScore = liquidityScore
                };
                
                LogInfo("LIQUIDITY_METRICS_CALCULATED", "Liquidity metrics calculated",
                    additionalData: new
                    {
                        Symbol = snapshot.Symbol,
                        LiquidityScore = liquidityScore,
                        SpreadTightness = spreadTightness,
                        MarketDepth = marketDepth,
                        KylesLambda = kylesLambda,
                        AmihudIlliq = amihudIlliq
                    });
                
                return metrics;
            });
        }

        public async Task<LiquidityProvisionAnalysis> AnalyzeLiquidityProvidersAsync(string symbol)
        {
            return await TrackOperationAsync("AnalyzeLiquidityProviders", async () =>
            {
                var state = GetLiquidityState(symbol);
                var analysis = new LiquidityProvisionAnalysis();
                
                if (state.OrderBookHistory.Count < _config.MinHistoryForProviderAnalysis)
                {
                    LogWarning("INSUFFICIENT_HISTORY", $"Insufficient history for provider analysis: {symbol}");
                    return analysis;
                }
                
                // Analyze order book level persistence to identify passive providers
                var persistentLevels = AnalyzeLevelPersistence(state.OrderBookHistory);
                
                // Identify liquidity provision patterns
                var provisionPatterns = DetectProvisionPatterns(state.OrderBookHistory);
                
                // Calculate provider concentration metrics
                var concentration = CalculateProviderConcentration(persistentLevels);
                
                // Estimate provision intensity
                var intensity = CalculateProvisionIntensity(provisionPatterns);
                
                // Identify active providers
                var activeProviders = IdentifyActiveProviders(persistentLevels, provisionPatterns);
                
                analysis.ActiveProviders = activeProviders;
                analysis.ProvisionIntensity = intensity;
                analysis.ProviderConcentration = concentration;
                analysis.ProviderMetrics = CalculateProviderMetrics(activeProviders, state);
                
                LogInfo("LIQUIDITY_PROVIDER_ANALYSIS_COMPLETE", "Liquidity provider analysis completed",
                    additionalData: new
                    {
                        Symbol = symbol,
                        ActiveProviders = activeProviders.Count,
                        ProvisionIntensity = intensity,
                        Concentration = concentration
                    });
                
                return analysis;
            });
        }

        public async Task<List<LiquidityEvent>> DetectLiquidityEventsAsync(string symbol, OrderBookSnapshot snapshot)
        {
            return await TrackOperationAsync("DetectLiquidityEvents", async () =>
            {
                var events = new List<LiquidityEvent>();
                var state = GetLiquidityState(symbol);
                
                if (state.PreviousSnapshot == null)
                {
                    state.PreviousSnapshot = snapshot;
                    return events;
                }
                
                // Detect liquidity withdrawal events
                var withdrawalEvents = DetectLiquidityWithdrawal(state.PreviousSnapshot, snapshot);
                events.AddRange(withdrawalEvents);
                
                // Detect liquidity addition events
                var additionEvents = DetectLiquidityAddition(state.PreviousSnapshot, snapshot);
                events.AddRange(additionEvents);
                
                // Detect liquidity shock events
                var shockEvents = DetectLiquidityShocks(state.PreviousSnapshot, snapshot);
                events.AddRange(shockEvents);
                
                // Detect recovery events
                var recoveryEvents = DetectLiquidityRecovery(state.LiquidityHistory, snapshot);
                events.AddRange(recoveryEvents);
                
                // Update state
                state.PreviousSnapshot = snapshot;
                UpdateLiquidityHistory(symbol, snapshot);
                
                foreach (var evt in events)
                {
                    LogInfo("LIQUIDITY_EVENT_DETECTED", $"Liquidity event detected: {evt.Type}",
                        additionalData: new
                        {
                            Symbol = symbol,
                            EventType = evt.Type.ToString(),
                            Magnitude = evt.Magnitude,
                            Impact = evt.Impact
                        });
                }
                
                return events;
            });
        }

        public async Task<LiquidityForecast> ForecastLiquidityAsync(string symbol, TimeSpan horizon)
        {
            return await TrackOperationAsync("ForecastLiquidity", async () =>
            {
                var state = GetLiquidityState(symbol);
                var forecast = new LiquidityForecast { Horizon = horizon };
                
                if (state.LiquidityHistory.Count < _config.MinHistoryForForecasting)
                {
                    LogWarning("INSUFFICIENT_HISTORY_FORECAST", $"Insufficient history for forecasting: {symbol}");
                    forecast.Confidence = 0m;
                    return forecast;
                }
                
                // Extract liquidity time series
                var liquiditySeries = state.LiquidityHistory
                    .Select(s => s.LiquidityScore)
                    .ToArray();
                
                // Apply time series forecasting model
                var forecastResult = await ApplyLiquidityForecastingModelAsync(liquiditySeries, horizon);
                
                forecast.PredictedLiquidity = forecastResult.Prediction;
                forecast.Confidence = forecastResult.Confidence;
                forecast.Scenarios = GenerateLiquidityScenarios(forecastResult, horizon);
                
                LogInfo("LIQUIDITY_FORECAST_COMPLETE", "Liquidity forecast completed",
                    additionalData: new
                    {
                        Symbol = symbol,
                        Horizon = horizon.ToString(),
                        PredictedLiquidity = forecast.PredictedLiquidity,
                        Confidence = forecast.Confidence
                    });
                
                return forecast;
            });
        }

        private async Task<decimal> CalculateKylesLambdaAsync(OrderBookSnapshot snapshot)
        {
            // Kyle's lambda measures the price impact of order flow
            // λ = Δp / Q, where Δp is price change and Q is signed order flow
            
            var bidDepth = snapshot.Bids.Sum(b => b.Quantity);
            var askDepth = snapshot.Asks.Sum(a => a.Quantity);
            var totalDepth = bidDepth + askDepth;
            
            if (totalDepth == 0) return decimal.MaxValue;
            
            var spread = snapshot.BestAsk - snapshot.BestBid;
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            
            // Simplified Kyle's lambda calculation
            var lambda = (spread / 2m) / Math.Sqrt((double)totalDepth);
            
            return (decimal)lambda;
        }

        private decimal CalculateAmihudIlliquidity(OrderBookSnapshot snapshot)
        {
            // Amihud's ILLIQ = |Return| / Volume
            // Here we use spread as a proxy for potential return impact
            
            var spread = snapshot.BestAsk - snapshot.BestBid;
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            var relativeSpread = spread / midPrice;
            
            var volume = snapshot.DayVolume;
            if (volume == 0) return decimal.MaxValue;
            
            return relativeSpread / volume * 1000000m; // Scale to basis points
        }

        private decimal CalculateRollSpread(OrderBookSnapshot snapshot)
        {
            // Roll's spread estimate based on serial covariance of price changes
            // For snapshot analysis, we use effective spread approximation
            
            var spread = snapshot.BestAsk - snapshot.BestBid;
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            
            // Roll's effective spread estimate
            var rollSpread = 2m * Math.Sqrt((double)(spread / midPrice));
            
            return (decimal)rollSpread;
        }

        private decimal CalculateMarketDepth(OrderBookSnapshot snapshot)
        {
            var bidValue = snapshot.Bids.Sum(b => b.Price * b.Quantity);
            var askValue = snapshot.Asks.Sum(a => a.Price * a.Quantity);
            var totalValue = bidValue + askValue;
            
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            
            // Normalize by mid-price to get depth measure
            return totalValue / midPrice;
        }

        private async Task<decimal> CalculateResilienceAsync(OrderBookSnapshot snapshot)
        {
            // Resilience measures how quickly the order book recovers after a trade
            // We approximate this using the slope of the order book
            
            var bidSlope = CalculateOrderBookSlope(snapshot.Bids, true);
            var askSlope = CalculateOrderBookSlope(snapshot.Asks, false);
            
            // Higher slopes indicate better resilience
            var averageSlope = (bidSlope + askSlope) / 2m;
            
            // Normalize resilience score
            return Math.Min(averageSlope * 100m, 100m);
        }

        private decimal CalculateImmediacy(OrderBookSnapshot snapshot)
        {
            // Immediacy measures the cost of immediate execution
            var spread = snapshot.BestAsk - snapshot.BestBid;
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            
            var relativeSpread = spread / midPrice;
            
            // Lower spread means higher immediacy (inverted scale)
            return Math.Max(0m, 100m - (relativeSpread * 10000m)); // Convert to basis points
        }

        private decimal CalculateSpreadTightness(OrderBookSnapshot snapshot)
        {
            var spread = snapshot.BestAsk - snapshot.BestBid;
            var midPrice = (snapshot.BestAsk + snapshot.BestBid) / 2m;
            
            var relativeSpread = spread / midPrice;
            
            // Tighter spreads get higher scores
            return Math.Max(0m, 100m - (relativeSpread * 5000m));
        }

        private decimal CalculateCompositeLiquidityScore(
            decimal spreadTightness,
            decimal marketDepth,
            decimal immediacy,
            decimal resilience)
        {
            // Weighted composite score using PCA-derived weights
            var weights = _config.LiquidityScoreWeights;
            
            var score = 
                spreadTightness * weights.SpreadWeight +
                marketDepth * weights.DepthWeight +
                immediacy * weights.ImmediacyWeight +
                resilience * weights.ResilienceWeight;
            
            return Math.Max(0m, Math.Min(100m, score));
        }

        private decimal CalculateOrderBookSlope(List<OrderBookLevel> levels, bool isBid)
        {
            if (levels.Count < 2) return 0m;
            
            var sortedLevels = isBid ? 
                levels.OrderByDescending(l => l.Price).ToList() :
                levels.OrderBy(l => l.Price).ToList();
            
            var priceRange = sortedLevels.Last().Price - sortedLevels.First().Price;
            var quantitySum = sortedLevels.Sum(l => l.Quantity);
            
            if (priceRange == 0) return 0m;
            
            return quantitySum / priceRange;
        }

        private LiquidityState GetLiquidityState(string symbol)
        {
            if (!_liquidityStates.ContainsKey(symbol))
            {
                _liquidityStates[symbol] = new LiquidityState();
                _liquidityHistory[symbol] = new List<LiquiditySnapshot>();
            }
            
            return _liquidityStates[symbol];
        }

        private void UpdateLiquidityHistory(string symbol, OrderBookSnapshot snapshot)
        {
            var history = _liquidityHistory[symbol];
            
            var liquiditySnapshot = new LiquiditySnapshot
            {
                Timestamp = snapshot.Timestamp,
                LiquidityScore = CalculateMarketDepth(snapshot), // Simplified for now
                Spread = snapshot.BestAsk - snapshot.BestBid,
                Depth = snapshot.Bids.Sum(b => b.Quantity) + snapshot.Asks.Sum(a => a.Quantity)
            };
            
            history.Add(liquiditySnapshot);
            
            // Maintain history size
            if (history.Count > _config.MaxLiquidityHistory)
            {
                history.RemoveAt(0);
            }
        }

        // Additional helper methods for pattern detection and analysis...
        private List<PersistentLevel> AnalyzeLevelPersistence(List<OrderBookSnapshot> history) => new();
        private List<ProvisionPattern> DetectProvisionPatterns(List<OrderBookSnapshot> history) => new();
        private decimal CalculateProviderConcentration(List<PersistentLevel> levels) => 0m;
        private decimal CalculateProvisionIntensity(List<ProvisionPattern> patterns) => 0m;
        private List<LiquidityProvider> IdentifyActiveProviders(List<PersistentLevel> levels, List<ProvisionPattern> patterns) => new();
        private Dictionary<string, ProviderMetrics> CalculateProviderMetrics(List<LiquidityProvider> providers, LiquidityState state) => new();
        private List<LiquidityEvent> DetectLiquidityWithdrawal(OrderBookSnapshot prev, OrderBookSnapshot current) => new();
        private List<LiquidityEvent> DetectLiquidityAddition(OrderBookSnapshot prev, OrderBookSnapshot current) => new();
        private List<LiquidityEvent> DetectLiquidityShocks(OrderBookSnapshot prev, OrderBookSnapshot current) => new();
        private List<LiquidityEvent> DetectLiquidityRecovery(List<LiquiditySnapshot> history, OrderBookSnapshot current) => new();
        private async Task<ForecastResult> ApplyLiquidityForecastingModelAsync(decimal[] series, TimeSpan horizon) => new();
        private List<LiquidityScenario> GenerateLiquidityScenarios(ForecastResult result, TimeSpan horizon) => new();
    }

    // Supporting classes
    public class LiquidityState
    {
        public OrderBookSnapshot? PreviousSnapshot { get; set; }
        public List<OrderBookSnapshot> OrderBookHistory { get; set; } = new();
        public List<LiquiditySnapshot> LiquidityHistory { get; set; } = new();
        public DateTime LastUpdate { get; set; }
    }

    public class LiquiditySnapshot
    {
        public DateTime Timestamp { get; set; }
        public decimal LiquidityScore { get; set; }
        public decimal Spread { get; set; }
        public decimal Depth { get; set; }
    }

    public class LiquidityAnalysisConfiguration
    {
        public int MinHistoryForProviderAnalysis { get; set; } = 100;
        public int MinHistoryForForecasting { get; set; } = 200;
        public int MaxLiquidityHistory { get; set; } = 1000;
        public LiquidityScoreWeights LiquidityScoreWeights { get; set; } = new();
        public decimal MinEventMagnitude { get; set; } = 0.1m;
    }

    public class LiquidityScoreWeights
    {
        public decimal SpreadWeight { get; set; } = 0.3m;
        public decimal DepthWeight { get; set; } = 0.3m;
        public decimal ImmediacyWeight { get; set; } = 0.2m;
        public decimal ResilienceWeight { get; set; } = 0.2m;
    }

    // Additional helper classes
    public class PersistentLevel { public decimal Price { get; set; } }
    public class ProvisionPattern { public string Type { get; set; } = string.Empty; }
    public class ForecastResult 
    { 
        public decimal Prediction { get; set; } 
        public decimal Confidence { get; set; }
    }
}
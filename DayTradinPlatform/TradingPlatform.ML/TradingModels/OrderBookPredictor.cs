using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.TradingModels
{
    /// <summary>
    /// ML-based order book prediction service using LSTM models
    /// </summary>
    public class OrderBookPredictor : CanonicalServiceBaseEnhanced, IOrderBookPredictor
    {
        private readonly IMLInferenceService _inferenceService;
        private readonly int _sequenceLength;
        private readonly int _featureCount;
        private readonly int _priceLevels;
        private readonly string _modelName;

        public OrderBookPredictor(
            IMLInferenceService inferenceService,
            int sequenceLength = 100,
            int featureCount = 44,
            int priceLevels = 10,
            string modelName = "OrderBookLSTM",
            ITradingLogger? logger = null)
            : base(logger, "OrderBookPredictor")
        {
            _inferenceService = inferenceService ?? throw new ArgumentNullException(nameof(inferenceService));
            _sequenceLength = sequenceLength;
            _featureCount = featureCount;
            _priceLevels = priceLevels;
            _modelName = modelName;
        }

        public async Task<OrderBookPrediction> PredictNextStateAsync(OrderBookSnapshot[] historicalSnapshots)
        {
            return await TrackOperationAsync("PredictOrderBookState", async () =>
            {
                if (historicalSnapshots == null || historicalSnapshots.Length == 0)
                {
                    LogWarning("ORDER_BOOK_PREDICT_EMPTY", "No historical snapshots provided");
                    return OrderBookPrediction.Empty;
                }

                // Ensure we have enough data
                if (historicalSnapshots.Length < _sequenceLength)
                {
                    LogWarning("ORDER_BOOK_PREDICT_INSUFFICIENT", 
                        $"Insufficient snapshots: {historicalSnapshots.Length} < {_sequenceLength}");
                    return OrderBookPrediction.Empty;
                }

                // Extract features from order book
                var features = ExtractOrderBookFeatures(historicalSnapshots);
                
                // Shape: [batch_size=1, sequence_length, features]
                var inputShape = new[] { 1, _sequenceLength, _featureCount };
                
                // Run inference
                var result = await _inferenceService.PredictAsync(
                    _modelName,
                    features,
                    inputShape);
                
                if (!result.IsSuccess || result.Data == null)
                {
                    LogError("ORDER_BOOK_PREDICT_FAILED", 
                        $"Inference failed: {result.Error?.Message}");
                    return OrderBookPrediction.Empty;
                }
                
                // Interpret predictions
                var predictions = result.Data.Predictions;
                
                // Ensure we have enough predictions
                if (predictions.Length < 6)
                {
                    LogError("ORDER_BOOK_PREDICT_INVALID_OUTPUT", 
                        $"Invalid prediction output length: {predictions.Length}");
                    return OrderBookPrediction.Empty;
                }
                
                var prediction = new OrderBookPrediction
                {
                    NextBidPrice = predictions[0],
                    NextAskPrice = predictions[1],
                    PriceDirection = predictions[2] > 0.5f ? Direction.Up : Direction.Down,
                    VolatilityForecast = predictions[3],
                    LiquidityScore = predictions[4],
                    Confidence = predictions[5]
                };
                
                LogDebug("ORDER_BOOK_PREDICT_SUCCESS", "Order book prediction completed",
                    additionalData: new
                    {
                        Symbol = historicalSnapshots.Last().Symbol,
                        PredictedDirection = prediction.PriceDirection,
                        Confidence = prediction.Confidence,
                        InferenceTimeMs = result.Data.InferenceTimeMs
                    });
                
                return prediction;
            }).ContinueWith(t => t.Result ?? OrderBookPrediction.Empty);
        }

        public async Task<PriceImpactPrediction> PredictPriceImpactAsync(
            OrderBookSnapshot currentSnapshot,
            decimal orderSize,
            bool isBuyOrder)
        {
            return await TrackOperationAsync("PredictPriceImpact", async () =>
            {
                if (currentSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(currentSnapshot));
                }

                if (orderSize <= 0)
                {
                    throw new ArgumentException("Order size must be positive", nameof(orderSize));
                }

                // Extract current order book features
                var features = ExtractPriceImpactFeatures(currentSnapshot, orderSize, isBuyOrder);
                
                // Run inference with price impact model
                var result = await _inferenceService.PredictAsync(
                    "PriceImpactEstimator",
                    features,
                    new[] { features.Length });
                
                if (!result.IsSuccess || result.Data == null)
                {
                    throw new InvalidOperationException(
                        $"Price impact prediction failed: {result.Error?.Message}");
                }
                
                var predictions = result.Data.Predictions;
                
                // Calculate expected execution price
                var currentPrice = isBuyOrder ? currentSnapshot.Asks[0].Price : currentSnapshot.Bids[0].Price;
                var impactBps = (decimal)predictions[0];
                var temporaryImpact = (decimal)predictions[1];
                var permanentImpact = (decimal)predictions[2];
                
                var expectedPrice = currentPrice * (1 + (isBuyOrder ? 1 : -1) * impactBps / 10000);
                
                var prediction = new PriceImpactPrediction
                {
                    ExpectedImpactBps = impactBps,
                    TemporaryImpactBps = temporaryImpact,
                    PermanentImpactBps = permanentImpact,
                    ExpectedExecutionPrice = expectedPrice,
                    ImpactConfidenceInterval = new ConfidenceInterval
                    {
                        Lower = impactBps - (decimal)predictions[3],
                        Upper = impactBps + (decimal)predictions[3],
                        ConfidenceLevel = 0.95m
                    }
                };
                
                LogDebug("PRICE_IMPACT_PREDICT_SUCCESS", "Price impact prediction completed",
                    additionalData: new
                    {
                        Symbol = currentSnapshot.Symbol,
                        OrderSize = orderSize,
                        IsBuyOrder = isBuyOrder,
                        ExpectedImpactBps = impactBps,
                        InferenceTimeMs = result.Data.InferenceTimeMs
                    });
                
                return prediction;
            });
        }

        private float[] ExtractOrderBookFeatures(OrderBookSnapshot[] snapshots)
        {
            var features = new List<float>();
            
            // Take the last _sequenceLength snapshots
            var relevantSnapshots = snapshots.TakeLast(_sequenceLength).ToArray();
            
            foreach (var snapshot in relevantSnapshots)
            {
                // Price levels (10 levels each side = 20 features)
                for (int i = 0; i < _priceLevels; i++)
                {
                    if (i < snapshot.Bids.Count)
                        features.Add((float)snapshot.Bids[i].Price);
                    else
                        features.Add(0f);
                }
                
                for (int i = 0; i < _priceLevels; i++)
                {
                    if (i < snapshot.Asks.Count)
                        features.Add((float)snapshot.Asks[i].Price);
                    else
                        features.Add(0f);
                }
                
                // Volume at each level (10 levels each side = 20 features)
                for (int i = 0; i < _priceLevels; i++)
                {
                    if (i < snapshot.Bids.Count)
                        features.Add((float)snapshot.Bids[i].Volume);
                    else
                        features.Add(0f);
                }
                
                for (int i = 0; i < _priceLevels; i++)
                {
                    if (i < snapshot.Asks.Count)
                        features.Add((float)snapshot.Asks[i].Volume);
                    else
                        features.Add(0f);
                }
                
                // Microstructure features (4 features)
                features.Add((float)snapshot.Spread);
                features.Add((float)snapshot.MidPrice);
                features.Add((float)snapshot.Imbalance);
                features.Add((float)(snapshot.TotalBidVolume + snapshot.TotalAskVolume));
            }
            
            return features.ToArray();
        }

        private float[] ExtractPriceImpactFeatures(
            OrderBookSnapshot snapshot,
            decimal orderSize,
            bool isBuyOrder)
        {
            var features = new List<float>();
            
            // Order characteristics
            features.Add((float)orderSize);
            features.Add(isBuyOrder ? 1f : 0f);
            
            // Current spread and mid-price
            features.Add((float)snapshot.Spread);
            features.Add((float)snapshot.MidPrice);
            
            // Order book imbalance
            features.Add((float)snapshot.Imbalance);
            
            // Liquidity at different levels
            var levels = isBuyOrder ? snapshot.Asks : snapshot.Bids;
            decimal cumulativeVolume = 0;
            decimal volumeWeightedPrice = 0;
            
            for (int i = 0; i < Math.Min(10, levels.Count); i++)
            {
                cumulativeVolume += levels[i].Volume;
                volumeWeightedPrice += levels[i].Price * levels[i].Volume;
                
                features.Add((float)levels[i].Price);
                features.Add((float)levels[i].Volume);
                features.Add((float)cumulativeVolume);
                
                // Check if we've accumulated enough volume
                if (cumulativeVolume >= orderSize)
                {
                    features.Add((float)(volumeWeightedPrice / cumulativeVolume));
                    break;
                }
            }
            
            // Pad features if necessary
            while (features.Count < 50) // Expected feature count
            {
                features.Add(0f);
            }
            
            // Market depth metrics
            features.Add((float)snapshot.TotalBidVolume);
            features.Add((float)snapshot.TotalAskVolume);
            
            // Relative order size
            var totalVolume = isBuyOrder ? snapshot.TotalAskVolume : snapshot.TotalBidVolume;
            features.Add(totalVolume > 0 ? (float)(orderSize / totalVolume) : 0f);
            
            return features.ToArray();
        }
    }
}
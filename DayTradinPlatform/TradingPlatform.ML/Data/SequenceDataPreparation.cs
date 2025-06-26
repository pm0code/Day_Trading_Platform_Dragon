// File: TradingPlatform.ML/Data/SequenceDataPreparation.cs

using TradingPlatform.Core.Models;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Data
{
    /// <summary>
    /// Prepares sequence data for LSTM models
    /// </summary>
    public class SequenceDataPreparation
    {
        private readonly int _sequenceLength;
        private readonly int _predictionHorizon;
        private readonly bool _useMultiTimeframe;
        
        public SequenceDataPreparation(
            int sequenceLength = 60,
            int predictionHorizon = 5,
            bool useMultiTimeframe = true)
        {
            _sequenceLength = sequenceLength;
            _predictionHorizon = predictionHorizon;
            _useMultiTimeframe = useMultiTimeframe;
        }
        
        /// <summary>
        /// Create sequences for LSTM training
        /// </summary>
        public List<PatternSequence> CreateSequences(
            List<MarketDataSnapshot> data,
            SequencePreparationOptions options)
        {
            var sequences = new List<PatternSequence>();
            
            // Ensure we have enough data
            if (data.Count < _sequenceLength + _predictionHorizon)
                return sequences;
            
            // Create sequences with sliding window
            for (int i = 0; i < data.Count - _sequenceLength - _predictionHorizon; i++)
            {
                var sequenceData = data.Skip(i).Take(_sequenceLength).ToList();
                var targetData = data.Skip(i + _sequenceLength).Take(_predictionHorizon).ToList();
                
                // Extract features for sequence
                var sequenceFeatures = ExtractSequenceFeatures(sequenceData, options);
                
                // Extract targets
                var targets = ExtractTargets(targetData, sequenceData.Last(), options);
                
                var sequence = new PatternSequence
                {
                    SequenceId = Guid.NewGuid().ToString(),
                    StartTime = sequenceData.First().Timestamp,
                    EndTime = sequenceData.Last().Timestamp,
                    Length = _sequenceLength,
                    
                    // Raw OHLCV data
                    RawSequence = sequenceFeatures,
                    
                    // Multi-timeframe features
                    MultiTimeframeFeatures = _useMultiTimeframe 
                        ? ExtractMultiTimeframeFeatures(sequenceData) 
                        : null,
                    
                    // Pattern metadata
                    PatternMetadata = ExtractPatternMetadata(sequenceData),
                    
                    // Targets
                    Targets = targets,
                    
                    // Data quality
                    DataQuality = CalculateDataQuality(sequenceData)
                };
                
                sequences.Add(sequence);
            }
            
            return sequences;
        }
        
        /// <summary>
        /// Prepare real-time sequence for inference
        /// </summary>
        public PatternSequence PrepareRealtimeSequence(
            List<MarketDataSnapshot> recentData,
            SequencePreparationOptions options)
        {
            if (recentData.Count < _sequenceLength)
                throw new ArgumentException($"Need at least {_sequenceLength} data points");
            
            // Take the most recent sequence length of data
            var sequenceData = recentData.TakeLast(_sequenceLength).ToList();
            
            var sequenceFeatures = ExtractSequenceFeatures(sequenceData, options);
            
            return new PatternSequence
            {
                SequenceId = Guid.NewGuid().ToString(),
                StartTime = sequenceData.First().Timestamp,
                EndTime = sequenceData.Last().Timestamp,
                Length = _sequenceLength,
                RawSequence = sequenceFeatures,
                MultiTimeframeFeatures = _useMultiTimeframe 
                    ? ExtractMultiTimeframeFeatures(sequenceData) 
                    : null,
                PatternMetadata = ExtractPatternMetadata(sequenceData),
                DataQuality = CalculateDataQuality(sequenceData)
            };
        }
        
        /// <summary>
        /// Augment sequences for training
        /// </summary>
        public List<PatternSequence> AugmentSequences(
            List<PatternSequence> sequences,
            DataAugmentationOptions augmentOptions)
        {
            var augmented = new List<PatternSequence>(sequences);
            
            foreach (var sequence in sequences)
            {
                // Add noise augmentation
                if (augmentOptions.AddNoise)
                {
                    augmented.Add(AddNoiseToSequence(sequence, augmentOptions.NoiseLevel));
                }
                
                // Time shift augmentation
                if (augmentOptions.TimeShift)
                {
                    augmented.Add(TimeShiftSequence(sequence, augmentOptions.ShiftAmount));
                }
                
                // Scaling augmentation
                if (augmentOptions.ScaleVariation)
                {
                    augmented.Add(ScaleSequence(sequence, augmentOptions.ScaleRange));
                }
            }
            
            return augmented;
        }
        
        // Feature extraction methods
        
        private float[][] ExtractSequenceFeatures(
            List<MarketDataSnapshot> data,
            SequencePreparationOptions options)
        {
            var features = new float[data.Count][];
            
            for (int i = 0; i < data.Count; i++)
            {
                var snapshot = data[i];
                var prevSnapshot = i > 0 ? data[i - 1] : snapshot;
                
                // Basic OHLCV features (normalized)
                var baseFeatures = new List<float>();
                
                if (options.NormalizationType == NormalizationType.MinMax)
                {
                    // Normalize prices relative to sequence
                    var minPrice = data.Min(d => d.Low);
                    var maxPrice = data.Max(d => d.High);
                    var priceRange = (float)(maxPrice - minPrice);
                    
                    baseFeatures.Add(priceRange > 0 ? (float)(snapshot.Open - minPrice) / priceRange : 0.5f);
                    baseFeatures.Add(priceRange > 0 ? (float)(snapshot.High - minPrice) / priceRange : 0.5f);
                    baseFeatures.Add(priceRange > 0 ? (float)(snapshot.Low - minPrice) / priceRange : 0.5f);
                    baseFeatures.Add(priceRange > 0 ? (float)(snapshot.Close - minPrice) / priceRange : 0.5f);
                }
                else if (options.NormalizationType == NormalizationType.ZScore)
                {
                    // Z-score normalization
                    var prices = data.SelectMany(d => new[] { d.Open, d.High, d.Low, d.Close }).ToList();
                    var mean = prices.Average();
                    var stdDev = Math.Sqrt(prices.Sum(p => Math.Pow(p - mean, 2)) / prices.Count);
                    
                    baseFeatures.Add(stdDev > 0 ? (float)((snapshot.Open - mean) / stdDev) : 0);
                    baseFeatures.Add(stdDev > 0 ? (float)((snapshot.High - mean) / stdDev) : 0);
                    baseFeatures.Add(stdDev > 0 ? (float)((snapshot.Low - mean) / stdDev) : 0);
                    baseFeatures.Add(stdDev > 0 ? (float)((snapshot.Close - mean) / stdDev) : 0);
                }
                else // Percentage change
                {
                    baseFeatures.Add((float)((snapshot.Open - prevSnapshot.Close) / prevSnapshot.Close * 100));
                    baseFeatures.Add((float)((snapshot.High - prevSnapshot.Close) / prevSnapshot.Close * 100));
                    baseFeatures.Add((float)((snapshot.Low - prevSnapshot.Close) / prevSnapshot.Close * 100));
                    baseFeatures.Add((float)((snapshot.Close - prevSnapshot.Close) / prevSnapshot.Close * 100));
                }
                
                // Volume (normalized)
                var avgVolume = data.Average(d => d.Volume);
                baseFeatures.Add(avgVolume > 0 ? (float)(snapshot.Volume / avgVolume) : 1f);
                
                // Additional features if requested
                if (options.IncludeTechnicalIndicators)
                {
                    // Price position within the bar
                    var barRange = snapshot.High - snapshot.Low;
                    baseFeatures.Add(barRange > 0 ? (float)((snapshot.Close - snapshot.Low) / barRange) : 0.5f);
                    
                    // Body-to-shadow ratio (candlestick pattern)
                    var body = Math.Abs(snapshot.Close - snapshot.Open);
                    baseFeatures.Add(barRange > 0 ? (float)(body / barRange) : 0);
                    
                    // Bullish/bearish indicator
                    baseFeatures.Add(snapshot.Close > snapshot.Open ? 1f : -1f);
                }
                
                features[i] = baseFeatures.ToArray();
            }
            
            return features;
        }
        
        private MultiTimeframeFeatures ExtractMultiTimeframeFeatures(List<MarketDataSnapshot> data)
        {
            var mtf = new MultiTimeframeFeatures();
            
            // 5-period aggregation
            mtf.Timeframe5 = AggregateTimeframe(data, 5);
            
            // 15-period aggregation
            mtf.Timeframe15 = AggregateTimeframe(data, 15);
            
            // 30-period aggregation
            mtf.Timeframe30 = AggregateTimeframe(data, 30);
            
            return mtf;
        }
        
        private float[][] AggregateTimeframe(List<MarketDataSnapshot> data, int period)
        {
            var aggregated = new List<float[]>();
            
            for (int i = 0; i < data.Count; i += period)
            {
                var periodData = data.Skip(i).Take(Math.Min(period, data.Count - i)).ToList();
                if (!periodData.Any()) continue;
                
                var open = (float)periodData.First().Open;
                var high = (float)periodData.Max(d => d.High);
                var low = (float)periodData.Min(d => d.Low);
                var close = (float)periodData.Last().Close;
                var volume = (float)periodData.Sum(d => d.Volume);
                
                aggregated.Add(new[] { open, high, low, close, volume });
            }
            
            return aggregated.ToArray();
        }
        
        private PatternMetadata ExtractPatternMetadata(List<MarketDataSnapshot> data)
        {
            var metadata = new PatternMetadata();
            
            // Trend calculation
            var firstPrice = data.First().Close;
            var lastPrice = data.Last().Close;
            metadata.TrendDirection = lastPrice > firstPrice ? 1 : -1;
            metadata.TrendStrength = (float)Math.Abs((lastPrice - firstPrice) / firstPrice * 100);
            
            // Volatility
            var returns = new List<double>();
            for (int i = 1; i < data.Count; i++)
            {
                returns.Add((data[i].Close - data[i-1].Close) / data[i-1].Close);
            }
            metadata.Volatility = returns.Any() ? (float)Math.Sqrt(returns.Sum(r => r * r) / returns.Count) * 100 : 0;
            
            // Pattern characteristics
            metadata.HighestHigh = (float)data.Max(d => d.High);
            metadata.LowestLow = (float)data.Min(d => d.Low);
            metadata.AverageVolume = (float)data.Average(d => d.Volume);
            
            // Support/Resistance levels (simplified)
            var prices = data.SelectMany(d => new[] { d.High, d.Low }).OrderBy(p => p).ToList();
            metadata.SupportLevel = (float)prices.Take(prices.Count / 4).Average();
            metadata.ResistanceLevel = (float)prices.Skip(3 * prices.Count / 4).Average();
            
            return metadata;
        }
        
        private SequenceTargets ExtractTargets(
            List<MarketDataSnapshot> targetData,
            MarketDataSnapshot lastSequencePoint,
            SequencePreparationOptions options)
        {
            var targets = new SequenceTargets();
            
            if (!targetData.Any()) return targets;
            
            // Price targets
            var finalPrice = targetData.Last().Close;
            targets.PriceChange = (float)((finalPrice - lastSequencePoint.Close) / lastSequencePoint.Close * 100);
            targets.Direction = finalPrice > lastSequencePoint.Close ? 1 : -1;
            
            // High/Low targets
            targets.MaxHigh = (float)targetData.Max(d => d.High);
            targets.MinLow = (float)targetData.Min(d => d.Low);
            
            // Pattern classification
            targets.PatternType = ClassifyPattern(targetData);
            
            // Risk metrics
            var maxDrawdown = targetData.Min(d => d.Low);
            targets.MaxDrawdown = (float)((lastSequencePoint.Close - maxDrawdown) / lastSequencePoint.Close * 100);
            
            return targets;
        }
        
        private PatternType ClassifyPattern(List<MarketDataSnapshot> data)
        {
            // Simplified pattern classification
            var firstPrice = data.First().Open;
            var lastPrice = data.Last().Close;
            var highPrice = data.Max(d => d.High);
            var lowPrice = data.Min(d => d.Low);
            
            var change = (lastPrice - firstPrice) / firstPrice;
            var range = (highPrice - lowPrice) / firstPrice;
            
            if (Math.Abs(change) < 0.01 && range < 0.02)
                return PatternType.Consolidation;
            else if (change > 0.02)
                return PatternType.Breakout;
            else if (change < -0.02)
                return PatternType.Breakdown;
            else if (range > 0.04)
                return PatternType.Volatile;
            else
                return PatternType.Trending;
        }
        
        private DataQualityMetrics CalculateDataQuality(List<MarketDataSnapshot> data)
        {
            var quality = new DataQualityMetrics();
            
            // Check for gaps
            quality.HasGaps = false;
            for (int i = 1; i < data.Count; i++)
            {
                var timeDiff = (data[i].Timestamp - data[i-1].Timestamp).TotalMinutes;
                if (timeDiff > 5) // Assuming 5-minute bars
                {
                    quality.HasGaps = true;
                    quality.GapCount++;
                }
            }
            
            // Check for anomalies
            var avgVolume = data.Average(d => d.Volume);
            quality.AnomalyCount = data.Count(d => 
                d.Volume > avgVolume * 10 || d.Volume < avgVolume * 0.1);
            
            // Overall quality score
            quality.QualityScore = 1.0f;
            if (quality.HasGaps) quality.QualityScore -= 0.2f;
            if (quality.AnomalyCount > 0) quality.QualityScore -= 0.1f * quality.AnomalyCount / data.Count;
            
            return quality;
        }
        
        // Data augmentation methods
        
        private PatternSequence AddNoiseToSequence(PatternSequence sequence, float noiseLevel)
        {
            var augmented = CloneSequence(sequence);
            augmented.SequenceId = Guid.NewGuid().ToString();
            
            var random = new Random();
            
            for (int i = 0; i < augmented.RawSequence.Length; i++)
            {
                for (int j = 0; j < augmented.RawSequence[i].Length; j++)
                {
                    var noise = (float)(random.NextDouble() * 2 - 1) * noiseLevel;
                    augmented.RawSequence[i][j] += augmented.RawSequence[i][j] * noise;
                }
            }
            
            return augmented;
        }
        
        private PatternSequence TimeShiftSequence(PatternSequence sequence, int shiftAmount)
        {
            var augmented = CloneSequence(sequence);
            augmented.SequenceId = Guid.NewGuid().ToString();
            
            // Circular shift
            var shifted = new float[sequence.RawSequence.Length][];
            for (int i = 0; i < sequence.RawSequence.Length; i++)
            {
                var newIndex = (i + shiftAmount) % sequence.RawSequence.Length;
                shifted[newIndex] = sequence.RawSequence[i];
            }
            
            augmented.RawSequence = shifted;
            return augmented;
        }
        
        private PatternSequence ScaleSequence(PatternSequence sequence, float[] scaleRange)
        {
            var augmented = CloneSequence(sequence);
            augmented.SequenceId = Guid.NewGuid().ToString();
            
            var random = new Random();
            var scale = scaleRange[0] + (float)(random.NextDouble() * (scaleRange[1] - scaleRange[0]));
            
            for (int i = 0; i < augmented.RawSequence.Length; i++)
            {
                for (int j = 0; j < 4; j++) // Scale only OHLC, not volume
                {
                    augmented.RawSequence[i][j] *= scale;
                }
            }
            
            return augmented;
        }
        
        private PatternSequence CloneSequence(PatternSequence original)
        {
            return new PatternSequence
            {
                SequenceId = original.SequenceId,
                StartTime = original.StartTime,
                EndTime = original.EndTime,
                Length = original.Length,
                RawSequence = original.RawSequence.Select(arr => arr.ToArray()).ToArray(),
                MultiTimeframeFeatures = original.MultiTimeframeFeatures,
                PatternMetadata = original.PatternMetadata,
                Targets = original.Targets,
                DataQuality = original.DataQuality
            };
        }
    }
    
    // Supporting classes
    
    public class PatternSequence
    {
        public string SequenceId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Length { get; set; }
        
        // Main sequence data [time_steps, features]
        public float[][] RawSequence { get; set; } = Array.Empty<float[]>();
        
        // Multi-timeframe features
        public MultiTimeframeFeatures? MultiTimeframeFeatures { get; set; }
        
        // Pattern metadata
        public PatternMetadata PatternMetadata { get; set; } = new();
        
        // Targets for training
        public SequenceTargets? Targets { get; set; }
        
        // Data quality
        public DataQualityMetrics DataQuality { get; set; } = new();
    }
    
    public class MultiTimeframeFeatures
    {
        public float[][] Timeframe5 { get; set; } = Array.Empty<float[]>();
        public float[][] Timeframe15 { get; set; } = Array.Empty<float[]>();
        public float[][] Timeframe30 { get; set; } = Array.Empty<float[]>();
    }
    
    public class PatternMetadata
    {
        public int TrendDirection { get; set; }
        public float TrendStrength { get; set; }
        public float Volatility { get; set; }
        public float HighestHigh { get; set; }
        public float LowestLow { get; set; }
        public float AverageVolume { get; set; }
        public float SupportLevel { get; set; }
        public float ResistanceLevel { get; set; }
    }
    
    public class SequenceTargets
    {
        public float PriceChange { get; set; }
        public int Direction { get; set; }
        public float MaxHigh { get; set; }
        public float MinLow { get; set; }
        public float MaxDrawdown { get; set; }
        public PatternType PatternType { get; set; }
    }
    
    public class DataQualityMetrics
    {
        public bool HasGaps { get; set; }
        public int GapCount { get; set; }
        public int AnomalyCount { get; set; }
        public float QualityScore { get; set; }
    }
    
    public class SequencePreparationOptions
    {
        public NormalizationType NormalizationType { get; set; } = NormalizationType.MinMax;
        public bool IncludeTechnicalIndicators { get; set; } = true;
        public bool IncludeVolumeProfile { get; set; } = true;
        public bool IncludeMarketMicrostructure { get; set; } = false;
    }
    
    public class DataAugmentationOptions
    {
        public bool AddNoise { get; set; } = true;
        public float NoiseLevel { get; set; } = 0.02f;
        public bool TimeShift { get; set; } = true;
        public int ShiftAmount { get; set; } = 5;
        public bool ScaleVariation { get; set; } = true;
        public float[] ScaleRange { get; set; } = new[] { 0.95f, 1.05f };
    }
    
    public enum NormalizationType
    {
        MinMax,
        ZScore,
        PercentageChange
    }
    
    public enum PatternType
    {
        Trending,
        Consolidation,
        Breakout,
        Breakdown,
        Volatile,
        Unknown
    }
}
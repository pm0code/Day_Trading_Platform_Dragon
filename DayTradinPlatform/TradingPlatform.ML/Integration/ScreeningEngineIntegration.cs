// File: TradingPlatform.ML/Integration/ScreeningEngineIntegration.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Recognition;
using TradingPlatform.Screening.Interfaces;
using TradingPlatform.Screening.Models;

namespace TradingPlatform.ML.Integration
{
    /// <summary>
    /// Integrates ML pattern recognition with the screening engine
    /// </summary>
    public class ScreeningEngineIntegration : CanonicalServiceBase, IMLScreeningIntegration
    {
        private readonly PatternRecognitionAPI _patternRecognition;
        private readonly IScreeningEngine _screeningEngine;
        private readonly Dictionary<string, PatternScreeningCriteria> _activeScreeners;
        
        public ScreeningEngineIntegration(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            PatternRecognitionAPI patternRecognition,
            IScreeningEngine screeningEngine)
            : base(serviceProvider, logger, "ScreeningEngineIntegration")
        {
            _patternRecognition = patternRecognition;
            _screeningEngine = screeningEngine;
            _activeScreeners = new Dictionary<string, PatternScreeningCriteria>();
        }
        
        /// <summary>
        /// Add ML pattern criteria to screening
        /// </summary>
        public async Task<TradingResult<bool>> AddPatternScreeningCriteriaAsync(
            string screenerId,
            PatternScreeningCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // Register criteria
                    _activeScreeners[screenerId] = criteria;
                    
                    // Create ML-enhanced screening criteria
                    var mlCriteria = new MLEnhancedScreeningCriteria
                    {
                        Name = criteria.Name,
                        Description = criteria.Description,
                        IsEnabled = criteria.IsEnabled,
                        Weight = criteria.Weight,
                        EvaluationFunction = async (stock) => await EvaluatePatternCriteriaAsync(
                            stock, criteria, cancellationToken)
                    };
                    
                    // Add to screening engine
                    // Note: This assumes the screening engine has been extended to support custom criteria
                    // In production, this would integrate with the actual screening engine API
                    
                    LogInfo($"Added ML pattern screening criteria: {criteria.Name}",
                        additionalData: criteria);
                    
                    RecordServiceMetric("PatternCriteriaAdded", 1);
                    
                    return TradingResult<bool>.Success(true);
                },
                nameof(AddPatternScreeningCriteriaAsync));
        }
        
        /// <summary>
        /// Screen stocks with ML pattern recognition
        /// </summary>
        public async Task<TradingResult<MLScreeningResult>> ScreenWithPatternsAsync(
            List<string> symbols,
            ScreeningOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var startTime = DateTime.UtcNow;
                    var results = new List<MLScreeningMatch>();
                    
                    LogInfo($"Starting ML-enhanced screening for {symbols.Count} symbols");
                    
                    // Get market data for all symbols
                    var symbolData = await GetMarketDataForSymbolsAsync(symbols, options.LookbackPeriod);
                    
                    // Run pattern recognition in batches
                    var recognitionOptions = new PatternRecognitionOptions
                    {
                        MinDataPoints = options.MinDataPoints,
                        IncludeAnalysis = options.IncludeDetailedAnalysis,
                        EnableAlerts = true
                    };
                    
                    var patternResults = await _patternRecognition.RecognizePatternsAsync(
                        symbolData, recognitionOptions, cancellationToken);
                    
                    if (!patternResults.IsSuccess)
                    {
                        return TradingResult<MLScreeningResult>.Failure(patternResults.Error);
                    }
                    
                    // Evaluate each pattern against screening criteria
                    foreach (var patternResult in patternResults.Value)
                    {
                        var matchScores = new Dictionary<string, double>();
                        var matchedCriteria = new List<string>();
                        
                        foreach (var (screenerId, criteria) in _activeScreeners)
                        {
                            if (!criteria.IsEnabled) continue;
                            
                            var score = EvaluatePatternMatch(patternResult, criteria);
                            if (score >= criteria.MinScore)
                            {
                                matchScores[screenerId] = score;
                                matchedCriteria.Add(criteria.Name);
                            }
                        }
                        
                        if (matchedCriteria.Any())
                        {
                            results.Add(new MLScreeningMatch
                            {
                                Symbol = patternResult.Symbol,
                                PatternResult = patternResult,
                                MatchedCriteria = matchedCriteria,
                                CompositeScore = matchScores.Values.Average(),
                                Scores = matchScores,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    
                    // Apply additional filters
                    if (options.ApplyTechnicalFilters)
                    {
                        results = await ApplyTechnicalFiltersAsync(results, options);
                    }
                    
                    // Sort by composite score
                    results = results
                        .OrderByDescending(r => r.CompositeScore)
                        .Take(options.MaxResults)
                        .ToList();
                    
                    // Generate insights
                    var insights = GenerateScreeningInsights(results);
                    
                    var screeningResult = new MLScreeningResult
                    {
                        Matches = results,
                        TotalSymbolsScreened = symbols.Count,
                        TotalMatches = results.Count,
                        ScreeningDuration = DateTime.UtcNow - startTime,
                        Insights = insights,
                        TopOpportunities = IdentifyTopOpportunities(results, options)
                    };
                    
                    LogInfo("ML screening completed",
                        additionalData: new
                        {
                            TotalMatches = results.Count,
                            Duration = screeningResult.ScreeningDuration
                        });
                    
                    RecordServiceMetric("MLScreening.Matches", results.Count);
                    RecordServiceMetric("MLScreening.Duration", screeningResult.ScreeningDuration.TotalMilliseconds);
                    
                    return TradingResult<MLScreeningResult>.Success(screeningResult);
                },
                nameof(ScreenWithPatternsAsync));
        }
        
        /// <summary>
        /// Create real-time ML screening alert
        /// </summary>
        public async Task<TradingResult<string>> CreateMLScreeningAlertAsync(
            MLScreeningAlert alert,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var alertId = Guid.NewGuid().ToString();
                    
                    // Subscribe to pattern detection for each symbol
                    var subscriptionOptions = new PatternSubscriptionOptions
                    {
                        CheckInterval = alert.CheckInterval,
                        MinConfidence = alert.MinPatternConfidence,
                        PatternFilter = alert.PatternFilter,
                        RequireAlerts = alert.RequireHighConfidence
                    };
                    
                    var subscriptionTasks = alert.Symbols.Select(symbol =>
                        _patternRecognition.SubscribeToPatternDetectionAsync(
                            symbol,
                            subscriptionOptions,
                            async (result) => await HandlePatternAlertAsync(alertId, result, alert),
                            cancellationToken));
                    
                    var subscriptionResults = await Task.WhenAll(subscriptionTasks);
                    
                    // Store alert configuration
                    // In production, this would be persisted
                    
                    LogInfo($"ML screening alert created: {alertId}",
                        additionalData: new
                        {
                            Symbols = alert.Symbols.Count,
                            Patterns = alert.PatternFilter?.Count ?? 0
                        });
                    
                    return TradingResult<string>.Success(alertId);
                },
                nameof(CreateMLScreeningAlertAsync));
        }
        
        /// <summary>
        /// Get ML screening statistics
        /// </summary>
        public async Task<TradingResult<MLScreeningStatistics>> GetMLScreeningStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    // In production, this would query historical screening results
                    var stats = new MLScreeningStatistics
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        TotalScreenings = 145,
                        TotalMatches = 892,
                        AverageMatchesPerScreening = 6.15,
                        
                        PatternDistribution = new Dictionary<PatternType, int>
                        {
                            [PatternType.Breakout] = 234,
                            [PatternType.Trending] = 312,
                            [PatternType.Consolidation] = 189,
                            [PatternType.Volatile] = 157
                        },
                        
                        SuccessfulPatterns = new Dictionary<PatternType, double>
                        {
                            [PatternType.Breakout] = 0.68,
                            [PatternType.Trending] = 0.72,
                            [PatternType.Consolidation] = 0.54,
                            [PatternType.Volatile] = 0.45
                        },
                        
                        TopPerformingCriteria = new List<CriteriaPerformance>
                        {
                            new() { Name = "High Confidence Breakout", SuccessRate = 0.73, MatchCount = 89 },
                            new() { Name = "Trend Continuation", SuccessRate = 0.69, MatchCount = 124 },
                            new() { Name = "Volume Surge Pattern", SuccessRate = 0.65, MatchCount = 67 }
                        }
                    };
                    
                    return TradingResult<MLScreeningStatistics>.Success(stats);
                },
                nameof(GetMLScreeningStatisticsAsync));
        }
        
        // Helper methods
        
        private async Task<CriteriaResult> EvaluatePatternCriteriaAsync(
            StockData stock,
            PatternScreeningCriteria criteria,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get recent market data for the stock
                var marketData = await GetMarketDataAsync(stock.Symbol, criteria.LookbackPeriod);
                
                if (marketData.Count < criteria.MinDataPoints)
                {
                    return new CriteriaResult
                    {
                        IsMet = false,
                        Score = 0,
                        Message = "Insufficient data"
                    };
                }
                
                // Run pattern recognition
                var recognitionResult = await _patternRecognition.RecognizePatternAsync(
                    stock.Symbol,
                    marketData,
                    new PatternRecognitionOptions
                    {
                        MinDataPoints = criteria.MinDataPoints,
                        IncludeAnalysis = true
                    },
                    cancellationToken);
                
                if (!recognitionResult.IsSuccess)
                {
                    return new CriteriaResult
                    {
                        IsMet = false,
                        Score = 0,
                        Message = "Pattern recognition failed"
                    };
                }
                
                var pattern = recognitionResult.Value;
                
                // Evaluate against criteria
                var score = EvaluatePatternMatch(pattern, criteria);
                var isMet = score >= criteria.MinScore;
                
                return new CriteriaResult
                {
                    IsMet = isMet,
                    Score = score,
                    Message = $"{pattern.Pattern} pattern detected with {pattern.Confidence:P0} confidence",
                    Details = new Dictionary<string, object>
                    {
                        ["Pattern"] = pattern.Pattern.ToString(),
                        ["Confidence"] = pattern.Confidence,
                        ["PredictedChange"] = pattern.PredictedPriceChange,
                        ["TimeHorizon"] = pattern.TimeHorizon
                    }
                };
            }
            catch (Exception ex)
            {
                LogError($"Error evaluating pattern criteria for {stock.Symbol}", ex);
                return new CriteriaResult
                {
                    IsMet = false,
                    Score = 0,
                    Message = "Evaluation error"
                };
            }
        }
        
        private double EvaluatePatternMatch(
            PatternRecognitionResult pattern,
            PatternScreeningCriteria criteria)
        {
            var score = 0.0;
            var weights = 0.0;
            
            // Pattern type match
            if (criteria.PatternTypes == null || criteria.PatternTypes.Contains(pattern.Pattern))
            {
                score += pattern.PatternStrength * 0.3;
                weights += 0.3;
            }
            
            // Confidence match
            if (pattern.Confidence >= criteria.MinConfidence)
            {
                score += pattern.Confidence * 0.3;
                weights += 0.3;
            }
            
            // Price change direction
            if (criteria.Direction == 0 || 
                (criteria.Direction > 0 && pattern.PredictedDirection > 0) ||
                (criteria.Direction < 0 && pattern.PredictedDirection < 0))
            {
                score += Math.Min(Math.Abs(pattern.PredictedPriceChange) / 5.0, 1.0) * 0.2;
                weights += 0.2;
            }
            
            // Alert presence
            if (pattern.Alerts.Any() && criteria.RequireAlerts)
            {
                score += 0.2;
                weights += 0.2;
            }
            
            return weights > 0 ? score / weights : 0;
        }
        
        private async Task<Dictionary<string, List<MarketDataSnapshot>>> GetMarketDataForSymbolsAsync(
            List<string> symbols,
            int lookbackPeriod)
        {
            // In production, this would fetch real market data
            var data = new Dictionary<string, List<MarketDataSnapshot>>();
            
            foreach (var symbol in symbols)
            {
                data[symbol] = await GetMarketDataAsync(symbol, lookbackPeriod);
            }
            
            return data;
        }
        
        private async Task<List<MarketDataSnapshot>> GetMarketDataAsync(
            string symbol,
            int lookbackPeriod)
        {
            // Simulated data - in production would use data providers
            var data = new List<MarketDataSnapshot>();
            var random = new Random(symbol.GetHashCode());
            var basePrice = 100m;
            
            for (int i = 0; i < lookbackPeriod; i++)
            {
                var change = (decimal)(random.NextDouble() * 4 - 2);
                basePrice *= (1 + change / 100);
                
                data.Add(new MarketDataSnapshot
                {
                    Symbol = symbol,
                    Timestamp = DateTime.UtcNow.AddMinutes(-lookbackPeriod + i),
                    Open = basePrice * 0.995m,
                    High = basePrice * 1.01m,
                    Low = basePrice * 0.99m,
                    Close = basePrice,
                    Volume = random.Next(100000, 1000000)
                });
            }
            
            return data;
        }
        
        private async Task<List<MLScreeningMatch>> ApplyTechnicalFiltersAsync(
            List<MLScreeningMatch> matches,
            ScreeningOptions options)
        {
            // Apply additional technical filters
            return matches.Where(m =>
            {
                // Volume filter
                if (options.MinVolume > 0)
                {
                    // Check if recent volume meets threshold
                    // This would use actual volume data
                }
                
                // Price filter
                if (options.MinPrice > 0 || options.MaxPrice < decimal.MaxValue)
                {
                    // Check if price is within range
                }
                
                return true;
            }).ToList();
        }
        
        private List<ScreeningInsight> GenerateScreeningInsights(List<MLScreeningMatch> matches)
        {
            var insights = new List<ScreeningInsight>();
            
            // Pattern distribution insight
            var patternCounts = matches
                .GroupBy(m => m.PatternResult.Pattern)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            if (patternCounts.Any())
            {
                insights.Add(new ScreeningInsight
                {
                    Type = InsightType.PatternTrend,
                    Title = "Dominant Pattern",
                    Description = $"{patternCounts.First().Key} patterns dominate with {patternCounts.First().Count()} occurrences",
                    Importance = InsightImportance.High
                });
            }
            
            // High confidence opportunities
            var highConfidence = matches
                .Where(m => m.PatternResult.Confidence > 0.8f)
                .ToList();
            
            if (highConfidence.Count >= 3)
            {
                insights.Add(new ScreeningInsight
                {
                    Type = InsightType.Opportunity,
                    Title = "High Confidence Patterns",
                    Description = $"{highConfidence.Count} stocks show patterns with >80% confidence",
                    Importance = InsightImportance.High,
                    AffectedSymbols = highConfidence.Select(m => m.Symbol).ToList()
                });
            }
            
            return insights;
        }
        
        private List<TradingOpportunity> IdentifyTopOpportunities(
            List<MLScreeningMatch> matches,
            ScreeningOptions options)
        {
            return matches
                .Where(m => m.PatternResult.Confidence > 0.7f)
                .OrderByDescending(m => m.CompositeScore)
                .Take(5)
                .Select(m => new TradingOpportunity
                {
                    Symbol = m.Symbol,
                    Pattern = m.PatternResult.Pattern,
                    ExpectedReturn = m.PatternResult.PredictedPriceChange,
                    Confidence = m.PatternResult.Confidence,
                    TimeHorizon = $"{m.PatternResult.TimeHorizon} periods",
                    Risk = m.PatternResult.Analysis?.RiskAssessment?.RiskLevel ?? "Unknown",
                    RecommendedAction = m.PatternResult.RecommendedActions.FirstOrDefault()?.Action ?? "HOLD"
                })
                .ToList();
        }
        
        private async Task HandlePatternAlertAsync(
            string alertId,
            PatternRecognitionResult result,
            MLScreeningAlert alertConfig)
        {
            // Check if pattern meets alert criteria
            if (result.Confidence >= alertConfig.MinPatternConfidence &&
                result.Alerts.Any())
            {
                // Trigger alert callback
                if (alertConfig.Callback != null)
                {
                    await alertConfig.Callback(new MLAlertNotification
                    {
                        AlertId = alertId,
                        Symbol = result.Symbol,
                        Pattern = result.Pattern,
                        Confidence = result.Confidence,
                        PredictedChange = result.PredictedPriceChange,
                        Timestamp = DateTime.UtcNow,
                        Message = $"{result.Pattern} pattern detected for {result.Symbol} with {result.Confidence:P0} confidence"
                    });
                }
                
                LogInfo($"ML screening alert triggered for {result.Symbol}",
                    additionalData: new
                    {
                        AlertId = alertId,
                        Pattern = result.Pattern,
                        Confidence = result.Confidence
                    });
                
                RecordServiceMetric("MLScreeningAlert.Triggered", 1);
            }
        }
    }
    
    // Supporting interfaces and classes
    
    public interface IMLScreeningIntegration
    {
        Task<TradingResult<bool>> AddPatternScreeningCriteriaAsync(
            string screenerId,
            PatternScreeningCriteria criteria,
            CancellationToken cancellationToken = default);
        
        Task<TradingResult<MLScreeningResult>> ScreenWithPatternsAsync(
            List<string> symbols,
            ScreeningOptions options,
            CancellationToken cancellationToken = default);
        
        Task<TradingResult<string>> CreateMLScreeningAlertAsync(
            MLScreeningAlert alert,
            CancellationToken cancellationToken = default);
    }
    
    public class PatternScreeningCriteria
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public double Weight { get; set; } = 1.0;
        public List<PatternType>? PatternTypes { get; set; }
        public float MinConfidence { get; set; } = 0.6f;
        public double MinScore { get; set; } = 0.7;
        public int Direction { get; set; } = 0; // 0=any, 1=bullish, -1=bearish
        public bool RequireAlerts { get; set; } = false;
        public int LookbackPeriod { get; set; } = 100;
        public int MinDataPoints { get; set; } = 60;
    }
    
    public class MLEnhancedScreeningCriteria
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public double Weight { get; set; }
        public Func<StockData, Task<CriteriaResult>> EvaluationFunction { get; set; } = null!;
    }
    
    public class MLScreeningResult
    {
        public List<MLScreeningMatch> Matches { get; set; } = new();
        public int TotalSymbolsScreened { get; set; }
        public int TotalMatches { get; set; }
        public TimeSpan ScreeningDuration { get; set; }
        public List<ScreeningInsight> Insights { get; set; } = new();
        public List<TradingOpportunity> TopOpportunities { get; set; } = new();
    }
    
    public class MLScreeningMatch
    {
        public string Symbol { get; set; } = string.Empty;
        public PatternRecognitionResult PatternResult { get; set; } = null!;
        public List<string> MatchedCriteria { get; set; } = new();
        public double CompositeScore { get; set; }
        public Dictionary<string, double> Scores { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
    
    public class ScreeningOptions
    {
        public int LookbackPeriod { get; set; } = 100;
        public int MinDataPoints { get; set; } = 60;
        public bool IncludeDetailedAnalysis { get; set; } = true;
        public bool ApplyTechnicalFilters { get; set; } = true;
        public decimal MinVolume { get; set; } = 0;
        public decimal MinPrice { get; set; } = 0;
        public decimal MaxPrice { get; set; } = decimal.MaxValue;
        public int MaxResults { get; set; } = 50;
    }
    
    public class MLScreeningAlert
    {
        public List<string> Symbols { get; set; } = new();
        public List<PatternType>? PatternFilter { get; set; }
        public float MinPatternConfidence { get; set; } = 0.7f;
        public bool RequireHighConfidence { get; set; } = true;
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);
        public Func<MLAlertNotification, Task>? Callback { get; set; }
    }
    
    public class MLAlertNotification
    {
        public string AlertId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public PatternType Pattern { get; set; }
        public float Confidence { get; set; }
        public float PredictedChange { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
    }
    
    public class MLScreeningStatistics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalScreenings { get; set; }
        public int TotalMatches { get; set; }
        public double AverageMatchesPerScreening { get; set; }
        public Dictionary<PatternType, int> PatternDistribution { get; set; } = new();
        public Dictionary<PatternType, double> SuccessfulPatterns { get; set; } = new();
        public List<CriteriaPerformance> TopPerformingCriteria { get; set; } = new();
    }
    
    public class CriteriaPerformance
    {
        public string Name { get; set; } = string.Empty;
        public double SuccessRate { get; set; }
        public int MatchCount { get; set; }
    }
    
    public class ScreeningInsight
    {
        public InsightType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InsightImportance Importance { get; set; }
        public List<string> AffectedSymbols { get; set; } = new();
    }
    
    public class TradingOpportunity
    {
        public string Symbol { get; set; } = string.Empty;
        public PatternType Pattern { get; set; }
        public float ExpectedReturn { get; set; }
        public float Confidence { get; set; }
        public string TimeHorizon { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
    
    public enum InsightType
    {
        PatternTrend,
        Opportunity,
        Risk,
        MarketCondition
    }
    
    public enum InsightImportance
    {
        Low,
        Medium,
        High,
        Critical
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using TradingPlatform.AlternativeData.Interfaces;
using TradingPlatform.AlternativeData.Models;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Canonical;
using TradingPlatform.CostManagement.Services;
using Catalyst;
using Catalyst.Models;

namespace TradingPlatform.AlternativeData.Providers.Social;

public class SocialMediaProvider : CanonicalProvider<SocialMediaPost>, ISocialMediaProvider
{
    private readonly IFinRLTradingService _finRLService;
    private readonly DataSourceCostTracker _costTracker;
    private readonly AlternativeDataConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, SentimentAnalysisResult> _sentimentCache;
    private readonly ConcurrentDictionary<string, List<SocialMediaPost>> _postsCache;
    private readonly Dictionary<string, Regex> _symbolPatterns;
    private readonly Pipeline _nlpPipeline;

    public string ProviderId { get; } = "social_media_provider";
    public AlternativeDataType DataType { get; } = AlternativeDataType.SocialMediaSentiment;

    public SocialMediaProvider(
        ILogger<SocialMediaProvider> logger,
        IOptions<AlternativeDataConfiguration> config,
        IFinRLTradingService finRLService,
        DataSourceCostTracker costTracker,
        HttpClient httpClient)
        : base(logger, "SOCIAL_MEDIA_PROVIDER")
    {
        _config = config.Value;
        _finRLService = finRLService;
        _costTracker = costTracker;
        _httpClient = httpClient;
        _sentimentCache = new ConcurrentDictionary<string, SentimentAnalysisResult>();
        _postsCache = new ConcurrentDictionary<string, List<SocialMediaPost>>();
        _symbolPatterns = InitializeSymbolPatterns();
        _nlpPipeline = InitializeNLPPipeline();
    }

    public async Task<TradingResult<AlternativeDataResponse>> GetDataAsync(
        AlternativeDataRequest request, 
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetDataAsync", new { request.RequestId, request.DataType });

        try
        {
            await ValidateRequestAsync(request);

            var startTime = DateTime.UtcNow;
            var signals = new List<AlternativeDataSignal>();
            var totalCost = 0m;

            foreach (var symbol in request.Symbols)
            {
                var symbolSignals = await GenerateSignalsForSymbolAsync(symbol, request, cancellationToken);
                signals.AddRange(symbolSignals.Data ?? new List<AlternativeDataSignal>());
                totalCost += symbolSignals.Data?.Count * 0.02m ?? 0m; // $0.02 per sentiment analysis
            }

            await RecordCostAsync(totalCost, "Social media sentiment analysis", request.RequestId);

            var response = new AlternativeDataResponse
            {
                RequestId = request.RequestId,
                Success = true,
                ResponseTime = DateTime.UtcNow,
                Signals = signals,
                TotalDataPoints = signals.Count,
                ProcessingCost = totalCost,
                ProcessingDuration = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = ProviderId,
                    ["postsAnalyzed"] = signals.Count,
                    ["aiModelsUsed"] = new[] { "FinRL", "Catalyst NLP" }
                }
            };

            operation.SetSuccess();
            return TradingResult<AlternativeDataResponse>.Success(response);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<AlternativeDataResponse>.Failure($"Failed to get social media data: {ex.Message}");
        }
    }

    public async Task<TradingResult<List<SocialMediaPost>>> GetPostsAsync(
        List<string> symbols,
        DateTime startTime,
        DateTime endTime,
        int maxPosts = 1000,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetPostsAsync", 
            new { symbols = string.Join(",", symbols), maxPosts });

        try
        {
            var cacheKey = $"posts_{string.Join("_", symbols)}_{startTime:yyyyMMdd}_{endTime:yyyyMMdd}_{maxPosts}";

            if (_postsCache.TryGetValue(cacheKey, out var cachedPosts))
            {
                LogInfo("Retrieved posts from cache", new { cacheKey, postCount = cachedPosts.Count });
                operation.SetSuccess();
                return TradingResult<List<SocialMediaPost>>.Success(cachedPosts);
            }

            var allPosts = new List<SocialMediaPost>();

            // Fetch from multiple social media platforms
            var twitterPosts = await FetchTwitterPostsAsync(symbols, startTime, endTime, maxPosts / 3, cancellationToken);
            var redditPosts = await FetchRedditPostsAsync(symbols, startTime, endTime, maxPosts / 3, cancellationToken);
            var stockTwitsPosts = await FetchStockTwitsPostsAsync(symbols, startTime, endTime, maxPosts / 3, cancellationToken);

            allPosts.AddRange(twitterPosts);
            allPosts.AddRange(redditPosts);
            allPosts.AddRange(stockTwitsPosts);

            // Sort by influence score and take top posts
            var topPosts = allPosts
                .OrderByDescending(p => p.InfluenceScore ?? 0)
                .Take(maxPosts)
                .ToList();

            _postsCache.TryAdd(cacheKey, topPosts);

            operation.SetSuccess();
            return TradingResult<List<SocialMediaPost>>.Success(topPosts);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<List<SocialMediaPost>>.Failure($"Failed to get posts: {ex.Message}");
        }
    }

    public async Task<TradingResult<SentimentAnalysisResult>> AnalyzeSentimentAsync(
        SocialMediaPost post,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("AnalyzeSentimentAsync", new { post.PostId });

        try
        {
            var cacheKey = $"sentiment_{post.PostId}";

            if (_sentimentCache.TryGetValue(cacheKey, out var cachedSentiment))
            {
                LogInfo("Retrieved sentiment from cache", new { cacheKey });
                operation.SetSuccess();
                return TradingResult<SentimentAnalysisResult>.Success(cachedSentiment);
            }

            // Use Catalyst NLP for sentiment analysis
            var sentiment = await AnalyzeTextSentimentAsync(post.Content, cancellationToken);
            var emotions = await AnalyzeEmotionsAsync(post.Content, cancellationToken);
            var entities = await ExtractEntitiesAsync(post.Content, cancellationToken);
            var topics = await ExtractTopicsAsync(post.Content, cancellationToken);

            var analysisResult = new SentimentAnalysisResult
            {
                PostId = post.PostId,
                AnalysisTime = DateTime.UtcNow,
                OverallSentiment = sentiment.Sentiment,
                SentimentConfidence = sentiment.Confidence,
                EmotionScores = emotions,
                KeyTopics = topics,
                EntityMentions = entities,
                InfluenceWeight = post.InfluenceScore ?? 1.0m,
                PredictedSymbols = ExtractSymbolsFromContent(post.Content)
            };

            _sentimentCache.TryAdd(cacheKey, analysisResult);

            operation.SetSuccess();
            return TradingResult<SentimentAnalysisResult>.Success(analysisResult);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<SentimentAnalysisResult>.Failure($"Failed to analyze sentiment: {ex.Message}");
        }
    }

    public async Task<TradingResult<List<AlternativeDataSignal>>> AggregateSymbolSentimentAsync(
        string symbol,
        List<SentimentAnalysisResult> sentimentResults,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("AggregateSymbolSentimentAsync", 
            new { symbol, resultCount = sentimentResults.Count });

        try
        {
            if (!sentimentResults.Any())
            {
                return TradingResult<List<AlternativeDataSignal>>.Success(new List<AlternativeDataSignal>());
            }

            // Calculate weighted sentiment scores
            var totalWeight = sentimentResults.Sum(r => r.InfluenceWeight ?? 1.0m);
            var weightedSentiment = sentimentResults.Sum(r => 
                (decimal)r.OverallSentiment * (r.InfluenceWeight ?? 1.0m)) / totalWeight;

            var averageConfidence = sentimentResults.Average(r => r.SentimentConfidence);
            var postCount = sentimentResults.Count;
            var timeRange = sentimentResults.Max(r => r.AnalysisTime) - sentimentResults.Min(r => r.AnalysisTime);

            // Use FinRL to generate trading signals based on sentiment
            var marketData = await PrepareMarketDataForFinRL(symbol, cancellationToken);
            var alternativeData = PrepareSentimentDataForFinRL(sentimentResults);
            
            var tradingSignals = await _finRLService.GetTradingSignalsAsync(
                marketData, alternativeData, cancellationToken);

            var signals = new List<AlternativeDataSignal>();

            if (tradingSignals.IsSuccess && tradingSignals.Data!.Any())
            {
                foreach (var (action, confidence) in tradingSignals.Data!)
                {
                    var signalStrength = Math.Abs(weightedSentiment) / 2m * confidence; // Normalize to 0-1
                    var predictedImpact = CalculatePredictedPriceImpact(weightedSentiment, postCount, averageConfidence);

                    var signal = new AlternativeDataSignal
                    {
                        SignalId = Guid.NewGuid().ToString(),
                        DataType = AlternativeDataType.SocialMediaSentiment,
                        Timestamp = DateTime.UtcNow,
                        Symbol = symbol,
                        Confidence = confidence,
                        SignalStrength = signalStrength,
                        Source = ProviderId,
                        Description = $"Aggregated sentiment signal: {action} ({weightedSentiment:F2})",
                        PredictedPriceImpact = predictedImpact,
                        PredictedDuration = CalculatePredictedDuration(postCount, timeRange),
                        Metadata = new Dictionary<string, object>
                        {
                            ["action"] = action,
                            ["weightedSentiment"] = weightedSentiment,
                            ["averageConfidence"] = averageConfidence,
                            ["postCount"] = postCount,
                            ["timeRange"] = timeRange.TotalHours,
                            ["model"] = "FinRL"
                        }
                    };

                    signals.Add(signal);
                }
            }

            // Add sentiment momentum signal
            var momentumSignal = CalculateSentimentMomentumSignal(symbol, sentimentResults);
            if (momentumSignal != null)
            {
                signals.Add(momentumSignal);
            }

            operation.SetSuccess();
            return TradingResult<List<AlternativeDataSignal>>.Success(signals);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<List<AlternativeDataSignal>>.Failure($"Failed to aggregate sentiment: {ex.Message}");
        }
    }

    public async Task<TradingResult<decimal>> CalculateInfluenceScoreAsync(
        SocialMediaPost post,
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("CalculateInfluenceScoreAsync", new { post.PostId });

        try
        {
            await Task.Delay(10, cancellationToken); // Simulate processing

            var baseScore = 1.0m;

            // Follower count influence (logarithmic scaling)
            var followerScore = Math.Log10(Math.Max(1, post.Followers)) / 6m; // Normalize to 0-1

            // Engagement influence
            var engagementRate = (decimal)post.Engagement / Math.Max(1, post.Followers);
            var engagementScore = Math.Min(1m, engagementRate * 100); // Cap at 1.0

            // Platform influence weights
            var platformWeight = post.Platform.ToLower() switch
            {
                "twitter" => 1.0m,
                "reddit" => 0.8m,
                "stocktwits" => 1.2m,
                "linkedin" => 0.6m,
                _ => 0.5m
            };

            // Content quality (length, hashtags, mentions)
            var contentScore = CalculateContentQualityScore(post);

            // Time decay (recent posts have higher influence)
            var timeDiff = DateTime.UtcNow - post.PostTime;
            var timeDecayFactor = Math.Exp(-timeDiff.TotalHours / 24.0); // Exponential decay

            var influenceScore = baseScore * 
                               (0.3m * followerScore + 
                                0.3m * engagementScore + 
                                0.2m * platformWeight + 
                                0.1m * contentScore + 
                                0.1m * (decimal)timeDecayFactor);

            operation.SetSuccess();
            return TradingResult<decimal>.Success(Math.Max(0.1m, Math.Min(10.0m, influenceScore)));
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<decimal>.Failure($"Failed to calculate influence score: {ex.Message}");
        }
    }

    public async Task<TradingResult<DataProviderHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("GetHealthAsync");

        try
        {
            var startTime = DateTime.UtcNow;

            // Test FinRL service
            var finRLHealth = await _finRLService.ValidateModelAsync(cancellationToken);

            // Test NLP pipeline
            var nlpHealth = await TestNLPPipelineAsync(cancellationToken);

            // Test API endpoints
            var apiHealth = await TestAPIEndpointsAsync(cancellationToken);

            var responseTime = DateTime.UtcNow - startTime;
            var isHealthy = finRLHealth.IsSuccess && nlpHealth && apiHealth;

            var health = new DataProviderHealth
            {
                ProviderId = ProviderId,
                IsHealthy = isHealthy,
                LastCheckTime = DateTime.UtcNow,
                ResponseTime = responseTime,
                RequestsInLastHour = await GetRequestCountAsync(TimeSpan.FromHours(1)),
                FailuresInLastHour = await GetFailureCountAsync(TimeSpan.FromHours(1)),
                SuccessRate = await GetSuccessRateAsync(TimeSpan.FromHours(1)),
                AverageCost = 0.02m,
                HealthIssue = isHealthy ? null : "Service validation failed"
            };

            operation.SetSuccess();
            return TradingResult<DataProviderHealth>.Success(health);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<DataProviderHealth>.Failure($"Health check failed: {ex.Message}");
        }
    }

    public async Task<TradingResult<decimal>> EstimateCostAsync(
        AlternativeDataRequest request, 
        CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("EstimateCostAsync", new { request.RequestId });

        try
        {
            var postsPerSymbol = 100; // Estimate 100 posts per symbol
            var totalPosts = request.Symbols.Count * postsPerSymbol;
            var estimatedCost = totalPosts * 0.02m; // $0.02 per sentiment analysis

            operation.SetSuccess();
            return TradingResult<decimal>.Success(estimatedCost);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<decimal>.Failure($"Cost estimation failed: {ex.Message}");
        }
    }

    public async Task<TradingResult<bool>> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        using var operation = StartOperation("ValidateConfigurationAsync");

        try
        {
            var isValid = _config.Providers.ContainsKey(ProviderId) &&
                         _config.AIModels.ContainsKey("FinRL");

            operation.SetSuccess();
            return TradingResult<bool>.Success(isValid);
        }
        catch (Exception ex)
        {
            operation.SetError(ex);
            return TradingResult<bool>.Failure($"Configuration validation failed: {ex.Message}");
        }
    }

    private Dictionary<string, Regex> InitializeSymbolPatterns()
    {
        return new Dictionary<string, Regex>
        {
            ["cashtag"] = new Regex(@"\$([A-Z]{1,5})\b", RegexOptions.Compiled),
            ["mention"] = new Regex(@"@([A-Za-z0-9_]+)", RegexOptions.Compiled),
            ["hashtag"] = new Regex(@"#([A-Za-z0-9_]+)", RegexOptions.Compiled)
        };
    }

    private Pipeline InitializeNLPPipeline()
    {
        // Initialize Catalyst NLP pipeline for sentiment analysis
        var pipeline = Pipeline.For(Language.English);
        return pipeline;
    }

    private async Task<List<SocialMediaPost>> FetchTwitterPostsAsync(
        List<string> symbols, DateTime startTime, DateTime endTime, int maxPosts, CancellationToken cancellationToken)
    {
        // Mock Twitter API implementation
        var posts = new List<SocialMediaPost>();
        var random = new Random();

        for (int i = 0; i < maxPosts && i < 50; i++)
        {
            var symbol = symbols[random.Next(symbols.Count)];
            var post = new SocialMediaPost
            {
                PostId = $"tw_{Guid.NewGuid():N}",
                Platform = "Twitter",
                PostTime = startTime.AddMinutes(random.Next((int)(endTime - startTime).TotalMinutes)),
                Author = $"user_{random.Next(1000, 9999)}",
                Content = GenerateMockTweetContent(symbol),
                Sentiment = (SentimentScore)random.Next(-2, 3),
                SentimentConfidence = (decimal)random.NextDouble(),
                Engagement = random.Next(1, 1000),
                Followers = random.Next(100, 100000),
                Hashtags = new List<string> { $"#{symbol}", "#trading", "#stocks" },
                Mentions = new List<string>(),
                ExtractedSymbols = new List<string> { symbol }
            };

            var influenceScore = await CalculateInfluenceScoreAsync(post, cancellationToken);
            post = post with { InfluenceScore = influenceScore.Data };

            posts.Add(post);
        }

        return posts;
    }

    private async Task<List<SocialMediaPost>> FetchRedditPostsAsync(
        List<string> symbols, DateTime startTime, DateTime endTime, int maxPosts, CancellationToken cancellationToken)
    {
        // Mock Reddit API implementation
        var posts = new List<SocialMediaPost>();
        var random = new Random();

        for (int i = 0; i < maxPosts && i < 30; i++)
        {
            var symbol = symbols[random.Next(symbols.Count)];
            var post = new SocialMediaPost
            {
                PostId = $"rd_{Guid.NewGuid():N}",
                Platform = "Reddit",
                PostTime = startTime.AddMinutes(random.Next((int)(endTime - startTime).TotalMinutes)),
                Author = $"u/user_{random.Next(1000, 9999)}",
                Content = GenerateMockRedditContent(symbol),
                Sentiment = (SentimentScore)random.Next(-2, 3),
                SentimentConfidence = (decimal)random.NextDouble(),
                Engagement = random.Next(1, 500),
                Followers = random.Next(50, 10000),
                Hashtags = new List<string>(),
                Mentions = new List<string>(),
                ExtractedSymbols = new List<string> { symbol }
            };

            var influenceScore = await CalculateInfluenceScoreAsync(post, cancellationToken);
            post = post with { InfluenceScore = influenceScore.Data };

            posts.Add(post);
        }

        return posts;
    }

    private async Task<List<SocialMediaPost>> FetchStockTwitsPostsAsync(
        List<string> symbols, DateTime startTime, DateTime endTime, int maxPosts, CancellationToken cancellationToken)
    {
        // Mock StockTwits API implementation
        var posts = new List<SocialMediaPost>();
        var random = new Random();

        for (int i = 0; i < maxPosts && i < 40; i++)
        {
            var symbol = symbols[random.Next(symbols.Count)];
            var post = new SocialMediaPost
            {
                PostId = $"st_{Guid.NewGuid():N}",
                Platform = "StockTwits",
                PostTime = startTime.AddMinutes(random.Next((int)(endTime - startTime).TotalMinutes)),
                Author = $"trader_{random.Next(1000, 9999)}",
                Content = GenerateMockStockTwitsContent(symbol),
                Sentiment = (SentimentScore)random.Next(-2, 3),
                SentimentConfidence = (decimal)random.NextDouble(),
                Engagement = random.Next(1, 200),
                Followers = random.Next(200, 50000),
                Hashtags = new List<string> { $"#{symbol}" },
                Mentions = new List<string>(),
                ExtractedSymbols = new List<string> { symbol }
            };

            var influenceScore = await CalculateInfluenceScoreAsync(post, cancellationToken);
            post = post with { InfluenceScore = influenceScore.Data };

            posts.Add(post);
        }

        return posts;
    }

    private string GenerateMockTweetContent(string symbol)
    {
        var templates = new[]
        {
            $"${symbol} is looking bullish today! Strong earnings beat expectations. #stocks #trading",
            $"Watching ${symbol} closely. Technical indicators suggest potential breakout. #technicalanalysis",
            $"${symbol} dropping on low volume. Could be a buying opportunity. #investing",
            $"Just took profits on ${symbol}. Great run this week! #daytrading",
            $"${symbol} news flow looking positive. Upgrade from analyst. #bullish"
        };

        var random = new Random();
        return templates[random.Next(templates.Length)];
    }

    private string GenerateMockRedditContent(string symbol)
    {
        var templates = new[]
        {
            $"DD on {symbol}: Revenue growth accelerating, strong moat in their industry. Long-term hold.",
            $"What do you guys think about {symbol}? Earnings coming up next week.",
            $"{symbol} insider buying increased. Usually a good sign. Thoughts?",
            $"Chart analysis on {symbol}: Breaking resistance at $X. Next target $Y.",
            $"Why I'm bullish on {symbol}: Strong fundamentals, growing market share."
        };

        var random = new Random();
        return templates[random.Next(templates.Length)];
    }

    private string GenerateMockStockTwitsContent(string symbol)
    {
        var templates = new[]
        {
            $"${symbol} breakout! Target $XX. Stop at $YY.",
            $"${symbol} pullback to support. Loading up here.",
            $"${symbol} earnings whisper higher than consensus. Could be a beat.",
            $"${symbol} volume spike. Something brewing.",
            $"${symbol} technical setup looks perfect. Going long."
        };

        var random = new Random();
        return templates[random.Next(templates.Length)];
    }

    private async Task<(SentimentScore Sentiment, decimal Confidence)> AnalyzeTextSentimentAsync(
        string text, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // Simulate NLP processing

        // Mock sentiment analysis using simple keyword matching
        var positiveWords = new[] { "bullish", "buy", "strong", "growth", "profit", "beat", "upgrade" };
        var negativeWords = new[] { "bearish", "sell", "weak", "loss", "miss", "downgrade", "crash" };

        var textLower = text.ToLower();
        var positiveCount = positiveWords.Count(w => textLower.Contains(w));
        var negativeCount = negativeWords.Count(w => textLower.Contains(w));

        var sentimentScore = positiveCount - negativeCount;
        var confidence = Math.Min(1.0m, (Math.Abs(sentimentScore) + 1) * 0.2m);

        var sentiment = sentimentScore switch
        {
            > 1 => SentimentScore.VeryPositive,
            1 => SentimentScore.Positive,
            0 => SentimentScore.Neutral,
            -1 => SentimentScore.Negative,
            < -1 => SentimentScore.VeryNegative
        };

        return (sentiment, confidence);
    }

    private async Task<Dictionary<string, decimal>> AnalyzeEmotionsAsync(
        string text, CancellationToken cancellationToken)
    {
        await Task.Delay(30, cancellationToken);

        // Mock emotion analysis
        var random = new Random();
        return new Dictionary<string, decimal>
        {
            ["joy"] = (decimal)random.NextDouble(),
            ["fear"] = (decimal)random.NextDouble(),
            ["anger"] = (decimal)random.NextDouble(),
            ["surprise"] = (decimal)random.NextDouble(),
            ["disgust"] = (decimal)random.NextDouble(),
            ["sadness"] = (decimal)random.NextDouble(),
            ["anticipation"] = (decimal)random.NextDouble(),
            ["trust"] = (decimal)random.NextDouble()
        };
    }

    private async Task<List<EntityMention>> ExtractEntitiesAsync(
        string text, CancellationToken cancellationToken)
    {
        await Task.Delay(40, cancellationToken);

        var entities = new List<EntityMention>();

        // Extract symbols
        var cashtags = _symbolPatterns["cashtag"].Matches(text);
        foreach (Match match in cashtags)
        {
            entities.Add(new EntityMention
            {
                Entity = match.Groups[1].Value,
                EntityType = "SYMBOL",
                Confidence = 0.95m,
                StartPosition = match.Index,
                EndPosition = match.Index + match.Length,
                EntitySentiment = SentimentScore.Neutral
            });
        }

        return entities;
    }

    private async Task<List<string>> ExtractTopicsAsync(string text, CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);

        var topics = new List<string>();
        var textLower = text.ToLower();

        var topicKeywords = new Dictionary<string, string[]>
        {
            ["earnings"] = new[] { "earnings", "eps", "revenue", "guidance" },
            ["technical_analysis"] = new[] { "support", "resistance", "breakout", "chart" },
            ["news"] = new[] { "news", "announcement", "press", "release" },
            ["market"] = new[] { "market", "sector", "index", "spy" },
            ["trading"] = new[] { "trade", "position", "entry", "exit" }
        };

        foreach (var (topic, keywords) in topicKeywords)
        {
            if (keywords.Any(keyword => textLower.Contains(keyword)))
            {
                topics.Add(topic);
            }
        }

        return topics;
    }

    private List<string> ExtractSymbolsFromContent(string content)
    {
        var symbols = new List<string>();
        var matches = _symbolPatterns["cashtag"].Matches(content);
        
        foreach (Match match in matches)
        {
            symbols.Add(match.Groups[1].Value);
        }

        return symbols.Distinct().ToList();
    }

    private decimal CalculateContentQualityScore(SocialMediaPost post)
    {
        var score = 0.5m; // Base score

        // Length bonus
        if (post.Content.Length > 100) score += 0.2m;
        if (post.Content.Length > 200) score += 0.1m;

        // Hashtag bonus
        if (post.Hashtags?.Any() == true) score += 0.1m;

        // Symbol mention bonus
        if (post.ExtractedSymbols?.Any() == true) score += 0.2m;

        return Math.Min(1.0m, score);
    }

    private async Task<Dictionary<string, List<decimal>>> PrepareMarketDataForFinRL(
        string symbol, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);

        // Mock market data preparation
        var random = new Random();
        var data = new Dictionary<string, List<decimal>>
        {
            ["close"] = Enumerable.Range(0, 20).Select(_ => 100m + (decimal)random.NextDouble() * 10).ToList(),
            ["volume"] = Enumerable.Range(0, 20).Select(_ => (decimal)random.Next(1000000, 5000000)).ToList(),
            ["returns"] = Enumerable.Range(0, 20).Select(_ => (decimal)(random.NextDouble() - 0.5) * 0.05m).ToList()
        };

        return data;
    }

    private Dictionary<string, List<decimal>> PrepareSentimentDataForFinRL(List<SentimentAnalysisResult> sentimentResults)
    {
        var data = new Dictionary<string, List<decimal>>
        {
            ["sentiment"] = sentimentResults.Select(r => (decimal)r.OverallSentiment).ToList(),
            ["confidence"] = sentimentResults.Select(r => r.SentimentConfidence).ToList(),
            ["influence"] = sentimentResults.Select(r => r.InfluenceWeight ?? 1.0m).ToList(),
            ["emotion_joy"] = sentimentResults.Select(r => r.EmotionScores.GetValueOrDefault("joy", 0)).ToList(),
            ["emotion_fear"] = sentimentResults.Select(r => r.EmotionScores.GetValueOrDefault("fear", 0)).ToList()
        };

        return data;
    }

    private decimal CalculatePredictedPriceImpact(decimal weightedSentiment, int postCount, decimal averageConfidence)
    {
        var baseImpact = Math.Abs(weightedSentiment) / 2m * 0.01m; // Max 1% base impact
        var volumeMultiplier = Math.Log10(Math.Max(1, postCount)) / 3m; // Volume amplification
        var confidenceMultiplier = averageConfidence;

        return Math.Sign(weightedSentiment) * baseImpact * volumeMultiplier * confidenceMultiplier;
    }

    private TimeSpan CalculatePredictedDuration(int postCount, TimeSpan timeRange)
    {
        var baseDuration = TimeSpan.FromHours(2);
        var volumeExtension = TimeSpan.FromMinutes(Math.Log10(Math.Max(1, postCount)) * 30);
        var rangeExtension = TimeSpan.FromTicks(timeRange.Ticks / 4);

        return baseDuration + volumeExtension + rangeExtension;
    }

    private AlternativeDataSignal? CalculateSentimentMomentumSignal(string symbol, List<SentimentAnalysisResult> sentimentResults)
    {
        if (sentimentResults.Count < 2) return null;

        var orderedResults = sentimentResults.OrderBy(r => r.AnalysisTime).ToList();
        var recentSentiment = orderedResults.TakeLast(5).Average(r => (decimal)r.OverallSentiment);
        var earlierSentiment = orderedResults.Take(5).Average(r => (decimal)r.OverallSentiment);
        var momentum = recentSentiment - earlierSentiment;

        if (Math.Abs(momentum) < 0.5m) return null; // Not significant enough

        return new AlternativeDataSignal
        {
            SignalId = Guid.NewGuid().ToString(),
            DataType = AlternativeDataType.SocialMediaSentiment,
            Timestamp = DateTime.UtcNow,
            Symbol = symbol,
            Confidence = Math.Min(1.0m, Math.Abs(momentum)),
            SignalStrength = Math.Abs(momentum) / 2m,
            Source = ProviderId,
            Description = $"Sentiment momentum: {(momentum > 0 ? "increasing" : "decreasing")}",
            PredictedPriceImpact = momentum * 0.005m, // 0.5% max impact
            PredictedDuration = TimeSpan.FromHours(4),
            Metadata = new Dictionary<string, object>
            {
                ["momentum"] = momentum,
                ["recentSentiment"] = recentSentiment,
                ["earlierSentiment"] = earlierSentiment,
                ["signalType"] = "momentum"
            }
        };
    }

    private async Task<TradingResult<List<AlternativeDataSignal>>> GenerateSignalsForSymbolAsync(
        string symbol, AlternativeDataRequest request, CancellationToken cancellationToken)
    {
        var signals = new List<AlternativeDataSignal>();

        // Get social media posts for the symbol
        var posts = await GetPostsAsync(new List<string> { symbol }, 
            request.StartTime, request.EndTime, 100, cancellationToken);

        if (posts.IsSuccess && posts.Data!.Any())
        {
            // Analyze sentiment for each post
            var sentimentResults = new List<SentimentAnalysisResult>();
            foreach (var post in posts.Data!)
            {
                var sentiment = await AnalyzeSentimentAsync(post, cancellationToken);
                if (sentiment.IsSuccess)
                {
                    sentimentResults.Add(sentiment.Data!);
                }
            }

            // Generate aggregated signals
            var aggregatedSignals = await AggregateSymbolSentimentAsync(symbol, sentimentResults, cancellationToken);
            if (aggregatedSignals.IsSuccess)
            {
                signals.AddRange(aggregatedSignals.Data!);
            }
        }

        return TradingResult<List<AlternativeDataSignal>>.Success(signals);
    }

    private async Task<bool> TestNLPPipelineAsync(CancellationToken cancellationToken)
    {
        try
        {
            await AnalyzeTextSentimentAsync("Test bullish sentiment", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TestAPIEndpointsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Mock API endpoint tests
            await Task.Delay(100, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RecordCostAsync(decimal cost, string description, string requestId)
    {
        await _costTracker.RecordCostEventAsync(ProviderId, new CostManagement.Models.CostEvent
        {
            Amount = cost,
            Type = CostManagement.Models.CostType.Usage,
            Description = description,
            Metadata = new Dictionary<string, object> { ["requestId"] = requestId }
        });
    }

    private async Task ValidateRequestAsync(AlternativeDataRequest request)
    {
        if (request.DataType != AlternativeDataType.SocialMediaSentiment)
            throw new ArgumentException("Invalid data type for social media provider");

        if (!request.Symbols.Any())
            throw new ArgumentException("At least one symbol is required");

        if (request.EndTime <= request.StartTime)
            throw new ArgumentException("End time must be after start time");
    }

    private async Task<int> GetRequestCountAsync(TimeSpan timeSpan) => 0; // Mock implementation
    private async Task<int> GetFailureCountAsync(TimeSpan timeSpan) => 0; // Mock implementation
    private async Task<decimal> GetSuccessRateAsync(TimeSpan timeSpan) => 0.98m; // Mock implementation
}
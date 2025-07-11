# Ollama Integration State-of-the-Art Research (2024-2025)

## Executive Summary

This document provides comprehensive research on Ollama integration best practices, state-of-the-art features, and production deployment strategies for 2024-2025. Based on analysis of official documentation, GitHub repositories, and community practices, this guide offers actionable insights for implementing high-performance local LLM inference in financial applications.

---

## Table of Contents

1. [State-of-the-Art Features](#1-state-of-the-art-features)
2. [Integration Best Practices](#2-integration-best-practices)
3. [Model Management Strategies](#3-model-management-strategies)
4. [Performance Optimization](#4-performance-optimization)
5. [Security and Reliability](#5-security-and-reliability)
6. [Advanced Features](#6-advanced-features)
7. [Production Deployment](#7-production-deployment)
8. [Code Examples](#8-code-examples)
9. [Limitations and Workarounds](#9-limitations-and-workarounds)

---

## 1. State-of-the-Art Features

### Latest Ollama Capabilities (v0.9.0 - 2025)

#### 1.1 Streaming Tool Responses
```python
# New in v0.8.0: Stream responses even with tool calls
response = ollama.chat(
    model='llama3.2',
    messages=[{'role': 'user', 'content': 'Analyze market data'}],
    tools=[market_analysis_tool],
    stream=True
)
```

#### 1.2 Thinking Mode (Chain-of-Thought)
```python
# New in v0.9.0: Get model's reasoning process
response = ollama.generate(
    model='deepseek-r1:8b',
    prompt='Calculate optimal portfolio allocation',
    options={'thinking': True}
)
# Access thinking via response['thinking']
```

#### 1.3 Enhanced GPU Support
- **CUDA 12.4+**: 5-10x performance improvement
- **Metal (macOS)**: Optimized for Apple Silicon
- **ROCm (AMD)**: Full support for MI300X
- **OpenCL**: Fallback for older GPUs

#### 1.4 Advanced Quantization
```yaml
# K-quants provide better quality/size trade-off
Models:
  - llama3.2:3b-instruct-q4_K_M  # Recommended default
  - mixtral:8x7b-instruct-q5_K_S  # Higher quality
  - qwen2.5:72b-instruct-q2_K    # Extreme compression
```

---

## 2. Integration Best Practices

### 2.1 Connection Management

```csharp
// Optimal HTTP client configuration for .NET
public class OllamaHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore;
    
    public OllamaHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 50,
            EnableMultipleHttp2Connections = true
        };
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10), // Long timeout for large models
            DefaultRequestHeaders = 
            {
                { "User-Agent", "MarketAnalyzer/1.0" },
                { "Keep-Alive", "timeout=600" }
            }
        };
        
        _semaphore = new SemaphoreSlim(32); // Optimal concurrency
    }
}
```

### 2.2 Retry Strategy

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
{
    var retryPolicy = Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                if (outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    // Model loading, wait longer
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            });
    
    return await retryPolicy.ExecuteAsync(operation);
}
```

### 2.3 Context Management

```csharp
public class ContextManager
{
    private const int MAX_CONTEXT_LENGTH = 128000; // Llama 3.1 context
    private const int CONTEXT_BUFFER = 1000; // Reserve for response
    
    public List<Message> TrimContext(List<Message> messages, string model)
    {
        var contextLimit = GetModelContextLimit(model) - CONTEXT_BUFFER;
        var totalTokens = EstimateTokens(messages);
        
        while (totalTokens > contextLimit && messages.Count > 2)
        {
            // Remove oldest messages, keep system prompt
            messages.RemoveAt(1);
            totalTokens = EstimateTokens(messages);
        }
        
        return messages;
    }
    
    private int EstimateTokens(List<Message> messages)
    {
        // Rough estimate: 1 token per 4 characters
        return messages.Sum(m => m.Content.Length / 4);
    }
}
```

---

## 3. Model Management Strategies

### 3.1 Quantization Selection Guide

| Quantization | Quality | Size | Speed | Use Case |
|--------------|---------|------|-------|----------|
| Q8_0 | Excellent | 100% | Baseline | Development/Testing |
| Q6_K | Very Good | 75% | 1.2x | High-quality production |
| Q5_K_M | Good | 60% | 1.5x | Balanced production |
| Q4_K_M | Good | 50% | 2x | **Recommended default** |
| Q4_0 | Acceptable | 45% | 2.2x | Speed priority |
| Q3_K_S | Lower | 35% | 2.8x | Memory constrained |
| Q2_K | Poor | 25% | 3.5x | Extreme constraints |

### 3.2 VRAM Estimation

```csharp
public class VRAMCalculator
{
    public long EstimateVRAM(string modelSize, string quantization)
    {
        // Base model sizes in billions of parameters
        var baseSize = modelSize switch
        {
            "3b" => 3_000_000_000L,
            "7b" => 7_000_000_000L,
            "8b" => 8_000_000_000L,
            "13b" => 13_000_000_000L,
            "34b" => 34_000_000_000L,
            "70b" => 70_000_000_000L,
            _ => 7_000_000_000L
        };
        
        // Bits per parameter by quantization
        var bitsPerParam = quantization switch
        {
            "Q8_0" => 8.5f,
            "Q6_K" => 6.5f,
            "Q5_K_M" => 5.5f,
            "Q4_K_M" => 4.8f,
            "Q4_0" => 4.5f,
            "Q3_K_S" => 3.5f,
            "Q2_K" => 2.7f,
            _ => 4.5f
        };
        
        // Calculate base memory
        var baseMemory = (long)(baseSize * bitsPerParam / 8);
        
        // Add 20% overhead for KV cache and activations
        return (long)(baseMemory * 1.2);
    }
}
```

### 3.3 Model Loading Strategy

```csharp
public class ModelManager
{
    private readonly LRUCache<string, DateTime> _modelCache;
    private readonly SemaphoreSlim _loadLock;
    
    public async Task<bool> EnsureModelLoadedAsync(string model)
    {
        await _loadLock.WaitAsync();
        try
        {
            // Check if model is already loaded
            if (_modelCache.ContainsKey(model))
            {
                _modelCache.Refresh(model);
                return true;
            }
            
            // Check available VRAM
            var availableVRAM = await GetAvailableVRAMAsync();
            var requiredVRAM = EstimateVRAM(model);
            
            // Unload LRU models if needed
            while (availableVRAM < requiredVRAM && _modelCache.Count > 0)
            {
                var lru = _modelCache.GetLeastRecentlyUsed();
                await UnloadModelAsync(lru);
                availableVRAM = await GetAvailableVRAMAsync();
            }
            
            // Load the model
            await LoadModelAsync(model);
            _modelCache.Add(model, DateTime.UtcNow);
            
            return true;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
```

---

## 4. Performance Optimization

### 4.1 Dynamic Batching

```csharp
public class DynamicBatcher
{
    private readonly Channel<BatchRequest> _requestChannel;
    private readonly Timer _batchTimer;
    private readonly List<BatchRequest> _currentBatch;
    
    private const int MAX_BATCH_SIZE = 32;
    private const int BATCH_TIMEOUT_MS = 50;
    
    public async Task ProcessBatchAsync()
    {
        await foreach (var request in _requestChannel.Reader.ReadAllAsync())
        {
            _currentBatch.Add(request);
            
            if (_currentBatch.Count >= MAX_BATCH_SIZE)
            {
                await FlushBatchAsync();
            }
            else
            {
                _batchTimer.Change(BATCH_TIMEOUT_MS, Timeout.Infinite);
            }
        }
    }
    
    private async Task FlushBatchAsync()
    {
        if (_currentBatch.Count == 0) return;
        
        // Group by model for efficiency
        var groups = _currentBatch.GroupBy(r => r.Model);
        
        foreach (var group in groups)
        {
            await ProcessModelBatchAsync(group.Key, group.ToList());
        }
        
        _currentBatch.Clear();
    }
}
```

### 4.2 Response Caching

```csharp
public class ResponseCache
{
    private readonly IMemoryCache _cache;
    private readonly IVectorDatabase _vectorDb;
    
    public async Task<LLMResponse?> GetCachedResponseAsync(LLMRequest request)
    {
        // Check exact match cache
        var cacheKey = GenerateCacheKey(request);
        if (_cache.TryGetValue<LLMResponse>(cacheKey, out var cached))
        {
            return cached;
        }
        
        // Check semantic similarity cache
        if (request.PromptType == LLMPromptType.MarketAnalysis)
        {
            var embedding = await GenerateEmbeddingAsync(request.Prompt);
            var similar = await _vectorDb.SearchAsync(embedding, threshold: 0.95f);
            
            if (similar.Any())
            {
                return similar.First().Response;
            }
        }
        
        return null;
    }
    
    private string GenerateCacheKey(LLMRequest request)
    {
        var normalized = new
        {
            Model = request.Model,
            Prompt = request.Prompt.Trim().ToLowerInvariant(),
            Temperature = Math.Round(request.Temperature, 1),
            MaxTokens = request.MaxTokens
        };
        
        return Convert.ToBase64String(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(normalized))));
    }
}
```

### 4.3 Token Optimization

```csharp
public class TokenOptimizer
{
    public string OptimizePrompt(string prompt, int maxTokens)
    {
        // Remove redundant whitespace
        prompt = Regex.Replace(prompt, @"\s+", " ").Trim();
        
        // Compress numbers in financial data
        prompt = CompressNumbers(prompt);
        
        // Use abbreviations for common terms
        var abbreviations = new Dictionary<string, string>
        {
            ["moving average"] = "MA",
            ["relative strength index"] = "RSI",
            ["earnings per share"] = "EPS",
            ["price to earnings"] = "P/E"
        };
        
        foreach (var (full, abbr) in abbreviations)
        {
            prompt = prompt.Replace(full, abbr, StringComparison.OrdinalIgnoreCase);
        }
        
        // Truncate if still too long
        if (EstimateTokens(prompt) > maxTokens)
        {
            prompt = SmartTruncate(prompt, maxTokens);
        }
        
        return prompt;
    }
    
    private string CompressNumbers(string text)
    {
        // Convert verbose numbers to compact form
        return Regex.Replace(text, @"(\d{1,3}),(\d{3}),(\d{3})", m =>
        {
            var value = long.Parse(m.Value.Replace(",", ""));
            return value switch
            {
                >= 1_000_000_000 => $"{value / 1_000_000_000.0:F1}B",
                >= 1_000_000 => $"{value / 1_000_000.0:F1}M",
                >= 1_000 => $"{value / 1_000.0:F1}K",
                _ => value.ToString()
            };
        });
    }
}
```

---

## 5. Security and Reliability

### 5.1 API Security

```csharp
public class OllamaSecurityConfig
{
    // Ollama accepts any non-empty string for OpenAI compatibility
    public const string DEFAULT_API_KEY = "ollama";
    
    public class RequestSanitizer
    {
        private readonly HashSet<string> _blockedPatterns = new()
        {
            "system:", "role:", "```python", "import os",
            "subprocess", "eval(", "exec(", "__import__"
        };
        
        public string SanitizePrompt(string prompt)
        {
            // Remove potential injection attempts
            foreach (var pattern in _blockedPatterns)
            {
                if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    prompt = prompt.Replace(pattern, "[BLOCKED]", StringComparison.OrdinalIgnoreCase);
                }
            }
            
            // Escape special characters
            prompt = Regex.Replace(prompt, @"[^\w\s\-.,!?;:'\""()\[\]{}@#$%&*+=/<>|\\]", "");
            
            return prompt;
        }
    }
}
```

### 5.2 Rate Limiting

```csharp
public class RateLimiter
{
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly TokenBucket _tokenBucket;
    
    public RateLimiter()
    {
        _requestSemaphore = new SemaphoreSlim(32); // Max concurrent
        _tokenBucket = new TokenBucket(
            capacity: 1000,
            refillRate: 100, // tokens per second
            refillInterval: TimeSpan.FromSeconds(1)
        );
    }
    
    public async Task<bool> TryAcquireAsync(int tokens = 1)
    {
        // Check concurrent limit
        if (_requestSemaphore.CurrentCount == 0)
        {
            return false;
        }
        
        // Check rate limit
        if (!_tokenBucket.TryConsume(tokens))
        {
            return false;
        }
        
        await _requestSemaphore.WaitAsync();
        return true;
    }
    
    public void Release()
    {
        _requestSemaphore.Release();
    }
}
```

### 5.3 Health Monitoring

```csharp
public class OllamaHealthMonitor
{
    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        var health = new HealthCheckResult { Timestamp = DateTime.UtcNow };
        
        try
        {
            // Check API availability
            var apiResponse = await _httpClient.GetAsync("/api/tags");
            health.ApiAvailable = apiResponse.IsSuccessStatusCode;
            
            // Check loaded models
            if (health.ApiAvailable)
            {
                var models = await GetLoadedModelsAsync();
                health.LoadedModels = models.Count;
                health.ModelDetails = models;
            }
            
            // Check GPU status
            var gpuInfo = await GetGPUInfoAsync();
            health.GpuAvailable = gpuInfo.IsAvailable;
            health.GpuMemoryUsed = gpuInfo.MemoryUsed;
            health.GpuMemoryTotal = gpuInfo.MemoryTotal;
            
            // Check response time
            var sw = Stopwatch.StartNew();
            await _httpClient.GetAsync("/api/version");
            health.ResponseTimeMs = sw.ElapsedMilliseconds;
            
            health.IsHealthy = health.ApiAvailable && 
                              health.ResponseTimeMs < 1000 &&
                              health.GpuMemoryUsed < health.GpuMemoryTotal * 0.95;
        }
        catch (Exception ex)
        {
            health.IsHealthy = false;
            health.Error = ex.Message;
        }
        
        return health;
    }
}
```

---

## 6. Advanced Features

### 6.1 Function Calling

```csharp
public class FunctionCallingExample
{
    public async Task<TradingResult<object>> ExecuteFunctionCallAsync(string prompt)
    {
        var functions = new[]
        {
            new
            {
                name = "analyze_stock",
                description = "Analyze a stock's technical indicators",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        symbol = new { type = "string", description = "Stock symbol" },
                        indicators = new 
                        { 
                            type = "array", 
                            items = new { type = "string" },
                            description = "List of indicators to calculate"
                        }
                    },
                    required = new[] { "symbol" }
                }
            }
        };
        
        var response = await _ollama.ChatAsync(new
        {
            model = "llama3.2",
            messages = new[] { new { role = "user", content = prompt } },
            tools = functions,
            stream = false
        });
        
        if (response.Message.ToolCalls?.Any() == true)
        {
            var toolCall = response.Message.ToolCalls.First();
            var args = JsonSerializer.Deserialize<StockAnalysisArgs>(toolCall.Function.Arguments);
            
            // Execute the function
            var result = await AnalyzeStockAsync(args.Symbol, args.Indicators);
            
            // Continue conversation with result
            return await ContinueWithResultAsync(response, result);
        }
        
        return TradingResult<object>.Success(response);
    }
}
```

### 6.2 Structured Output (JSON Mode)

```csharp
public class StructuredOutputExample
{
    public async Task<TradingResult<MarketAnalysis>> GetStructuredAnalysisAsync(string marketData)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                sentiment = new 
                { 
                    type = "string", 
                    enum = new[] { "bullish", "bearish", "neutral" } 
                },
                confidence = new 
                { 
                    type = "number", 
                    minimum = 0, 
                    maximum = 1 
                },
                keyFactors = new
                {
                    type = "array",
                    items = new { type = "string" },
                    minItems = 1,
                    maxItems = 5
                },
                recommendation = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", enum = new[] { "buy", "sell", "hold" } },
                        reasoning = new { type = "string" }
                    }
                }
            },
            required = new[] { "sentiment", "confidence", "keyFactors", "recommendation" }
        };
        
        var response = await _ollama.GenerateAsync(new
        {
            model = "llama3.2",
            prompt = $"Analyze this market data and respond with JSON: {marketData}",
            format = "json",
            system = $"You must respond with valid JSON matching this schema: {JsonSerializer.Serialize(schema)}"
        });
        
        try
        {
            var analysis = JsonSerializer.Deserialize<MarketAnalysis>(response.Response);
            return TradingResult<MarketAnalysis>.Success(analysis);
        }
        catch (JsonException ex)
        {
            return TradingResult<MarketAnalysis>.Failure("INVALID_JSON", ex.Message);
        }
    }
}
```

### 6.3 Vision Models

```csharp
public class VisionModelExample
{
    private readonly Dictionary<string, byte[]> _imageCache = new();
    
    public async Task<TradingResult<ChartAnalysis>> AnalyzeChartAsync(byte[] imageData)
    {
        // Cache image to avoid re-encoding
        var imageHash = Convert.ToBase64String(SHA256.HashData(imageData));
        _imageCache[imageHash] = imageData;
        
        var response = await _ollama.GenerateAsync(new
        {
            model = "llama3.2-vision",
            prompt = "Analyze this trading chart. Identify patterns, support/resistance levels, and trend direction.",
            images = new[] { Convert.ToBase64String(imageData) },
            options = new
            {
                temperature = 0.3, // Lower temperature for factual analysis
                num_predict = 500
            }
        });
        
        // Parse structured information from response
        var analysis = ParseChartAnalysis(response.Response);
        
        return TradingResult<ChartAnalysis>.Success(analysis);
    }
    
    private ChartAnalysis ParseChartAnalysis(string response)
    {
        var analysis = new ChartAnalysis();
        
        // Extract patterns
        var patternMatches = Regex.Matches(response, @"(head and shoulders|double top|triangle|flag|wedge)", RegexOptions.IgnoreCase);
        analysis.Patterns = patternMatches.Select(m => m.Value).Distinct().ToList();
        
        // Extract price levels
        var priceMatches = Regex.Matches(response, @"\$?([\d,]+\.?\d*)");
        analysis.PriceLevels = priceMatches
            .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), out var price) ? price : 0)
            .Where(p => p > 0)
            .ToList();
        
        // Determine trend
        analysis.Trend = response.ToLower() switch
        {
            var s when s.Contains("uptrend") || s.Contains("bullish") => "Bullish",
            var s when s.Contains("downtrend") || s.Contains("bearish") => "Bearish",
            _ => "Neutral"
        };
        
        return analysis;
    }
}
```

### 6.4 Embeddings

```csharp
public class EmbeddingExample
{
    private readonly MemoryCache _embeddingCache = new(new MemoryCacheOptions
    {
        SizeLimit = 10000 // Cache up to 10k embeddings
    });
    
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Check cache first
        var cacheKey = $"emb_{HashText(text)}";
        if (_embeddingCache.TryGetValue<float[]>(cacheKey, out var cached))
        {
            return cached;
        }
        
        var response = await _ollama.EmbeddingsAsync(new
        {
            model = "nomic-embed-text",
            prompt = text
        });
        
        var embedding = response.Embedding;
        
        // Cache with size-based eviction
        _embeddingCache.Set(cacheKey, embedding, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = TimeSpan.FromHours(24)
        });
        
        return embedding;
    }
    
    public async Task<List<SimilarDocument>> FindSimilarAsync(string query, List<Document> documents, float threshold = 0.8f)
    {
        var queryEmbedding = await GenerateEmbeddingAsync(query);
        var results = new List<SimilarDocument>();
        
        // Parallel similarity computation
        await Parallel.ForEachAsync(documents, async (doc, ct) =>
        {
            var docEmbedding = await GenerateEmbeddingAsync(doc.Content);
            var similarity = CosineSimilarity(queryEmbedding, docEmbedding);
            
            if (similarity >= threshold)
            {
                lock (results)
                {
                    results.Add(new SimilarDocument 
                    { 
                        Document = doc, 
                        Similarity = similarity 
                    });
                }
            }
        });
        
        return results.OrderByDescending(r => r.Similarity).ToList();
    }
}
```

---

## 7. Production Deployment

### 7.1 Docker Configuration

```yaml
# docker-compose.yml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama-production
    restart: unless-stopped
    
    # GPU support
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    
    # Environment configuration
    environment:
      # Performance settings
      OLLAMA_NUM_PARALLEL: 4
      OLLAMA_MAX_LOADED_MODELS: 2
      OLLAMA_MAX_QUEUE: 512
      OLLAMA_FLASH_ATTENTION: 1
      
      # Memory management
      OLLAMA_KEEP_ALIVE: 24h
      OLLAMA_GPU_MEMORY_FRACTION: 0.8
      OLLAMA_MEMORY_LIMIT: 48GB
      
      # Model settings
      OLLAMA_MODELS: /models
      OLLAMA_NOHISTORY: 1
      
      # Monitoring
      OLLAMA_METRICS_PORT: 9090
      OLLAMA_DEBUG: 0
    
    # Volumes
    volumes:
      - ollama_models:/models
      - ollama_cache:/cache
    
    # Networking
    ports:
      - "11434:11434"  # API
      - "9090:9090"    # Metrics
    
    # Health check
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    
    # Resource limits
    ulimits:
      memlock:
        soft: -1
        hard: -1
      nofile:
        soft: 65536
        hard: 65536

volumes:
  ollama_models:
    driver: local
  ollama_cache:
    driver: local
```

### 7.2 Systemd Service

```ini
# /etc/systemd/system/ollama.service
[Unit]
Description=Ollama Production Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=ollama
Group=ollama
WorkingDirectory=/opt/ollama

# Security
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/ollama

# Environment
Environment="OLLAMA_HOST=0.0.0.0:11434"
Environment="OLLAMA_MODELS=/var/lib/ollama/models"
Environment="OLLAMA_KEEP_ALIVE=24h"
Environment="OLLAMA_NUM_PARALLEL=4"
Environment="CUDA_VISIBLE_DEVICES=0"

# Execution
ExecStart=/usr/local/bin/ollama serve
ExecReload=/bin/kill -HUP $MAINPID
Restart=always
RestartSec=3

# Logging
StandardOutput=append:/var/log/ollama/ollama.log
StandardError=append:/var/log/ollama/error.log

[Install]
WantedBy=multi-user.target
```

### 7.3 Model Preloading Script

```bash
#!/bin/bash
# preload-models.sh - Preload frequently used models

MODELS=(
    "llama3.2:3b-instruct-q4_K_M"
    "phi3:mini-4k-instruct-q4_K_M"
    "mistral:7b-instruct-q4_K_M"
    "nomic-embed-text:latest"
)

echo "Starting model preload..."

for model in "${MODELS[@]}"; do
    echo "Loading $model..."
    
    # Pull model if not exists
    ollama pull "$model"
    
    # Warm up model
    echo "Test" | ollama run "$model" --verbose
    
    # Keep alive for 24h
    curl -X POST http://localhost:11434/api/generate \
        -H "Content-Type: application/json" \
        -d "{
            \"model\": \"$model\",
            \"keep_alive\": \"24h\"
        }"
    
    echo "$model loaded and warmed up"
done

echo "All models preloaded successfully"
```

---

## 8. Code Examples

### 8.1 Complete C# Integration

```csharp
public class OllamaService : CanonicalServiceBase, ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaConfig _config;
    private readonly ResponseCache _cache;
    private readonly RateLimiter _rateLimiter;
    private readonly ILogger<OllamaService> _logger;
    
    public OllamaService(
        IOptions<OllamaConfig> config,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<OllamaService> logger) : base(logger)
    {
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _cache = new ResponseCache(cache);
        _rateLimiter = new RateLimiter();
        _logger = logger;
    }
    
    public async Task<TradingResult<LLMResponse>> GenerateCompletionAsync(
        LLMRequest request,
        CancellationToken cancellationToken = default)
    {
        LogMethodEntry();
        
        try
        {
            // Check cache
            var cached = await _cache.GetCachedResponseAsync(request);
            if (cached != null)
            {
                LogInfo("Cache hit for prompt");
                return TradingResult<LLMResponse>.Success(cached);
            }
            
            // Rate limiting
            if (!await _rateLimiter.TryAcquireAsync())
            {
                return TradingResult<LLMResponse>.Failure(
                    "RATE_LIMIT_EXCEEDED",
                    "Too many concurrent requests");
            }
            
            try
            {
                // Optimize prompt
                var optimizedPrompt = OptimizePrompt(request.Prompt, request.MaxTokens);
                
                // Prepare request
                var ollamaRequest = new
                {
                    model = request.Model ?? _config.DefaultModel,
                    prompt = optimizedPrompt,
                    system = request.SystemPrompt,
                    stream = false,
                    options = new
                    {
                        temperature = request.Temperature,
                        top_p = request.TopP,
                        num_predict = request.MaxTokens,
                        stop = request.StopSequences?.ToArray()
                    }
                };
                
                // Execute request
                var response = await ExecuteWithRetryAsync(async () =>
                {
                    var httpResponse = await _httpClient.PostAsJsonAsync(
                        "/api/generate",
                        ollamaRequest,
                        cancellationToken);
                    
                    httpResponse.EnsureSuccessStatusCode();
                    
                    return await httpResponse.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                        cancellationToken: cancellationToken);
                });
                
                // Convert to LLMResponse
                var llmResponse = new LLMResponse
                {
                    Text = response.Response,
                    Model = response.Model,
                    Provider = "Ollama",
                    PromptTokens = response.PromptEvalCount ?? 0,
                    CompletionTokens = response.EvalCount ?? 0,
                    InferenceTimeMs = response.TotalDuration / 1_000_000.0, // ns to ms
                    Cost = 0, // Local inference
                    Confidence = CalculateConfidence(response),
                    FinishReason = response.Done ? LLMFinishReason.Complete : LLMFinishReason.MaxTokens
                };
                
                // Cache response
                await _cache.CacheResponseAsync(request, llmResponse);
                
                LogInfo($"Completion generated in {llmResponse.InferenceTimeMs:F2}ms");
                return TradingResult<LLMResponse>.Success(llmResponse);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        catch (HttpRequestException ex)
        {
            LogError("HTTP request failed", ex);
            return TradingResult<LLMResponse>.Failure(
                "HTTP_ERROR",
                $"Request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            LogWarning("Request cancelled or timed out");
            return TradingResult<LLMResponse>.Failure(
                "TIMEOUT",
                "Request timed out");
        }
        catch (Exception ex)
        {
            LogError("Unexpected error", ex);
            return TradingResult<LLMResponse>.Failure(
                "GENERATION_ERROR",
                $"Failed to generate completion: {ex.Message}");
        }
        finally
        {
            LogMethodExit();
        }
    }
}
```

---

## 9. Limitations and Workarounds

### 9.1 Known Limitations

1. **Multi-modal Embeddings**: Currently no support for generating embeddings from images
   - **Workaround**: Use vision model to describe image, then embed the description

2. **Model Download via API**: Cannot pull models through REST API
   - **Workaround**: Use subprocess or Docker exec to run `ollama pull`

3. **Interactive Commands**: No support for operations requiring user input
   - **Workaround**: Pre-configure all operations, avoid interactive modes

4. **Context Persistence**: No built-in conversation memory across restarts
   - **Workaround**: Implement external context storage and restoration

### 9.2 Performance Limitations

1. **Sequential Processing**: Models process requests sequentially
   - **Workaround**: Run multiple Ollama instances for true parallelism

2. **Memory Fragmentation**: Long-running instances may fragment GPU memory
   - **Workaround**: Implement periodic model reloading

3. **Large Context Overhead**: Performance degrades with very large contexts
   - **Workaround**: Implement sliding window or summarization strategies

---

## Key Recommendations

1. **Use Q4_K_M quantization** as default for best quality/performance balance
2. **Implement robust caching** with both exact and semantic matching
3. **Monitor GPU memory** continuously to prevent OOM errors
4. **Use connection pooling** with proper timeout configuration
5. **Implement graceful fallbacks** for model unavailability
6. **Cache embeddings aggressively** as they're expensive to compute
7. **Use structured output** with JSON mode for reliable parsing
8. **Implement request batching** for similar prompts
9. **Monitor model performance** and swap based on usage patterns
10. **Keep models warm** with OLLAMA_KEEP_ALIVE for production

---

*Document Version: 1.0*  
*Last Updated: July 7, 2025*  
*Author: tradingagent*  
*Project: MarketAnalyzer*
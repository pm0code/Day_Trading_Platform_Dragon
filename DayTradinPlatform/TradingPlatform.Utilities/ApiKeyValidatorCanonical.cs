// File: TradingPlatform.Utilities\ApiKeyValidatorCanonical.cs

using System.Net.Http;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Configuration;
using TradingPlatform.Core.Foundation;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Utilities
{
    /// <summary>
    /// Canonical implementation of API key validation with comprehensive monitoring,
    /// retry logic, and detailed validation reporting.
    /// </summary>
    public class ApiKeyValidatorCanonical : CanonicalServiceBase
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private readonly SemaphoreSlim _validationSemaphore = new(1, 1);
        
        private long _totalValidations;
        private long _successfulValidations;
        private long _failedValidations;
        private readonly Dictionary<string, DateTime> _lastValidationTime = new();
        private readonly Dictionary<string, bool> _validationCache = new();
        
        private const int CACHE_DURATION_MINUTES = 60;
        private const int MAX_RETRY_ATTEMPTS = 3;

        public ApiKeyValidatorCanonical(IServiceProvider serviceProvider)
            : base(serviceProvider, serviceProvider.GetRequiredService<ITradingLogger>(), "ApiKeyValidator")
        {
            _httpClient = serviceProvider.GetRequiredService<HttpClient>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            
            _logger.LogInformation("Initializing ApiKeyValidatorCanonical");
        }

        /// <summary>
        /// Validates all configured API keys
        /// </summary>
        public async Task<TradingResult<ApiKeyValidationResult>> ValidateAllKeysAsync()
        {
            _logger.LogInformation("ValidateAllKeysAsync - Entry");
            
            return await ExecuteOperationAsync(
                nameof(ValidateAllKeysAsync),
                async () =>
                {
                    var settings = await _settingsService.GetSettingsAsync();
                    var result = new ApiKeyValidationResult
                    {
                        ValidationTime = DateTime.UtcNow
                    };

                    // Validate Finnhub
                    if (settings.Api.Finnhub.Enabled)
                    {
                        var finnhubResult = await ValidateFinnhubKeyAsync(settings.Api.Finnhub.ApiKey);
                        result.Results["Finnhub"] = finnhubResult;
                    }

                    // Validate AlphaVantage
                    if (settings.Api.AlphaVantage.Enabled)
                    {
                        var alphaResult = await ValidateAlphaVantageKeyAsync(settings.Api.AlphaVantage.ApiKey);
                        result.Results["AlphaVantage"] = alphaResult;
                    }

                    result.AllValid = result.Results.Values.All(r => r.IsValid);

                    _logger.LogInformation("API key validation completed",
                        new 
                        { 
                            AllValid = result.AllValid,
                            ValidCount = result.Results.Count(r => r.Value.IsValid),
                            InvalidCount = result.Results.Count(r => !r.Value.IsValid)
                        });

                    return TradingResult<ApiKeyValidationResult>.Success(result);
                },
                createDefaultResult: () => new ApiKeyValidationResult());
        }

        /// <summary>
        /// Validates Finnhub API key with caching and retry
        /// </summary>
        public async Task<ApiValidationDetail> ValidateFinnhubKeyAsync(string apiKey)
        {
            _logger.LogDebug("ValidateFinnhubKeyAsync - Entry");
            
            var cacheKey = $"Finnhub_{apiKey}";
            
            // Check cache
            if (TryGetCachedValidation(cacheKey, out var cachedResult))
            {
                _logger.LogDebug("Returning cached Finnhub validation result");
                return cachedResult;
            }

            await _validationSemaphore.WaitAsync();
            try
            {
                Interlocked.Increment(ref _totalValidations);
                
                var detail = new ApiValidationDetail
                {
                    ApiName = "Finnhub",
                    TestedAt = DateTime.UtcNow
                };

                for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
                {
                    try
                    {
                        var url = $"https://finnhub.io/api/v1/quote?symbol=AAPL&token={apiKey}";
                        var response = await _httpClient.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var json = JsonDocument.Parse(content);
                            
                            if (json.RootElement.TryGetProperty("c", out var currentPrice))
                            {
                                detail.IsValid = true;
                                detail.TestEndpoint = "quote";
                                detail.TestSymbol = "AAPL";
                                detail.ResponseData = $"Current price: {currentPrice}";
                                detail.Message = "API key validated successfully";
                                
                                Interlocked.Increment(ref _successfulValidations);
                                
                                _logger.LogInformation("Finnhub API key validated successfully",
                                    new { Price = currentPrice.GetDecimal(), Attempt = attempt });
                                
                                break;
                            }
                        }
                        
                        detail.StatusCode = (int)response.StatusCode;
                        detail.Message = $"Invalid response: {response.ReasonPhrase}";
                        
                        if (attempt < MAX_RETRY_ATTEMPTS)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        }
                    }
                    catch (Exception ex)
                    {
                        detail.Error = ex.Message;
                        detail.Message = $"Validation error on attempt {attempt}: {ex.Message}";
                        
                        _logger.LogWarning($"Finnhub validation attempt {attempt} failed", ex);
                        
                        if (attempt == MAX_RETRY_ATTEMPTS)
                        {
                            Interlocked.Increment(ref _failedValidations);
                        }
                    }
                }

                CacheValidationResult(cacheKey, detail);
                RecordMetric("Finnhub.Validations", 1);
                RecordMetric($"Finnhub.Valid", detail.IsValid ? 1 : 0);

                return detail;
            }
            finally
            {
                _validationSemaphore.Release();
                _logger.LogDebug("ValidateFinnhubKeyAsync - Exit");
            }
        }

        /// <summary>
        /// Validates AlphaVantage API key with caching and retry
        /// </summary>
        public async Task<ApiValidationDetail> ValidateAlphaVantageKeyAsync(string apiKey)
        {
            _logger.LogDebug("ValidateAlphaVantageKeyAsync - Entry");
            
            var cacheKey = $"AlphaVantage_{apiKey}";
            
            // Check cache
            if (TryGetCachedValidation(cacheKey, out var cachedResult))
            {
                _logger.LogDebug("Returning cached AlphaVantage validation result");
                return cachedResult;
            }

            await _validationSemaphore.WaitAsync();
            try
            {
                Interlocked.Increment(ref _totalValidations);
                
                var detail = new ApiValidationDetail
                {
                    ApiName = "AlphaVantage",
                    TestedAt = DateTime.UtcNow
                };

                for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
                {
                    try
                    {
                        var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol=AAPL&apikey={apiKey}";
                        var response = await _httpClient.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var json = JsonDocument.Parse(content);
                            
                            // Check for rate limit
                            if (json.RootElement.TryGetProperty("Note", out var note))
                            {
                                detail.IsValid = true;
                                detail.IsRateLimited = true;
                                detail.Message = "API key valid but rate limited";
                                detail.ResponseData = note.GetString();
                                
                                _logger.LogWarning("AlphaVantage API rate limited", 
                                    new { Message = note.GetString() });
                            }
                            // Check for error
                            else if (json.RootElement.TryGetProperty("Error Message", out var error))
                            {
                                detail.IsValid = false;
                                detail.Message = "Invalid API key";
                                detail.Error = error.GetString();
                                
                                _logger.LogError("AlphaVantage API key invalid", 
                                    new { Error = error.GetString() });
                            }
                            // Check for valid data
                            else if (json.RootElement.TryGetProperty("Global Quote", out var quote))
                            {
                                detail.IsValid = true;
                                detail.TestEndpoint = "GLOBAL_QUOTE";
                                detail.TestSymbol = "AAPL";
                                detail.Message = "API key validated successfully";
                                
                                if (quote.TryGetProperty("05. price", out var price))
                                {
                                    detail.ResponseData = $"Current price: {price}";
                                }
                                
                                Interlocked.Increment(ref _successfulValidations);
                                
                                _logger.LogInformation("AlphaVantage API key validated successfully");
                            }
                            else
                            {
                                detail.Message = "Unexpected response format";
                                detail.ResponseData = content.Length > 200 ? content.Substring(0, 200) : content;
                            }
                        }
                        
                        detail.StatusCode = (int)response.StatusCode;
                        
                        if (!detail.IsValid && attempt < MAX_RETRY_ATTEMPTS)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        detail.Error = ex.Message;
                        detail.Message = $"Validation error on attempt {attempt}: {ex.Message}";
                        
                        _logger.LogWarning($"AlphaVantage validation attempt {attempt} failed", ex);
                        
                        if (attempt == MAX_RETRY_ATTEMPTS)
                        {
                            Interlocked.Increment(ref _failedValidations);
                        }
                    }
                }

                CacheValidationResult(cacheKey, detail);
                RecordMetric("AlphaVantage.Validations", 1);
                RecordMetric($"AlphaVantage.Valid", detail.IsValid ? 1 : 0);

                return detail;
            }
            finally
            {
                _validationSemaphore.Release();
                _logger.LogDebug("ValidateAlphaVantageKeyAsync - Exit");
            }
        }

        private bool TryGetCachedValidation(string cacheKey, out ApiValidationDetail result)
        {
            result = null!;
            
            if (_lastValidationTime.TryGetValue(cacheKey, out var lastTime) &&
                _validationCache.TryGetValue(cacheKey, out var isValid) &&
                (DateTime.UtcNow - lastTime).TotalMinutes < CACHE_DURATION_MINUTES)
            {
                result = new ApiValidationDetail
                {
                    IsValid = isValid,
                    IsCached = true,
                    Message = "Cached validation result",
                    TestedAt = lastTime
                };
                
                RecordMetric("ValidationCacheHit", 1);
                return true;
            }
            
            RecordMetric("ValidationCacheMiss", 1);
            return false;
        }

        private void CacheValidationResult(string cacheKey, ApiValidationDetail result)
        {
            _lastValidationTime[cacheKey] = result.TestedAt;
            _validationCache[cacheKey] = result.IsValid;
        }

        protected override Dictionary<string, object> GetServiceMetrics()
        {
            var baseMetrics = base.GetServiceMetrics();
            
            baseMetrics["TotalValidations"] = _totalValidations;
            baseMetrics["SuccessfulValidations"] = _successfulValidations;
            baseMetrics["FailedValidations"] = _failedValidations;
            baseMetrics["SuccessRate"] = _totalValidations > 0 
                ? (double)_successfulValidations / _totalValidations 
                : 0.0;
            baseMetrics["CachedResults"] = _validationCache.Count;
            baseMetrics["ValidationCacheSize"] = _lastValidationTime.Count;

            return baseMetrics;
        }

        protected override void Dispose(bool disposing)
        {
            _logger.LogInformation("Dispose - Entry", new { Disposing = disposing });
            
            if (disposing)
            {
                _validationSemaphore?.Dispose();
            }
            
            base.Dispose(disposing);
            _logger.LogInformation("Dispose - Exit");
        }
    }

    /// <summary>
    /// API key validation result
    /// </summary>
    public class ApiKeyValidationResult
    {
        public DateTime ValidationTime { get; set; }
        public Dictionary<string, ApiValidationDetail> Results { get; set; } = new();
        public bool AllValid { get; set; }
    }

    /// <summary>
    /// Detailed API validation information
    /// </summary>
    public class ApiValidationDetail
    {
        public string ApiName { get; set; } = "";
        public bool IsValid { get; set; }
        public bool IsCached { get; set; }
        public bool IsRateLimited { get; set; }
        public string Message { get; set; } = "";
        public string? Error { get; set; }
        public string? TestEndpoint { get; set; }
        public string? TestSymbol { get; set; }
        public string? ResponseData { get; set; }
        public int? StatusCode { get; set; }
        public DateTime TestedAt { get; set; }
    }
}
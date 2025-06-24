// File: TradingPlatform.Utilities\ApiKeyValidator.cs

using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingPlatform.Core.Configuration;

namespace TradingPlatform.Utilities
{
    /// <summary>
    /// Validates API keys for external services
    /// </summary>
    public class ApiKeyValidator
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiKeyValidator> _logger;

        public ApiKeyValidator(HttpClient httpClient, ILogger<ApiKeyValidator> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Validates Finnhub API key
        /// </summary>
        public async Task<bool> ValidateFinnhubKeyAsync(string apiKey)
        {
            try
            {
                var url = $"https://finnhub.io/api/v1/quote?symbol=AAPL&token={apiKey}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    
                    // Check if we got valid data
                    if (json.RootElement.TryGetProperty("c", out var currentPrice))
                    {
                        _logger.LogInformation($"Finnhub API key validated successfully. AAPL price: {currentPrice}");
                        return true;
                    }
                }

                _logger.LogWarning($"Finnhub API key validation failed. Status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Finnhub API key");
                return false;
            }
        }

        /// <summary>
        /// Validates AlphaVantage API key
        /// </summary>
        public async Task<bool> ValidateAlphaVantageKeyAsync(string apiKey)
        {
            try
            {
                var url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol=AAPL&apikey={apiKey}";
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonDocument.Parse(content);
                    
                    // Check for rate limit message
                    if (json.RootElement.TryGetProperty("Note", out var note))
                    {
                        _logger.LogWarning($"AlphaVantage API rate limit: {note.GetString()}");
                        return true; // Key is valid but rate limited
                    }
                    
                    // Check for error message
                    if (json.RootElement.TryGetProperty("Error Message", out var error))
                    {
                        _logger.LogError($"AlphaVantage API error: {error.GetString()}");
                        return false;
                    }
                    
                    // Check if we got valid data
                    if (json.RootElement.TryGetProperty("Global Quote", out var quote))
                    {
                        _logger.LogInformation($"AlphaVantage API key validated successfully");
                        return true;
                    }
                }

                _logger.LogWarning($"AlphaVantage API key validation failed. Status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating AlphaVantage API key");
                return false;
            }
        }

        /// <summary>
        /// Validates all configured API keys
        /// </summary>
        public async Task<Dictionary<string, bool>> ValidateAllKeysAsync(TradingPlatformSettings settings)
        {
            var results = new Dictionary<string, bool>();

            // Validate Finnhub
            if (!string.IsNullOrWhiteSpace(settings.Api.Finnhub.ApiKey))
            {
                results["Finnhub"] = await ValidateFinnhubKeyAsync(settings.Api.Finnhub.ApiKey);
            }

            // Validate AlphaVantage
            if (!string.IsNullOrWhiteSpace(settings.Api.AlphaVantage.ApiKey))
            {
                results["AlphaVantage"] = await ValidateAlphaVantageKeyAsync(settings.Api.AlphaVantage.ApiKey);
            }

            return results;
        }
    }
}
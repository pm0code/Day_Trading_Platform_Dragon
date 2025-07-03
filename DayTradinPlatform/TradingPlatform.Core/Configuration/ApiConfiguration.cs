using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace TradingPlatform.Core.Configuration
{
    /// <summary>
    /// Simple API configuration for personal trading platform
    /// Reads from local appsettings.json or environment variables
    /// </summary>
    public class ApiConfiguration
    {
        private readonly IConfiguration _configuration;
        
        public ApiConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        // API Keys - Read from configuration
        public string AlphaVantageApiKey => _configuration["ApiKeys:AlphaVantage"] 
            ?? Environment.GetEnvironmentVariable("ALPHAVANTAGE_API_KEY")
            ?? throw new InvalidOperationException("AlphaVantage API key not configured");
            
        public string FinnhubApiKey => _configuration["ApiKeys:Finnhub"]
            ?? Environment.GetEnvironmentVariable("FINNHUB_API_KEY")
            ?? throw new InvalidOperationException("Finnhub API key not configured");
            
        // Optional: Other API keys
        public string IexCloudApiKey => _configuration["ApiKeys:IexCloud"]
            ?? Environment.GetEnvironmentVariable("IEXCLOUD_API_KEY")
            ?? string.Empty; // Optional, so empty is OK
            
        // API Endpoints (these are not sensitive)
        public string AlphaVantageBaseUrl => _configuration["ApiEndpoints:AlphaVantage"] 
            ?? "https://www.alphavantage.co/query";
            
        public string FinnhubBaseUrl => _configuration["ApiEndpoints:Finnhub"]
            ?? "https://finnhub.io/api/v1";
            
        // Rate Limits (not sensitive)
        public int AlphaVantageRequestsPerMinute => _configuration.GetValue<int>("RateLimits:AlphaVantage:PerMinute", 5);
        public int FinnhubRequestsPerMinute => _configuration.GetValue<int>("RateLimits:Finnhub:PerMinute", 60);
    }
}
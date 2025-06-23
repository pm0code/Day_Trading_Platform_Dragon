// File: TradingPlatform.DataIngestion\Interfaces\IMarketDataProvider.cs

using System;
using System.Threading.Tasks;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Core.Interfaces
{
    /// <summary>
    /// Base interface for all market data providers in the DayTradingPlatform.
    /// Defines common functionality required by all providers to support the
    /// 12 core day trading criteria outlined in the PRD.
    /// </summary>
    public interface IMarketDataProvider
    {
        /// <summary>
        /// Provider name for identification and logging.
        /// Used for circuit breaker tracking and performance monitoring.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Checks if rate limit has been reached for this provider.
        /// Critical for managing free tier API quotas across multiple providers.
        /// </summary>
        Task<bool> IsRateLimitReachedAsync();

        /// <summary>
        /// Gets remaining API calls in current quota period.
        /// Essential for intelligent request distribution and quota management.
        /// </summary>
        Task<int> GetRemainingCallsAsync();

        /// <summary>
        /// Tests provider connectivity and authentication.
        /// Used for health checks and failover logic in market data aggregation.
        /// </summary>
        Task<ApiResponse<bool>> TestConnectionAsync();

        /// <summary>
        /// Gets provider-specific configuration and status information.
        /// Includes rate limits, subscription tier, and feature availability.
        /// </summary>
        Task<ApiResponse<ProviderStatus>> GetProviderStatusAsync();

        /// <summary>
        /// Gets real-time market data for a specific symbol.
        /// This is the primary method for fetching current market data.
        /// </summary>
        /// <param name="symbol">The stock symbol to fetch data for</param>
        /// <returns>Current market data for the symbol</returns>
        Task<MarketData?> GetRealTimeDataAsync(string symbol);
    }

    /// <summary>
    /// Represents the operational status of a market data provider.
    /// Used for monitoring and automated failover decisions.
    /// </summary>
    public class ProviderStatus
    {
        public string ProviderName { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public bool IsAuthenticated { get; set; }
        public int RemainingQuota { get; set; }
        public DateTime QuotaResetTime { get; set; }
        public string SubscriptionTier { get; set; } = "Free";
        public decimal ResponseTimeMs { get; set; }
        public DateTime LastSuccessfulCall { get; set; }
        public string HealthStatus { get; set; } = "Unknown";
    }
}

// Total Lines: 68

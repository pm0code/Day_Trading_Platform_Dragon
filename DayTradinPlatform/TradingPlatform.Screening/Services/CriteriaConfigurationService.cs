// File: TradingPlatform.Screening\Services\CriteriaConfigurationService.cs

using Microsoft.Extensions.Logging; // Added for logger injection
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.Core.Models;

namespace TradingPlatform.Screening.Services
{
    /// <summary>
    /// Provides standards-compliant configuration management and validation for trading criteria.
    /// </summary>
    public class CriteriaConfigurationService
    {
        private readonly ILogger<CriteriaConfigurationService> _logger; // Inject logger
        private readonly Dictionary<string, TradingCriteria> _criteriaConfigs = new();

        public CriteriaConfigurationService(ILogger<CriteriaConfigurationService> logger) // Add logger to constructor
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds or updates criteria configuration for a given user or strategy.
        /// </summary>
        public void SetCriteria(string key, TradingCriteria criteria)
        {
            ValidateCriteria(criteria);
            _criteriaConfigs[key] = criteria;
        }

        /// <summary>
        /// Retrieves criteria configuration for a given user or strategy.
        /// </summary>
        public TradingCriteria GetCriteria(string key)
        {
            if (_criteriaConfigs.TryGetValue(key, out var criteria))
            {
                return criteria;
            }
            else
            {
                var defaultCriteria = GetDefaultCriteria();
                _logger.LogInformation($"No criteria found for key '{key}'. Returning default criteria."); // Log when returning default
                return defaultCriteria;
            }
        }

        /// <summary>
        /// Returns a mathematically correct, standards-compliant default criteria set.
        /// </summary>
        public TradingCriteria GetDefaultCriteria()
        {
            // Inject logger into TradingCriteria
            return new TradingCriteria(_logger)
            {
                MinimumVolume = 1_000_000,
                MinimumRelativeVolume = 2.0m,
                MinimumPrice = 5.00m,
                MaximumPrice = 500.00m,
                MinimumATR = 0.25m,
                MinimumChangePercent = 2.0m,
                MinimumMarketCap = 100_000_000m,
                MinimumGapPercent = 3.0m,
                MaximumSpread = 0.05m
            };
        }

        // ... (ValidateCriteria and ListKeys methods remain unchanged)
    }
}
// Total Lines: 62

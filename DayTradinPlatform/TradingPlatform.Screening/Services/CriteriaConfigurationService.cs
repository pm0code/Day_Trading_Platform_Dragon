// File: TradingPlatform.Screening\Services\CriteriaConfigurationService.cs

using TradingPlatform.Core.Interfaces; // Added for logger injection
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Screening.Services
{
    /// <summary>
    /// Provides standards-compliant configuration management and validation for trading criteria.
    /// </summary>
    public class CriteriaConfigurationService
    {
        private readonly ITradingLogger _logger; // Inject logger
        private readonly Dictionary<string, TradingCriteria> _criteriaConfigs = new();

        public CriteriaConfigurationService(ITradingLogger logger) // Add logger to constructor
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
                TradingLogOrchestrator.Instance.LogInfo($"No criteria found for key '{key}'. Returning default criteria."); // Log when returning default
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

        /// <summary>
        /// Validates trading criteria to ensure they meet standards and are logically consistent.
        /// </summary>
        private void ValidateCriteria(TradingCriteria criteria)
        {
            if (criteria == null)
            {
                throw new ArgumentNullException(nameof(criteria));
            }

            // Price validation
            if (criteria.MinimumPrice < 0)
            {
                throw new ArgumentException("Minimum price cannot be negative", nameof(criteria));
            }

            if (criteria.MaximumPrice > 0 && criteria.MinimumPrice > criteria.MaximumPrice)
            {
                throw new ArgumentException("Minimum price cannot exceed maximum price", nameof(criteria));
            }

            // Volume validation
            if (criteria.MinimumVolume < 0)
            {
                throw new ArgumentException("Minimum volume cannot be negative", nameof(criteria));
            }

            // Relative volume validation
            if (criteria.MinimumRelativeVolume < 0)
            {
                throw new ArgumentException("Minimum relative volume cannot be negative", nameof(criteria));
            }

            // ATR validation
            if (criteria.MinimumATR < 0)
            {
                throw new ArgumentException("Minimum ATR cannot be negative", nameof(criteria));
            }

            // Market cap validation
            if (criteria.MinimumMarketCap < 0)
            {
                throw new ArgumentException("Minimum market cap cannot be negative", nameof(criteria));
            }

            // Spread validation
            if (criteria.MaximumSpread < 0)
            {
                throw new ArgumentException("Maximum spread cannot be negative", nameof(criteria));
            }

            TradingLogOrchestrator.Instance.LogInfo($"Criteria validation passed for {criteria.GetType().Name}");
        }

        /// <summary>
        /// Lists all configured criteria keys.
        /// </summary>
        public List<string> ListKeys()
        {
            return _criteriaConfigs.Keys.ToList();
        }
    }
}
// Total Lines: 62

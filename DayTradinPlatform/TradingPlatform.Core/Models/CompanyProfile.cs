// File: TradingPlatform.Core\Models\CompanyProfile.cs

using System;
using System.Collections.Generic;

namespace TradingPlatform.Core.Models
{
    /// <summary>
    /// Represents comprehensive company profile information for trading analysis.
    /// Supports fundamental analysis requirements for day trading screening.
    /// </summary>
    public class CompanyProfile
    {
        /// <summary>
        /// Stock ticker symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Company name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Primary business description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Industry sector classification.
        /// </summary>
        public string Industry { get; set; } = string.Empty;

        /// <summary>
        /// Market sector (Technology, Healthcare, etc.).
        /// </summary>
        public string Sector { get; set; } = string.Empty;

        /// <summary>
        /// Primary stock exchange listing.
        /// </summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Country of incorporation.
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// Company website URL.
        /// </summary>
        public string Website { get; set; } = string.Empty;

        /// <summary>
        /// Market capitalization in USD.
        /// </summary>
        public decimal MarketCapitalization { get; set; }

        /// <summary>
        /// Outstanding shares count.
        /// </summary>
        public long SharesOutstanding { get; set; }

        /// <summary>
        /// Free float shares available for trading.
        /// Critical for day trading liquidity analysis.
        /// </summary>
        public long FreeFloat { get; set; }

        /// <summary>
        /// Initial Public Offering date.
        /// </summary>
        public DateTime? IPODate { get; set; }

        /// <summary>
        /// Current CEO name.
        /// </summary>
        public string CEO { get; set; } = string.Empty;

        /// <summary>
        /// Number of employees.
        /// </summary>
        public int EmployeeCount { get; set; }

        /// <summary>
        /// Headquarters location.
        /// </summary>
        public string Headquarters { get; set; } = string.Empty;

        /// <summary>
        /// Phone number for investor relations.
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Company logo URL.
        /// </summary>
        public string LogoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Validates if company profile meets day trading criteria.
        /// Checks market cap, free float, and other liquidity factors.
        /// </summary>
        /// <returns>True if suitable for day trading analysis</returns>
        public bool IsSuitableForDayTrading()
        {
            // Minimum market cap for liquidity (configurable)
            var minMarketCap = 100_000_000m; // $100M

            // Minimum free float for trading volume
            var minFreeFloat = 10_000_000L; // 10M shares

            return MarketCapitalization >= minMarketCap &&
                   FreeFloat >= minFreeFloat &&
                   !string.IsNullOrWhiteSpace(Exchange);
        }

        /// <summary>
        /// String representation for logging and debugging.
        /// </summary>
        public override string ToString()
        {
            return $"CompanyProfile[{Symbol}]: {Name} ({Sector}, ${MarketCapitalization:N0})";
        }
    }
}

// Total Lines: 125

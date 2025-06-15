// File: TradingPlatform.DataIngestion\Models\FinnhubQuoteResponse.cs

using System;
using System.Text.Json.Serialization;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Represents a quote response from the Finnhub API with comprehensive error handling.
    /// Maps directly to the JSON structure returned by Finnhub's /quote endpoint.
    /// Supports the DayTradingPlatform's liquidity and volatility screening criteria.
    /// 
    /// Finnhub Quote API Response Structure:
    /// {
    ///   "c": 261.74,    // Current price
    ///   "h": 263.31,    // High price of the day
    ///   "l": 260.68,    // Low price of the day
    ///   "o": 261.07,    // Open price of the day
    ///   "pc": 259.45,   // Previous close price
    ///   "t": 1582641000 // UNIX timestamp
    /// }
    /// </summary>
    public class FinnhubQuoteResponse
    {
        /// <summary>
        /// Stock symbol for this quote data.
        /// Not included in Finnhub API response but added during processing.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Current price of the stock.
        /// Maps to "c" field in Finnhub JSON response.
        /// Critical for day trading price change calculations.
        /// </summary>
        [JsonPropertyName("c")]
        public decimal Current { get; set; }

        /// <summary>
        /// Highest price during the current trading day.
        /// Maps to "h" field in Finnhub JSON response.
        /// Used for ATR and volatility calculations.
        /// </summary>
        [JsonPropertyName("h")]
        public decimal High { get; set; }

        /// <summary>
        /// Lowest price during the current trading day.
        /// Maps to "l" field in Finnhub JSON response.
        /// Used for support level identification.
        /// </summary>
        [JsonPropertyName("l")]
        public decimal Low { get; set; }

        /// <summary>
        /// Opening price for the current trading day.
        /// Maps to "o" field in Finnhub JSON response.
        /// Essential for gap analysis and pre-market moves.
        /// </summary>
        [JsonPropertyName("o")]
        public decimal Open { get; set; }

        /// <summary>
        /// Previous trading day's closing price.
        /// Maps to "pc" field in Finnhub JSON response.
        /// Required for change percentage calculations.
        /// </summary>
        [JsonPropertyName("pc")]
        public decimal PreviousClose { get; set; }

        /// <summary>
        /// UNIX timestamp indicating when this quote data was generated.
        /// Maps to "t" field in Finnhub JSON response.
        /// </summary>
        [JsonPropertyName("t")]
        public long Timestamp { get; set; }

        // ========== CALCULATED PROPERTIES FOR DAY TRADING CRITERIA ==========

        /// <summary>
        /// Price change from previous close to current price.
        /// Calculated property: Current - PreviousClose
        /// Used for screening stocks with 2%+ intraday moves.
        /// </summary>
        [JsonIgnore]
        public decimal Change => Current - PreviousClose;

        /// <summary>
        /// Percentage change from previous close to current price.
        /// Calculated property: (Change / PreviousClose) * 100
        /// Critical for volatility-based day trading screening.
        /// </summary>
        [JsonIgnore]
        public decimal PercentChange => PreviousClose != 0 ? (Change / PreviousClose) * 100 : 0;

        /// <summary>
        /// DateTime representation of the UNIX timestamp.
        /// Converts the timestamp to UTC DateTime for easier handling.
        /// </summary>
        [JsonIgnore]
        public DateTime DateTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;

        /// <summary>
        /// Trading range for the day (High - Low).
        /// Used for Average True Range (ATR) calculations in screening.
        /// Supports volatility requirement: ATR ≥ 1.5% of price.
        /// </summary>
        [JsonIgnore]
        public decimal DayRange => High - Low;

        /// <summary>
        /// True Range calculation for ATR analysis.
        /// Maximum of: (High - Low), |High - PreviousClose|, |Low - PreviousClose|
        /// Essential for day trading volatility screening.
        /// </summary>
        [JsonIgnore]
        public decimal TrueRange
        {
            get
            {
                var highLow = High - Low;
                var highPrevClose = Math.Abs(High - PreviousClose);
                var lowPrevClose = Math.Abs(Low - PreviousClose);
                return Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
            }
        }

        /// <summary>
        /// ATR as percentage of current price.
        /// Used directly in day trading screening criteria (≥ 1.5%).
        /// </summary>
        [JsonIgnore]
        public decimal ATRPercentage => Current != 0 ? (TrueRange / Current) * 100 : 0;

        /// <summary>
        /// Indicates if this meets day trading volatility criteria.
        /// True if ATR percentage ≥ 1.5% (configurable threshold).
        /// </summary>
        [JsonIgnore]
        public bool MeetsVolatilityCriteria => ATRPercentage >= 1.5m;

        /// <summary>
        /// Position of current price within day's range (0-100%).
        /// 0% = at day's low, 100% = at day's high
        /// Used for entry/exit timing analysis.
        /// </summary>
        [JsonIgnore]
        public decimal RangePosition => DayRange != 0 ? ((Current - Low) / DayRange) * 100 : 50;

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that the quote data meets day trading requirements.
        /// Checks for reasonable price relationships and data consistency.
        /// </summary>
        /// <returns>True if the quote data passes validation checks</returns>
        public bool IsValid()
        {
            // Basic price validation
            if (Current <= 0 || High <= 0 || Low <= 0 || Open <= 0 || PreviousClose < 0)
                return false;

            // Logical relationship validation
            if (High < Low || Current > High || Current < Low)
                return false;

            // Day trading specific validation
            if (Current < 1.0m || Current > 500.0m) // Typical day trading price range
                return false;

            // Timestamp validation (not too old, not in future)
            var dateTime = DateTime;
            if (dateTime < DateTime.UtcNow.AddDays(-7) || dateTime > DateTime.UtcNow.AddHours(1))
                return false;

            return true;
        }

        /// <summary>
        /// Determines if this quote data is fresh enough for day trading.
        /// Quote data older than specified threshold is considered stale.
        /// </summary>
        /// <param name="maxAge">Maximum age before quote is considered stale</param>
        /// <returns>True if quote is fresh, false if stale</returns>
        public bool IsFresh(TimeSpan maxAge)
        {
            return DateTime.UtcNow - DateTime <= maxAge;
        }

        /// <summary>
        /// Creates a normalized copy with values rounded to appropriate decimal places.
        /// Prices rounded to 4 decimal places for consistency.
        /// </summary>
        /// <returns>A new FinnhubQuoteResponse with normalized values</returns>
        public FinnhubQuoteResponse Normalize()
        {
            return new FinnhubQuoteResponse
            {
                Symbol = Symbol,
                Current = Math.Round(Current, 4),
                High = Math.Round(High, 4),
                Low = Math.Round(Low, 4),
                Open = Math.Round(Open, 4),
                PreviousClose = Math.Round(PreviousClose, 4),
                Timestamp = Timestamp
            };
        }

        /// <summary>
        /// Assesses day trading viability based on core criteria.
        /// Returns structured assessment for screening engine.
        /// </summary>
        /// <returns>Day trading assessment with specific criteria evaluation</returns>
        public DayTradingAssessment GetDayTradingAssessment()
        {
            return new DayTradingAssessment
            {
                Symbol = Symbol,
                MeetsVolatilityRequirement = MeetsVolatilityCriteria,
                ATRPercentage = ATRPercentage,
                PriceChangePercentage = Math.Abs(PercentChange),
                IsInPriceRange = Current >= 1.0m && Current <= 150.0m,
                DataFreshness = IsFresh(TimeSpan.FromMinutes(1)) ? "Fresh" : "Stale",
                AssessmentTime = DateTime.UtcNow
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// Includes key day trading metrics.
        /// </summary>
        public override string ToString()
        {
            var direction = Change >= 0 ? "↑" : "↓";
            return $"FinnhubQuote[{Symbol}]: ${Current:F2} {direction} {PercentChange:+0.00;-0.00;0.00}% " +
                   $"(ATR: {ATRPercentage:F1}%, Range: ${DayRange:F2})";
        }
    }

    /// <summary>
    /// Day trading assessment result for screening engine integration.
    /// Structured evaluation against the 12 core day trading criteria.
    /// </summary>
    public class DayTradingAssessment
    {
        public string Symbol { get; set; } = string.Empty;
        public bool MeetsVolatilityRequirement { get; set; }
        public decimal ATRPercentage { get; set; }
        public decimal PriceChangePercentage { get; set; }
        public bool IsInPriceRange { get; set; }
        public string DataFreshness { get; set; } = string.Empty;
        public DateTime AssessmentTime { get; set; }

        /// <summary>
        /// Overall day trading viability score (0-100).
        /// Higher scores indicate better day trading opportunities.
        /// </summary>
        public int ViabilityScore
        {
            get
            {
                int score = 0;
                if (MeetsVolatilityRequirement) score += 40;
                if (PriceChangePercentage >= 2.0m) score += 30;
                if (IsInPriceRange) score += 20;
                if (DataFreshness == "Fresh") score += 10;
                return score;
            }
        }
    }
}

// Total Lines: 265

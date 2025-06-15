// File: TradingPlatform.DataIngestion\Models\AlphaVantageQuoteResponse.cs

using System;
using System.Text.Json.Serialization;

namespace TradingPlatform.DataIngestion.Models
{
    /// <summary>
    /// Represents a quote response from the AlphaVantage GLOBAL_QUOTE API.
    /// Maps directly to the JSON structure returned by AlphaVantage's quote endpoint.
    /// 
    /// AlphaVantage Global Quote API Response Structure:
    /// {
    ///   "Global Quote": {
    ///     "01. symbol": "AAPL",
    ///     "02. open": "150.00",
    ///     "03. high": "152.00",
    ///     "04. low": "149.50",
    ///     "05. price": "151.25",
    ///     "06. volume": "1234567",
    ///     "07. latest trading day": "2025-06-04",
    ///     "08. previous close": "150.50",
    ///     "09. change": "0.75",
    ///     "10. change percent": "0.4988%"
    ///   }
    /// }
    /// 
    /// This model uses string types as returned by AlphaVantage API, with conversion
    /// to decimal types handled in the provider mapping logic for financial precision.
    /// </summary>
    public class AlphaVantageGlobalQuoteResponse
    {
        /// <summary>
        /// The main quote data object containing all price and volume information.
        /// Maps to "Global Quote" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("Global Quote")]
        public AlphaVantageQuote GlobalQuote { get; set; } = new AlphaVantageQuote();

        /// <summary>
        /// Indicates if the response contains valid quote data.
        /// Checks for non-null GlobalQuote with valid symbol.
        /// </summary>
        [JsonIgnore]
        public bool HasValidData => GlobalQuote != null && !string.IsNullOrWhiteSpace(GlobalQuote.Symbol);

        /// <summary>
        /// Gets the trading date from the latest trading day field.
        /// Returns DateTime.MinValue if parsing fails.
        /// </summary>
        [JsonIgnore]
        public DateTime TradingDate
        {
            get
            {
                if (GlobalQuote?.LatestTradingDay != null &&
                    DateTime.TryParse(GlobalQuote.LatestTradingDay, out var result))
                {
                    return result;
                }
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            return HasValidData
                ? $"AlphaVantageQuote[{GlobalQuote.Symbol}]: ${GlobalQuote.Price} ({GlobalQuote.ChangePercent})"
                : "AlphaVantageQuote[Invalid]";
        }
    }

    /// <summary>
    /// Represents an individual quote from AlphaVantage Global Quote API.
    /// Contains current market data with standard OHLCV information.
    /// All properties use string format as returned by AlphaVantage API.
    /// </summary>
    public class AlphaVantageQuote
    {
        /// <summary>
        /// The stock symbol identifier.
        /// Maps to "01. symbol" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("01. symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Opening price for the current/latest trading day.
        /// Maps to "02. open" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("02. open")]
        public string Open { get; set; } = "0";

        /// <summary>
        /// Highest price during the current/latest trading day.
        /// Maps to "03. high" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("03. high")]
        public string High { get; set; } = "0";

        /// <summary>
        /// Lowest price during the current/latest trading day.
        /// Maps to "04. low" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("04. low")]
        public string Low { get; set; } = "0";

        /// <summary>
        /// Current/latest price of the stock.
        /// Maps to "05. price" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("05. price")]
        public string Price { get; set; } = "0";

        /// <summary>
        /// Volume of shares traded during the current/latest trading day.
        /// Maps to "06. volume" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("06. volume")]
        public string Volume { get; set; } = "0";

        /// <summary>
        /// The latest trading day for this data.
        /// Maps to "07. latest trading day" field in AlphaVantage JSON response.
        /// Format: "YYYY-MM-DD"
        /// </summary>
        [JsonPropertyName("07. latest trading day")]
        public string LatestTradingDay { get; set; } = string.Empty;

        /// <summary>
        /// Previous trading day's closing price.
        /// Maps to "08. previous close" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("08. previous close")]
        public string PreviousClose { get; set; } = "0";

        /// <summary>
        /// Price change from previous close to current price.
        /// Maps to "09. change" field in AlphaVantage JSON response.
        /// </summary>
        [JsonPropertyName("09. change")]
        public string Change { get; set; } = "0";

        /// <summary>
        /// Percentage change from previous close to current price.
        /// Maps to "10. change percent" field in AlphaVantage JSON response.
        /// Format includes percentage sign (e.g., "0.4988%").
        /// </summary>
        [JsonPropertyName("10. change percent")]
        public string ChangePercent { get; set; } = "0%";

        // ========== CALCULATED PROPERTIES ==========

        /// <summary>
        /// Converts the string price to decimal for financial calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal PriceAsDecimal
        {
            get
            {
                return decimal.TryParse(Price, out var result) ? result : 0m;
            }
        }

        /// <summary>
        /// Converts the string change to decimal for calculations.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal ChangeAsDecimal
        {
            get
            {
                return decimal.TryParse(Change, out var result) ? result : 0m;
            }
        }

        /// <summary>
        /// Converts the percentage change string to decimal (without % symbol).
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public decimal ChangePercentAsDecimal
        {
            get
            {
                var cleanPercent = ChangePercent.TrimEnd('%');
                return decimal.TryParse(cleanPercent, out var result) ? result : 0m;
            }
        }

        /// <summary>
        /// Converts volume string to long integer.
        /// Returns 0 if conversion fails.
        /// </summary>
        [JsonIgnore]
        public long VolumeAsLong
        {
            get
            {
                return long.TryParse(Volume, out var result) ? result : 0L;
            }
        }

        /// <summary>
        /// Indicates if this quote represents an uptick (price increase).
        /// </summary>
        [JsonIgnore]
        public bool IsUptick => ChangeAsDecimal > 0;

        /// <summary>
        /// Indicates if this quote represents a downtick (price decrease).
        /// </summary>
        [JsonIgnore]
        public bool IsDowntick => ChangeAsDecimal < 0;

        /// <summary>
        /// Trading range for the day (High - Low) as decimal.
        /// </summary>
        [JsonIgnore]
        public decimal DayRange
        {
            get
            {
                var highDecimal = decimal.TryParse(High, out var h) ? h : 0m;
                var lowDecimal = decimal.TryParse(Low, out var l) ? l : 0m;
                return highDecimal - lowDecimal;
            }
        }

        // ========== VALIDATION AND UTILITY METHODS ==========

        /// <summary>
        /// Validates that the quote data is logically consistent.
        /// Checks for reasonable values and proper relationships.
        /// </summary>
        /// <returns>True if the quote data passes validation checks</returns>
        public bool IsValid()
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(Symbol) || string.IsNullOrWhiteSpace(Price))
                return false;

            // Validate numeric conversions
            if (PriceAsDecimal <= 0)
                return false;

            // Check price relationships if all values are present
            if (!string.IsNullOrWhiteSpace(High) && !string.IsNullOrWhiteSpace(Low))
            {
                var highDecimal = decimal.TryParse(High, out var h) ? h : 0m;
                var lowDecimal = decimal.TryParse(Low, out var l) ? l : 0m;
                var openDecimal = decimal.TryParse(Open, out var o) ? o : 0m;

                if (highDecimal > 0 && lowDecimal > 0)
                {
                    if (highDecimal < lowDecimal || PriceAsDecimal > highDecimal || PriceAsDecimal < lowDecimal)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a normalized copy with consistent decimal precision.
        /// Rounds all price values to 4 decimal places.
        /// </summary>
        /// <returns>A new AlphaVantageQuote with normalized values</returns>
        public AlphaVantageQuote Normalize()
        {
            return new AlphaVantageQuote
            {
                Symbol = Symbol,
                Open = Math.Round(decimal.TryParse(Open, out var o) ? o : 0m, 4).ToString(),
                High = Math.Round(decimal.TryParse(High, out var h) ? h : 0m, 4).ToString(),
                Low = Math.Round(decimal.TryParse(Low, out var l) ? l : 0m, 4).ToString(),
                Price = Math.Round(PriceAsDecimal, 4).ToString(),
                Volume = Volume,
                LatestTradingDay = LatestTradingDay,
                PreviousClose = Math.Round(decimal.TryParse(PreviousClose, out var pc) ? pc : 0m, 4).ToString(),
                Change = Math.Round(ChangeAsDecimal, 4).ToString(),
                ChangePercent = Math.Round(ChangePercentAsDecimal, 2).ToString() + "%"
            };
        }

        /// <summary>
        /// String representation for logging and debugging purposes.
        /// </summary>
        public override string ToString()
        {
            var direction = IsUptick ? "↑" : IsDowntick ? "↓" : "→";
            return $"AlphaVantageQuote[{Symbol}]: ${Price} {direction} {ChangePercent} " +
                   $"(H: ${High}, L: ${Low}, Vol: {Volume:N0})";
        }
    }
}

// Total Lines: 247

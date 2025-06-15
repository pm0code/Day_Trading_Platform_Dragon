// File: TradingPlatform.Core\Models\ApiResponse.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradingPlatform.Core.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Provider { get; set; }
        public int RemainingCalls { get; set; }
        public string Status { get; set; }
    }

    public class AlphaVantageGlobalQuoteResponse : ApiResponse<AlphaVantageQuote> { }
    public class AlphaVantageQuote
    {
        [JsonPropertyName("01. symbol")]
        public string Symbol { get; set; }

        [JsonPropertyName("02. open")]
        public string Open { get; set; }

        [JsonPropertyName("03. high")]
        public string High { get; set; }

        [JsonPropertyName("04. low")]
        public string Low { get; set; }

        [JsonPropertyName("05. price")]
        public string Price { get; set; }

        [JsonPropertyName("06. volume")]
        public string Volume { get; set; }

        [JsonPropertyName("07. latest trading day")]
        public string LatestTradingDay { get; set; }

        [JsonPropertyName("08. previous close")]
        public string PreviousClose { get; set; }

        [JsonPropertyName("09. change")]
        public string Change { get; set; }

        [JsonPropertyName("10. change percent")]
        public string ChangePercent { get; set; }
    }

    public class FinnhubQuoteResponse : ApiResponse<FinnhubQuote> { }
    public class FinnhubQuote
    {
        [JsonPropertyName("c")]
        public decimal Current { get; set; }

        [JsonPropertyName("d")]
        public decimal Change { get; set; }

        [JsonPropertyName("dp")]
        public decimal PercentChange { get; set; }

        [JsonPropertyName("h")]
        public decimal High { get; set; }

        [JsonPropertyName("l")]
        public decimal Low { get; set; }

        [JsonPropertyName("o")]
        public decimal Open { get; set; }

        [JsonPropertyName("pc")]
        public decimal PreviousClose { get; set; }
    }

    public class FinnhubCandleResponse : ApiResponse<FinnhubCandleData> { }
    public class FinnhubCandleData
    {
        [JsonPropertyName("o")]
        public decimal[] Open { get; set; }

        [JsonPropertyName("h")]
        public decimal[] High { get; set; }

        [JsonPropertyName("l")]
        public decimal[] Low { get; set; }

        [JsonPropertyName("c")]
        public decimal[] Close { get; set; }

        [JsonPropertyName("v")]
        public long[] Volume { get; set; }

        [JsonPropertyName("t")]
        public long[] Timestamp { get; set; }

        [JsonPropertyName("s")]
        public string Status { get; set; }
    }

    public class AlphaVantageTimeSeriesDailyAdjustedResponse : ApiResponse<Dictionary<string, AlphaVantageDailyAdjustedData>> { }
    public class AlphaVantageDailyAdjustedData
    {
        [JsonPropertyName("1. open")]
        public decimal Open { get; set; }

        [JsonPropertyName("2. high")]
        public decimal High { get; set; }

        [JsonPropertyName("3. low")]
        public decimal Low { get; set; }

        [JsonPropertyName("4. close")]
        public decimal Close { get; set; }

        [JsonPropertyName("5. adjusted close")]
        public decimal AdjustedClose { get; set; }

        [JsonPropertyName("6. volume")]
        public long Volume { get; set; }

        [JsonPropertyName("7. dividend amount")]
        public decimal DividendAmount { get; set; }

        [JsonPropertyName("8. split coefficient")]
        public decimal SplitCoefficient { get; set; }
    }
}
// Total Lines: 121

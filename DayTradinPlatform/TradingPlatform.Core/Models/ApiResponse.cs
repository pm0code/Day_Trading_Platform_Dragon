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

    public class AlphaVantageGlobalQuoteResponse
    {
        [JsonPropertyName("Global Quote")]
        public AlphaVantageQuote GlobalQuote { get; set; }
    }
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
        
        // Helper properties for decimal conversion
        [JsonIgnore]
        public decimal PriceAsDecimal => decimal.TryParse(Price, out var val) ? val : 0m;
        
        [JsonIgnore]
        public long VolumeAsLong => long.TryParse(Volume, out var val) ? val : 0L;
        
        [JsonIgnore]
        public decimal ChangeAsDecimal => decimal.TryParse(Change, out var val) ? val : 0m;
        
        [JsonIgnore]
        public decimal ChangePercentAsDecimal
        {
            get
            {
                var cleanPercent = ChangePercent?.Replace("%", "").Trim();
                return decimal.TryParse(cleanPercent, out var val) ? val : 0m;
            }
        }
    }

    public class AlphaVantageTimeSeriesResponse
    {
        [JsonPropertyName("Meta Data")]
        public Dictionary<string, string> MetaData { get; set; }
        
        [JsonPropertyName("Time Series (Daily)")]
        public Dictionary<string, AlphaVantageTimeSeriesData> TimeSeries { get; set; }
        
        public List<DailyData> ToDailyData()
        {
            var result = new List<DailyData>();
            if (TimeSeries != null)
            {
                foreach (var kvp in TimeSeries)
                {
                    if (DateTime.TryParse(kvp.Key, out var date))
                    {
                        result.Add(new DailyData
                        {
                            Symbol = MetaData?["2. Symbol"] ?? "",
                            Date = date,
                            Open = kvp.Value.OpenAsDecimal,
                            High = kvp.Value.HighAsDecimal,
                            Low = kvp.Value.LowAsDecimal,
                            Close = kvp.Value.CloseAsDecimal,
                            Volume = kvp.Value.VolumeAsLong,
                            AdjustedClose = kvp.Value.CloseAsDecimal
                        });
                    }
                }
            }
            return result;
        }
    }
    
    public class AlphaVantageTimeSeriesData
    {
        [JsonPropertyName("1. open")]
        public string Open { get; set; }
        
        [JsonPropertyName("2. high")]
        public string High { get; set; }
        
        [JsonPropertyName("3. low")]
        public string Low { get; set; }
        
        [JsonPropertyName("4. close")]
        public string Close { get; set; }
        
        [JsonPropertyName("5. volume")]
        public string Volume { get; set; }
        
        // Helper properties
        [JsonIgnore]
        public decimal OpenAsDecimal => decimal.TryParse(Open, out var val) ? val : 0m;
        
        [JsonIgnore]
        public decimal HighAsDecimal => decimal.TryParse(High, out var val) ? val : 0m;
        
        [JsonIgnore]
        public decimal LowAsDecimal => decimal.TryParse(Low, out var val) ? val : 0m;
        
        [JsonIgnore]
        public decimal CloseAsDecimal => decimal.TryParse(Close, out var val) ? val : 0m;
        
        [JsonIgnore]
        public long VolumeAsLong => long.TryParse(Volume, out var val) ? val : 0L;
    }

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

    public class FinnhubQuoteResponse
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
    
    public class FinnhubCandleResponse
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

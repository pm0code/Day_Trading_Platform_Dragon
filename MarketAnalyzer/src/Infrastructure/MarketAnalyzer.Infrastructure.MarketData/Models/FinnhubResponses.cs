using System.Text.Json.Serialization;

namespace MarketAnalyzer.Infrastructure.MarketData.Models;

/// <summary>
/// Finnhub API response models for various endpoints.
/// Uses System.Text.Json for high-performance serialization.
/// </summary>

/// <summary>
/// Response model for Finnhub quote endpoint.
/// </summary>
public class FinnhubQuoteResponse
{
    /// <summary>
    /// Current price
    /// </summary>
    [JsonPropertyName("c")]
    public double C { get; set; }

    /// <summary>
    /// Change
    /// </summary>
    [JsonPropertyName("d")]
    public double D { get; set; }

    /// <summary>
    /// Percent change
    /// </summary>
    [JsonPropertyName("dp")]
    public double Dp { get; set; }

    /// <summary>
    /// High price of the day
    /// </summary>
    [JsonPropertyName("h")]
    public double H { get; set; }

    /// <summary>
    /// Low price of the day
    /// </summary>
    [JsonPropertyName("l")]
    public double L { get; set; }

    /// <summary>
    /// Open price of the day
    /// </summary>
    [JsonPropertyName("o")]
    public double O { get; set; }

    /// <summary>
    /// Previous close price
    /// </summary>
    [JsonPropertyName("pc")]
    public double Pc { get; set; }

    /// <summary>
    /// Volume
    /// </summary>
    [JsonPropertyName("v")]
    public double V { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    [JsonPropertyName("t")]
    public long T { get; set; }
}

/// <summary>
/// Response model for Finnhub company profile endpoint.
/// </summary>
public class FinnhubProfileResponse
{
    /// <summary>
    /// Company name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Stock exchange
    /// </summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    /// <summary>
    /// Company's industry
    /// </summary>
    [JsonPropertyName("finnhubIndustry")]
    public string? FinnhubIndustry { get; set; }

    /// <summary>
    /// Market capitalization
    /// </summary>
    [JsonPropertyName("marketCapitalization")]
    public double MarketCapitalization { get; set; }

    /// <summary>
    /// Company's country
    /// </summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Company's currency
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>
    /// Company's ticker symbol
    /// </summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    /// <summary>
    /// Company's logo URL
    /// </summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    /// <summary>
    /// Company's website URL
    /// </summary>
    [JsonPropertyName("weburl")]
    public string? WebUrl { get; set; }

    /// <summary>
    /// Number of outstanding shares
    /// </summary>
    [JsonPropertyName("shareOutstanding")]
    public double ShareOutstanding { get; set; }

    /// <summary>
    /// IPO date
    /// </summary>
    [JsonPropertyName("ipo")]
    public string? Ipo { get; set; }
}

/// <summary>
/// Response model for Finnhub candle/historical data endpoint.
/// </summary>
public class FinnhubCandleResponse
{
    /// <summary>
    /// Close prices
    /// </summary>
    [JsonPropertyName("c")]
    public double[]? C { get; set; }

    /// <summary>
    /// High prices
    /// </summary>
    [JsonPropertyName("h")]
    public double[]? H { get; set; }

    /// <summary>
    /// Low prices
    /// </summary>
    [JsonPropertyName("l")]
    public double[]? L { get; set; }

    /// <summary>
    /// Open prices
    /// </summary>
    [JsonPropertyName("o")]
    public double[]? O { get; set; }

    /// <summary>
    /// Status of the response
    /// </summary>
    [JsonPropertyName("s")]
    public string? S { get; set; }

    /// <summary>
    /// Timestamps
    /// </summary>
    [JsonPropertyName("t")]
    public long[]? T { get; set; }

    /// <summary>
    /// Volume data
    /// </summary>
    [JsonPropertyName("v")]
    public double[]? V { get; set; }
}

/// <summary>
/// Response model for Finnhub symbol search endpoint.
/// </summary>
public class FinnhubSearchResponse
{
    /// <summary>
    /// Number of results
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// Search results
    /// </summary>
    [JsonPropertyName("result")]
    public FinnhubSearchResult[]? Result { get; set; }
}

/// <summary>
/// Individual search result from Finnhub symbol search.
/// </summary>
public class FinnhubSearchResult
{
    /// <summary>
    /// Symbol description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Display symbol
    /// </summary>
    [JsonPropertyName("displaySymbol")]
    public string? DisplaySymbol { get; set; }

    /// <summary>
    /// Symbol
    /// </summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Type
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Response model for Finnhub WebSocket real-time trades.
/// </summary>
public class FinnhubWebSocketResponse
{
    /// <summary>
    /// Message type
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Trade data
    /// </summary>
    [JsonPropertyName("data")]
    public FinnhubWebSocketTrade[]? Data { get; set; }
}

/// <summary>
/// Individual trade from Finnhub WebSocket stream.
/// </summary>
public class FinnhubWebSocketTrade
{
    /// <summary>
    /// Symbol
    /// </summary>
    [JsonPropertyName("s")]
    public string S { get; set; } = string.Empty;

    /// <summary>
    /// Price
    /// </summary>
    [JsonPropertyName("p")]
    public double P { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    [JsonPropertyName("t")]
    public long T { get; set; }

    /// <summary>
    /// Volume
    /// </summary>
    [JsonPropertyName("v")]
    public double V { get; set; }

    /// <summary>
    /// Trade conditions
    /// </summary>
    [JsonPropertyName("c")]
    public string[]? C { get; set; }
}
using MarketAnalyzer.Foundation;

namespace MarketAnalyzer.Domain.Entities;

/// <summary>
/// Represents a real-time market quote with price and volume information.
/// ALL financial values MUST use decimal type for precision compliance.
/// </summary>
public class MarketQuote
{
    /// <summary>
    /// Gets the stock symbol.
    /// </summary>
    public string Symbol { get; private set; }

    /// <summary>
    /// Gets the current market price. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal CurrentPrice { get; private set; }

    /// <summary>
    /// Gets the opening price for the trading day. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal DayOpen { get; private set; }

    /// <summary>
    /// Gets the highest price for the trading day. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal DayHigh { get; private set; }

    /// <summary>
    /// Gets the lowest price for the trading day. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal DayLow { get; private set; }

    /// <summary>
    /// Gets the previous trading day's closing price. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal PreviousClose { get; private set; }

    /// <summary>
    /// Gets the trading volume for the day.
    /// </summary>
    public long Volume { get; private set; }

    /// <summary>
    /// Gets the timestamp when this quote was generated (market time).
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    /// Gets the high-precision hardware timestamp for ultra-low latency tracking.
    /// Used for performance measurement and latency optimization.
    /// </summary>
    public long HardwareTimestamp { get; private set; }

    /// <summary>
    /// Gets the bid price. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal? BidPrice { get; private set; }

    /// <summary>
    /// Gets the ask price. MANDATORY: decimal for financial precision.
    /// </summary>
    public decimal? AskPrice { get; private set; }

    /// <summary>
    /// Gets the bid size.
    /// </summary>
    public long? BidSize { get; private set; }

    /// <summary>
    /// Gets the ask size.
    /// </summary>
    public long? AskSize { get; private set; }

    /// <summary>
    /// Gets the market status at the time of this quote.
    /// </summary>
    public MarketStatus MarketStatus { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this quote is from real-time data or delayed.
    /// </summary>
    public bool IsRealTime { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketQuote"/> class.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="currentPrice">The current price (MUST be decimal)</param>
    /// <param name="dayOpen">The day's opening price (MUST be decimal)</param>
    /// <param name="dayHigh">The day's high price (MUST be decimal)</param>
    /// <param name="dayLow">The day's low price (MUST be decimal)</param>
    /// <param name="previousClose">The previous close price (MUST be decimal)</param>
    /// <param name="volume">The trading volume</param>
    /// <param name="timestamp">The quote timestamp</param>
    /// <param name="hardwareTimestamp">The hardware timestamp</param>
    /// <param name="marketStatus">The market status</param>
    /// <param name="isRealTime">Whether this is real-time data</param>
    public MarketQuote(
        string symbol,
        decimal currentPrice,
        decimal dayOpen,
        decimal dayHigh,
        decimal dayLow,
        decimal previousClose,
        long volume,
        DateTime timestamp,
        long hardwareTimestamp,
        MarketStatus marketStatus,
        bool isRealTime = true)
    {
        Symbol = ValidateSymbol(symbol);
        CurrentPrice = ValidatePrice(currentPrice, nameof(currentPrice));
        DayOpen = ValidatePrice(dayOpen, nameof(dayOpen));
        DayHigh = ValidatePrice(dayHigh, nameof(dayHigh));
        DayLow = ValidatePrice(dayLow, nameof(dayLow));
        PreviousClose = ValidatePrice(previousClose, nameof(previousClose));
        Volume = ValidateVolume(volume);
        Timestamp = timestamp;
        HardwareTimestamp = hardwareTimestamp;
        MarketStatus = marketStatus;
        IsRealTime = isRealTime;

        ValidatePriceConsistency();
    }

    /// <summary>
    /// Updates the bid/ask spread information.
    /// </summary>
    /// <param name="bidPrice">The bid price (MUST be decimal)</param>
    /// <param name="askPrice">The ask price (MUST be decimal)</param>
    /// <param name="bidSize">The bid size</param>
    /// <param name="askSize">The ask size</param>
    /// <returns>A result indicating success or failure</returns>
    public TradingResult<bool> UpdateBidAsk(decimal? bidPrice, decimal? askPrice, long? bidSize, long? askSize)
    {
        try
        {
            if (bidPrice.HasValue)
                ValidatePrice(bidPrice.Value, nameof(bidPrice));

            if (askPrice.HasValue)
                ValidatePrice(askPrice.Value, nameof(askPrice));

            if (bidPrice.HasValue && askPrice.HasValue && bidPrice.Value > askPrice.Value)
            {
                return TradingResult<bool>.Failure("INVALID_SPREAD", "Bid price cannot be higher than ask price");
            }

            BidPrice = bidPrice;
            AskPrice = askPrice;
            BidSize = bidSize;
            AskSize = askSize;

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return TradingResult<bool>.Failure("BID_ASK_UPDATE_FAILED", $"Failed to update bid/ask: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the percentage change from previous close. MANDATORY: decimal result.
    /// </summary>
    /// <returns>The percentage change as a decimal (e.g., 0.05 for 5%)</returns>
    public TradingResult<decimal> GetPercentageChange()
    {
        try
        {
            if (PreviousClose == 0)
            {
                return TradingResult<decimal>.Failure("DIVISION_BY_ZERO", "Cannot calculate percentage change when previous close is zero");
            }

            var change = (CurrentPrice - PreviousClose) / PreviousClose;
            return TradingResult<decimal>.Success(change);
        }
        catch (Exception ex)
        {
            return TradingResult<decimal>.Failure("CALCULATION_ERROR", $"Failed to calculate percentage change: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the absolute price change from previous close. MANDATORY: decimal result.
    /// </summary>
    /// <returns>The absolute price change</returns>
    public decimal GetPriceChange()
    {
        return CurrentPrice - PreviousClose;
    }

    /// <summary>
    /// Calculates the bid-ask spread. MANDATORY: decimal result.
    /// </summary>
    /// <returns>The bid-ask spread or null if bid/ask not available</returns>
    public TradingResult<decimal?> GetBidAskSpread()
    {
        try
        {
            if (!BidPrice.HasValue || !AskPrice.HasValue)
            {
                return TradingResult<decimal?>.Success(null);
            }

            var spread = AskPrice.Value - BidPrice.Value;
            return TradingResult<decimal?>.Success(spread);
        }
        catch (Exception ex)
        {
            return TradingResult<decimal?>.Failure("SPREAD_CALCULATION_ERROR", $"Failed to calculate bid-ask spread: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Calculates the mid-point price between bid and ask. MANDATORY: decimal result.
    /// </summary>
    /// <returns>The mid-point price or null if bid/ask not available</returns>
    public TradingResult<decimal?> GetMidPrice()
    {
        try
        {
            if (!BidPrice.HasValue || !AskPrice.HasValue)
            {
                return TradingResult<decimal?>.Success(null);
            }

            var midPrice = (BidPrice.Value + AskPrice.Value) / 2;
            return TradingResult<decimal?>.Success(midPrice);
        }
        catch (Exception ex)
        {
            return TradingResult<decimal?>.Failure("MID_PRICE_CALCULATION_ERROR", $"Failed to calculate mid price: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Determines if this quote is stale based on the specified age threshold.
    /// </summary>
    /// <param name="maxAge">The maximum acceptable age</param>
    /// <returns>True if the quote is stale</returns>
    public bool IsStale(TimeSpan maxAge)
    {
        return DateTime.UtcNow - Timestamp > maxAge;
    }

    /// <summary>
    /// Creates a cache key for this quote.
    /// </summary>
    /// <returns>A cache key string</returns>
    public string GetCacheKey()
    {
        return $"quote:{Symbol}:{Timestamp:yyyyMMddHHmmss}";
    }

    /// <summary>
    /// Returns a string representation of the quote.
    /// </summary>
    /// <returns>A string representation</returns>
    public override string ToString()
    {
        var change = GetPriceChange();
        var changeSign = change >= 0 ? "+" : "";
        return $"{Symbol}: {CurrentPrice:C} ({changeSign}{change:C}) Vol: {Volume:N0}";
    }

    #region Validation Methods

    private static string ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

        return symbol.ToUpperInvariant().Trim();
    }

    private static decimal ValidatePrice(decimal price, string parameterName)
    {
        if (price < 0)
            throw new ArgumentException($"Price cannot be negative: {price}", parameterName);

        if (price > 1_000_000m) // Sanity check for extremely high prices
            throw new ArgumentException($"Price exceeds maximum allowed value: {price}", parameterName);

        return price;
    }

    private static long ValidateVolume(long volume)
    {
        if (volume < 0)
            throw new ArgumentException($"Volume cannot be negative: {volume}", nameof(volume));

        return volume;
    }

    private void ValidatePriceConsistency()
    {
        if (DayHigh < DayLow)
            throw new ArgumentException($"Day high ({DayHigh}) cannot be less than day low ({DayLow})");

        if (CurrentPrice < DayLow || CurrentPrice > DayHigh)
            throw new ArgumentException($"Current price ({CurrentPrice}) must be between day low ({DayLow}) and day high ({DayHigh})");
    }

    #endregion
}

/// <summary>
/// Represents the market status at the time of a quote.
/// </summary>
public enum MarketStatus
{
    /// <summary>
    /// Market status is unknown
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Market is closed
    /// </summary>
    Closed = 1,

    /// <summary>
    /// Pre-market trading
    /// </summary>
    PreMarket = 2,

    /// <summary>
    /// Market is open for regular trading
    /// </summary>
    Open = 3,

    /// <summary>
    /// After-hours trading
    /// </summary>
    AfterHours = 4,

    /// <summary>
    /// Market is halted
    /// </summary>
    Halted = 5
}
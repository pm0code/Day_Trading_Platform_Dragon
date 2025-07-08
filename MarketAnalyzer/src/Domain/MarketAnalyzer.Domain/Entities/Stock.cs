using MarketAnalyzer.Foundation;

namespace MarketAnalyzer.Domain.Entities;

/// <summary>
/// Represents a stock entity with core identifying information and classification.
/// All financial calculations MUST use decimal type for precision compliance.
/// </summary>
public class Stock
{
    /// <summary>
    /// Gets the stock symbol (e.g., "AAPL", "MSFT").
    /// </summary>
    public string Symbol { get; private set; }

    /// <summary>
    /// Gets the exchange where the stock is traded (e.g., "NASDAQ", "NYSE").
    /// </summary>
    public string Exchange { get; private set; }

    /// <summary>
    /// Gets the company name.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the market capitalization classification.
    /// </summary>
    public MarketCap MarketCap { get; private set; }

    /// <summary>
    /// Gets the sector classification.
    /// </summary>
    public Sector Sector { get; private set; }

    /// <summary>
    /// Gets the industry classification.
    /// </summary>
    public string Industry { get; private set; }

    /// <summary>
    /// Gets the country where the company is headquartered.
    /// </summary>
    public string Country { get; private set; }

    /// <summary>
    /// Gets the currency in which the stock is traded.
    /// </summary>
    public string Currency { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the stock is actively traded.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the date when the stock information was last updated.
    /// </summary>
    public DateTime LastUpdated { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Stock"/> class.
    /// </summary>
    /// <param name="symbol">The stock symbol</param>
    /// <param name="exchange">The exchange</param>
    /// <param name="name">The company name</param>
    /// <param name="marketCap">The market cap classification</param>
    /// <param name="sector">The sector</param>
    /// <param name="industry">The industry</param>
    /// <param name="country">The country</param>
    /// <param name="currency">The currency</param>
    /// <param name="isActive">Whether the stock is actively traded</param>
    public Stock(
        string symbol,
        string exchange,
        string name,
        MarketCap marketCap,
        Sector sector,
        string industry,
        string country,
        string currency,
        bool isActive = true)
    {
        Symbol = ValidateSymbol(symbol);
        Exchange = ValidateExchange(exchange);
        Name = ValidateName(name);
        MarketCap = marketCap;
        Sector = sector;
        Industry = ValidateIndustry(industry);
        Country = ValidateCountry(country);
        Currency = ValidateCurrency(currency);
        IsActive = isActive;
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the stock information.
    /// </summary>
    /// <param name="name">The updated company name</param>
    /// <param name="marketCap">The updated market cap classification</param>
    /// <param name="sector">The updated sector</param>
    /// <param name="industry">The updated industry</param>
    /// <param name="isActive">Whether the stock is actively traded</param>
    /// <returns>A result indicating success or failure</returns>
    public TradingResult<bool> UpdateInformation(
        string name,
        MarketCap marketCap,
        Sector sector,
        string industry,
        bool isActive)
    {
        try
        {
            Name = ValidateName(name);
            MarketCap = marketCap;
            Sector = sector;
            Industry = ValidateIndustry(industry);
            IsActive = isActive;
            LastUpdated = DateTime.UtcNow;

            return TradingResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return TradingResult<bool>.Failure("STOCK_UPDATE_FAILED", $"Failed to update stock information: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a stock identifier string for caching and indexing.
    /// </summary>
    /// <returns>A unique identifier string</returns>
    public string GetIdentifier()
    {
        return $"{Exchange}:{Symbol}";
    }

    /// <summary>
    /// Determines if this stock matches the specified criteria.
    /// </summary>
    /// <param name="marketCap">Optional market cap filter</param>
    /// <param name="sector">Optional sector filter</param>
    /// <param name="activeOnly">Whether to include only active stocks</param>
    /// <returns>True if the stock matches the criteria</returns>
    public bool MatchesCriteria(MarketCap? marketCap = null, Sector? sector = null, bool activeOnly = true)
    {
        if (activeOnly && !IsActive)
            return false;

        if (marketCap.HasValue && MarketCap != marketCap.Value)
            return false;

        if (sector.HasValue && Sector != sector.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Returns a string representation of the stock.
    /// </summary>
    /// <returns>A string representation</returns>
    public override string ToString()
    {
        return $"{Symbol} ({Name}) - {Exchange}";
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current stock.
    /// </summary>
    /// <param name="obj">The object to compare</param>
    /// <returns>True if the objects are equal</returns>
    public override bool Equals(object? obj)
    {
        if (obj is not Stock other)
            return false;

        return Symbol.Equals(other.Symbol, StringComparison.OrdinalIgnoreCase) &&
               Exchange.Equals(other.Exchange, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a hash code for the stock.
    /// </summary>
    /// <returns>A hash code</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Symbol.ToUpperInvariant(),
            Exchange.ToUpperInvariant());
    }

    #region Validation Methods

    private static string ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));

        if (symbol.Length > 10)
            throw new ArgumentException("Symbol cannot exceed 10 characters", nameof(symbol));

        return symbol.ToUpperInvariant().Trim();
    }

    private static string ValidateExchange(string exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
            throw new ArgumentException("Exchange cannot be null or empty", nameof(exchange));

        return exchange.ToUpperInvariant().Trim();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));

        return name.Trim();
    }

    private static string ValidateIndustry(string industry)
    {
        if (string.IsNullOrWhiteSpace(industry))
            throw new ArgumentException("Industry cannot be null or empty", nameof(industry));

        return industry.Trim();
    }

    private static string ValidateCountry(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be null or empty", nameof(country));

        return country.Trim();
    }

    private static string ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));

        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-character ISO code", nameof(currency));

        return currency.ToUpperInvariant().Trim();
    }

    #endregion
}

/// <summary>
/// Represents market capitalization classifications.
/// </summary>
public enum MarketCap
{
    /// <summary>
    /// Unknown market cap
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Nano cap (under $50 million)
    /// </summary>
    NanoCap = 1,

    /// <summary>
    /// Micro cap ($50 million - $300 million)
    /// </summary>
    MicroCap = 2,

    /// <summary>
    /// Small cap ($300 million - $2 billion)
    /// </summary>
    SmallCap = 3,

    /// <summary>
    /// Mid cap ($2 billion - $10 billion)
    /// </summary>
    MidCap = 4,

    /// <summary>
    /// Large cap ($10 billion - $200 billion)
    /// </summary>
    LargeCap = 5,

    /// <summary>
    /// Mega cap (over $200 billion)
    /// </summary>
    MegaCap = 6
}

/// <summary>
/// Represents sector classifications based on GICS (Global Industry Classification Standard).
/// </summary>
public enum Sector
{
    /// <summary>
    /// Unknown sector
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Energy
    /// </summary>
    Energy = 1,

    /// <summary>
    /// Materials
    /// </summary>
    Materials = 2,

    /// <summary>
    /// Industrials
    /// </summary>
    Industrials = 3,

    /// <summary>
    /// Consumer Discretionary
    /// </summary>
    ConsumerDiscretionary = 4,

    /// <summary>
    /// Consumer Staples
    /// </summary>
    ConsumerStaples = 5,

    /// <summary>
    /// Health Care
    /// </summary>
    HealthCare = 6,

    /// <summary>
    /// Financials
    /// </summary>
    Financials = 7,

    /// <summary>
    /// Information Technology
    /// </summary>
    Technology = 8,

    /// <summary>
    /// Communication Services
    /// </summary>
    CommunicationServices = 9,

    /// <summary>
    /// Utilities
    /// </summary>
    Utilities = 10,

    /// <summary>
    /// Real Estate
    /// </summary>
    RealEstate = 11
}
using FluentAssertions;
using MarketAnalyzer.Domain.Entities;

namespace MarketAnalyzer.Domain.Tests.Entities;

public class StockTests
{
    [Fact]
    public void Constructor_Should_Create_Valid_Stock()
    {
        // Arrange
        const string symbol = "AAPL";
        const string exchange = "NASDAQ";
        const string name = "Apple Inc.";
        const MarketCap marketCap = MarketCap.MegaCap;
        const Sector sector = Sector.Technology;
        const string industry = "Consumer Electronics";
        const string country = "United States";
        const string currency = "USD";

        // Act
        var stock = new Stock(symbol, exchange, name, marketCap, sector, industry, country, currency);

        // Assert
        stock.Symbol.Should().Be("AAPL");
        stock.Exchange.Should().Be("NASDAQ");
        stock.Name.Should().Be("Apple Inc.");
        stock.MarketCap.Should().Be(MarketCap.MegaCap);
        stock.Sector.Should().Be(Sector.Technology);
        stock.Industry.Should().Be("Consumer Electronics");
        stock.Country.Should().Be("United States");
        stock.Currency.Should().Be("USD");
        stock.IsActive.Should().BeTrue();
        stock.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_Should_Normalize_Symbol_To_UpperCase()
    {
        // Arrange & Act
        var stock = new Stock("aapl", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Assert
        stock.Symbol.Should().Be("AAPL");
    }

    [Fact]
    public void Constructor_Should_Normalize_Currency_To_UpperCase()
    {
        // Arrange & Act
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "usd");

        // Assert
        stock.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_Should_Throw_For_Null_Symbol()
    {
        // Arrange & Act & Assert
        var action = () => new Stock(null!, "NASDAQ", "Apple Inc.", 
            MarketCap.MegaCap, Sector.Technology, "Consumer Electronics", "United States", "USD");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Symbol cannot be null or empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Should_Throw_For_Invalid_Symbol(string invalidSymbol)
    {
        // Arrange & Act & Assert
        var action = () => new Stock(invalidSymbol, "NASDAQ", "Apple Inc.", 
            MarketCap.MegaCap, Sector.Technology, "Consumer Electronics", "United States", "USD");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Symbol cannot be null or empty*");
    }

    [Fact]
    public void Constructor_Should_Throw_For_Symbol_Too_Long()
    {
        // Arrange
        var longSymbol = "VERYLONGSYMBOL";

        // Act & Assert
        var action = () => new Stock(longSymbol, "NASDAQ", "Apple Inc.", 
            MarketCap.MegaCap, Sector.Technology, "Consumer Electronics", "United States", "USD");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Symbol cannot exceed 10 characters*");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void Constructor_Should_Throw_For_Invalid_Currency(string invalidCurrency)
    {
        // Arrange & Act & Assert
        var action = () => new Stock("AAPL", "NASDAQ", "Apple Inc.", 
            MarketCap.MegaCap, Sector.Technology, "Consumer Electronics", "United States", invalidCurrency);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateInformation_Should_Update_Stock_Successfully()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");
        var originalLastUpdated = stock.LastUpdated;

        // Wait a bit to ensure timestamp difference
        Thread.Sleep(10);

        // Act
        var result = stock.UpdateInformation("Apple Inc. (Updated)", MarketCap.LargeCap, 
            Sector.Technology, "Technology Hardware", false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        stock.Name.Should().Be("Apple Inc. (Updated)");
        stock.MarketCap.Should().Be(MarketCap.LargeCap);
        stock.Industry.Should().Be("Technology Hardware");
        stock.IsActive.Should().BeFalse();
        stock.LastUpdated.Should().BeAfter(originalLastUpdated);
    }

    [Fact]
    public void GetIdentifier_Should_Return_Exchange_Colon_Symbol()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Act
        var identifier = stock.GetIdentifier();

        // Assert
        identifier.Should().Be("NASDAQ:AAPL");
    }

    [Fact]
    public void MatchesCriteria_Should_Return_True_For_Matching_Criteria()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Act & Assert
        stock.MatchesCriteria(MarketCap.MegaCap, Sector.Technology, true).Should().BeTrue();
        stock.MatchesCriteria(marketCap: MarketCap.MegaCap).Should().BeTrue();
        stock.MatchesCriteria(sector: Sector.Technology).Should().BeTrue();
        stock.MatchesCriteria().Should().BeTrue();
    }

    [Fact]
    public void MatchesCriteria_Should_Return_False_For_Non_Matching_Criteria()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Act & Assert
        stock.MatchesCriteria(MarketCap.SmallCap).Should().BeFalse();
        stock.MatchesCriteria(sector: Sector.Energy).Should().BeFalse();
    }

    [Fact]
    public void MatchesCriteria_Should_Return_False_For_Inactive_Stock_When_ActiveOnly()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD", false);

        // Act & Assert
        stock.MatchesCriteria(activeOnly: true).Should().BeFalse();
        stock.MatchesCriteria(activeOnly: false).Should().BeTrue();
    }

    [Fact]
    public void Equals_Should_Return_True_For_Same_Symbol_And_Exchange()
    {
        // Arrange
        var stock1 = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");
        var stock2 = new Stock("AAPL", "NASDAQ", "Apple Inc. (Different Name)", MarketCap.LargeCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Act & Assert
        stock1.Equals(stock2).Should().BeTrue();
        stock1.GetHashCode().Should().Be(stock2.GetHashCode());
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_Symbol()
    {
        // Arrange
        var stock1 = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");
        var stock2 = new Stock("MSFT", "NASDAQ", "Microsoft Corporation", MarketCap.MegaCap, 
            Sector.Technology, "Software", "United States", "USD");

        // Act & Assert
        stock1.Equals(stock2).Should().BeFalse();
    }

    [Fact]
    public void ToString_Should_Return_Formatted_String()
    {
        // Arrange
        var stock = new Stock("AAPL", "NASDAQ", "Apple Inc.", MarketCap.MegaCap, 
            Sector.Technology, "Consumer Electronics", "United States", "USD");

        // Act
        var result = stock.ToString();

        // Assert
        result.Should().Be("AAPL (Apple Inc.) - NASDAQ");
    }
}
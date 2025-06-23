using TradingPlatform.FixEngine.Models;
using TradingPlatform.FixEngine.Core;
using TradingPlatform.Core.Models;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.FixEngine.Trading;

/// <summary>
/// High-performance order router for US equity markets
/// Supports direct market access to NYSE, NASDAQ, BATS with sub-millisecond execution
/// MVP focuses on US markets with post-MVP extensions to international markets
/// </summary>
public sealed class OrderRouter : IDisposable
{
    private readonly ITradingLogger _logger;
    private readonly Dictionary<string, FixSession> _venueSessions = new();
    private readonly SemaphoreSlim _routingLock = new(1, 1);

    // US Market venues for MVP
    public static readonly Dictionary<string, VenueConfig> USMarketVenues = new()
    {
        ["NYSE"] = new VenueConfig
        {
            Name = "New York Stock Exchange",
            Mic = "XNYS",
            SupportsHiddenOrders = true,
            SupportsIcebergOrders = true,
            MaxOrderSize = 1_000_000m,
            LatencyRank = 1 // Primary venue
        },
        ["NASDAQ"] = new VenueConfig
        {
            Name = "NASDAQ",
            Mic = "XNAS",
            SupportsHiddenOrders = true,
            SupportsIcebergOrders = true,
            MaxOrderSize = 1_000_000m,
            LatencyRank = 2
        },
        ["BATS"] = new VenueConfig
        {
            Name = "BATS BZX Exchange",
            Mic = "BATS",
            SupportsHiddenOrders = true,
            SupportsIcebergOrders = false,
            MaxOrderSize = 500_000m,
            LatencyRank = 3
        },
        ["IEX"] = new VenueConfig
        {
            Name = "Investors Exchange",
            Mic = "IEXG",
            SupportsHiddenOrders = false,
            SupportsIcebergOrders = false,
            MaxOrderSize = 100_000m,
            LatencyRank = 4
        },
        ["ARCA"] = new VenueConfig
        {
            Name = "NYSE Arca",
            Mic = "ARCX",
            SupportsHiddenOrders = true,
            SupportsIcebergOrders = true,
            MaxOrderSize = 750_000m,
            LatencyRank = 2
        }
    };

    public OrderRouter(ITradingLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Routes order to optimal venue based on current market conditions and order characteristics
    /// Optimized for US equity markets with sub-millisecond routing decisions
    /// </summary>
    public async Task<string> RouteOrderAsync(OrderRequest request)
    {
        await _routingLock.WaitAsync();
        try
        {
            var routingStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L;

            // Smart order routing logic for US markets
            var optimalVenue = SelectOptimalVenue(request);
            var session = await GetOrCreateSession(optimalVenue);

            if (session?.IsConnected != true)
            {
                TradingLogOrchestrator.Instance.LogWarning($"No active session for venue {optimalVenue}, trying fallback");
                optimalVenue = GetFallbackVenue(request, optimalVenue);
                session = await GetOrCreateSession(optimalVenue);
            }

            if (session?.IsConnected != true)
            {
                throw new InvalidOperationException("No available venues for order routing");
            }

            var fixOrder = CreateFixOrder(request, optimalVenue);
            var success = await session.SendMessageAsync(fixOrder);

            var routingLatency = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000L) - routingStartTime;
            _logger.LogInfo($"Order routed to {optimalVenue} in {routingLatency / 1000.0:F1}μs");

            return success ? $"{optimalVenue}-{fixOrder.GetField(11)}" : throw new Exception("Failed to send order");
        }
        finally
        {
            _routingLock.Release();
        }
    }

    /// <summary>
    /// Selects optimal venue for US equity order based on order size, type, and market conditions
    /// </summary>
    public string SelectOptimalVenue(OrderRequest request)
    {
        var orderSize = request.Quantity * request.Price;

        // Large orders: Route to dark pools or venues with hidden order support
        if (orderSize > 100_000m)
        {
            if (request.IsHiddenOrder)
            {
                return USMarketVenues
                    .Where(v => v.Value.SupportsHiddenOrders && orderSize <= v.Value.MaxOrderSize)
                    .OrderBy(v => v.Value.LatencyRank)
                    .FirstOrDefault().Key ?? "NYSE";
            }

            // Consider iceberg orders for large visible orders
            if (orderSize > 250_000m)
            {
                return USMarketVenues
                    .Where(v => v.Value.SupportsIcebergOrders && orderSize <= v.Value.MaxOrderSize)
                    .OrderBy(v => v.Value.LatencyRank)
                    .FirstOrDefault().Key ?? "NYSE";
            }
        }

        // Market orders: Route to venue with best liquidity (typically NYSE/NASDAQ)
        if (request.OrderType == "MARKET")
        {
            return request.Symbol.StartsWith("Q") ? "NASDAQ" : "NYSE";
        }

        // Limit orders: Consider spread and venue characteristics
        if (request.OrderType == "LIMIT")
        {
            // Tech stocks typically have better liquidity on NASDAQ
            if (IsTechStock(request.Symbol))
            {
                return "NASDAQ";
            }

            // Traditional stocks typically route to NYSE
            return "NYSE";
        }

        // Default to NYSE for unknown scenarios
        return "NYSE";
    }

    /// <summary>
    /// Gets fallback venue if primary venue is unavailable
    /// </summary>
    private string GetFallbackVenue(OrderRequest request, string failedVenue)
    {
        var alternatives = USMarketVenues.Keys
            .Where(v => v != failedVenue)
            .OrderBy(v => USMarketVenues[v].LatencyRank);

        foreach (var venue in alternatives)
        {
            var config = USMarketVenues[venue];
            var orderSize = request.Quantity * request.Price;

            if (orderSize <= config.MaxOrderSize)
            {
                if (request.IsHiddenOrder && !config.SupportsHiddenOrders)
                    continue;

                return venue;
            }
        }

        return "NYSE"; // Final fallback
    }

    /// <summary>
    /// Creates FIX NewOrderSingle message optimized for US equity markets
    /// </summary>
    private FixMessage CreateFixOrder(OrderRequest request, string venue)
    {
        var order = new FixMessage
        {
            MsgType = FixMessageTypes.NewOrderSingle
        };

        // Standard order fields
        order.SetField(11, GenerateClOrdId()); // ClOrdID
        order.SetField(55, request.Symbol); // Symbol
        order.SetField(54, request.Side == "BUY" ? "1" : "2"); // Side
        order.SetField(38, request.Quantity); // OrderQty
        order.SetField(40, GetFixOrderType(request.OrderType)); // OrdType

        // Price for limit orders
        if (request.OrderType == "LIMIT" || request.OrderType == "STOP_LIMIT")
        {
            order.SetField(44, request.Price); // Price
        }

        // Stop price for stop orders
        if (request.OrderType.Contains("STOP"))
        {
            order.SetField(99, request.StopPrice ?? request.Price); // StopPx
        }

        // Time in force (US market standard)
        order.SetField(59, request.TimeInForce switch
        {
            "IOC" => "3", // Immediate or Cancel
            "FOK" => "4", // Fill or Kill  
            "GTD" => "6", // Good Till Date
            _ => "0"      // Day order (default)
        });

        // Hidden order handling for supported venues
        if (request.IsHiddenOrder && USMarketVenues[venue].SupportsHiddenOrders)
        {
            order.SetField(18, "H"); // ExecInst = Hidden
        }

        // Venue-specific routing
        order.SetField(100, USMarketVenues[venue].Mic); // ExDestination

        // Compliance fields for US markets
        order.SetField(1, request.Account ?? "DEFAULT"); // Account
        order.SetField(21, "1"); // HandlInst = Automated execution

        return order;
    }

    private async Task<FixSession?> GetOrCreateSession(string venue)
    {
        if (_venueSessions.TryGetValue(venue, out var existingSession) && existingSession.IsConnected)
        {
            return existingSession;
        }

        // Create new session for venue (configuration would come from config file)
        var session = new FixSession($"DAYTRADER", venue, _logger);

        // Connect to venue (endpoints would be configurable)
        var connected = await ConnectToVenue(session, venue);
        if (connected)
        {
            _venueSessions[venue] = session;
            return session;
        }

        return null;
    }

    private async Task<bool> ConnectToVenue(FixSession session, string venue)
    {
        // Production endpoints would be configured via appsettings
        // These are placeholder endpoints for development
        var endpoints = new Dictionary<string, (string host, int port)>
        {
            ["NYSE"] = ("fix.nyse.com", 9001),
            ["NASDAQ"] = ("fix.nasdaq.com", 9002),
            ["BATS"] = ("fix.bats.com", 9003),
            ["IEX"] = ("fix.iex.com", 9004),
            ["ARCA"] = ("fix.arca.com", 9005)
        };

        if (endpoints.TryGetValue(venue, out var endpoint))
        {
            return await session.ConnectAsync(endpoint.host, endpoint.port, TimeSpan.FromSeconds(5));
        }

        TradingLogOrchestrator.Instance.LogWarning($"No endpoint configuration found for venue: {venue}");
        return false;
    }

    private static string GenerateClOrdId()
    {
        return $"DT{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(1000, 9999)}";
    }

    private static string GetFixOrderType(string orderType)
    {
        return orderType switch
        {
            "MARKET" => "1",
            "LIMIT" => "2",
            "STOP" => "3",
            "STOP_LIMIT" => "4",
            _ => "2" // Default to limit
        };
    }

    private static bool IsTechStock(string symbol)
    {
        // Simplified tech stock detection for US markets
        // Production version would use sector classification data
        var techSymbols = new HashSet<string>
        {
            "AAPL", "MSFT", "GOOGL", "GOOG", "AMZN", "TSLA", "META", "NVDA",
            "NFLX", "CRM", "ORCL", "ADBE", "NOW", "SNOW", "UBER", "LYFT"
        };

        return techSymbols.Contains(symbol) || symbol.StartsWith("Q");
    }

    public void Dispose()
    {
        foreach (var session in _venueSessions.Values)
        {
            session.Dispose();
        }
        _routingLock.Dispose();
    }
}

/// <summary>
/// Venue configuration for US equity markets
/// </summary>
public record VenueConfig
{
    public required string Name { get; init; }
    public required string Mic { get; init; } // Market Identifier Code
    public required bool SupportsHiddenOrders { get; init; }
    public required bool SupportsIcebergOrders { get; init; }
    public required decimal MaxOrderSize { get; init; }
    public required int LatencyRank { get; init; } // 1 = lowest latency
}

/// <summary>
/// Order request model optimized for US equity markets
/// </summary>
public record OrderRequest
{
    public required string Symbol { get; init; }
    public required string Side { get; init; } // BUY/SELL
    public required decimal Quantity { get; init; }
    public required decimal Price { get; init; }
    public decimal? StopPrice { get; init; }
    public required string OrderType { get; init; } // MARKET/LIMIT/STOP/STOP_LIMIT
    public string TimeInForce { get; init; } = "DAY";
    public bool IsHiddenOrder { get; init; }
    public string? Account { get; init; }
}
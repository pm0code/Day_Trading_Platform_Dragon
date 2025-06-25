using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.TimeSeries.Interfaces;
using TradingPlatform.TimeSeries.Models;

namespace TradingPlatform.TimeSeries.Examples
{
    /// <summary>
    /// Comprehensive examples of time-series database usage for trading platform
    /// </summary>
    public class TimeSeriesUsageExample
    {
        /// <summary>
        /// Example 1: High-frequency market data collection service
        /// </summary>
        public class MarketDataCollector : BackgroundService
        {
            private readonly ITimeSeriesService _timeSeriesService;
            private readonly ITradingLogger _logger;
            private readonly Random _random = new();

            public MarketDataCollector(
                ITimeSeriesService timeSeriesService,
                ITradingLogger logger)
            {
                _timeSeriesService = timeSeriesService;
                _logger = logger;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };
                var batchSize = 100;
                var batch = new List<MarketDataPoint>();

                while (!stoppingToken.IsCancellationRequested)
                {
                    // Generate market data for all symbols
                    foreach (var symbol in symbols)
                    {
                        var basePrice = 100m + _random.Next(0, 200);
                        var spread = 0.01m + (decimal)_random.NextDouble() * 0.05m;
                        
                        var marketData = new MarketDataPoint
                        {
                            Symbol = symbol,
                            Exchange = "NASDAQ",
                            Price = basePrice,
                            Bid = basePrice - spread / 2,
                            Ask = basePrice + spread / 2,
                            BidSize = _random.Next(100, 10000),
                            AskSize = _random.Next(100, 10000),
                            Volume = _random.Next(1000000, 50000000),
                            High = basePrice + _random.Next(0, 5),
                            Low = basePrice - _random.Next(0, 5),
                            Open = basePrice + (decimal)(_random.NextDouble() - 0.5) * 5,
                            VWAP = basePrice + (decimal)(_random.NextDouble() - 0.5) * 2,
                            TradeCount = _random.Next(1000, 50000),
                            DataType = "quote",
                            Source = "MarketDataCollector",
                            Timestamp = DateTime.UtcNow
                        };
                        
                        marketData.Tags["market_session"] = IsMarketOpen() ? "regular" : "extended";
                        marketData.Tags["liquidity"] = marketData.Volume > 10000000 ? "high" : "normal";
                        
                        batch.Add(marketData);
                    }

                    // Write batch when full or periodically
                    if (batch.Count >= batchSize)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var result = await _timeSeriesService.WritePointsAsync(batch, stoppingToken);
                        stopwatch.Stop();
                        
                        if (result.IsSuccess)
                        {
                            _logger.LogDebug($"Wrote {batch.Count} market data points in {stopwatch.ElapsedMilliseconds}ms");
                        }
                        else
                        {
                            _logger.LogError("Failed to write market data batch", null,
                                additionalData: new { Error = result.Error });
                        }
                        
                        batch.Clear();
                    }

                    // Simulate market data rate (100ms = 10 updates/second)
                    await Task.Delay(100, stoppingToken);
                }
            }

            private bool IsMarketOpen()
            {
                var now = DateTime.UtcNow;
                var easternTime = TimeZoneInfo.ConvertTimeFromUtc(now, 
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
                
                return easternTime.DayOfWeek != DayOfWeek.Saturday &&
                       easternTime.DayOfWeek != DayOfWeek.Sunday &&
                       easternTime.TimeOfDay >= new TimeSpan(9, 30, 0) &&
                       easternTime.TimeOfDay <= new TimeSpan(16, 0, 0);
            }
        }

        /// <summary>
        /// Example 2: Order execution tracking and analysis
        /// </summary>
        public class OrderExecutionTracker
        {
            private readonly ITimeSeriesService _timeSeriesService;
            private readonly ITradingLogger _logger;

            public OrderExecutionTracker(
                ITimeSeriesService timeSeriesService,
                ITradingLogger logger)
            {
                _timeSeriesService = timeSeriesService;
                _logger = logger;
            }

            public async Task TrackOrderExecutionAsync(Order order, Fill fill)
            {
                var stopwatch = Stopwatch.StartNew();
                
                var executionPoint = new OrderExecutionPoint
                {
                    OrderId = order.OrderId,
                    Symbol = order.Symbol,
                    Side = order.Side.ToString(),
                    OrderType = order.OrderType.ToString(),
                    Status = order.Status.ToString(),
                    Quantity = order.Quantity,
                    Price = order.Price,
                    ExecutedQuantity = fill.Quantity,
                    ExecutedPrice = fill.Price,
                    Commission = fill.Commission,
                    LatencyMicroseconds = (long)(fill.ExecutionTime - order.SubmitTime).TotalMicroseconds,
                    Venue = fill.Venue,
                    Strategy = order.Strategy ?? "manual",
                    Source = "OrderExecutionTracker",
                    Timestamp = fill.ExecutionTime
                };
                
                executionPoint.Tags["account"] = order.Account;
                executionPoint.Tags["session"] = order.Session;
                
                var result = await _timeSeriesService.WritePointAsync(executionPoint);
                stopwatch.Stop();
                
                if (result.IsSuccess)
                {
                    _logger.LogInfo($"Tracked order execution for {order.Symbol}",
                        additionalData: new 
                        { 
                            OrderId = order.OrderId,
                            LatencyUs = executionPoint.LatencyMicroseconds,
                            WriteLatencyMs = stopwatch.ElapsedMilliseconds
                        });
                }
                
                // Analyze execution quality
                await AnalyzeExecutionQualityAsync(order.Symbol, fill.Price);
            }

            private async Task AnalyzeExecutionQualityAsync(string symbol, decimal executionPrice)
            {
                // Get recent market data to compare execution price
                var marketDataResult = await _timeSeriesService.GetRangeAsync<MarketDataPoint>(
                    "market_data",
                    DateTime.UtcNow.AddMinutes(-1),
                    DateTime.UtcNow,
                    new Dictionary<string, string> { ["symbol"] = symbol },
                    limit: 10);

                if (marketDataResult.IsSuccess && marketDataResult.Value.Any())
                {
                    var avgBid = marketDataResult.Value.Average(p => p.Bid);
                    var avgAsk = marketDataResult.Value.Average(p => p.Ask);
                    var midpoint = (avgBid + avgAsk) / 2;
                    var slippage = Math.Abs(executionPrice - midpoint);
                    
                    _logger.LogInfo($"Execution quality analysis for {symbol}",
                        additionalData: new
                        {
                            ExecutionPrice = executionPrice,
                            MarketMidpoint = midpoint,
                            Slippage = slippage,
                            SlippageBps = (slippage / midpoint) * 10000
                        });
                }
            }
        }

        /// <summary>
        /// Example 3: Real-time risk monitoring service
        /// </summary>
        public class RiskMonitoringService : IHostedService
        {
            private readonly ITimeSeriesService _timeSeriesService;
            private readonly ITradingLogger _logger;
            private Timer? _monitoringTimer;

            public RiskMonitoringService(
                ITimeSeriesService timeSeriesService,
                ITradingLogger logger)
            {
                _timeSeriesService = timeSeriesService;
                _logger = logger;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                _monitoringTimer = new Timer(
                    async _ => await CalculateAndStoreRiskMetrics(cancellationToken),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(30)); // Calculate every 30 seconds
                
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                _monitoringTimer?.Dispose();
                return Task.CompletedTask;
            }

            private async Task CalculateAndStoreRiskMetrics(CancellationToken cancellationToken)
            {
                try
                {
                    // Get recent position data
                    var positionsResult = await _timeSeriesService.GetRangeAsync<PositionPoint>(
                        "positions",
                        DateTime.UtcNow.AddMinutes(-5),
                        DateTime.UtcNow,
                        limit: 1000,
                        cancellationToken: cancellationToken);

                    if (!positionsResult.IsSuccess || !positionsResult.Value.Any())
                        return;

                    var positions = positionsResult.Value
                        .GroupBy(p => p.Symbol)
                        .Select(g => g.OrderByDescending(p => p.Timestamp).First())
                        .ToList();

                    // Calculate portfolio metrics
                    var totalValue = positions.Sum(p => p.MarketValue);
                    var totalPnL = positions.Sum(p => p.UnrealizedPnL + p.RealizedPnL);
                    var maxPosition = positions.Max(p => p.MarketValue);
                    
                    // Calculate VaR (simplified)
                    var returns = await CalculateHistoricalReturns(positions.Select(p => p.Symbol));
                    var var95 = CalculateVaR(returns, 0.95m);
                    var var99 = CalculateVaR(returns, 0.99m);
                    
                    // Store risk metrics
                    var riskMetrics = new RiskMetricsPoint
                    {
                        Portfolio = "main",
                        TotalValue = totalValue,
                        DailyPnL = totalPnL,
                        DrawdownPercent = CalculateDrawdown(totalValue),
                        VaR95 = var95,
                        VaR99 = var99,
                        ExpectedShortfall = CalculateExpectedShortfall(returns, 0.95m),
                        SharpeRatio = CalculateSharpeRatio(returns),
                        MaxPositionSize = maxPosition,
                        MarginUsed = totalValue * 0.25m, // Simplified margin calculation
                        BuyingPower = totalValue * 4m - (totalValue * 0.25m),
                        ActivePositions = positions.Count,
                        Source = "RiskMonitoringService",
                        Timestamp = DateTime.UtcNow
                    };
                    
                    // Add risk by symbol
                    foreach (var position in positions)
                    {
                        riskMetrics.RiskBySymbol[position.Symbol] = 
                            Math.Abs(position.MarketValue / totalValue);
                    }
                    
                    await _timeSeriesService.WritePointAsync(riskMetrics, cancellationToken);
                    
                    // Check for risk alerts
                    await CheckRiskAlerts(riskMetrics);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error calculating risk metrics", ex);
                }
            }

            private async Task<List<decimal>> CalculateHistoricalReturns(IEnumerable<string> symbols)
            {
                var returns = new List<decimal>();
                
                foreach (var symbol in symbols)
                {
                    var pricesResult = await _timeSeriesService.GetRangeAsync<MarketDataPoint>(
                        "market_data",
                        DateTime.UtcNow.AddDays(-30),
                        DateTime.UtcNow,
                        new Dictionary<string, string> { ["symbol"] = symbol },
                        limit: 1000);
                    
                    if (pricesResult.IsSuccess && pricesResult.Value.Count > 1)
                    {
                        var prices = pricesResult.Value.OrderBy(p => p.Timestamp).ToList();
                        for (int i = 1; i < prices.Count; i++)
                        {
                            var dailyReturn = (prices[i].Price - prices[i - 1].Price) / prices[i - 1].Price;
                            returns.Add(dailyReturn);
                        }
                    }
                }
                
                return returns;
            }

            private decimal CalculateVaR(List<decimal> returns, decimal confidenceLevel)
            {
                if (!returns.Any()) return 0;
                
                returns.Sort();
                var index = (int)Math.Ceiling((1m - confidenceLevel) * returns.Count) - 1;
                index = Math.Max(0, Math.Min(index, returns.Count - 1));
                
                return Math.Abs(returns[index]);
            }

            private decimal CalculateExpectedShortfall(List<decimal> returns, decimal confidenceLevel)
            {
                if (!returns.Any()) return 0;
                
                returns.Sort();
                var cutoff = (int)Math.Ceiling((1m - confidenceLevel) * returns.Count);
                
                if (cutoff <= 0) return 0;
                
                var tailReturns = returns.Take(cutoff);
                return Math.Abs(tailReturns.Average());
            }

            private decimal CalculateSharpeRatio(List<decimal> returns)
            {
                if (!returns.Any()) return 0;
                
                var avgReturn = returns.Average();
                var stdDev = (decimal)Math.Sqrt((double)returns.Select(r => (r - avgReturn) * (r - avgReturn)).Average());
                
                if (stdDev == 0) return 0;
                
                var riskFreeRate = 0.02m / 252m; // 2% annual risk-free rate
                return (avgReturn - riskFreeRate) / stdDev * (decimal)Math.Sqrt(252);
            }

            private decimal CalculateDrawdown(decimal currentValue)
            {
                // Simplified - would track high water mark in production
                var highWaterMark = currentValue * 1.1m; // Assume 10% below peak
                return (highWaterMark - currentValue) / highWaterMark;
            }

            private async Task CheckRiskAlerts(RiskMetricsPoint metrics)
            {
                var alerts = new List<AlertPoint>();
                
                // Check drawdown
                if (metrics.DrawdownPercent > 0.1m)
                {
                    alerts.Add(new AlertPoint
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        AlertType = "DRAWDOWN_LIMIT",
                        Severity = metrics.DrawdownPercent > 0.2m ? "CRITICAL" : "WARNING",
                        Message = $"Portfolio drawdown {metrics.DrawdownPercent:P1} exceeds limit",
                        Component = "RiskMonitor",
                        Source = "RiskMonitoringService"
                    });
                }
                
                // Check VaR
                if (metrics.VaR95 > metrics.TotalValue * 0.05m)
                {
                    alerts.Add(new AlertPoint
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        AlertType = "VAR_LIMIT",
                        Severity = "WARNING",
                        Message = $"95% VaR ({metrics.VaR95:C}) exceeds 5% of portfolio value",
                        Component = "RiskMonitor",
                        Source = "RiskMonitoringService"
                    });
                }
                
                // Check position concentration
                var maxConcentration = metrics.RiskBySymbol.Values.Max();
                if (maxConcentration > 0.2m)
                {
                    var symbol = metrics.RiskBySymbol.First(kvp => kvp.Value == maxConcentration).Key;
                    alerts.Add(new AlertPoint
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        AlertType = "CONCENTRATION_LIMIT",
                        Severity = "WARNING",
                        Symbol = symbol,
                        Message = $"Position concentration in {symbol} ({maxConcentration:P1}) exceeds 20% limit",
                        Component = "RiskMonitor",
                        Source = "RiskMonitoringService"
                    });
                }
                
                if (alerts.Any())
                {
                    await _timeSeriesService.WritePointsAsync(alerts);
                }
            }
        }

        /// <summary>
        /// Example 4: Performance monitoring and analysis
        /// </summary>
        public class PerformanceMonitor
        {
            private readonly ITimeSeriesService _timeSeriesService;

            public PerformanceMonitor(ITimeSeriesService timeSeriesService)
            {
                _timeSeriesService = timeSeriesService;
            }

            public async Task RecordPerformanceMetricsAsync(
                string component,
                string operation,
                TimeSpan latency,
                Dictionary<string, long>? customMetrics = null)
            {
                var performancePoint = new PerformanceMetricsPoint
                {
                    Component = component,
                    Operation = operation,
                    LatencyNanoseconds = (long)(latency.TotalMilliseconds * 1_000_000),
                    MemoryBytes = GC.GetTotalMemory(false),
                    CpuPercent = GetCpuUsage(),
                    ThreadCount = Process.GetCurrentProcess().Threads.Count,
                    GcGen0 = GC.CollectionCount(0),
                    GcGen1 = GC.CollectionCount(1),
                    GcGen2 = GC.CollectionCount(2),
                    MessagesProcessed = 1,
                    Throughput = 1.0 / latency.TotalSeconds,
                    ErrorCount = 0,
                    Source = "PerformanceMonitor",
                    Timestamp = DateTime.UtcNow
                };
                
                if (customMetrics != null)
                {
                    foreach (var metric in customMetrics)
                    {
                        performancePoint.CustomMetrics[metric.Key] = metric.Value;
                    }
                }
                
                await _timeSeriesService.WritePointAsync(performancePoint);
                
                // Alert on slow operations
                if (latency.TotalMilliseconds > 100)
                {
                    await _timeSeriesService.WritePointAsync(new AlertPoint
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        AlertType = "SLOW_OPERATION",
                        Severity = latency.TotalMilliseconds > 1000 ? "CRITICAL" : "WARNING",
                        Message = $"{component}.{operation} took {latency.TotalMilliseconds:F0}ms",
                        Component = component,
                        Source = "PerformanceMonitor"
                    });
                }
            }

            private double GetCpuUsage()
            {
                // Simplified CPU usage calculation
                return Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / 
                       Environment.ProcessorCount / 
                       Environment.TickCount * 100;
            }
        }

        /// <summary>
        /// Example 5: Query and analysis examples
        /// </summary>
        public class TimeSeriesAnalyzer
        {
            private readonly ITimeSeriesService _timeSeriesService;
            private readonly ITradingLogger _logger;

            public TimeSeriesAnalyzer(
                ITimeSeriesService timeSeriesService,
                ITradingLogger logger)
            {
                _timeSeriesService = timeSeriesService;
                _logger = logger;
            }

            /// <summary>
            /// Get trading statistics for a time period
            /// </summary>
            public async Task<TradingStatsPoint> GetTradingStatsAsync(
                DateTime start,
                DateTime end,
                string? strategy = null)
            {
                // Query executions
                var executionsQuery = $@"
                    from(bucket: ""order-data"")
                        |> range(start: {start:yyyy-MM-ddTHH:mm:ssZ}, stop: {end:yyyy-MM-ddTHH:mm:ssZ})
                        |> filter(fn: (r) => r._measurement == ""order_execution"")
                        |> filter(fn: (r) => r.status == ""FILLED"")
                        {(strategy != null ? $@"|> filter(fn: (r) => r.strategy == ""{strategy}"")" : "")}";
                
                var executionsResult = await _timeSeriesService.QueryAsync<OrderExecutionPoint>(executionsQuery);
                
                if (!executionsResult.IsSuccess || !executionsResult.Value.Any())
                {
                    return new TradingStatsPoint { Period = "custom" };
                }
                
                var executions = executionsResult.Value;
                
                // Calculate P&L
                var pnlBySymbol = new Dictionary<string, decimal>();
                foreach (var exec in executions.GroupBy(e => e.Symbol))
                {
                    var buys = exec.Where(e => e.Side == "BUY").ToList();
                    var sells = exec.Where(e => e.Side == "SELL").ToList();
                    
                    var buyValue = buys.Sum(e => e.ExecutedQuantity * e.ExecutedPrice);
                    var sellValue = sells.Sum(e => e.ExecutedQuantity * e.ExecutedPrice);
                    var commission = exec.Sum(e => e.Commission);
                    
                    pnlBySymbol[exec.Key] = sellValue - buyValue - commission;
                }
                
                var totalPnL = pnlBySymbol.Values.Sum();
                var winners = pnlBySymbol.Values.Count(pnl => pnl > 0);
                var losers = pnlBySymbol.Values.Count(pnl => pnl < 0);
                
                return new TradingStatsPoint
                {
                    Period = $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}",
                    TotalTrades = executions.Count,
                    WinningTrades = winners,
                    LosingTrades = losers,
                    TotalPnL = totalPnL,
                    AverageWin = winners > 0 ? pnlBySymbol.Values.Where(pnl => pnl > 0).Average() : 0,
                    AverageLoss = losers > 0 ? Math.Abs(pnlBySymbol.Values.Where(pnl => pnl < 0).Average()) : 0,
                    WinRate = executions.Any() ? (decimal)winners / (winners + losers) : 0,
                    ProfitFactor = CalculateProfitFactor(pnlBySymbol.Values),
                    MaxDrawdown = await CalculateMaxDrawdownAsync(start, end),
                    TotalVolume = executions.Sum(e => e.ExecutedQuantity * e.ExecutedPrice),
                    TotalCommission = executions.Sum(e => e.Commission),
                    PnLByStrategy = strategy != null 
                        ? new Dictionary<string, decimal> { [strategy] = totalPnL }
                        : await GetPnLByStrategyAsync(start, end),
                    Source = "TradingStatsAnalyzer",
                    Timestamp = DateTime.UtcNow
                };
            }

            private decimal CalculateProfitFactor(IEnumerable<decimal> pnlValues)
            {
                var profits = pnlValues.Where(pnl => pnl > 0).Sum();
                var losses = Math.Abs(pnlValues.Where(pnl => pnl < 0).Sum());
                
                return losses > 0 ? profits / losses : profits > 0 ? decimal.MaxValue : 0;
            }

            private async Task<decimal> CalculateMaxDrawdownAsync(DateTime start, DateTime end)
            {
                var portfolioQuery = $@"
                    from(bucket: ""performance-metrics"")
                        |> range(start: {start:yyyy-MM-ddTHH:mm:ssZ}, stop: {end:yyyy-MM-ddTHH:mm:ssZ})
                        |> filter(fn: (r) => r._measurement == ""risk_metrics"")
                        |> filter(fn: (r) => r._field == ""total_value"")
                        |> max()";
                
                var result = await _timeSeriesService.QueryAsync<RiskMetricsPoint>(portfolioQuery);
                
                return result.IsSuccess && result.Value.Any() 
                    ? result.Value.Max(r => r.DrawdownPercent)
                    : 0;
            }

            private async Task<Dictionary<string, decimal>> GetPnLByStrategyAsync(
                DateTime start, 
                DateTime end)
            {
                var strategyPnL = new Dictionary<string, decimal>();
                
                // This would aggregate P&L by strategy
                // Simplified for example
                
                return strategyPnL;
            }
        }

        /// <summary>
        /// Service registration example
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {
            // Add InfluxDB time-series
            services.AddInfluxDbTimeSeriesForDevelopment();
            
            // Add example services
            services.AddHostedService<MarketDataCollector>();
            services.AddSingleton<OrderExecutionTracker>();
            services.AddHostedService<RiskMonitoringService>();
            services.AddSingleton<PerformanceMonitor>();
            services.AddSingleton<TimeSeriesAnalyzer>();
        }
    }

    // Supporting models for examples
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType OrderType { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime SubmitTime { get; set; }
        public string? Strategy { get; set; }
        public string Account { get; set; } = string.Empty;
        public string Session { get; set; } = string.Empty;
    }

    public class Fill
    {
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Commission { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string Venue { get; set; } = string.Empty;
    }
}
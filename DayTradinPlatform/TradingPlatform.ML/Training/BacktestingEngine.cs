// File: TradingPlatform.ML/Training/BacktestingEngine.cs

using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Core.Logging;
using TradingPlatform.Core.Models;
using TradingPlatform.Foundation.Models;
using TradingPlatform.ML.Interfaces;
using TradingPlatform.ML.Models;

namespace TradingPlatform.ML.Training
{
    /// <summary>
    /// Backtesting engine for ML model evaluation
    /// </summary>
    public class BacktestingEngine : CanonicalServiceBase
    {
        private readonly decimal _initialCapital;
        private readonly decimal _transactionCost;
        private readonly decimal _slippage;
        
        public BacktestingEngine(
            IServiceProvider serviceProvider,
            ITradingLogger logger,
            decimal initialCapital = 100000m)
            : base(serviceProvider, logger, "BacktestingEngine")
        {
            _initialCapital = initialCapital;
            _transactionCost = 0.001m; // 0.1% per trade
            _slippage = 0.0005m; // 0.05% slippage
        }
        
        /// <summary>
        /// Run backtesting simulation
        /// </summary>
        public async Task<TradingResult<BacktestResult>> RunBacktestAsync(
            IPredictiveModel<PricePredictionInput, PricePrediction> model,
            IList<MarketDataSnapshot> historicalData,
            BacktestOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var portfolio = new BacktestPortfolio(_initialCapital);
                    var trades = new List<BacktestTrade>();
                    var equityCurve = new List<EquityPoint>();
                    var signals = new List<TradingSignal>();
                    
                    LogInfo($"Starting backtest from {historicalData.First().Timestamp} to {historicalData.Last().Timestamp}",
                        additionalData: new { 
                            DataPoints = historicalData.Count,
                            InitialCapital = _initialCapital,
                            Strategy = options.StrategyName
                        });
                    
                    // Process each data point
                    for (int i = options.LookbackPeriod; i < historicalData.Count - 1; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        var currentData = historicalData[i];
                        var nextData = historicalData[i + 1];
                        
                        // Generate prediction
                        var input = CreatePredictionInput(historicalData, i);
                        var predictionResult = await model.PredictAsync(input, cancellationToken);
                        
                        if (!predictionResult.IsSuccess)
                            continue;
                        
                        var prediction = predictionResult.Value;
                        
                        // Generate trading signal
                        var signal = GenerateTradingSignal(
                            prediction, 
                            currentData, 
                            portfolio,
                            options);
                        
                        signals.Add(signal);
                        
                        // Execute trades based on signal
                        if (signal.Action != TradeAction.Hold)
                        {
                            var trade = ExecuteTrade(
                                signal,
                                currentData,
                                nextData,
                                portfolio,
                                options);
                            
                            if (trade != null)
                            {
                                trades.Add(trade);
                                RecordServiceMetric("Backtest.TradeCount", trades.Count);
                            }
                        }
                        
                        // Update portfolio value
                        portfolio.UpdateValue(currentData.Close);
                        
                        // Record equity curve
                        equityCurve.Add(new EquityPoint
                        {
                            Timestamp = currentData.Timestamp,
                            Value = portfolio.TotalValue,
                            Cash = portfolio.Cash,
                            PositionValue = portfolio.PositionValue,
                            DrawdownPercent = portfolio.DrawdownPercent
                        });
                        
                        // Progress reporting
                        if (i % 1000 == 0)
                        {
                            var progress = (double)(i - options.LookbackPeriod) / 
                                         (historicalData.Count - options.LookbackPeriod - 1);
                            LogInfo($"Backtest progress: {progress:P1}",
                                additionalData: new {
                                    CurrentValue = portfolio.TotalValue,
                                    TradeCount = trades.Count,
                                    CurrentDate = currentData.Timestamp
                                });
                        }
                    }
                    
                    // Calculate performance metrics
                    var metrics = CalculatePerformanceMetrics(
                        trades, 
                        equityCurve, 
                        _initialCapital);
                    
                    var result = new BacktestResult
                    {
                        StartDate = historicalData[options.LookbackPeriod].Timestamp,
                        EndDate = historicalData.Last().Timestamp,
                        InitialCapital = _initialCapital,
                        FinalCapital = portfolio.TotalValue,
                        TotalReturn = (portfolio.TotalValue - _initialCapital) / _initialCapital,
                        Trades = trades,
                        EquityCurve = equityCurve,
                        PerformanceMetrics = metrics,
                        SignalCount = signals.Count,
                        ModelMetrics = await GetModelMetrics(model, signals)
                    };
                    
                    LogInfo("Backtest completed",
                        additionalData: new {
                            TotalReturn = $"{result.TotalReturn:P2}",
                            SharpeRatio = metrics.SharpeRatio,
                            MaxDrawdown = $"{metrics.MaxDrawdown:P2}",
                            WinRate = $"{metrics.WinRate:P2}",
                            TotalTrades = trades.Count
                        });
                    
                    return TradingResult<BacktestResult>.Success(result);
                },
                nameof(RunBacktestAsync));
        }
        
        /// <summary>
        /// Run comparative backtesting
        /// </summary>
        public async Task<TradingResult<ComparativeBacktestResult>> RunComparativeBacktestAsync(
            Dictionary<string, IPredictiveModel<PricePredictionInput, PricePrediction>> models,
            IList<MarketDataSnapshot> historicalData,
            BacktestOptions options,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteServiceOperationAsync(
                async () =>
                {
                    var results = new Dictionary<string, BacktestResult>();
                    
                    foreach (var (modelName, model) in models)
                    {
                        LogInfo($"Running backtest for model: {modelName}");
                        
                        var result = await RunBacktestAsync(
                            model, 
                            historicalData, 
                            options with { StrategyName = modelName },
                            cancellationToken);
                        
                        if (result.IsSuccess)
                        {
                            results[modelName] = result.Value;
                        }
                    }
                    
                    var comparativeResult = new ComparativeBacktestResult
                    {
                        ModelResults = results,
                        BestModel = results.OrderByDescending(r => r.Value.PerformanceMetrics.SharpeRatio)
                                          .First().Key,
                        ComparisonMetrics = GenerateComparisonMetrics(results)
                    };
                    
                    LogInfo("Comparative backtest completed",
                        additionalData: new {
                            BestModel = comparativeResult.BestModel,
                            ModelCount = results.Count
                        });
                    
                    return TradingResult<ComparativeBacktestResult>.Success(comparativeResult);
                },
                nameof(RunComparativeBacktestAsync));
        }
        
        // Helper methods
        
        private PricePredictionInput CreatePredictionInput(
            IList<MarketDataSnapshot> data,
            int currentIndex)
        {
            // This would use the actual feature engineering
            // Simplified for demonstration
            var current = data[currentIndex];
            
            return new PricePredictionInput
            {
                Open = (float)current.Open,
                High = (float)current.High,
                Low = (float)current.Low,
                Close = (float)current.Close,
                Volume = (float)current.Volume,
                RSI = 50f, // Would calculate actual RSI
                MACD = 0f, // Would calculate actual MACD
                VolumeRatio = 1f,
                PriceChangePercent = currentIndex > 0 ? 
                    (float)((current.Close - data[currentIndex-1].Close) / data[currentIndex-1].Close * 100) : 0f,
                DayOfWeek = (float)current.Timestamp.DayOfWeek,
                HourOfDay = current.Timestamp.Hour
            };
        }
        
        private TradingSignal GenerateTradingSignal(
            PricePrediction prediction,
            MarketDataSnapshot currentData,
            BacktestPortfolio portfolio,
            BacktestOptions options)
        {
            var signal = new TradingSignal
            {
                Timestamp = currentData.Timestamp,
                Symbol = currentData.Symbol,
                PredictedPrice = prediction.PredictedPrice,
                CurrentPrice = (float)currentData.Close,
                Confidence = prediction.Confidence,
                PredictedChange = prediction.PriceChangePercent
            };
            
            // Determine action based on prediction and thresholds
            if (prediction.PriceChangePercent > options.BuyThreshold && 
                prediction.Confidence > options.ConfidenceThreshold)
            {
                signal.Action = portfolio.HasPosition ? TradeAction.Hold : TradeAction.Buy;
                signal.Strength = Math.Min(prediction.PriceChangePercent / 5f, 1f); // Normalize to 0-1
            }
            else if (prediction.PriceChangePercent < -options.SellThreshold && 
                     prediction.Confidence > options.ConfidenceThreshold)
            {
                signal.Action = portfolio.HasPosition ? TradeAction.Sell : TradeAction.Hold;
                signal.Strength = Math.Min(Math.Abs(prediction.PriceChangePercent) / 5f, 1f);
            }
            else
            {
                signal.Action = TradeAction.Hold;
                signal.Strength = 0f;
            }
            
            return signal;
        }
        
        private BacktestTrade? ExecuteTrade(
            TradingSignal signal,
            MarketDataSnapshot currentData,
            MarketDataSnapshot nextData,
            BacktestPortfolio portfolio,
            BacktestOptions options)
        {
            BacktestTrade? trade = null;
            
            if (signal.Action == TradeAction.Buy && !portfolio.HasPosition)
            {
                // Calculate position size
                var positionSize = CalculatePositionSize(
                    portfolio.Cash,
                    currentData.Close,
                    options);
                
                if (positionSize > 0)
                {
                    var executionPrice = nextData.Open * (1 + _slippage);
                    var cost = positionSize * executionPrice * (1 + _transactionCost);
                    
                    if (cost <= portfolio.Cash)
                    {
                        trade = new BacktestTrade
                        {
                            TradeId = Guid.NewGuid().ToString(),
                            EntryTime = nextData.Timestamp,
                            EntryPrice = executionPrice,
                            Quantity = positionSize,
                            Direction = TradeDirection.Long,
                            Signal = signal
                        };
                        
                        portfolio.OpenPosition(positionSize, executionPrice, cost);
                        
                        RecordServiceMetric("Backtest.Buy", 1);
                    }
                }
            }
            else if (signal.Action == TradeAction.Sell && portfolio.HasPosition)
            {
                var executionPrice = nextData.Open * (1 - _slippage);
                var proceeds = portfolio.PositionSize * executionPrice * (1 - _transactionCost);
                
                trade = portfolio.CurrentTrade;
                if (trade != null)
                {
                    trade.ExitTime = nextData.Timestamp;
                    trade.ExitPrice = executionPrice;
                    trade.PnL = proceeds - (portfolio.PositionSize * portfolio.EntryPrice);
                    trade.PnLPercent = trade.PnL / (portfolio.PositionSize * portfolio.EntryPrice);
                    trade.HoldingPeriod = trade.ExitTime - trade.EntryTime;
                }
                
                portfolio.ClosePosition(proceeds);
                
                RecordServiceMetric("Backtest.Sell", 1);
            }
            
            return trade;
        }
        
        private decimal CalculatePositionSize(
            decimal availableCash,
            decimal price,
            BacktestOptions options)
        {
            // Fixed percentage of capital
            var targetValue = availableCash * options.PositionSizePercent;
            var shares = Math.Floor(targetValue / price);
            
            // Apply maximum position size
            var maxShares = Math.Floor(availableCash * options.MaxPositionPercent / price);
            
            return Math.Min(shares, maxShares);
        }
        
        private PerformanceMetrics CalculatePerformanceMetrics(
            List<BacktestTrade> trades,
            List<EquityPoint> equityCurve,
            decimal initialCapital)
        {
            var metrics = new PerformanceMetrics();
            
            if (!trades.Any() || !equityCurve.Any())
                return metrics;
            
            // Basic metrics
            metrics.TotalTrades = trades.Count;
            metrics.WinningTrades = trades.Count(t => t.PnL > 0);
            metrics.LosingTrades = trades.Count(t => t.PnL < 0);
            metrics.WinRate = metrics.TotalTrades > 0 ? 
                (double)metrics.WinningTrades / metrics.TotalTrades : 0;
            
            // PnL metrics
            metrics.TotalPnL = trades.Sum(t => t.PnL);
            metrics.AveragePnL = trades.Average(t => t.PnL);
            metrics.AverageWin = trades.Where(t => t.PnL > 0).DefaultIfEmpty().Average(t => t.PnL);
            metrics.AverageLoss = trades.Where(t => t.PnL < 0).DefaultIfEmpty().Average(t => t.PnL);
            
            // Risk metrics
            metrics.MaxDrawdown = CalculateMaxDrawdown(equityCurve);
            metrics.MaxDrawdownDuration = CalculateMaxDrawdownDuration(equityCurve);
            
            // Return metrics
            var returns = CalculateReturns(equityCurve);
            metrics.AnnualizedReturn = CalculateAnnualizedReturn(
                equityCurve.First().Value,
                equityCurve.Last().Value,
                equityCurve.Last().Timestamp - equityCurve.First().Timestamp);
            
            metrics.SharpeRatio = CalculateSharpeRatio(returns, metrics.AnnualizedReturn);
            metrics.SortinoRatio = CalculateSortinoRatio(returns, metrics.AnnualizedReturn);
            metrics.CalmarRatio = metrics.MaxDrawdown > 0 ? 
                metrics.AnnualizedReturn / metrics.MaxDrawdown : 0;
            
            // Trading metrics
            metrics.ProfitFactor = metrics.AverageLoss != 0 ? 
                Math.Abs(metrics.AverageWin / metrics.AverageLoss) : 0;
            
            metrics.ExpectedValue = metrics.WinRate * metrics.AverageWin + 
                                  (1 - metrics.WinRate) * metrics.AverageLoss;
            
            return metrics;
        }
        
        private double CalculateMaxDrawdown(List<EquityPoint> equityCurve)
        {
            double maxDrawdown = 0;
            decimal peak = equityCurve.First().Value;
            
            foreach (var point in equityCurve)
            {
                if (point.Value > peak)
                    peak = point.Value;
                
                var drawdown = (double)((peak - point.Value) / peak);
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
            
            return maxDrawdown;
        }
        
        private TimeSpan CalculateMaxDrawdownDuration(List<EquityPoint> equityCurve)
        {
            TimeSpan maxDuration = TimeSpan.Zero;
            DateTime? drawdownStart = null;
            decimal peak = equityCurve.First().Value;
            
            foreach (var point in equityCurve)
            {
                if (point.Value >= peak)
                {
                    if (drawdownStart.HasValue)
                    {
                        var duration = point.Timestamp - drawdownStart.Value;
                        maxDuration = duration > maxDuration ? duration : maxDuration;
                        drawdownStart = null;
                    }
                    peak = point.Value;
                }
                else if (!drawdownStart.HasValue)
                {
                    drawdownStart = point.Timestamp;
                }
            }
            
            return maxDuration;
        }
        
        private List<double> CalculateReturns(List<EquityPoint> equityCurve)
        {
            var returns = new List<double>();
            
            for (int i = 1; i < equityCurve.Count; i++)
            {
                var dailyReturn = (double)((equityCurve[i].Value - equityCurve[i-1].Value) / 
                                         equityCurve[i-1].Value);
                returns.Add(dailyReturn);
            }
            
            return returns;
        }
        
        private double CalculateAnnualizedReturn(decimal startValue, decimal endValue, TimeSpan period)
        {
            var years = period.TotalDays / 365.25;
            var totalReturn = (double)((endValue - startValue) / startValue);
            
            return Math.Pow(1 + totalReturn, 1 / years) - 1;
        }
        
        private double CalculateSharpeRatio(List<double> returns, double riskFreeRate = 0.02)
        {
            if (!returns.Any()) return 0;
            
            var avgReturn = returns.Average();
            var stdDev = CalculateStandardDeviation(returns);
            
            if (stdDev == 0) return 0;
            
            // Annualized Sharpe
            return (avgReturn * 252 - riskFreeRate) / (stdDev * Math.Sqrt(252));
        }
        
        private double CalculateSortinoRatio(List<double> returns, double targetReturn = 0)
        {
            if (!returns.Any()) return 0;
            
            var avgReturn = returns.Average();
            var downside = returns.Where(r => r < targetReturn).ToList();
            
            if (!downside.Any()) return 0;
            
            var downsideDeviation = CalculateStandardDeviation(downside);
            
            if (downsideDeviation == 0) return 0;
            
            return (avgReturn * 252 - targetReturn) / (downsideDeviation * Math.Sqrt(252));
        }
        
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            
            var mean = values.Average();
            var sumSquares = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }
        
        private async Task<Dictionary<string, double>> GetModelMetrics(
            IPredictiveModel<PricePredictionInput, PricePrediction> model,
            List<TradingSignal> signals)
        {
            var metrics = new Dictionary<string, double>();
            
            // Calculate prediction accuracy
            var correctDirections = signals.Count(s => 
                (s.PredictedChange > 0 && s.ActualChange > 0) ||
                (s.PredictedChange < 0 && s.ActualChange < 0));
            
            metrics["DirectionalAccuracy"] = signals.Any() ? 
                (double)correctDirections / signals.Count : 0;
            
            // Average confidence
            metrics["AverageConfidence"] = signals.Any() ? 
                signals.Average(s => s.Confidence) : 0;
            
            // Signal quality
            var profitableSignals = signals.Count(s => 
                s.Action != TradeAction.Hold && s.ActualChange * (s.Action == TradeAction.Buy ? 1 : -1) > 0);
            
            metrics["SignalQuality"] = signals.Any(s => s.Action != TradeAction.Hold) ?
                (double)profitableSignals / signals.Count(s => s.Action != TradeAction.Hold) : 0;
            
            return metrics;
        }
        
        private Dictionary<string, double> GenerateComparisonMetrics(
            Dictionary<string, BacktestResult> results)
        {
            var metrics = new Dictionary<string, double>();
            
            foreach (var (modelName, result) in results)
            {
                metrics[$"{modelName}_Sharpe"] = result.PerformanceMetrics.SharpeRatio;
                metrics[$"{modelName}_Return"] = result.PerformanceMetrics.AnnualizedReturn;
                metrics[$"{modelName}_MaxDD"] = result.PerformanceMetrics.MaxDrawdown;
                metrics[$"{modelName}_WinRate"] = result.PerformanceMetrics.WinRate;
            }
            
            return metrics;
        }
    }
    
    // Supporting classes
    
    public class BacktestPortfolio
    {
        public decimal Cash { get; private set; }
        public decimal PositionSize { get; private set; }
        public decimal EntryPrice { get; private set; }
        public decimal CurrentPrice { get; private set; }
        public bool HasPosition => PositionSize > 0;
        public decimal PositionValue => PositionSize * CurrentPrice;
        public decimal TotalValue => Cash + PositionValue;
        public decimal DrawdownPercent { get; private set; }
        public BacktestTrade? CurrentTrade { get; private set; }
        
        private decimal _peakValue;
        
        public BacktestPortfolio(decimal initialCash)
        {
            Cash = initialCash;
            _peakValue = initialCash;
        }
        
        public void OpenPosition(decimal size, decimal price, decimal totalCost)
        {
            PositionSize = size;
            EntryPrice = price;
            CurrentPrice = price;
            Cash -= totalCost;
            
            CurrentTrade = new BacktestTrade
            {
                TradeId = Guid.NewGuid().ToString(),
                Quantity = size,
                Direction = TradeDirection.Long
            };
        }
        
        public void ClosePosition(decimal proceeds)
        {
            Cash += proceeds;
            PositionSize = 0;
            EntryPrice = 0;
            CurrentTrade = null;
        }
        
        public void UpdateValue(decimal currentMarketPrice)
        {
            CurrentPrice = currentMarketPrice;
            
            if (TotalValue > _peakValue)
                _peakValue = TotalValue;
            
            DrawdownPercent = _peakValue > 0 ? (_peakValue - TotalValue) / _peakValue : 0;
        }
    }
    
    public class BacktestResult
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal InitialCapital { get; set; }
        public decimal FinalCapital { get; set; }
        public decimal TotalReturn { get; set; }
        public List<BacktestTrade> Trades { get; set; } = new();
        public List<EquityPoint> EquityCurve { get; set; } = new();
        public PerformanceMetrics PerformanceMetrics { get; set; } = new();
        public int SignalCount { get; set; }
        public Dictionary<string, double> ModelMetrics { get; set; } = new();
    }
    
    public class BacktestTrade
    {
        public string TradeId { get; set; } = string.Empty;
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public TradeDirection Direction { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
        public TimeSpan HoldingPeriod { get; set; }
        public TradingSignal? Signal { get; set; }
    }
    
    public class TradingSignal
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public TradeAction Action { get; set; }
        public float Strength { get; set; }
        public float PredictedPrice { get; set; }
        public float CurrentPrice { get; set; }
        public float PredictedChange { get; set; }
        public float ActualChange { get; set; }
        public float Confidence { get; set; }
    }
    
    public class EquityPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Value { get; set; }
        public decimal Cash { get; set; }
        public decimal PositionValue { get; set; }
        public decimal DrawdownPercent { get; set; }
    }
    
    public class PerformanceMetrics
    {
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal AveragePnL { get; set; }
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public TimeSpan MaxDrawdownDuration { get; set; }
        public double AnnualizedReturn { get; set; }
        public double SharpeRatio { get; set; }
        public double SortinoRatio { get; set; }
        public double CalmarRatio { get; set; }
        public double ProfitFactor { get; set; }
        public decimal ExpectedValue { get; set; }
    }
    
    public class BacktestOptions
    {
        public string StrategyName { get; set; } = "ML Strategy";
        public int LookbackPeriod { get; set; } = 50;
        public decimal PositionSizePercent { get; set; } = 0.1m; // 10% per trade
        public decimal MaxPositionPercent { get; set; } = 0.3m; // 30% max
        public float BuyThreshold { get; set; } = 0.5f; // 0.5% predicted gain
        public float SellThreshold { get; set; } = 0.5f; // 0.5% predicted loss
        public float ConfidenceThreshold { get; set; } = 0.6f; // 60% confidence
        public bool UseStopLoss { get; set; } = true;
        public decimal StopLossPercent { get; set; } = 0.02m; // 2% stop loss
        public bool UseTakeProfit { get; set; } = true;
        public decimal TakeProfitPercent { get; set; } = 0.05m; // 5% take profit
    }
    
    public class ComparativeBacktestResult
    {
        public Dictionary<string, BacktestResult> ModelResults { get; set; } = new();
        public string BestModel { get; set; } = string.Empty;
        public Dictionary<string, double> ComparisonMetrics { get; set; } = new();
    }
    
    public enum TradeAction
    {
        Hold,
        Buy,
        Sell
    }
    
    public enum TradeDirection
    {
        Long,
        Short
    }
    
    public class SensitivityAnalysisResult
    {
        public float BaselinePrediction { get; set; }
        public Dictionary<string, FeatureSensitivity> FeatureSensitivities { get; set; } = new();
        public string MostSensitiveFeature { get; set; } = string.Empty;
        public string LeastSensitiveFeature { get; set; } = string.Empty;
    }
    
    public class FeatureSensitivity
    {
        public string FeatureName { get; set; } = string.Empty;
        public double AverageSensitivity { get; set; }
        public double MaxSensitivity { get; set; }
        public double MinSensitivity { get; set; }
    }
    
    public class SensitivityOptions
    {
        public double[] PerturbationLevels { get; set; } = { -0.1, -0.05, 0.05, 0.1 };
    }
}
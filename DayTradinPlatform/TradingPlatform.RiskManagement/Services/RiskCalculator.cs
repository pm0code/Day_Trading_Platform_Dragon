using TradingPlatform.RiskManagement.Models;

using TradingPlatform.Core.Interfaces;
namespace TradingPlatform.RiskManagement.Services;

public class RiskCalculator : IRiskCalculator
{
    private readonly ILogger _logger;

    public RiskCalculator(ILogger logger)
    {
        _logger = logger;
    }

    public decimal CalculateVaR(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m)
    {
        var returnsList = returns.ToList();
        if (!returnsList.Any()) return 0m;

        returnsList.Sort();
        var index = (int)Math.Ceiling((1m - confidenceLevel) * returnsList.Count) - 1;
        index = Math.Max(0, Math.Min(index, returnsList.Count - 1));
        
        var var95 = Math.Abs(returnsList[index]);
        _logger.LogDebug("VaR calculated: {VaR} at {ConfidenceLevel}% confidence", var95, confidenceLevel * 100);
        return var95;
    }

    public decimal CalculateExpectedShortfall(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m)
    {
        var returnsList = returns.ToList();
        if (!returnsList.Any()) return 0m;

        returnsList.Sort();
        var cutoff = (int)Math.Ceiling((1m - confidenceLevel) * returnsList.Count);
        
        if (cutoff <= 0) return 0m;
        
        var tailReturns = returnsList.Take(cutoff);
        var expectedShortfall = Math.Abs(tailReturns.Average());
        
        _logger.LogDebug("Expected Shortfall calculated: {ES} at {ConfidenceLevel}% confidence", 
            expectedShortfall, confidenceLevel * 100);
        return expectedShortfall;
    }

    public decimal CalculateMaxDrawdown(IEnumerable<decimal> portfolioValues)
    {
        var values = portfolioValues.ToList();
        if (values.Count < 2) return 0m;

        decimal maxDrawdown = 0m;
        decimal peak = values[0];

        foreach (var value in values)
        {
            if (value > peak)
            {
                peak = value;
            }
            else
            {
                var drawdown = (peak - value) / peak;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
        }

        var maxDrawdownPercent = maxDrawdown * 100m;
        _logger.LogDebug("Max Drawdown calculated: {MaxDrawdown}%", maxDrawdownPercent);
        return maxDrawdownPercent;
    }

    public decimal CalculateSharpeRatio(IEnumerable<decimal> returns, decimal riskFreeRate)
    {
        var returnsList = returns.ToList();
        if (!returnsList.Any()) return 0m;

        var excessReturns = returnsList.Select(r => r - riskFreeRate);
        var meanExcessReturn = excessReturns.Average();
        var standardDeviation = CalculateStandardDeviation(excessReturns);

        if (standardDeviation == 0m) return 0m;

        var sharpeRatio = meanExcessReturn / standardDeviation;
        _logger.LogDebug("Sharpe Ratio calculated: {SharpeRatio}", sharpeRatio);
        return sharpeRatio;
    }

    public decimal CalculatePositionSize(decimal accountBalance, decimal riskPerTrade, decimal stopLoss)
    {
        if (stopLoss <= 0m) return 0m;

        var riskAmount = accountBalance * riskPerTrade;
        var positionSize = riskAmount / stopLoss;
        
        _logger.LogDebug("Position size calculated: {PositionSize} (Risk: {RiskAmount}, Stop: {StopLoss})", 
            positionSize, riskAmount, stopLoss);
        return positionSize;
    }

    public RiskMetrics CalculatePortfolioRisk(IEnumerable<Position> positions)
    {
        var positionsList = positions.ToList();
        
        if (!positionsList.Any())
        {
            return new RiskMetrics(0m, 0m, 0m, 0m, 0m, 0m, 0m, DateTime.UtcNow);
        }

        var returns = CalculatePositionReturns(positionsList);
        var portfolioValues = positionsList.Select(p => p.MarketValue);
        
        var var95 = CalculateVaR(returns, 0.95m);
        var var99 = CalculateVaR(returns, 0.99m);
        var expectedShortfall = CalculateExpectedShortfall(returns, 0.95m);
        var maxDrawdown = CalculateMaxDrawdown(portfolioValues);
        var sharpeRatio = CalculateSharpeRatio(returns, 0.02m); // Assuming 2% risk-free rate
        var volatility = CalculateStandardDeviation(returns);
        
        // Simplified beta calculation (would need market data for proper calculation)
        var beta = 1.0m;

        var metrics = new RiskMetrics(
            VaR95: var95,
            VaR99: var99,
            ExpectedShortfall: expectedShortfall,
            SharpeRatio: sharpeRatio,
            MaxDrawdown: maxDrawdown,
            Beta: beta,
            PortfolioVolatility: volatility,
            CalculatedAt: DateTime.UtcNow
        );

        _logger.LogInformation("Portfolio risk metrics calculated - VaR95: {VaR95}, Sharpe: {Sharpe}, Vol: {Vol}", 
            var95, sharpeRatio, volatility);

        return metrics;
    }

    private IEnumerable<decimal> CalculatePositionReturns(IEnumerable<Position> positions)
    {
        return positions.Select(p =>
        {
            if (p.AveragePrice <= 0m) return 0m;
            return (p.CurrentPrice - p.AveragePrice) / p.AveragePrice;
        });
    }

    private decimal CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var valuesList = values.ToList();
        if (valuesList.Count < 2) return 0m;

        var mean = valuesList.Average();
        var sumSquaredDifferences = valuesList.Sum(v => (v - mean) * (v - mean));
        var variance = sumSquaredDifferences / (valuesList.Count - 1);
        
        // Using decimal square root approximation
        return (decimal)Math.Sqrt((double)variance);
    }
}
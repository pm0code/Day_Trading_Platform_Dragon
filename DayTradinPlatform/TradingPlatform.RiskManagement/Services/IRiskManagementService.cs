using TradingPlatform.RiskManagement.Models;
using TradingPlatform.Foundation.Models;

namespace TradingPlatform.RiskManagement.Services;

public interface IRiskManagementService
{
    Task<TradingResult<RiskStatus>> GetRiskStatusAsync();
    Task<TradingResult<RiskLimits>> GetRiskLimitsAsync();
    Task<TradingResult<bool>> UpdateRiskLimitsAsync(RiskLimits limits);
    Task<TradingResult<bool>> ValidateOrderAsync(OrderRiskRequest request);
    Task<TradingResult<decimal>> CalculatePositionRiskAsync(string symbol, decimal quantity, decimal price);
    Task<TradingResult<RiskMetrics>> GetRiskMetricsAsync();
}

public interface IRiskCalculator
{
    decimal CalculateVaR(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m);
    decimal CalculateExpectedShortfall(IEnumerable<decimal> returns, decimal confidenceLevel = 0.95m);
    decimal CalculateMaxDrawdown(IEnumerable<decimal> portfolioValues);
    decimal CalculateSharpeRatio(IEnumerable<decimal> returns, decimal riskFreeRate);
    decimal CalculatePositionSize(decimal accountBalance, decimal riskPerTrade, decimal stopLoss);
    RiskMetrics CalculatePortfolioRisk(IEnumerable<Position> positions);
}

public interface IPositionMonitor
{
    Task<IEnumerable<Position>> GetAllPositionsAsync();
    Task<Position?> GetPositionAsync(string symbol);
    Task UpdatePositionAsync(Position position);
    Task<decimal> GetTotalExposureAsync();
    Task<decimal> GetSymbolExposureAsync(string symbol);
    Task<IEnumerable<Position>> GetPositionsExceedingLimitsAsync();
}

public interface IRiskAlertService
{
    Task<IEnumerable<RiskAlert>> GetActiveAlertsAsync();
    Task CreateAlertAsync(RiskAlert alert);
    Task ResolveAlertAsync(string alertId);
    Task<bool> CheckDrawdownLimitAsync(decimal currentDrawdown);
    Task<bool> CheckPositionLimitAsync(string symbol, decimal quantity);
    Task<bool> CheckDailyLossLimitAsync(decimal currentLoss);
}

public interface IComplianceMonitor
{
    Task<TradingResult<ComplianceStatus>> GetComplianceStatusAsync();
    Task<TradingResult<bool>> ValidatePatternDayTradingAsync(string accountId);
    Task<TradingResult<bool>> ValidateMarginRequirementsAsync(string accountId, decimal orderValue);
    Task<TradingResult<bool>> ValidateRegulatoryLimitsAsync(OrderRiskRequest request);
    Task<TradingResult<bool>> LogComplianceEventAsync(ComplianceEvent complianceEvent);
    Task<TradingResult<ComplianceMetrics>> GetMetricsAsync();
}
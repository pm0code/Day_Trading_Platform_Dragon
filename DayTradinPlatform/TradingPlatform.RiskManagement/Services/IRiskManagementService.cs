using TradingPlatform.RiskManagement.Models;

namespace TradingPlatform.RiskManagement.Services;

public interface IRiskManagementService
{
    Task<RiskStatus> GetRiskStatusAsync();
    Task<RiskLimits> GetRiskLimitsAsync();
    Task UpdateRiskLimitsAsync(RiskLimits limits);
    Task<bool> ValidateOrderAsync(OrderRiskRequest request);
    Task<decimal> CalculatePositionRiskAsync(string symbol, decimal quantity, decimal price);
    Task<RiskMetrics> GetRiskMetricsAsync();
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
    Task<ComplianceStatus> GetComplianceStatusAsync();
    Task<bool> ValidatePatternDayTradingAsync(string accountId);
    Task<bool> ValidateMarginRequirementsAsync(string accountId, decimal orderValue);
    Task<bool> ValidateRegulatoryLimitsAsync(OrderRiskRequest request);
    Task LogComplianceEventAsync(ComplianceEvent complianceEvent);
}
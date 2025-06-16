namespace TradingPlatform.RiskManagement.Models;

public record RiskStatus(
    decimal CurrentDrawdown,
    decimal MaxDrawdown,
    decimal DailyPnL,
    decimal TotalExposure,
    decimal AvailableCapital,
    RiskLevel RiskLevel,
    DateTime LastUpdated
);

public record RiskLimits(
    decimal MaxDailyLoss,
    decimal MaxDrawdown,
    decimal MaxPositionSize,
    decimal MaxTotalExposure,
    decimal MaxSymbolConcentration,
    int MaxPositions,
    bool EnableStopLoss
);

public record RiskMetrics(
    decimal VaR95,
    decimal VaR99,
    decimal ExpectedShortfall,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal Beta,
    decimal PortfolioVolatility,
    DateTime CalculatedAt
);

public record Position(
    string Symbol,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal MarketValue,
    decimal RiskExposure,
    DateTime OpenTime,
    DateTime LastUpdated
);

public record OrderRiskRequest(
    string Symbol,
    decimal Quantity,
    decimal Price,
    OrderType OrderType,
    string AccountId,
    decimal? StopLoss = null,
    decimal? TakeProfit = null
);

public record RiskAlert(
    string Id,
    RiskAlertType Type,
    string Symbol,
    string Message,
    RiskLevel Severity,
    DateTime CreatedAt,
    DateTime? ResolvedAt = null,
    bool IsResolved = false
);

public record ComplianceStatus(
    bool IsCompliant,
    IEnumerable<ComplianceViolation> Violations,
    PatternDayTradingStatus PDTStatus,
    MarginStatus MarginStatus,
    DateTime LastChecked
);

public record ComplianceViolation(
    string RuleId,
    string Description,
    string Symbol,
    decimal Amount,
    ViolationSeverity Severity,
    DateTime OccurredAt
);

public record PatternDayTradingStatus(
    bool IsPatternDayTrader,
    int DayTradesUsed,
    int DayTradesRemaining,
    decimal MinimumEquity,
    DateTime PeriodStart
);

public record MarginStatus(
    decimal MaintenanceMargin,
    decimal InitialMargin,
    decimal ExcessLiquidity,
    decimal BuyingPower,
    bool HasMarginCall
);

public record ComplianceEvent(
    string EventId,
    string EventType,
    string Description,
    string AccountId,
    string? Symbol,
    decimal? Amount,
    DateTime Timestamp,
    Dictionary<string, object> Metadata
);

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RiskAlertType
{
    DrawdownLimit,
    PositionLimit,
    DailyLossLimit,
    ConcentrationLimit,
    MarginCall,
    ComplianceViolation,
    SystemRisk
}

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit
}

public enum ViolationSeverity
{
    Warning,
    Minor,
    Major,
    Critical
}
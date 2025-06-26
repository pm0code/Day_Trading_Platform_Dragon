using Microsoft.CodeAnalysis;
using TradingPlatform.CodeAnalysis.Framework;

namespace TradingPlatform.CodeAnalysis.Diagnostics
{
    /// <summary>
    /// Central repository for all diagnostic descriptors used by TradingPlatform analyzers.
    /// Organized by category for comprehensive code quality enforcement.
    /// </summary>
    public static class DiagnosticDescriptors
    {
        #region Financial Precision Rules (TP0001-TP0099)

        public static readonly DiagnosticDescriptor UseDecimalForMoney = CreateRule(
            DiagnosticIds.UseDecimalForMoney,
            "Use decimal for monetary values",
            "'{0}' uses {1} type for monetary value; use {2} instead to prevent precision loss",
            AnalyzerCategories.FinancialPrecision,
            DiagnosticSeverity.Error,
            "All monetary values must use System.Decimal to ensure financial precision. Using double or float can lead to rounding errors in financial calculations.");

        public static readonly DiagnosticDescriptor AvoidPrecisionLoss = CreateRule(
            DiagnosticIds.AvoidPrecisionLoss,
            "Avoid precision loss in financial calculations",
            "'{0}' operation may cause precision loss when used with financial values",
            AnalyzerCategories.FinancialPrecision,
            DiagnosticSeverity.Warning,
            "Financial calculations should avoid operations that return double/float types. Use decimal-specific math operations or custom implementations.");

        public static readonly DiagnosticDescriptor ValidateFinancialCalculation = CreateRule(
            DiagnosticIds.ValidateFinancialCalculation,
            "Validate financial calculation accuracy",
            "Financial calculation '{0}' should be validated for accuracy and overflow conditions",
            AnalyzerCategories.FinancialPrecision,
            DiagnosticSeverity.Info);

        #endregion

        #region Canonical Pattern Rules (TP0100-TP0199)

        public static readonly DiagnosticDescriptor UseCanonicalBase = CreateRule(
            DiagnosticIds.UseCanonicalBase,
            "Extend canonical base class",
            "Service class '{0}' should extend {1} to follow canonical patterns",
            AnalyzerCategories.CanonicalPatterns,
            DiagnosticSeverity.Warning,
            "All service classes must extend appropriate canonical base classes (CanonicalServiceBase, CanonicalRepositoryBase, etc.) to ensure consistent lifecycle management and logging.");

        public static readonly DiagnosticDescriptor UseTradingResult = CreateRule(
            DiagnosticIds.UseTradingResult,
            "Use TradingResult for operation returns",
            "Method '{0}' returns {1}; use TradingResult<T> for consistent error handling",
            AnalyzerCategories.CanonicalPatterns,
            DiagnosticSeverity.Warning,
            "All operation methods should return TradingResult<T> to provide consistent error handling and success/failure patterns.");

        public static readonly DiagnosticDescriptor ImplementLifecycle = CreateRule(
            DiagnosticIds.ImplementLifecycle,
            "Implement lifecycle methods",
            "Service '{0}' must implement OnInitializeAsync, OnStartAsync, and OnStopAsync",
            AnalyzerCategories.CanonicalPatterns,
            DiagnosticSeverity.Error,
            "Canonical services must implement lifecycle methods for proper initialization and cleanup.");

        public static readonly DiagnosticDescriptor ImplementHealthCheck = CreateRule(
            DiagnosticIds.ImplementHealthCheck,
            "Implement health check",
            "Service '{0}' interacts with external resources and must implement health checks",
            AnalyzerCategories.CanonicalPatterns,
            DiagnosticSeverity.Warning);

        public static readonly DiagnosticDescriptor UseCanonicalLogging = CreateRule(
            DiagnosticIds.UseCanonicalLogging,
            "Use canonical logging pattern",
            "{0} Use inherited logging methods from canonical base class",
            AnalyzerCategories.CanonicalPatterns,
            DiagnosticSeverity.Warning);

        #endregion

        #region Performance Rules (TP0200-TP0299)

        public static readonly DiagnosticDescriptor AvoidBoxing = CreateRule(
            DiagnosticIds.AvoidBoxing,
            "Avoid boxing in performance-critical path",
            "Boxing operation detected in performance-critical code: {0}",
            AnalyzerCategories.Performance,
            DiagnosticSeverity.Warning);

        public static readonly DiagnosticDescriptor UseObjectPooling = CreateRule(
            DiagnosticIds.UseObjectPooling,
            "Use object pooling for frequently allocated objects",
            "Type '{0}' is frequently allocated; consider using object pooling",
            AnalyzerCategories.Performance,
            DiagnosticSeverity.Info);

        public static readonly DiagnosticDescriptor AvoidAllocation = CreateRule(
            DiagnosticIds.AvoidAllocation,
            "Avoid allocations in hot path",
            "Allocation detected in hot path: {0}. Consider using stack allocation or pooling",
            AnalyzerCategories.Performance,
            DiagnosticSeverity.Warning);

        public static readonly DiagnosticDescriptor UseSpan = CreateRule(
            DiagnosticIds.UseSpan,
            "Use Span<T> for memory efficiency",
            "Consider using Span<T> or Memory<T> instead of {0} for better performance",
            AnalyzerCategories.Performance,
            DiagnosticSeverity.Info);

        #endregion

        #region Security Rules (TP0300-TP0399)

        public static readonly DiagnosticDescriptor NoHardcodedSecrets = CreateRule(
            DiagnosticIds.NoHardcodedSecrets,
            "No hardcoded secrets",
            "Potential hardcoded secret detected: {0}",
            AnalyzerCategories.Security,
            DiagnosticSeverity.Error,
            "Never hardcode secrets, API keys, or passwords. Use secure configuration or key vault services.");

        public static readonly DiagnosticDescriptor UseSafeSQL = CreateRule(
            DiagnosticIds.UseSafeSQL,
            "Use parameterized SQL queries",
            "SQL injection vulnerability: use parameterized queries instead of string concatenation",
            AnalyzerCategories.Security,
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor ProtectPII = CreateRule(
            DiagnosticIds.ProtectPII,
            "Protect personally identifiable information",
            "PII field '{0}' must be encrypted or protected",
            AnalyzerCategories.Security,
            DiagnosticSeverity.Warning);

        #endregion

        #region Architecture Rules (TP0400-TP0499)

        public static readonly DiagnosticDescriptor LayerViolation = CreateRule(
            DiagnosticIds.LayerViolation,
            "Architectural layer violation",
            "Layer '{0}' should not reference '{1}'",
            AnalyzerCategories.Architecture,
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor CircularDependency = CreateRule(
            DiagnosticIds.CircularDependency,
            "Circular dependency detected",
            "Circular dependency between '{0}' and '{1}'",
            AnalyzerCategories.Architecture,
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor ModuleIsolation = CreateRule(
            DiagnosticIds.ModuleIsolation,
            "Module isolation violation",
            "Module '{0}' exposes internal type '{1}' in public API",
            AnalyzerCategories.Architecture,
            DiagnosticSeverity.Warning);

        #endregion

        #region Error Handling Rules (TP0500-TP0599)

        public static readonly DiagnosticDescriptor NoSilentFailure = CreateRule(
            DiagnosticIds.NoSilentFailure,
            "No silent failures allowed",
            "{0} - all errors must be logged or returned as TradingResult",
            AnalyzerCategories.ErrorHandling,
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor UseCanonicalLoggingPattern = CreateRule(
            DiagnosticIds.UseCanonicalLogging,
            "Use canonical logging pattern",
            "Use structured logging with proper context: {0}",
            AnalyzerCategories.ErrorHandling,
            DiagnosticSeverity.Warning);

        public static readonly DiagnosticDescriptor ImplementRetry = CreateRule(
            DiagnosticIds.ImplementRetry,
            "Implement retry logic",
            "External service call '{0}' should implement retry logic with exponential backoff",
            AnalyzerCategories.ErrorHandling,
            DiagnosticSeverity.Info);

        #endregion

        #region Helper Methods

        private static DiagnosticDescriptor CreateRule(
            string id,
            string title,
            string messageFormat,
            string category,
            DiagnosticSeverity severity = DiagnosticSeverity.Warning,
            string description = null)
        {
            return TradingPlatformAnalyzerBase.CreateRule(
                id, title, messageFormat, category, severity, description);
        }

        #endregion
    }
}
// TradingPlatform.Core.Instrumentation.MethodInstrumentationInterceptor
// Runtime method interception for comprehensive automated logging
// High-performance interception with minimal overhead for trading operations
// Integrates with Enhanced TradingLogOrchestrator for complete observability

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core.Instrumentation;

/// <summary>
/// High-performance method interception for automated logging instrumentation
/// Provides method entry/exit logging, performance tracking, and exception handling
/// Designed for ultra-low latency trading operations with configurable overhead
/// </summary>
public static class MethodInstrumentationInterceptor
{
    private static readonly TradingLogOrchestrator _logger = TradingLogOrchestrator.Instance;
    private static readonly Dictionary<string, MethodInstrumentationInfo> _methodCache = new();
    private static readonly object _cacheLock = new object();

    /// <summary>
    /// Intercept method entry with comprehensive context logging
    /// </summary>
    /// <param name="memberName">Method name (automatically injected)</param>
    /// <param name="sourceFilePath">Source file path (automatically injected)</param>
    /// <param name="sourceLineNumber">Source line number (automatically injected)</param>
    /// <param name="parameters">Method parameters for logging</param>
    /// <returns>Execution context for method exit tracking</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodExecutionContext EnterMethod(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object?[] parameters)
    {
        var methodInfo = GetOrCreateMethodInfo(memberName, sourceFilePath, sourceLineNumber);
        
        // Skip instrumentation if configured to suppress
        if (methodInfo.Attribute?.SuppressLogging == true || 
            !ShouldInstrument(methodInfo))
        {
            return new MethodExecutionContext { ShouldLog = false };
        }

        var context = new MethodExecutionContext
        {
            MethodName = memberName,
            SourceFilePath = sourceFilePath,
            SourceLineNumber = sourceLineNumber,
            StartTimestamp = GetHighPrecisionTimestamp(),
            CorrelationId = Guid.NewGuid().ToString("N"),
            ShouldLog = true,
            InstrumentationInfo = methodInfo
        };

        // Log method entry with parameters if configured
        if (methodInfo.Attribute?.LogParameters == true)
        {
            LogMethodEntry(context, parameters);
        }
        else if (methodInfo.Attribute?.Level >= InstrumentationLevel.Standard)
        {
            LogMethodEntry(context, null);
        }

        return context;
    }

    /// <summary>
    /// Intercept method exit with performance tracking and result logging
    /// </summary>
    /// <param name="context">Execution context from method entry</param>
    /// <param name="returnValue">Method return value (if configured to log)</param>
    /// <param name="exception">Exception if method failed</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExitMethod(MethodExecutionContext context, object? returnValue = null, Exception? exception = null)
    {
        if (!context.ShouldLog) return;

        var endTimestamp = GetHighPrecisionTimestamp();
        var executionMicroseconds = (endTimestamp - context.StartTimestamp) / 10; // Convert ticks to microseconds

        // Check for performance violations
        var methodInfo = context.InstrumentationInfo;
        var isPerformanceViolation = executionMicroseconds > methodInfo.Attribute?.ExpectedMaxExecutionMicroseconds;

        // Log method exit with performance data
        LogMethodExit(context, executionMicroseconds, returnValue, exception, isPerformanceViolation);

        // Log trading-specific compliance data
        if (methodInfo.Attribute?.IsTradingCritical == true)
        {
            LogTradingCompliance(context, executionMicroseconds, exception);
        }

        // Track performance metrics for analytics
        if (methodInfo.Attribute?.TrackPerformance == true)
        {
            UpdatePerformanceMetrics(context, executionMicroseconds);
        }
    }

    /// <summary>
    /// Convenience method for wrapping method execution with instrumentation
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="method">Method to execute</param>
    /// <param name="memberName">Method name (automatically injected)</param>
    /// <param name="sourceFilePath">Source file path (automatically injected)</param>
    /// <param name="sourceLineNumber">Source line number (automatically injected)</param>
    /// <param name="parameters">Method parameters</param>
    /// <returns>Method result</returns>
    public static T InstrumentMethod<T>(
        Func<T> method,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object?[] parameters)
    {
        var context = EnterMethod(memberName, sourceFilePath, sourceLineNumber, parameters);
        T result = default(T)!;
        Exception? exception = null;

        try
        {
            result = method();
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            ExitMethod(context, typeof(T) == typeof(void) ? null : (object?)result, exception);
        }

        return result;
    }

    /// <summary>
    /// Async method instrumentation wrapper
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="method">Async method to execute</param>
    /// <param name="memberName">Method name (automatically injected)</param>
    /// <param name="sourceFilePath">Source file path (automatically injected)</param>
    /// <param name="sourceLineNumber">Source line number (automatically injected)</param>
    /// <param name="parameters">Method parameters</param>
    /// <returns>Method result</returns>
    public static async Task<T> InstrumentMethodAsync<T>(
        Func<Task<T>> method,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object?[] parameters)
    {
        var context = EnterMethod(memberName, sourceFilePath, sourceLineNumber, parameters);
        T result = default(T)!;
        Exception? exception = null;

        try
        {
            result = await method();
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            ExitMethod(context, (object?)result, exception);
        }

        return result;
    }

    #region Private Implementation

    private static MethodInstrumentationInfo GetOrCreateMethodInfo(string memberName, string sourceFilePath, int sourceLineNumber)
    {
        var key = $"{sourceFilePath}:{memberName}:{sourceLineNumber}";
        
        if (_methodCache.TryGetValue(key, out var info))
            return info;

        lock (_cacheLock)
        {
            if (_methodCache.TryGetValue(key, out info))
                return info;

            // Try to find instrumentation attribute via reflection
            var methodInfo = TryFindMethodInfo(memberName, sourceFilePath);
            var instrumentationAttr = methodInfo?.GetCustomAttribute<MethodInstrumentationAttribute>();
            var tradingAttr = methodInfo?.GetCustomAttribute<TradingOperationAttribute>();
            var performanceAttr = methodInfo?.GetCustomAttribute<PerformanceCriticalAttribute>();
            var auditAttr = methodInfo?.GetCustomAttribute<AuditTrailAttribute>();

            // Use the most specific attribute
            var finalAttr = auditAttr ?? tradingAttr ?? performanceAttr ?? instrumentationAttr;

            info = new MethodInstrumentationInfo
            {
                MethodName = memberName,
                SourceFilePath = sourceFilePath,
                SourceLineNumber = sourceLineNumber,
                Attribute = finalAttr,
                MethodInfo = methodInfo
            };

            _methodCache[key] = info;
            return info;
        }
    }

    private static MethodInfo? TryFindMethodInfo(string memberName, string sourceFilePath)
    {
        try
        {
            // Extract namespace and class from file path
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            
            // Try to find the method via reflection
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.Name.Contains(fileName));
                foreach (var type in types)
                {
                    var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == memberName);
                    if (method != null)
                        return method;
                }
            }
        }
        catch
        {
            // Reflection failed - continue without attribute information
        }

        return null;
    }

    private static bool ShouldInstrument(MethodInstrumentationInfo methodInfo)
    {
        var config = TradingLogOrchestrator.Instance.GetConfiguration();
        
        // Check global instrumentation level
        if (methodInfo.Attribute?.Level == InstrumentationLevel.None)
            return false;

        // Check configuration scope
        return config.Scope switch
        {
            LoggingScope.Critical => methodInfo.Attribute?.IsTradingCritical == true || 
                                   methodInfo.Attribute?.Level >= InstrumentationLevel.Comprehensive,
            LoggingScope.ProjectSpecific => IsEnabledProject(methodInfo.SourceFilePath),
            LoggingScope.All => true,
            _ => false
        };
    }

    private static bool IsEnabledProject(string sourceFilePath)
    {
        var config = TradingLogOrchestrator.Instance.GetConfiguration();
        return config.EnabledProjects.Any(p => sourceFilePath.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static void LogMethodEntry(MethodExecutionContext context, object?[]? parameters)
    {
        var tradingContext = CreateTradingContext(context);
        var performanceContext = CreatePerformanceContext(context, 0);

        _logger.LogMethodEntry(
            $"Entering {context.MethodName}",
            context.MethodName,
            context.SourceFilePath,
            context.SourceLineNumber,
            tradingContext,
            performanceContext,
            parameters
        );
    }

    private static void LogMethodExit(MethodExecutionContext context, long executionMicroseconds, 
        object? returnValue, Exception? exception, bool isPerformanceViolation)
    {
        var tradingContext = CreateTradingContext(context);
        var performanceContext = CreatePerformanceContext(context, executionMicroseconds);

        if (exception != null)
        {
            _logger.LogError(
                $"Method {context.MethodName} failed after {executionMicroseconds}μs",
                exception,
                context.MethodName,
                "Method execution failed",
                "Check method implementation and input parameters",
                new { 
                    ExecutionTime = $"{executionMicroseconds}μs",
                    CorrelationId = context.CorrelationId,
                    ReturnValue = returnValue
                },
                context.SourceFilePath,
                context.SourceLineNumber,
                tradingContext,
                performanceContext
            );
        }
        else
        {
            var level = isPerformanceViolation ? LogLevel.Warning : LogLevel.Debug;
            var message = isPerformanceViolation 
                ? $"Method {context.MethodName} exceeded performance threshold: {executionMicroseconds}μs"
                : $"Exiting {context.MethodName} after {executionMicroseconds}μs";

            _logger.LogMethodExit(
                message,
                context.MethodName,
                context.SourceFilePath,
                context.SourceLineNumber,
                tradingContext,
                performanceContext,
                returnValue,
                level
            );
        }
    }

    private static void LogTradingCompliance(MethodExecutionContext context, long executionMicroseconds, Exception? exception)
    {
        var attr = context.InstrumentationInfo.Attribute as TradingOperationAttribute;
        if (attr == null) return;

        _logger.LogTrade(
            $"Trading operation: {context.MethodName}",
            attr.Category.ToString(),
            exception?.Message ?? "Success",
            null, // symbol
            null, // quantity
            null, // price
            new
            {
                OperationCategory = attr.Category,
                AffectsPositions = attr.AffectsPositions,
                InvolvesRisk = attr.InvolvesRisk,
                RequiresCompliance = attr.RequiresComplianceReporting,
                BusinessImpact = attr.BusinessImpact,
                ExecutionTime = $"{executionMicroseconds}μs",
                CorrelationId = context.CorrelationId,
                Status = exception == null ? "Success" : "Failed"
            },
            context.SourceFilePath,
            context.SourceLineNumber
        );
    }

    private static void UpdatePerformanceMetrics(MethodExecutionContext context, long executionMicroseconds)
    {
        // Update performance analytics for trending
        // This would integrate with the Performance Monitor from Phase 1
    }

    private static TradingContext? CreateTradingContext(MethodExecutionContext context)
    {
        var attr = context.InstrumentationInfo.Attribute;
        if (attr?.IsTradingCritical != true) return null;

        return new TradingContext
        {
            Action = context.MethodName,
            OrderId = context.CorrelationId
        };
    }

    private static PerformanceContext CreatePerformanceContext(MethodExecutionContext context, long executionMicroseconds)
    {
        return new PerformanceContext
        {
            DurationNanoseconds = executionMicroseconds * 1000, // Convert microseconds to nanoseconds
            DurationMilliseconds = executionMicroseconds / 1000.0, // Convert microseconds to milliseconds
            Operation = context.MethodName
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetHighPrecisionTimestamp()
    {
        return Stopwatch.GetTimestamp();
    }

    #endregion
}

/// <summary>
/// Method execution context for tracking instrumentation state
/// </summary>
public class MethodExecutionContext
{
    public string MethodName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public int SourceLineNumber { get; set; }
    public long StartTimestamp { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public bool ShouldLog { get; set; }
    public MethodInstrumentationInfo InstrumentationInfo { get; set; } = new();
}

/// <summary>
/// Cached method instrumentation information
/// </summary>
public class MethodInstrumentationInfo
{
    public string MethodName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public int SourceLineNumber { get; set; }
    public MethodInstrumentationAttribute? Attribute { get; set; }
    public MethodInfo? MethodInfo { get; set; }
}
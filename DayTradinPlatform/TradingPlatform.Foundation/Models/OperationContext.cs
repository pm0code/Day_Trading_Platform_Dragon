namespace TradingPlatform.Foundation.Models;

/// <summary>
/// Context information for trading operations providing correlation, timing, and metadata.
/// Essential for distributed tracing, performance monitoring, and audit compliance.
/// </summary>
public class OperationContext
{
    /// <summary>
    /// Unique identifier for correlating this operation across distributed systems.
    /// Should be propagated through all related operations and logged consistently.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// The trading operation being performed (e.g., "OrderSubmission", "MarketDataFetch").
    /// Used for categorizing metrics and logs.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// When the operation was initiated.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// User or service account initiating the operation.
    /// Required for audit trails and authorization checks.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Session identifier for grouping related operations.
    /// Useful for tracking user sessions and debugging.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Trading account identifier for financial operations.
    /// Critical for position tracking and compliance monitoring.
    /// </summary>
    public string? AccountId { get; set; }

    /// <summary>
    /// Trading symbol if operation is symbol-specific.
    /// Used for symbol-level monitoring and performance analysis.
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// Strategy name if operation is part of a trading strategy.
    /// Enables strategy-level performance tracking and attribution.
    /// </summary>
    public string? StrategyName { get; set; }

    /// <summary>
    /// Order identifier for order-related operations.
    /// Essential for order lifecycle tracking and compliance.
    /// </summary>
    public string? OrderId { get; set; }

    /// <summary>
    /// Maximum allowed duration for this operation.
    /// Used for timeout enforcement and SLA monitoring.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Priority level for the operation affecting processing order.
    /// </summary>
    public OperationPriority Priority { get; set; } = OperationPriority.Normal;

    /// <summary>
    /// Additional metadata specific to the operation.
    /// Allows extensibility without breaking contracts.
    /// </summary>
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Tags for categorizing and filtering operations.
    /// Useful for metrics aggregation and monitoring dashboards.
    /// </summary>
    public HashSet<string> Tags { get; }

    /// <summary>
    /// Parent operation context for hierarchical operations.
    /// Enables building operation call trees for complex workflows.
    /// </summary>
    public OperationContext? Parent { get; }

    /// <summary>
    /// Cancellation token for cooperative cancellation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    public OperationContext(
        string operationName,
        string? correlationId = null,
        OperationContext? parent = null,
        CancellationToken cancellationToken = default)
    {
        OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N");
        StartTime = DateTime.UtcNow;
        Parent = parent;
        CancellationToken = cancellationToken;
        Metadata = new Dictionary<string, object>();
        Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Inherit common properties from parent
        if (parent != null)
        {
            UserId = parent.UserId;
            SessionId = parent.SessionId;
            AccountId = parent.AccountId;

            // Copy relevant tags from parent
            foreach (var tag in parent.Tags.Where(t => t.StartsWith("inherit:", StringComparison.OrdinalIgnoreCase)))
            {
                Tags.Add(tag);
            }
        }
    }

    /// <summary>
    /// Creates a child operation context inheriting properties from this context.
    /// </summary>
    /// <param name="childOperationName">Name of the child operation</param>
    /// <param name="cancellationToken">Cancellation token for the child operation</param>
    /// <returns>New operation context as a child of this context</returns>
    public OperationContext CreateChild(string childOperationName, CancellationToken cancellationToken = default)
    {
        return new OperationContext(childOperationName, CorrelationId, this, cancellationToken);
    }

    /// <summary>
    /// Calculates the duration since the operation started.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Checks if the operation has exceeded its timeout.
    /// </summary>
    public bool IsTimedOut => Timeout.HasValue && Duration > Timeout.Value;

    /// <summary>
    /// Adds a tag to the operation for categorization.
    /// </summary>
    /// <param name="tag">Tag to add</param>
    /// <returns>This context for fluent chaining</returns>
    public OperationContext WithTag(string tag)
    {
        Tags.Add(tag);
        return this;
    }

    /// <summary>
    /// Adds multiple tags to the operation.
    /// </summary>
    /// <param name="tags">Tags to add</param>
    /// <returns>This context for fluent chaining</returns>
    public OperationContext WithTags(params string[] tags)
    {
        foreach (var tag in tags)
        {
            Tags.Add(tag);
        }
        return this;
    }

    /// <summary>
    /// Adds metadata to the operation context.
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value</param>
    /// <returns>This context for fluent chaining</returns>
    public OperationContext WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the user ID for this operation.
    /// </summary>
    public OperationContext WithUser(string userId)
    {
        UserId = userId;
        return this;
    }

    /// <summary>
    /// Sets the account ID for this operation.
    /// </summary>
    public OperationContext WithAccount(string accountId)
    {
        AccountId = accountId;
        return this;
    }

    /// <summary>
    /// Sets the trading symbol for this operation.
    /// </summary>
    public OperationContext WithSymbol(string symbol)
    {
        Symbol = symbol;
        return this;
    }

    /// <summary>
    /// Sets the strategy name for this operation.
    /// </summary>
    public OperationContext WithStrategy(string strategyName)
    {
        StrategyName = strategyName;
        return this;
    }

    /// <summary>
    /// Sets the order ID for this operation.
    /// </summary>
    public OperationContext WithOrder(string orderId)
    {
        OrderId = orderId;
        return this;
    }

    /// <summary>
    /// Sets the timeout for this operation.
    /// </summary>
    public OperationContext WithTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the priority for this operation.
    /// </summary>
    public OperationContext WithPriority(OperationPriority priority)
    {
        Priority = priority;
        return this;
    }

    /// <summary>
    /// Gets a breadcrumb trail of operation names from root to this operation.
    /// Useful for understanding the call hierarchy in complex workflows.
    /// </summary>
    public string GetOperationPath()
    {
        var path = new List<string>();
        var current = this;

        while (current != null)
        {
            path.Insert(0, current.OperationName);
            current = current.Parent;
        }

        return string.Join(" > ", path);
    }

    /// <summary>
    /// Creates a dictionary suitable for structured logging.
    /// Contains all relevant context information in a flat structure.
    /// </summary>
    public Dictionary<string, object> ToLogContext()
    {
        var context = new Dictionary<string, object>
        {
            ["CorrelationId"] = CorrelationId,
            ["OperationName"] = OperationName,
            ["StartTime"] = StartTime,
            ["Duration"] = Duration.TotalMilliseconds,
            ["Priority"] = Priority.ToString()
        };

        if (!string.IsNullOrEmpty(UserId))
            context["UserId"] = UserId;

        if (!string.IsNullOrEmpty(SessionId))
            context["SessionId"] = SessionId;

        if (!string.IsNullOrEmpty(AccountId))
            context["AccountId"] = AccountId;

        if (!string.IsNullOrEmpty(Symbol))
            context["Symbol"] = Symbol;

        if (!string.IsNullOrEmpty(StrategyName))
            context["Strategy"] = StrategyName;

        if (!string.IsNullOrEmpty(OrderId))
            context["OrderId"] = OrderId;

        if (Tags.Any())
            context["Tags"] = string.Join(",", Tags);

        if (Parent != null)
            context["ParentOperation"] = Parent.OperationName;

        // Add custom metadata
        foreach (var kvp in Metadata)
        {
            context[$"Meta.{kvp.Key}"] = kvp.Value;
        }

        return context;
    }

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"Operation: {OperationName}",
            $"CorrelationId: {CorrelationId}",
            $"Duration: {Duration.TotalMilliseconds:F1}ms"
        };

        if (!string.IsNullOrEmpty(Symbol))
            parts.Add($"Symbol: {Symbol}");

        if (!string.IsNullOrEmpty(OrderId))
            parts.Add($"OrderId: {OrderId}");

        if (Priority != OperationPriority.Normal)
            parts.Add($"Priority: {Priority}");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Priority levels for trading operations affecting processing order.
/// </summary>
public enum OperationPriority
{
    /// <summary>
    /// Low priority operations that can be delayed.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority for standard operations.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority for time-sensitive operations.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority for emergency operations.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Factory for creating operation contexts with common configurations.
/// Provides convenience methods for typical trading scenarios.
/// </summary>
public static class OperationContextFactory
{
    /// <summary>
    /// Creates a context for market data operations.
    /// </summary>
    public static OperationContext MarketData(string symbol, string operation = "MarketDataFetch")
    {
        return new OperationContext(operation)
            .WithSymbol(symbol)
            .WithTag("market-data")
            .WithTimeout(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Creates a context for order management operations.
    /// </summary>
    public static OperationContext OrderOperation(string orderId, string operation, string? accountId = null)
    {
        var context = new OperationContext(operation)
            .WithOrder(orderId)
            .WithTag("order-management")
            .WithPriority(OperationPriority.High)
            .WithTimeout(TimeSpan.FromSeconds(10));

        if (!string.IsNullOrEmpty(accountId))
            context.WithAccount(accountId);

        return context;
    }

    /// <summary>
    /// Creates a context for strategy execution operations.
    /// </summary>
    public static OperationContext Strategy(string strategyName, string operation = "StrategyExecution")
    {
        return new OperationContext(operation)
            .WithStrategy(strategyName)
            .WithTag("strategy")
            .WithTimeout(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a context for risk management operations.
    /// </summary>
    public static OperationContext RiskCheck(string operation = "RiskCheck")
    {
        return new OperationContext(operation)
            .WithTag("risk-management")
            .WithPriority(OperationPriority.High)
            .WithTimeout(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Creates a context for compliance operations.
    /// </summary>
    public static OperationContext Compliance(string operation = "ComplianceCheck")
    {
        return new OperationContext(operation)
            .WithTag("compliance")
            .WithPriority(OperationPriority.Critical)
            .WithTimeout(TimeSpan.FromSeconds(5));
    }
}
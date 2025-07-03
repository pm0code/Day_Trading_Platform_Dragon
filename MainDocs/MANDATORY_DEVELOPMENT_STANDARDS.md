# 🚨 MANDATORY DEVELOPMENT STANDARDS AND PATTERNS 🚨

**THIS DOCUMENT SUPERSEDES ALL OTHER GUIDANCE AND MUST BE FOLLOWED WITHOUT EXCEPTION**

Last Updated: 2025-01-29

## 🔴 CRITICAL: READ THIS FIRST

This document consolidates ALL mandatory development standards for the DayTradingPlatform project. Every developer, including AI assistants, MUST read and follow these standards. Violations will result in code rejection.

## Table of Contents

1. [Core Development Principles](#core-development-principles)
2. [Research-First Mandate](#research-first-mandate)
3. [Canonical Service Implementation](#canonical-service-implementation)
4. [Method Logging Requirements](#method-logging-requirements)
5. [Financial Precision Standards](#financial-precision-standards)
6. [Error Handling Standards](#error-handling-standards)
7. [Testing Requirements](#testing-requirements)
8. [Architecture Standards](#architecture-standards)
9. [Performance Requirements](#performance-requirements)
10. [Security Standards](#security-standards)
11. [Documentation Requirements](#documentation-requirements)
12. [Code Analysis and Quality](#code-analysis-and-quality)
13. [Standard Tools and Libraries](#standard-tools-and-libraries)
14. [Development Workflow](#development-workflow)
15. [Progress Reporting](#progress-reporting)

---

## 1. Core Development Principles

### 1.1 Zero Custom Implementation Policy
- **NEVER** create custom implementations when industry standards exist
- **ALWAYS** use canonical service implementations
- **NEVER** duplicate functionality that exists in canonical services
- **ALWAYS** check for existing implementations before creating new ones

### 1.2 Research-First Development
- **MANDATORY**: 2-4 hours minimum research before building anything
- **MANDATORY**: Create research reports documenting all findings
- **MANDATORY**: Read COMPLETE documentation - no guessing allowed
- **MANDATORY**: Get approval before implementation

### 1.3 Dead Code Removal
- **MANDATORY**: Remove all dead code after successful migrations
- **NEVER** leave commented-out code in production
- **ALWAYS** use version control for code history

---

## 2. Research-First Mandate

### 2.1 Pre-Implementation Research Requirements

```markdown
BEFORE WRITING ANY CODE:
1. ✅ Research existing solutions (2-4 hours minimum)
2. ✅ Document findings in a research report
3. ✅ Identify industry-standard patterns
4. ✅ Get approval for approach
5. ✅ Only then begin implementation
```

### 2.2 Research Report Template

```markdown
# Research Report: [Feature/Component Name]
Date: [YYYY-MM-DD]
Researcher: [Name/AI]

## Executive Summary
[Brief overview of findings]

## Research Conducted
- [ ] Industry standards reviewed
- [ ] Existing patterns analyzed
- [ ] Similar implementations studied
- [ ] Performance implications considered
- [ ] Security implications reviewed

## Findings
1. **Standard Solutions Found:**
   - [List all relevant standards]
   
2. **Recommended Approach:**
   - [Detailed recommendation]
   
3. **Alternatives Considered:**
   - [List alternatives and why rejected]

## Approval
- [ ] Approach approved by: [Name]
- [ ] Date: [YYYY-MM-DD]
```

---

## 3. Canonical Service Implementation

### 3.1 Mandatory Canonical Services

ALL services MUST extend the appropriate canonical base class:

```csharp
// ❌ WRONG - Direct interface implementation
public class MyService : IMyService 
{
    // This violates standards
}

// ✅ CORRECT - Extends canonical base
public class MyService : CanonicalServiceBase, IMyService
{
    public MyService(ITradingLogger logger) 
        : base(logger, "MyService")
    {
        // Constructor implementation
    }
}
```

### 3.2 Available Canonical Base Classes

- `CanonicalServiceBase` - For general services
- `CanonicalExecutor<TRequest, TResult>` - For execution services
- `CanonicalStrategyBase` - For trading strategies
- `CanonicalDataAccessBase` - For data access layers
- `CanonicalRepositoryBase<T>` - For repositories
- `CanonicalValidatorBase<T>` - For validators

### 3.3 Canonical Service Features

All canonical services provide:
- ✅ Automatic method entry/exit logging
- ✅ Health checks and metrics
- ✅ Proper lifecycle management (Initialize, Start, Stop)
- ✅ TradingResult<T> pattern implementation
- ✅ Comprehensive error handling
- ✅ Performance tracking

---

## 4. Method Logging Requirements

### 4.1 MANDATORY: Every Method Must Log Entry and Exit

```csharp
public async Task<TradingResult<Order>> ProcessOrderAsync(OrderRequest request)
{
    LogMethodEntry(); // MANDATORY
    try
    {
        // Method implementation
        
        LogMethodExit(); // MANDATORY
        return TradingResult<Order>.Success(order);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process order");
        LogMethodExit(); // MANDATORY even in error cases
        return TradingResult<Order>.Failure("Processing failed", "PROCESS_ERROR");
    }
}
```

### 4.2 Constructor and Property Logging

```csharp
public class TradingService : CanonicalServiceBase
{
    public TradingService(IConfiguration config, ITradingLogger logger) 
        : base(logger, "TradingService")
    {
        // Base class handles constructor logging
        _config = config;
    }
    
    private string _status = "Idle";
    public string Status 
    { 
        get 
        { 
            LogPropertyGet(); // Log property access
            return _status; 
        }
        set 
        { 
            LogPropertySet(value); // Log property changes
            _status = value; 
        }
    }
}
```

---

## 5. Financial Precision Standards

### 5.1 CRITICAL: System.Decimal for ALL Financial Values

```csharp
// ❌ NEVER use float or double for money
public double CalculatePrice(double quantity, double unitPrice) // WRONG!

// ✅ ALWAYS use decimal for financial calculations
public decimal CalculatePrice(decimal quantity, decimal unitPrice) // CORRECT!
{
    return quantity * unitPrice;
}
```

### 5.2 Precision Requirements

- **Prices**: 8 decimal places minimum
- **Quantities**: 8 decimal places minimum
- **Percentages**: Store as decimal (0.05m for 5%)
- **Currency**: Always include currency code

### 5.3 Financial Calculation Helpers

Use canonical helpers for complex calculations:

```csharp
// Use DecimalMathCanonical for mathematical operations
var sqrt = DecimalMathCanonical.Sqrt(value);
var power = DecimalMathCanonical.Pow(base, exponent);

// Use TradingMathCanonical for trading calculations
var returns = TradingMathCanonical.CalculateReturns(prices);
var sharpe = TradingMathCanonical.CalculateSharpeRatio(returns, riskFreeRate);
```

---

## 6. Error Handling Standards

### 6.1 No Silent Failures Policy

```csharp
// ❌ WRONG - Silent failure
try
{
    ProcessData();
}
catch
{
    // Swallowing exception - NEVER DO THIS
}

// ✅ CORRECT - Comprehensive error handling
try
{
    ProcessData();
}
catch (ValidationException vex)
{
    _logger.LogWarning(vex, "Validation failed for data processing");
    return TradingResult.Failure("Validation failed", "VALIDATION_ERROR");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error in data processing");
    return TradingResult.Failure($"Processing failed: {ex.Message}", "PROCESS_ERROR");
}
```

### 6.2 TradingResult<T> Pattern

ALL operations MUST return TradingResult<T>:

```csharp
public async Task<TradingResult<Portfolio>> GetPortfolioAsync(string userId)
{
    LogMethodEntry();
    
    try
    {
        // Validation
        if (string.IsNullOrEmpty(userId))
        {
            LogMethodExit();
            return TradingResult<Portfolio>.Failure(
                "User ID is required", 
                "INVALID_USER_ID");
        }
        
        // Operation
        var portfolio = await _repository.GetPortfolioAsync(userId);
        
        if (portfolio == null)
        {
            LogMethodExit();
            return TradingResult<Portfolio>.Failure(
                "Portfolio not found", 
                "PORTFOLIO_NOT_FOUND");
        }
        
        LogMethodExit();
        return TradingResult<Portfolio>.Success(portfolio);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get portfolio for user {UserId}", userId);
        LogMethodExit();
        return TradingResult<Portfolio>.Failure(
            $"Failed to retrieve portfolio: {ex.Message}", 
            "RETRIEVAL_ERROR");
    }
}
```

---

## 7. Testing Requirements

### 7.1 Minimum Coverage Requirements

- **Unit Tests**: 80% minimum coverage (90% target)
- **Integration Tests**: All service interactions
- **E2E Tests**: Critical user workflows
- **Performance Tests**: All high-frequency operations
- **Security Tests**: All authentication/authorization flows

### 7.2 Test Structure

```csharp
public class OrderServiceTests
{
    [Fact]
    public async Task ProcessOrder_ValidOrder_ShouldSucceed()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithSymbol("AAPL")
            .WithQuantity(100m)
            .WithPrice(150.50m)
            .Build();
            
        // Act
        var result = await _service.ProcessOrderAsync(order);
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Submitted, result.Value.Status);
    }
    
    [Theory]
    [InlineData(null, "SYMBOL_REQUIRED")]
    [InlineData("", "SYMBOL_REQUIRED")]
    [InlineData("INVALID#SYMBOL", "INVALID_SYMBOL")]
    public async Task ProcessOrder_InvalidSymbol_ShouldFail(
        string symbol, string expectedError)
    {
        // Test implementation
    }
}
```

### 7.3 Financial Calculation Tests

```csharp
[Fact]
public void CalculatePrice_ShouldMaintainPrecision()
{
    // Arrange
    decimal quantity = 123.45678901m;
    decimal price = 987.65432109m;
    decimal expected = 121946.67890083899589m;
    
    // Act
    decimal result = TradingMathCanonical.CalculateValue(quantity, price);
    
    // Assert
    Assert.Equal(expected, result);
    Assert.Equal(17, GetDecimalPlaces(result)); // Verify precision maintained
}
```

---

## 8. Architecture Standards

### 8.1 Hexagonal Architecture (Mandatory)

```
UI Layer (WinUI 3 / Blazor)
    ↓
Application Layer (Use Cases)
    ↓
Domain Layer (Business Logic)
    ↓
Infrastructure Layer (Data Access, External Services)
```

### 8.2 Project Structure

```
TradingPlatform/
├── TradingPlatform.Core/           # Domain models, interfaces
│   ├── Canonical/                  # Canonical implementations
│   ├── Models/                     # Domain models
│   └── Interfaces/                 # Core interfaces
├── TradingPlatform.Application/    # Use cases, orchestration
├── TradingPlatform.Infrastructure/ # Data access, external services
├── TradingPlatform.API/           # REST API
├── TradingPlatform.UI/            # User interface
└── TradingPlatform.Tests/         # All tests
```

### 8.3 Dependency Rules

- ✅ UI → Application → Domain → Infrastructure
- ❌ Domain → Application (NEVER)
- ❌ Domain → Infrastructure (NEVER)
- ❌ Domain → UI (NEVER)

---

## 9. Performance Requirements

### 9.1 Latency Targets

- **Order Execution**: < 50ms (target < 10ms)
- **Market Data Updates**: < 10ms
- **UI Responsiveness**: < 100ms
- **API Response Time**: < 200ms (95th percentile)

### 9.2 Optimization Techniques

```csharp
// Object pooling for high-frequency allocations
public class OrderPool
{
    private readonly ObjectPool<Order> _pool;
    
    public Order Rent() => _pool.Get();
    public void Return(Order order) => _pool.Return(order);
}

// Async/await for I/O operations
public async Task<IEnumerable<Trade>> GetTradesAsync()
{
    // Parallel execution for independent operations
    var tasks = symbols.Select(s => GetTradesBySymbolAsync(s));
    var results = await Task.WhenAll(tasks);
    return results.SelectMany(r => r);
}

// Memory-efficient data structures
public readonly struct PriceLevel
{
    public readonly decimal Price;
    public readonly decimal Quantity;
    public readonly int OrderCount;
}
```

---

## 10. Security Standards

### 10.1 Authentication & Authorization

- OAuth 2.0 / OpenID Connect for authentication
- Role-based access control (RBAC)
- API key rotation every 90 days
- Multi-factor authentication (MFA) required

### 10.2 Data Protection

```csharp
// Use SecureConfiguration for sensitive data
public class ApiService : CanonicalServiceBase
{
    private readonly ISecureConfiguration _secureConfig;
    
    public async Task<string> GetApiKeyAsync()
    {
        // Never hardcode secrets
        return _secureConfig.GetValue("ExternalApi:ApiKey");
    }
}
```

### 10.3 Security Scanning

- **Static Analysis**: Run on every commit
- **Dynamic Analysis**: Run before deployment
- **Dependency Scanning**: Daily vulnerability checks
- **Penetration Testing**: Quarterly

---

## 11. Documentation Requirements

### 11.1 Code Documentation

```csharp
/// <summary>
/// Processes a market order for immediate execution.
/// </summary>
/// <param name="request">The order request containing symbol, quantity, and side</param>
/// <returns>A TradingResult containing the executed order or error details</returns>
/// <exception cref="ValidationException">Thrown when order validation fails</exception>
/// <remarks>
/// This method implements smart order routing to find the best execution venue.
/// Large orders are automatically split to minimize market impact.
/// </remarks>
public async Task<TradingResult<Order>> ProcessMarketOrderAsync(OrderRequest request)
{
    // Implementation
}
```

### 11.2 Project Documentation

Required documentation files:
- `README.md` - Project overview and setup
- `ARCHITECTURE.md` - System architecture
- `API.md` - API documentation
- `DEPLOYMENT.md` - Deployment procedures
- `TROUBLESHOOTING.md` - Common issues
- `CHANGELOG.md` - Version history

---

## 12. Code Analysis and Quality

### 12.1 Roslyn Analyzers (Mandatory)

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" />
  <PackageReference Include="StyleCop.Analyzers" Version="1.2.0" />
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.0.0" />
  <PackageReference Include="TradingPlatform.Analyzers" Version="1.0.0" />
</ItemGroup>
```

### 12.2 Code Metrics Thresholds

- **Cyclomatic Complexity**: Max 10 per method
- **Lines of Code**: Max 50 per method
- **Class Coupling**: Max 10 dependencies
- **Maintainability Index**: Min 70

### 12.3 Zero Warning Policy

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>5</WarningLevel>
  <NoWarn></NoWarn> <!-- No suppressions allowed -->
</PropertyGroup>
```

---

## 13. Standard Tools and Libraries

### 13.1 Mandatory Tools by Category

#### Logging
- **Serilog** - Structured logging (NO alternatives)
- **Seq** - Log aggregation (NO alternatives)

#### Testing
- **xUnit** - Unit testing framework
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library

#### Serialization
- **System.Text.Json** - Primary (NO Newtonsoft except legacy)
- **Protobuf-net** - Binary serialization

#### HTTP/API
- **Refit** - Type-safe HTTP client
- **Polly** - Resilience and retry policies

#### Dependency Injection
- **Microsoft.Extensions.DependencyInjection** - ONLY

#### Database
- **Entity Framework Core** - ORM
- **Dapper** - Micro-ORM for performance-critical queries
- **TimescaleDB** - Time-series data

#### Messaging
- **Azure Service Bus** - Cloud messaging
- **RabbitMQ** - On-premises messaging

#### Caching
- **Microsoft.Extensions.Caching** - In-memory
- **Redis** - Distributed caching

### 13.2 Prohibited Libraries

- ❌ Newtonsoft.Json (except for legacy compatibility)
- ❌ log4net, NLog (use Serilog)
- ❌ Castle Windsor, Autofac (use MS DI)
- ❌ NUnit, MSTest (use xUnit)
- ❌ Any unlicensed or GPL libraries

---

## 14. Development Workflow

### 14.1 Pre-Development Checklist

```markdown
- [ ] Research completed (2-4 hours minimum)
- [ ] Research report created and approved
- [ ] Existing canonical services identified
- [ ] Architecture review completed
- [ ] Security implications reviewed
- [ ] Performance targets defined
- [ ] Test plan created
```

### 14.2 Development Process

1. **Research Phase** (Mandatory)
   - Research existing solutions
   - Document findings
   - Get approval

2. **Design Phase**
   - Create technical design
   - Review with team
   - Update documentation

3. **Implementation Phase**
   - Implement using canonical patterns
   - Follow all logging requirements
   - Maintain test coverage

4. **Review Phase**
   - Self-review against checklist
   - Peer review
   - Automated analysis

5. **Deployment Phase**
   - All tests passing
   - Zero warnings
   - Documentation updated

---

## 15. Progress Reporting

### 15.1 Long-Running Operations

```csharp
public async Task<TradingResult> ProcessLargeDatasetAsync(
    IEnumerable<Trade> trades,
    IProgress<ProcessingProgress> progress)
{
    LogMethodEntry();
    
    var totalCount = trades.Count();
    var processed = 0;
    
    foreach (var trade in trades)
    {
        // Process trade
        await ProcessTradeAsync(trade);
        
        processed++;
        
        // Report progress every 100 items or 1%
        if (processed % 100 == 0 || processed % (totalCount / 100) == 0)
        {
            var progressInfo = new ProcessingProgress
            {
                TotalItems = totalCount,
                ProcessedItems = processed,
                PercentComplete = (decimal)processed / totalCount * 100,
                EstimatedTimeRemaining = EstimateTimeRemaining(processed, totalCount),
                CurrentItem = trade.Id,
                Message = $"Processing trade {trade.Id}"
            };
            
            progress?.Report(progressInfo);
            
            _logger.LogInformation(
                "Processing progress: {Processed}/{Total} ({Percent:F1}%)",
                processed, totalCount, progressInfo.PercentComplete);
        }
    }
    
    LogMethodExit();
    return TradingResult.Success();
}
```

---

## 🚨 ENFORCEMENT

### Automated Enforcement

1. **Pre-commit hooks** validate standards compliance
2. **CI/CD pipeline** rejects non-compliant code
3. **Roslyn analyzers** enforce patterns in real-time
4. **Code reviews** must verify standard compliance

### Manual Enforcement

1. **Architecture reviews** - Weekly
2. **Code quality audits** - Monthly
3. **Performance reviews** - Quarterly
4. **Security audits** - Quarterly

### Violations

- **First violation**: Warning and education
- **Second violation**: Code rejection
- **Third violation**: Escalation to management

---

## 📚 Quick Reference Card

```csharp
// Every service MUST follow this pattern:
public class MyService : CanonicalServiceBase, IMyService
{
    public MyService(ITradingLogger logger) : base(logger, "MyService") { }
    
    public async Task<TradingResult<MyResult>> DoSomethingAsync(MyRequest request)
    {
        LogMethodEntry(); // MANDATORY
        
        try
        {
            // Validate
            if (!IsValid(request))
            {
                LogMethodExit();
                return TradingResult<MyResult>.Failure("Invalid request", "VALIDATION_ERROR");
            }
            
            // Process
            var result = await ProcessAsync(request);
            
            // Return
            LogMethodExit(); // MANDATORY
            return TradingResult<MyResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            LogMethodExit(); // MANDATORY
            return TradingResult<MyResult>.Failure($"Operation failed: {ex.Message}", "OPERATION_ERROR");
        }
    }
}
```

---

## 🔗 Related Documents

- [CLAUDE.md](../CLAUDE.md) - AI-specific guidance
- [ARCHITECTURE.md](../ARCHITECTURE.md) - System architecture
- [Financial Calculation Standards](../TradingPlatform.Core/Documentation/FinancialCalculationStandards.md)
- [Trading Golden Rules](The_Complete_Day_Trading_Reference_Guide_Golden_Rules.md)

---

**Remember: These standards are MANDATORY. No exceptions. No excuses.**

*Last reviewed: 2025-01-29*
*Next review: 2025-02-29*
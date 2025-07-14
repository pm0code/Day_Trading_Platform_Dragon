# 🚨 MANDATORY DEVELOPMENT STANDARDS AND PATTERNS 🚨

**THIS DOCUMENT SUPERSEDES ALL OTHER GUIDANCE AND MUST BE FOLLOWED WITHOUT EXCEPTION**

Last Updated: 2010-07-11
Version: 4.0

## 🔴 CRITICAL: READ THIS FIRST

This document consolidates ALL mandatory development standards for the DayTradingPlatform and MarketAnalyzer projects. Every developer, including AI assistants, MUST read and follow these standards. Violations will result in code rejection.

## 🚨 CRITICAL ADDITION: MANDATORY BOOKLET-FIRST DEVELOPMENT PROTOCOL

### 0.1 ABSOLUTE REQUIREMENT: AI Error Resolution System Integration

**MANDATORY**: NEVER fix any bug, error, or issue without first generating a research booklet through the AI Error Resolution System.

```bash
# MANDATORY WORKFLOW FOR ALL FIXES:
1. Capture error output to file
2. Run through BookletBuilder: 
   cd /mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/MarketAnalyzer/tools/MarketAnalyzer.BuildTools/src
   dotnet run -- error_output.txt
3. WAIT for AI team analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
4. Review generated booklet in /docs/error-booklets/[DATE]/
5. Understand root cause from research
6. ONLY THEN apply fix based on booklet guidance
```

**VIOLATIONS WILL RESULT IN**:
- Incomplete understanding of issues
- Superficial "quick fixes" that miss root causes
- Architectural drift and technical debt accumulation
- Violation of systematic engineering principles

**NO EXCEPTIONS** - Even for "simple" or "obvious" fixes!

### 0.2 Status Checkpoint Review (SCR) Protocol

**MANDATORY**: Implement fix counter system with checkpoint reviews every 10 fixes.

#### Fix Counter Protocol:
1. **Initialize**: Start each session with Fix Counter: [0/10]
2. **Track**: Increment counter with EVERY fix applied
3. **Report**: Show counter in EVERY response: 📊 Fix Counter: [X/10]
4. **Checkpoint**: At 10 fixes, perform mandatory Status Checkpoint Review (SCR)
5. **Reset**: After checkpoint, reset counter to [0/10]

#### Status Checkpoint Review Requirements:
**MANDATORY**: Use comprehensive 18-point SCR template located at:
`/MainDocs/After_Pivot/Status_Checkpoint_Review_Template_2010-01-09.md`

**Process**:
1. Copy template to DisasterRecovery folder with timestamp
2. Complete ALL 18 sections thoroughly
3. Document metrics and risk assessment
4. Make approval decision based on findings
5. List action items for next batch

**FAILURE TO RUN CHECKPOINTS = ARCHITECTURAL DRIFT = PROJECT FAILURE**

### 0.3 Gemini API Integration for Architectural Validation

**MANDATORY**: For any architectural C# issues, design patterns, or complex technical decisions:

```bash
# Gemini API Usage:
curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=AIzaSyDP7daxEmHxuSTA3ZObO4Rgkl2HswqpHcs" \
  -H 'Content-Type: application/json' \
  -X POST \
  -d '{"contents":[{"parts":[{"text":"Your architectural C# question here"}]}]}'
```

1. **Consult Gemini API** for expert architectural guidance
2. **Include Gemini's analysis** in decision-making process
3. **Cross-reference** booklet recommendations with Gemini insights
4. **Document** Gemini's architectural recommendations

## Table of Contents

0. [**CRITICAL**: Mandatory Booklet-First Development Protocol](#critical-mandatory-booklet-first-development-protocol)
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
16. [**HIGH PRIORITY** Additions: Observability & Distributed Tracing](#high-priority-additions-observability--distributed-tracing)
17. [**HIGH PRIORITY** Additions: Containerization & Orchestration](#high-priority-additions-containerization--orchestration)
18. [**HIGH PRIORITY** Additions: API Design Principles](#high-priority-additions-api-design-principles)
19. [**HIGH PRIORITY** Additions: Event-Driven Architecture (EDA) Principles](#high-priority-additions-event-driven-architecture-eda-principles)
20. [**MEDIUM PRIORITY** Additions: AI/ML Model Deployment & MLOps](#medium-priority-additions-aiml-model-deployment--mlops)
21. [**MEDIUM PRIORITY** Additions: Advanced Performance Tuning](#medium-priority-additions-advanced-performance-tuning)
22. [**MEDIUM PRIORITY** Additions: Third-Party Library Management & Supply Chain Security](#medium-priority-additions-third-party-library-management--supply-chain-security)
23. [**MANDATORY** Canonical Tool Development Requirements](#mandatory-canonical-tool-development-requirements)

-----

## 1\. Core Development Principles

### 1.1 Zero Custom Implementation Policy

  - **NEVER** create custom implementations when industry standards exist.
  - **ALWAYS** use canonical service implementations.
  - **NEVER** duplicate functionality that exists in canonical services.
  - **ALWAYS** check for existing implementations before creating new ones.

### 1.2 Research-First Development

  - **MANDATORY**: 2-4 hours minimum research before building anything.
  - **MANDATORY**: Create research reports documenting all findings.
  - **MANDATORY**: Read COMPLETE documentation - no guessing allowed.
  - **MANDATORY**: Get approval before implementation.

### 1.3 Dead Code Removal

  - **MANDATORY**: Remove all dead code after successful migrations.
  - **NEVER** leave commented-out code in production.
  - **ALWAYS** use version control for code history.

-----

## 2\. Research-First Mandate

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

-----

## 3\. Canonical Service Implementation

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
  - `CanonicalToolServiceBase` - For DevTools and analysis tools

### 3.3 Canonical Service Features

All canonical services provide:

  - ✅ Automatic method entry/exit logging
  - ✅ Health checks and metrics
  - ✅ Proper lifecycle management (Initialize, Start, Stop)
  - ✅ TradingResult\<T\> pattern implementation
  - ✅ Comprehensive error handling
  - ✅ Performance tracking
  - ✅ OpenTelemetry integration

-----

## 4\. Method Logging Requirements

### 4.1 MANDATORY: ITradingLogger Implementation

**CRITICAL**: ALL services MUST use `ITradingLogger` interface, not `ILogger<T>`.

```csharp
// ❌ WRONG - Using Microsoft ILogger
public class MyService
{
    private readonly ILogger<MyService> _logger; // VIOLATION!
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
}

// ✅ CORRECT - Using ITradingLogger
public class MyService : CanonicalServiceBase, IMyService
{
    public MyService(ITradingLogger logger) 
        : base(logger, "MyService")
    {
        // Base class provides ITradingLogger functionality
    }
}
```

### 4.2 MANDATORY: Every Method Must Log Entry and Exit

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
        LogError("Failed to process order", ex);
        LogMethodExit(); // MANDATORY even in error cases
        return TradingResult<Order>.Failure("Processing failed", "PROCESS_ERROR");
    }
}
```

### 4.3 Constructor and Property Logging

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

-----

## 5\. Financial Precision Standards

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

-----

## 6\. Error Handling Standards

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
    LogWarning("Validation failed for data processing", vex);
    return TradingResult.Failure("Validation failed", "VALIDATION_ERROR");
}
catch (Exception ex)
{
    LogError("Unexpected error in data processing", ex);
    return TradingResult.Failure($"Processing failed: {ex.Message}", "PROCESS_ERROR");
}
```

### 6.2 TradingResult\<T\> Pattern

ALL operations MUST return TradingResult\<T\>:

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
        LogError($"Failed to get portfolio for user {userId}", ex);
        LogMethodExit();
        return TradingResult<Portfolio>.Failure(
            $"Failed to retrieve portfolio: {ex.Message}", 
            "RETRIEVAL_ERROR");
    }
}
```

-----

## 7\. Testing Requirements

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

-----

## 8\. Architecture Standards

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

-----

## 9\. Performance Requirements

### 9.1 Latency Targets

  - **Order Execution**: \< 50ms (target \< 10ms)
  - **Market Data Updates**: \< 10ms
  - **UI Responsiveness**: \< 100ms
  - **API Response Time**: \< 200ms (95th percentile)

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

-----

## 10\. Security Standards

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

-----

## 11\. Documentation Requirements

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

-----

## 12\. Code Analysis and Quality

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

**MANDATORY**: All code MUST compile with zero warnings.

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningLevel>5</WarningLevel>
  <NoWarn></NoWarn>
</PropertyGroup>
```

-----

## 12.4 Canonical Tool Development Requirements

**MANDATORY**: All MCP analyzer tools MUST follow the canonical tool development requirements documented at:

**[/home/nader/my_projects/CS/mcp-code-analyzer/src-v2/tools/CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md](/home/nader/my_projects/CS/mcp-code-analyzer/src-v2/tools/CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md)**

This document specifies:
- 20 mandatory format details for all tool outputs
- Canonical base class extension requirements
- Issue reporting standards
- Testing and documentation requirements

**CRITICAL**: Failure to follow these requirements will result in tool rejection.

-----

## 13\. Standard Tools and Libraries

### 13.1 Mandatory Tools by Category

#### Logging

  - **Serilog** - Structured logging (NO alternatives)
  - **Seq** - Log aggregation (NO alternatives)
  - **ITradingLogger** - Mandatory interface (NO ILogger<T>)

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
  - ❌ ILogger<T> (use ITradingLogger)
  - ❌ Any unlicensed or GPL libraries

-----

## 14\. Development Workflow

### 14.1 Pre-Development Checklist

```markdown
- [ ] Research completed (2-4 hours minimum)
- [ ] Research report created and approved
- [ ] Existing canonical services identified
- [ ] Architecture review completed
- [ ] Security implications reviewed
- [ ] Performance targets defined
- [ ] Test plan created
- [ ] AI Error Resolution System booklet generated
- [ ] Fix counter initialized [0/10]
```

### 14.2 Development Process

1.  **Research Phase** (Mandatory)

      * Research existing solutions
      * Document findings
      * Get approval

2.  **Booklet Generation Phase** (Mandatory)

      * Generate error booklet for any issues
      * Wait for AI team analysis
      * Review and understand recommendations

3.  **Design Phase**

      * Create technical design
      * Review with team
      * Update documentation

4.  **Implementation Phase**

      * Implement using canonical patterns
      * Follow all logging requirements
      * Maintain test coverage
      * Track fixes with counter

5.  **Review Phase**

      * Self-review against checklist
      * Peer review
      * Automated analysis
      * Status Checkpoint Review at 10 fixes

6.  **Deployment Phase**

      * All tests passing
      * Zero warnings
      * Documentation updated

-----

## 15\. Progress Reporting

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
            
            LogInfo($"Processing progress: {processed}/{totalCount} ({progressInfo.PercentComplete:F1}%)");
        }
    }
    
    LogMethodExit();
    return TradingResult.Success();
}
```

-----

## 16\. **HIGH PRIORITY** Additions: Observability & Distributed Tracing

### 16.1 Comprehensive Observability Mandate

**MANDATORY**: All services and applications MUST implement comprehensive observability, extending beyond basic logging to include metrics and distributed tracing. This is critical for understanding system behavior, performance profiling, and rapid incident response in distributed environments.

### 16.2 Distributed Tracing Implementation

```csharp
// MANDATORY: OpenTelemetry integration in all services
public class TradingService : CanonicalServiceBase
{
    private static readonly ActivitySource ActivitySource = new("TradingService");
    
    public async Task<TradingResult<Order>> ProcessOrderAsync(OrderRequest request)
    {
        using var activity = ActivitySource.StartActivity("ProcessOrder");
        activity?.SetTag("order.symbol", request.Symbol);
        activity?.SetTag("order.quantity", request.Quantity.ToString());
        
        LogMethodEntry();
        try
        {
            // Method implementation with tracing
            LogMethodExit();
            return TradingResult<Order>.Success(order);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogError("Failed to process order", ex);
            LogMethodExit();
            throw;
        }
    }
}
```

### 16.3 Metrics and Alerting

  - **MANDATORY**: Expose application and business metrics (e.g., request rates, error rates, latency, order fill rates, trading strategy performance).
  - Use Prometheus-compatible metrics exporters.
  - Define clear alerting thresholds for critical metrics.

### 16.4 Required Tools

  - **OpenTelemetry SDKs**: For tracing and metrics instrumentation.
  - **Prometheus**: For metrics collection and storage.
  - **Grafana**: For metrics visualization and dashboards.
  - **Jaeger / Zipkin**: For distributed trace visualization.

-----

## 17\. **HIGH PRIORITY** Additions: Containerization & Orchestration

### 17.1 Mandatory Containerization

  - **MANDATORY**: All applications and services MUST be containerized using Docker.
  - Dockerfiles MUST follow best practices:
      * Use multi-stage builds for minimal production images.
      * Utilize minimal base images (e.g., `alpine`, `distroless`).
      * Avoid running as root inside containers.
      * Copy only necessary files into the final image.

### 17.2 Orchestration with Kubernetes

  - **MANDATORY**: Kubernetes MUST be used for deploying, scaling, and managing all containerized workloads.
  - Kubernetes deployments MUST include:
      * **Resource Limits and Requests**: Defined for CPU and memory to prevent resource exhaustion.
      * **Liveness and Readiness Probes**: Configured for robust health checking and traffic management.
      * **Network Policies**: To control inter-pod communication for security.
      * **Pod Security Standards**: Adherence to defined security contexts.

### 17.3 Configuration Management

  - Sensitive configuration MUST be managed using Kubernetes Secrets or an external secret management system (e.g., HashiCorp Vault), not directly in Docker images or Kubernetes manifests.
  - Non-sensitive configuration SHOULD be managed using ConfigMaps.

-----

## 18\. **HIGH PRIORITY** Additions: API Design Principles

### 18.1 Consistent API Design

All APIs (REST, gRPC, internal, external) MUST adhere to a consistent set of design principles.

### 18.2 REST API Principles

  - **Versioning**: APIs MUST be versioned (e.g., `api/v1/resource`, header versioning for internal APIs).
  - **Idempotency**: Operations that modify state (POST, PUT, DELETE) SHOULD be designed to be idempotent where logical and safe. Clients MUST implement retry mechanisms for idempotent operations.
  - **Standard HTTP Methods**: Use appropriate HTTP methods (GET for retrieval, POST for creation, PUT for full updates, PATCH for partial updates, DELETE for deletion).
  - **Resource-Oriented**: APIs MUST be designed around resources (nouns) rather than actions (verbs).
  - **Consistent Error Responses**: API error responses MUST follow a standardized format, including a machine-readable error code, a human-readable message, and specific details where applicable. Use appropriate HTTP status codes (e.g., 400 for bad request, 401 for unauthorized, 403 for forbidden, 404 for not found, 500 for internal server error). This should align with the `TradingResult<T>` pattern.

### 18.3 Data Querying and Manipulation

  - **Paging**: Implement standardized paging for collection resources (e.g., `?page=1&size=20`, `?offset=0&limit=20`).
  - **Filtering and Sorting**: Provide consistent mechanisms for filtering (`?status=active`) and sorting (`?sort=-createdAt`).

### 18.4 Asynchronous Operations

  - For long-running operations, consider asynchronous API patterns (e.g., returning a 202 Accepted with a link to a status endpoint, or using webhooks for completion notification).

-----

## 19\. **HIGH PRIORITY** Additions: Event-Driven Architecture (EDA) Principles

### 19.1 Event-Driven Paradigm

Where applicable, services SHOULD leverage event-driven patterns to achieve high scalability, decoupling, and responsiveness.

### 19.2 Event Naming Conventions

  - Events MUST be named clearly, typically in the past tense, indicating what happened (e.g., `OrderPlacedEvent`, `TradeExecutedEvent`, `AccountUpdatedEvent`).

### 19.3 Event Schema Management

  - Define strict schemas for all events using a schema registry (e.g., Avro, Protobuf).
  - Implement forward and backward compatibility for schema evolution. Breaking changes to event schemas are strictly prohibited without a clear migration strategy and deprecation period.

### 19.4 Event Producers and Consumers

  - **Producer Responsibility**: Event producers are responsible for publishing valid events to the correct topics/queues.
  - **Consumer Idempotency**: Event consumers MUST be designed to be idempotent, meaning processing the same event multiple times will not lead to undesired side effects.
  - **Dead Letter Queues (DLQ)**: All event consumers MUST be configured with Dead Letter Queues for unprocessable messages.

### 19.5 Message Brokers

  - Continue to use **Azure Service Bus** (cloud) and **RabbitMQ** (on-premises) as the canonical message brokers.
  - Consider event streaming platforms like Kafka for high-throughput, real-time data streams where appropriate.

-----

## 20\. **MEDIUM PRIORITY** Additions: AI/ML Model Deployment & MLOps

### 20.1 MLOps Pipeline Automation

  - **MANDATORY**: Implement automated MLOps pipelines for the lifecycle of all AI/ML models, covering:
      * **Data Preparation**: Automated data ingestion and feature engineering.
      * **Model Training**: Automated retraining based on data drift or schedule.
      * **Model Evaluation**: Automated evaluation metrics and performance tracking.
      * **Model Versioning**: Storing and managing different versions of models in a model registry.
      * **Model Deployment**: Automated deployment to production environments (e.g., as microservices).
      * **Model Monitoring**: Continuous monitoring of model performance in production.

### 20.2 Model Deployment Patterns

  - Use deployment strategies such as A/B testing, canary releases, or blue/green deployments for new model versions to minimize risk.
  - Models SHOULD be exposed via REST APIs or gRPC for consistent access by trading services.

### 20.3 Model Monitoring and Explainability

  - Monitor models for data drift, concept drift, and performance degradation.
  - Prioritize explainable AI (XAI) techniques for critical trading models to provide transparency on predictions.

### 20.4 Tools

  - **MLflow**: For experiment tracking, model registry, and model lifecycle management.
  - **Kubeflow / Azure Machine Learning**: For orchestrating ML pipelines on Kubernetes.

-----

## 21\. **MEDIUM PRIORITY** Additions: Advanced Performance Tuning

### 21.1 Deeper Optimization Techniques

Beyond standard async/await and object pooling, implement advanced techniques for extreme performance.

### 21.2 Low-Latency Communication

  - For critical high-frequency components, explore and implement low-latency network protocols (e.g., raw UDP, IPC mechanisms like shared memory) if standard HTTP/gRPC introduces unacceptable overhead.
  - Use efficient serialization formats (e.g., Protobuf-net, FlatBuffers) over JSON for high-volume data exchange.

### 21.3 CPU Cache and Memory Alignment

  - Design data structures to be CPU cache-friendly (e.g., `readonly struct` where appropriate, sequential memory access).
  - Be mindful of false sharing in multi-threaded environments.

### 21.4 JIT Optimization Awareness

  - Understand and leverage C\# and .NET runtime optimizations (e.g., `AggressiveInlining`, `MethodImplOptions`).
  - Avoid patterns that might inhibit JIT optimizations (e.g., excessive virtual calls in hot paths).

### 21.5 Benchmarking and Profiling Methodologies

  - **MANDATORY**: Establish a standardized benchmarking methodology using tools like BenchmarkDotNet for critical code paths.
  - Regularly profile applications using tools like dotTrace, Visual Studio Profiler, or PerfView to identify performance bottlenecks.
  - Maintain performance baselines and track regressions as part of CI/CD.

-----

## 22\. **MEDIUM PRIORITY** Additions: Third-Party Library Management & Supply Chain Security

### 22.1 Formalized Library Vetting Process

  - **MANDATORY**: Any new third-party library or dependency (beyond the standard tools listed in Section 13) MUST undergo a formal vetting process before inclusion. This process includes:
      * License compatibility review (no GPL or restrictive licenses unless explicitly approved).
      * Security vulnerability assessment (beyond automated scans).
      * Performance implications.
      * Maintainer reputation and community support.
      * Long-term support and deprecation policy.

### 22.2 Software Bill of Materials (SBOM)

  - **MANDATORY**: Generate and maintain a Software Bill of Materials (SBOM) for all deployed artifacts. The SBOM MUST list all direct and transitive dependencies.

### 22.3 Private Package Feeds

  - Where applicable, use internal, secured package feeds (e.g., Azure Artifacts, NuGet Server) to host approved internal packages and cache external dependencies, providing an additional layer of control and resilience.

### 22.4 Automated Dependency Scanning Enforcement

  - Strengthen "Dependency Scanning: Daily vulnerability checks" by integrating these scans directly into the CI/CD pipeline, with policies to fail builds if critical vulnerabilities are detected.

-----

## 23. **MANDATORY** Canonical Tool Development Requirements

### 23.1 MCP Code Analyzer Tool Standards

All tools in the MCP Code Analyzer project MUST follow strict canonical development requirements to ensure consistency, quality, and comprehensive issue reporting.

### 23.2 Tool Development Requirements Document

**MANDATORY REFERENCE**: [/home/nader/my_projects/CS/mcp-code-analyzer/src-v2/tools/CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md](/home/nader/my_projects/CS/mcp-code-analyzer/src-v2/tools/CANONICAL_TOOL_DEVELOPMENT_REQUIREMENTS.md)

This document specifies:
- **20 Mandatory Format Details**: Every tool output must include ALL 20 specified fields
- **Canonical Base Class**: All tools MUST extend `CanonicalToolBase<TArgs>`
- **Issue Reporting**: Comprehensive `CanonicalIssue[]` format with full context
- **Progress Reporting**: Required for all operations >1 second
- **Telemetry Integration**: OpenTelemetry spans and metrics

### 23.3 Key Requirements Summary

1. **Tool Identification**: Name, version, timestamp
2. **Location Details**: File, line, column, class, method
3. **Code Context**: Snippets with highlighted lines
4. **AI Analysis**: Provider, model, confidence, explanation, solution
5. **Impact Assessment**: Business and technical impact
6. **Metrics**: Performance, security, complexity scores
7. **Remediation**: Step-by-step fix instructions
8. **Testing**: Guidance with assertions
9. **References**: Documentation and resources
10. **Dependencies**: Related issues and files

### 23.4 Enforcement

- **Build-time Validation**: TypeScript compilation enforces interface compliance
- **Runtime Validation**: Base class validates all required fields
- **Code Review**: Manual verification of all 20 format details
- **Automated Tests**: Unit tests verify canonical compliance

**CRITICAL**: Non-compliant tools will be rejected at code review. No exceptions.

-----

## 🚨 ENFORCEMENT

### Automated Enforcement

1.  **Pre-commit hooks** validate standards compliance.
2.  **CI/CD pipeline** rejects non-compliant code.
3.  **Roslyn analyzers** enforce patterns in real-time.
4.  **Code reviews** must verify standard compliance.
5.  **AI Error Resolution System** mandatory for all fixes.
6.  **Status Checkpoint Reviews** every 10 fixes.

### Manual Enforcement

1.  **Architecture reviews** - Weekly
2.  **Code quality audits** - Monthly
3.  **Performance reviews** - Quarterly
4.  **Security audits** - Quarterly
5.  **Booklet compliance reviews** - Per development cycle

### Violations

  - **First violation**: Warning and education
  - **Second violation**: Code rejection
  - **Third violation**: Escalation to management
  - **Booklet bypass**: Immediate code rejection

-----

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
            LogError("Operation failed", ex);
            LogMethodExit(); // MANDATORY
            return TradingResult<MyResult>.Failure($"Operation failed: {ex.Message}", "OPERATION_ERROR");
        }
    }
}
```

## 📋 Mandatory Development Checklist

```markdown
### Before ANY Code Changes:
- [ ] Fix counter initialized: [0/10]
- [ ] AI Error Resolution System ready
- [ ] Booklet generation process tested
- [ ] All canonical patterns reviewed
- [ ] ITradingLogger interface confirmed
- [ ] TradingResult<T> pattern understood
- [ ] Zero warning policy configured
- [ ] OpenTelemetry integration verified

### During Development:
- [ ] Generate booklet for EVERY error/issue
- [ ] Wait for AI team analysis (Mistral, DeepSeek, CodeGemma, Gemma2)
- [ ] Read and understand booklet recommendations
- [ ] Apply fixes ONLY based on booklet guidance
- [ ] Track fixes: 📊 Fix Counter: [X/10]
- [ ] Perform SCR at 10 fixes
- [ ] Reset counter after checkpoint

### Before Commit:
- [ ] Zero compilation errors
- [ ] Zero warnings (TreatWarningsAsErrors=true)
- [ ] All tests passing
- [ ] Code coverage >80%
- [ ] Canonical patterns validated
- [ ] Documentation updated
- [ ] Performance benchmarks verified
```

-----

## 🔗 Related Documents

  - [CLAUDE.md](../CLAUDE.md) - AI-specific guidance
  - [ARCHITECTURE.md](../ARCHITECTURE.md) - System architecture
  - [Financial Calculation Standards](../TradingPlatform.Core/Documentation/FinancialCalculationStandards.md)
  - [Trading Golden Rules](The_Complete_Day_Trading_Reference_Guide_Golden_Rules.md)
  - [AI Error Resolution System](../MarketAnalyzer/docs/AI_ERROR_RESOLUTION_WORKFLOW.md)
  - [Status Checkpoint Review Template](../MainDocs/After_Pivot/Status_Checkpoint_Review_Template_2010-01-09.md)

-----

**Remember: These standards are MANDATORY. No exceptions. No excuses. No fixes without booklets.**

*Last reviewed: 2010-07-11*
*Next review: 2010-08-11*
*Version: 4.0*
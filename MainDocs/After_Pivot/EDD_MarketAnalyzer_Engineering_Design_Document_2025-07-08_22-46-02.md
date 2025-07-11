# Engineering Design Document: MarketAnalyzer
## High-Performance Day Trading Analysis & Recommendation System

**Version**: 2.0  
**Date**: July 8, 2025  
**Created**: 2025-07-07 11:47:00 PDT  
**Last Modified**: 2025-07-08 22:46:02 PDT  
**Author**: Claude (Anthropic)  
**Status**: Updated with Architecture Validation  
**Repository**: https://github.com/pm0code/MarketAnalyzer

---

## 1. Executive Summary

MarketAnalyzer is a high-performance, single-user desktop application for Windows 11 x64 that provides real-time market analysis, technical indicators, AI-driven insights, and trading recommendations. Built entirely in C#/.NET 8/9, it leverages modern hardware capabilities including multi-core CPUs and dual GPUs for maximum performance.

### Key Design Principles
- **Performance First**: Sub-millisecond latency for critical operations
- **Canonical Architecture**: Consistent patterns across all components
- **Hardware Optimization**: Full utilization of i9-14900K (24 cores) and dual GPUs
- **Zero Trust**: No external dependencies for core functionality
- **Observable**: Comprehensive logging and metrics at every layer
- **Architecture Validation**: Automated enforcement of architectural constraints

### Critical Updates in v2.0
- Added mandatory architecture validation layer
- Introduced continuous compliance monitoring
- Enhanced type system constraints
- Implemented automated quality gates

---

## 2. System Architecture

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────┐  ┌─────────────────────────────────────────────┐
│          PRODUCTION SYSTEM                  │  │         DEVELOPMENT TOOLS                   │
│         (The Henhouse)                      │  │         (The Guardians)                     │
├─────────────────────────────────────────────┤  ├─────────────────────────────────────────────┤
│                                             │  │                                             │
│  ┌───────────────────────────────────────┐ │  │  ┌───────────────────────────────────────┐ │
│  │      Presentation Layer               │ │  │  │      Tool Presentation               │ │
│  │    MarketAnalyzer.Desktop             │ │  │  │    Console Applications              │ │
│  │  (WinUI 3, MVVM, Community Toolkit)   │ │  │  │    (Error Analysis, Reports)         │ │
│  └───────────────────────────────────────┘ │  │  └───────────────────────────────────────┘ │
│  ┌───────────────────────────────────────┐ │  │  ┌───────────────────────────────────────┐ │
│  │      Application Layer                │ │  │  │      Tool Application Layer          │ │
│  │   MarketAnalyzer.Application          │ │  │  │    BuildTools.Application            │ │
│  │  (Use Cases, Orchestration, DTOs)     │ │  │  │  (AI Orchestration, Analysis)        │ │
│  └───────────────────────────────────────┘ │  │  └───────────────────────────────────────┘ │
│  ┌───────────────────────────────────────┐ │  │  ┌───────────────────────────────────────┐ │
│  │        Domain Layer                   │ │  │  │      Tool Domain Layer               │ │
│  │     MarketAnalyzer.Domain             │ │  │  │    BuildTools.Domain                 │ │
│  │ (Entities, Value Objects, Services)   │ │  │  │  (Error Models, Booklets)            │ │
│  └───────────────────────────────────────┘ │  │  └───────────────────────────────────────┘ │
│  ┌───────────────────────────────────────┐ │  │  ┌───────────────────────────────────────┐ │
│  │     Infrastructure Layer              │ │  │  │    Tool Infrastructure               │ │
│  ├───────────────────────────────────────┤ │  │  ├───────────────────────────────────────┤ │
│  │ MarketData (Finnhub API)              │ │  │  │ AI Services (Ollama/Gemini)          │ │
│  │ TechnicalAnalysis                     │ │  │  │ Source Code Analysis                 │ │
│  │ AI/ML (ML.NET, ONNX)                  │ │  │  │ Build Output Parsing                 │ │
│  │ Storage (LiteDB)                      │ │  │  │ Report Generation                    │ │
│  │ Caching (Redis/Memory)                │ │  │  │ Architecture Validation              │ │
│  └───────────────────────────────────────┘ │  │  └───────────────────────────────────────┘ │
│  ┌───────────────────────────────────────┐ │  │  ┌───────────────────────────────────────┐ │
│  │      Foundation Layer                 │ │  │  │    DevTools Foundation               │ │
│  │   MarketAnalyzer.Foundation           │ │  │  │  MarketAnalyzer.DevTools.Foundation  │ │
│  │  • CanonicalServiceBase               │ │  │  │  • CanonicalToolServiceBase          │ │
│  │  • TradingResult<T>                   │ │  │  │  • ToolResult<T>                     │ │
│  │  • ITradingLogger                     │ │  │  │  • ILogger<T>                        │ │
│  └───────────────────────────────────────┘ │  │  └───────────────────────────────────────┘ │
│                                             │  │                                             │
│            MarketAnalyzer.sln               │  │         MarketAnalyzer.DevTools.sln         │
└─────────────────────────────────────────────┘  └─────────────────────────────────────────────┘
                     │                                              │
                     │                                              │
                     └──────────── NO REFERENCES ───────────────────┘
                                  File-based only
                           (Build outputs, logs, telemetry)

RELEASE BUILD: Only left side deployed     DEV BUILD: Both sides available
```

### 2.2 Project Structure

```
MarketAnalyzer/
├── src/
│   ├── Foundation/
│   │   ├── MarketAnalyzer.Foundation/          # Canonical patterns, base classes
│   │   └── MarketAnalyzer.Foundation.Tests/    
│   ├── Domain/
│   │   ├── MarketAnalyzer.Domain/              # Core business logic
│   │   └── MarketAnalyzer.Domain.Tests/        
│   ├── Infrastructure/
│   │   ├── MarketAnalyzer.Infrastructure.MarketData/
│   │   ├── MarketAnalyzer.Infrastructure.TechnicalAnalysis/
│   │   ├── MarketAnalyzer.Infrastructure.AI/
│   │   ├── MarketAnalyzer.Infrastructure.Storage/
│   │   └── MarketAnalyzer.Infrastructure.Caching/
│   ├── Application/
│   │   ├── MarketAnalyzer.Application/         # Use cases, orchestration
│   │   └── MarketAnalyzer.Application.Tests/   
│   └── Presentation/
│       ├── MarketAnalyzer.Desktop/             # WinUI 3 application
│       └── MarketAnalyzer.Desktop.Tests/       
├── tests/
│   ├── MarketAnalyzer.ArchitectureTests/      # Architecture validation (NEW)
│   ├── MarketAnalyzer.IntegrationTests/
│   ├── MarketAnalyzer.PerformanceTests/
│   └── MarketAnalyzer.E2ETests/
├── docs/
│   ├── ADR/                                    # Architecture Decision Records (NEW)
│   ├── Standards/                              # Coding standards and patterns
│   └── BugReports/                            # Historical issues and resolutions
├── scripts/
│   ├── validate-architecture.ps1               # Architecture validation (NEW)
│   ├── check-duplicate-types.ps1               # Type uniqueness check (NEW)
│   ├── verify-canonical-patterns.ps1           # Pattern compliance (NEW)
│   └── run-checkpoint.ps1                      # Checkpoint automation (NEW)
└── tools/
    └── analyzers/                              # Custom Roslyn analyzers (NEW)
```

### 2.3 Architectural Constraints & Validation (NEW)

#### Type Definition Rules

1. **Single Source of Truth Principle**
   - Each type exists in exactly ONE location within the codebase
   - No duplicate type names across different namespaces
   - Canonical types defined in Foundation layer are the authoritative version

2. **Layer Hierarchy & Dependencies**
   ```
   Presentation → Application → Domain → Foundation
                             ↘        ↗
                          Infrastructure
   ```
   - **Foundation**: No dependencies on other layers
   - **Domain**: Only depends on Foundation
   - **Infrastructure**: Depends on Domain and Foundation
   - **Application**: Depends on Domain, Foundation, and Infrastructure
   - **Presentation**: Only depends on Application

3. **Type Creation Rules**
   - **Foundation**: Base types, interfaces, common value objects
   - **Domain**: Business entities, domain-specific value objects, domain services
   - **Application**: DTOs, view models, use case requests/responses
   - **Infrastructure**: Implementation details, external API models
   - **No Cross-Layer Type Creation**: Layers cannot create types that belong in other layers

#### Automated Enforcement

1. **Pre-Commit Validation**
   ```powershell
   # .git/hooks/pre-commit
   ./scripts/validate-architecture.ps1
   ./scripts/check-duplicate-types.ps1
   ./scripts/verify-canonical-patterns.ps1
   ```

2. **Continuous Integration**
   ```yaml
   # .github/workflows/architecture-validation.yml
   - name: Run Architecture Tests
     run: dotnet test MarketAnalyzer.ArchitectureTests
   
   - name: Check Build Health
     run: ./scripts/check-build-health.ps1
   ```

3. **Architecture Test Examples**
   ```csharp
   [Test]
   public void Foundation_Should_Have_No_Dependencies()
   {
       var foundation = Types.InAssembly(typeof(CanonicalServiceBase).Assembly);
       var otherLayers = Types.InAssemblies(GetNonFoundationAssemblies());
       
       foundation.Should()
           .NotHaveDependencyOnAny(otherLayers.GetNames())
           .GetResult()
           .IsSuccessful.Should().BeTrue();
   }
   
   [Test]
   public void All_Services_Must_Inherit_CanonicalServiceBase()
   {
       Types.InAssemblies(GetAllAssemblies())
           .That().HaveNameEndingWith("Service")
           .Should().Inherit(typeof(CanonicalServiceBase))
           .GetResult()
           .IsSuccessful.Should().BeTrue();
   }
   ```

---

## 3. Core Components Design

### 3.1 Foundation Layer

#### Canonical Base Classes
```csharp
namespace MarketAnalyzer.Foundation.Canonical
{
    // Base class for all services - MANDATORY inheritance
    public abstract class CanonicalServiceBase : IDisposable
    {
        protected ITradingLogger Logger { get; }
        protected string ServiceName { get; }
        private readonly ConcurrentDictionary<string, Metric> _metrics;
        
        protected CanonicalServiceBase(ITradingLogger logger, string serviceName)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServiceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _metrics = new ConcurrentDictionary<string, Metric>();
            LogMethodEntry();
        }
        
        // MANDATORY: All derived classes must implement these
        protected abstract Task<TradingResult<bool>> OnInitializeAsync(CancellationToken ct);
        protected abstract Task<TradingResult<bool>> OnStartAsync(CancellationToken ct);
        protected abstract Task<TradingResult<bool>> OnStopAsync(CancellationToken ct);
        
        // MANDATORY: Must be called at entry/exit of EVERY method
        protected void LogMethodEntry([CallerMemberName] string methodName = "")
        {
            Logger.LogDebug($"[{ServiceName}] Entering {methodName}");
        }
        
        protected void LogMethodExit([CallerMemberName] string methodName = "")
        {
            Logger.LogDebug($"[{ServiceName}] Exiting {methodName}");
        }
        
        protected void UpdateMetric(string metricName, double value)
        {
            _metrics.AddOrUpdate(metricName, 
                new Metric(metricName, value), 
                (key, existing) => existing.Update(value));
        }
    }
    
    // Result pattern for all operations - NO EXCEPTIONS
    public class TradingResult<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public TradingError? Error { get; }
        public string TraceId { get; }
        
        private TradingResult(bool isSuccess, T? value, TradingError? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            TraceId = Guid.NewGuid().ToString();
        }
        
        public static TradingResult<T> Success(T value)
        {
            return new TradingResult<T>(true, value, null);
        }
        
        public static TradingResult<T> Failure(string errorCode, string message, Exception? ex = null)
        {
            var error = new TradingError(errorCode, message, ex);
            return new TradingResult<T>(false, default, error);
        }
    }
    
    // MANDATORY: All financial calculations use decimal
    public abstract class FinancialCalculationBase
    {
        // NEVER use float or double for money
        protected decimal CalculateValue(decimal price, decimal quantity)
        {
            return price * quantity;
        }
    }
}
```

### 3.2 Domain Layer

#### Core Entities
```csharp
namespace MarketAnalyzer.Domain.Entities
{
    // Domain entities use Foundation types
    public class Stock
    {
        public string Symbol { get; private set; }
        public string Exchange { get; private set; }
        public string Name { get; private set; }
        public MarketCap MarketCap { get; private set; }
        public Sector Sector { get; private set; }
        
        private Stock() { } // EF Core
        
        public static TradingResult<Stock> Create(
            string symbol, 
            string exchange, 
            string name)
        {
            // Validation logic
            if (string.IsNullOrWhiteSpace(symbol))
                return TradingResult<Stock>.Failure("INVALID_SYMBOL", "Symbol cannot be empty");
                
            var stock = new Stock
            {
                Symbol = symbol.ToUpperInvariant(),
                Exchange = exchange,
                Name = name
            };
            
            return TradingResult<Stock>.Success(stock);
        }
    }
    
    // Value Objects
    public sealed class MarketQuote : ValueObject
    {
        public string Symbol { get; }
        public decimal Price { get; }      // ALWAYS decimal for money
        public decimal Volume { get; }     // ALWAYS decimal for precision
        public DateTime Timestamp { get; }
        public long HardwareTimestamp { get; } // For HFT precision
        
        private MarketQuote(string symbol, decimal price, decimal volume, DateTime timestamp)
        {
            Symbol = symbol;
            Price = price;
            Volume = volume;
            Timestamp = timestamp;
            HardwareTimestamp = Stopwatch.GetTimestamp();
        }
        
        public static TradingResult<MarketQuote> Create(
            string symbol, 
            decimal price, 
            decimal volume)
        {
            if (price <= 0)
                return TradingResult<MarketQuote>.Failure("INVALID_PRICE", "Price must be positive");
                
            var quote = new MarketQuote(symbol, price, volume, DateTime.UtcNow);
            return TradingResult<MarketQuote>.Success(quote);
        }
        
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Symbol;
            yield return Price;
            yield return Volume;
            yield return Timestamp;
        }
    }
}
```

### 3.3 Infrastructure Layer

#### Market Data Implementation
```csharp
namespace MarketAnalyzer.Infrastructure.MarketData
{
    public class FinnhubMarketDataService : CanonicalServiceBase, IMarketDataService
    {
        private readonly HttpClient _httpClient;
        private readonly Channel<ApiRequest> _requestChannel;
        private readonly SemaphoreSlim _rateLimiter;
        
        public FinnhubMarketDataService(
            IHttpClientFactory httpClientFactory,
            ITradingLogger logger)
            : base(logger, nameof(FinnhubMarketDataService))
        {
            LogMethodEntry();
            
            _httpClient = httpClientFactory.CreateClient("Finnhub");
            _requestChannel = Channel.CreateUnbounded<ApiRequest>();
            _rateLimiter = new SemaphoreSlim(30, 30); // 30 req/sec burst
            
            LogMethodExit();
        }
        
        public async Task<TradingResult<MarketQuote>> GetQuoteAsync(
            string symbol, 
            CancellationToken cancellationToken = default)
        {
            LogMethodEntry();
            
            try
            {
                await _rateLimiter.WaitAsync(cancellationToken);
                
                var response = await _httpClient.GetAsync(
                    $"quote?symbol={symbol}", 
                    cancellationToken);
                    
                if (!response.IsSuccessStatusCode)
                {
                    LogError($"API error: {response.StatusCode}");
                    LogMethodExit();
                    return TradingResult<MarketQuote>.Failure(
                        "API_ERROR", 
                        $"Failed to get quote: {response.StatusCode}");
                }
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<FinnhubQuoteDto>(json);
                
                var quoteResult = MarketQuote.Create(symbol, data.CurrentPrice, data.Volume);
                
                LogMethodExit();
                return quoteResult;
            }
            catch (Exception ex)
            {
                LogError("Failed to get quote", ex);
                LogMethodExit();
                return TradingResult<MarketQuote>.Failure(
                    "QUOTE_FETCH_ERROR", 
                    "Failed to fetch quote", 
                    ex);
            }
            finally
            {
                _rateLimiter.Release();
            }
        }
        
        protected override async Task<TradingResult<bool>> OnInitializeAsync(CancellationToken ct)
        {
            LogMethodEntry();
            // Initialization logic
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        
        protected override async Task<TradingResult<bool>> OnStartAsync(CancellationToken ct)
        {
            LogMethodEntry();
            // Start processing requests
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
        
        protected override async Task<TradingResult<bool>> OnStopAsync(CancellationToken ct)
        {
            LogMethodEntry();
            // Cleanup
            LogMethodExit();
            return TradingResult<bool>.Success(true);
        }
    }
}
```

---

## 4. Quality Assurance & Validation

### 4.1 Build Standards

1. **Zero Tolerance Policy**
   - 0 Errors, 0 Warnings before ANY commit
   - Warnings as Errors enabled in all projects
   - Code analysis severity set to "Error" for violations

2. **Mandatory Checkpoints**
   - Run checkpoint script every 10 fixes
   - Cannot proceed without passing checkpoint
   - Automated tracking of fix progress

### 4.2 Architecture Tests

```csharp
namespace MarketAnalyzer.ArchitectureTests
{
    [TestFixture]
    public class LayerDependencyTests
    {
        [Test]
        public void Domain_Should_Not_Reference_Infrastructure()
        {
            var result = Types.InAssembly(typeof(Stock).Assembly)
                .Should()
                .NotHaveDependencyOn("MarketAnalyzer.Infrastructure")
                .GetResult();
                
            result.IsSuccessful.Should().BeTrue($"Violations: {string.Join(", ", result.FailingTypes)}");
        }
        
        [Test]
        public void No_Duplicate_Type_Names_Across_Assemblies()
        {
            var allTypes = Types.InAssemblies(GetAllAssemblies())
                .GetTypes()
                .GroupBy(t => t.Name)
                .Where(g => g.Count() > 1)
                .ToList();
                
            allTypes.Should().BeEmpty($"Duplicate types found: {string.Join(", ", allTypes.Select(g => g.Key))}");
        }
    }
    
    [TestFixture]
    public class CanonicalPatternTests
    {
        [Test]
        public void All_Methods_Must_Have_Entry_Exit_Logging()
        {
            var serviceTypes = Types.InAssemblies(GetAllAssemblies())
                .That().Inherit(typeof(CanonicalServiceBase))
                .GetTypes();
                
            foreach (var type in serviceTypes)
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.DeclaringType == type && !m.IsSpecialName);
                    
                foreach (var method in methods)
                {
                    var methodBody = method.GetMethodBody();
                    // Verify LogMethodEntry and LogMethodExit calls
                    // Implementation details...
                }
            }
        }
        
        [Test]
        public void All_Financial_Values_Must_Use_Decimal()
        {
            var violations = Types.InAssemblies(GetAllAssemblies())
                .That().HaveNameContaining("Price", "Amount", "Value", "Cost")
                .GetTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(float))
                .ToList();
                
            violations.Should().BeEmpty($"Found financial properties using float/double: {string.Join(", ", violations.Select(p => $"{p.DeclaringType.Name}.{p.Name}"))}");
        }
    }
}
```

### 4.3 Continuous Monitoring

1. **Real-time Build Status Dashboard**
   - Displays current error/warning count
   - Shows architecture test results
   - Tracks technical debt metrics

2. **Weekly Architecture Health Reports**
   - Dependency violations
   - Pattern compliance
   - Performance metrics
   - Code coverage trends

3. **Automated Alerts**
   - Slack notifications for build failures
   - Email alerts for architecture violations
   - Dashboard updates for stakeholders

---

## 5. Performance Requirements

### 5.1 Latency Targets
- API Response: <100ms (p99)
- Indicator Calculation: <50ms
- AI Inference: <200ms
- UI Render: 60fps consistent

### 5.2 Resource Utilization
- Memory: <2GB steady state
- CPU: <10% idle, <50% active trading
- GPU: >80% utilization during AI inference
- Network: <100MB/hour

### 5.3 Scalability
- Handle 1000+ simultaneous symbols
- Process 10,000+ quotes/second
- Calculate 100+ indicators in parallel
- Support 50+ AI models concurrently

---

## 6. Security & Compliance

### 6.1 Data Protection
- API keys encrypted at rest (DPAPI)
- TLS 1.3 for all external communication
- No sensitive data in logs
- Secure credential storage

### 6.2 Audit Trail
- All operations logged with correlation IDs
- User actions tracked
- Performance metrics recorded
- Error details captured

---

## 7. Deployment & Operations

### 7.1 Deployment Package
- Single MSI installer
- Automatic dependency resolution
- Configuration migration
- Rollback capability

### 7.2 Monitoring
- Application Insights integration
- Custom performance counters
- Health check endpoints
- Diagnostic data collection

### 7.3 Updates
- Automatic update checks
- Delta patching
- Rollback on failure
- Update scheduling

---

## 8. Architecture Decision Records (ADRs)

### ADR-001: Canonical Service Pattern
**Status**: Accepted  
**Context**: Need consistent service implementation  
**Decision**: All services inherit from CanonicalServiceBase  
**Consequences**: Uniform logging, metrics, lifecycle management  

### ADR-002: Financial Precision
**Status**: Accepted  
**Context**: Financial calculations require precision  
**Decision**: Always use decimal for monetary values  
**Consequences**: No floating-point errors, slightly higher memory usage  

### ADR-003: Architecture Validation
**Status**: Accepted  
**Context**: Prevent architectural drift and technical debt  
**Decision**: Automated architecture tests and continuous validation  
**Consequences**: Higher initial setup cost, long-term quality benefits  

---

## 9. Risk Mitigation

### 9.1 Technical Risks
1. **Architecture Violations**
   - Mitigation: Automated tests catch violations immediately
   - Monitoring: Continuous architecture validation
   
2. **Performance Degradation**
   - Mitigation: Continuous benchmarking
   - Monitoring: Real-time performance metrics

3. **Type System Conflicts**
   - Mitigation: Single source of truth enforcement
   - Monitoring: Duplicate type detection

### 9.2 Operational Risks
1. **Build Failures**
   - Mitigation: Pre-commit validation
   - Recovery: Automated rollback

2. **Integration Issues**
   - Mitigation: Contract testing
   - Recovery: Circuit breakers

---

## 10. Success Criteria

### 10.1 Technical Metrics
- Build Status: 0 errors/0 warnings continuously
- Architecture Tests: 100% passing
- Code Coverage: >90%
- Performance: All targets met
- Security: No critical vulnerabilities

### 10.2 Quality Metrics
- Technical Debt Ratio: <5%
- Code Duplication: <3%
- Cyclomatic Complexity: <10
- Documentation: 100% public APIs

### 10.3 Operational Metrics
- Availability: >99.9%
- MTTR: <30 minutes
- Deployment Success: >95%
- User Satisfaction: >90%

---

*This Engineering Design Document represents the comprehensive technical blueprint for MarketAnalyzer, incorporating lessons learned from previous build failures and establishing robust validation mechanisms to ensure long-term architectural integrity and code quality.*
# Trading Platform Test Suites

This document provides a comprehensive overview of all test suites in the Trading Platform, following canonical testing patterns.

## Test Projects Overview

### 1. TradingPlatform.UnitTests
Fast, isolated unit tests for individual components.

**Key Features:**
- Canonical test base classes (`CanonicalTestBase`, `CanonicalServiceTestBase`)
- Test builders for creating test data (MarketDataBuilder, OrderBuilder, PositionBuilder)
- Custom FluentAssertions extensions for TradingResult
- Comprehensive mocking infrastructure
- Test output integration with xUnit

**Coverage Areas:**
- Foundation models (TradingResult, TradingError)
- Core services and canonical base classes
- Financial calculations with decimal precision
- Service lifecycle management
- Error handling and edge cases

**Run Unit Tests:**
```bash
dotnet test TradingPlatform.UnitTests --logger "console;verbosity=detailed"
```

### 2. TradingPlatform.IntegrationTests
End-to-end integration tests with real dependencies using Testcontainers.

**Key Features:**
- Containerized Redis and PostgreSQL
- Shared test fixtures for resource management
- Async test patterns
- Real message queue testing
- Complete workflow validation

**Coverage Areas:**
- Redis message bus integration
- Golden Rules engine workflow
- Strategy execution pipeline
- End-to-end trading workflows
- Multi-component interactions

**Run Integration Tests:**
```bash
dotnet test TradingPlatform.IntegrationTests --filter "FullyQualifiedName~Integration"
```

### 3. TradingPlatform.PerformanceTests
Performance benchmarks and load tests for ultra-low latency validation.

**Key Features:**
- BenchmarkDotNet integration
- Ultra-low latency configuration (< 100μs targets)
- NBomber load testing
- Memory and threading diagnostics
- Concurrent operation testing

**Benchmark Suites:**
- TradingResult operations
- Order execution latency
- Golden Rules evaluation performance
- High-frequency order processing
- Message queue throughput

**Run Performance Tests:**
```bash
cd TradingPlatform.PerformanceTests
dotnet run -c Release
```

### 4. TradingPlatform.SecurityTests
Security-focused tests for vulnerability prevention.

**Key Features:**
- SQL injection testing
- XSS prevention validation
- Path traversal detection
- Sensitive data protection
- Input validation security
- Cryptographic operation testing

**Coverage Areas:**
- Order validation security
- API key protection
- Password strength validation
- Data sanitization
- Logging security (no sensitive data)
- Constant-time comparisons

**Run Security Tests:**
```bash
dotnet test TradingPlatform.SecurityTests --logger "console;verbosity=detailed"
```

## Canonical Test Framework

### Base Classes

1. **CanonicalTestBase**
   - Base for all tests
   - Mock logger setup
   - Service provider configuration
   - Test timeout management
   - Output helper integration

2. **CanonicalServiceTestBase<TService>**
   - Base for testing canonical services
   - Standard lifecycle tests
   - Health check validation
   - Performance metrics testing
   - Concurrency testing

3. **SecurityTestBase**
   - Base for security tests
   - Common attack pattern data
   - Security validation utilities
   - Cryptographic helpers

### Test Builders

Fluent builders for creating test data:
```csharp
var marketData = new MarketDataBuilder()
    .WithSymbol("AAPL")
    .WithUptrend()
    .WithHighVolume()
    .Build();

var order = new OrderBuilder()
    .WithLimitOrder(150m)
    .WithQuantity(100)
    .AsExecuted(150.05m, 100)
    .Build();
```

### Custom Assertions

FluentAssertions extensions for TradingResult:
```csharp
result.Should().BeSuccess();
result.Should().HaveValue("expected");
result.Should().HaveError("error message");
result.Should().HaveErrorCode("ERR001");
```

## Test Organization

```
Tests/
├── TradingPlatform.UnitTests/
│   ├── Framework/           # Base classes and utilities
│   ├── Builders/           # Test data builders
│   ├── Extensions/         # Custom assertions
│   ├── Foundation/         # Foundation project tests
│   ├── Core/              # Core project tests
│   └── [Project]/         # Tests for each project
│
├── TradingPlatform.IntegrationTests/
│   ├── Fixtures/          # Shared test fixtures
│   ├── Messaging/         # Message queue tests
│   ├── GoldenRules/       # Golden Rules integration
│   ├── Strategies/        # Strategy integration
│   └── EndToEnd/          # Complete workflows
│
├── TradingPlatform.PerformanceTests/
│   ├── Framework/         # Benchmark base classes
│   ├── Benchmarks/        # BenchmarkDotNet tests
│   └── LoadTests/         # NBomber load tests
│
└── TradingPlatform.SecurityTests/
    ├── Framework/         # Security test base
    ├── InputValidation/   # Input security tests
    ├── Authorization/     # Auth security tests
    ├── DataProtection/    # Data security tests
    └── ApiSecurity/       # API security tests
```

## Running All Tests

### Sequential Execution
```bash
# Unit Tests
dotnet test TradingPlatform.UnitTests

# Integration Tests (requires Docker)
dotnet test TradingPlatform.IntegrationTests

# Security Tests
dotnet test TradingPlatform.SecurityTests

# Performance Tests (interactive)
cd TradingPlatform.PerformanceTests && dotnet run -c Release
```

### Parallel Execution
```bash
# Run all non-performance tests in parallel
dotnet test DayTradingPlatform.sln --filter "FullyQualifiedName!~Performance" -p:ParallelizeTestCollections=true
```

### CI/CD Pipeline
```yaml
- name: Run Unit Tests
  run: dotnet test TradingPlatform.UnitTests --collect:"XPlat Code Coverage"

- name: Run Integration Tests
  run: dotnet test TradingPlatform.IntegrationTests

- name: Run Security Tests
  run: dotnet test TradingPlatform.SecurityTests

- name: Run Performance Benchmarks
  run: |
    cd TradingPlatform.PerformanceTests
    dotnet run -c Release -- --filter "*" --exporters json
```

## Code Coverage

Generate coverage reports:
```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Generate HTML report
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## Best Practices

1. **Test Naming**: Use descriptive names following pattern `MethodName_Scenario_ExpectedResult`
2. **Test Organization**: Group related tests in nested classes
3. **Test Data**: Use builders for complex objects, avoid magic values
4. **Assertions**: One logical assertion per test, use FluentAssertions
5. **Async Tests**: Always use async/await properly, avoid .Result or .Wait()
6. **Test Isolation**: Each test should be independent and repeatable
7. **Performance Tests**: Run in Release mode on dedicated hardware
8. **Security Tests**: Include both positive and negative test cases

## Troubleshooting

### Container Issues (Integration Tests)
```bash
# Check Docker status
docker ps

# Clean up containers
docker container prune

# Reset Docker
docker system prune -a
```

### Performance Test Issues
- Ensure no other applications are running
- Use Release configuration
- Close unnecessary applications
- Run on consistent hardware

### Test Discovery Issues
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Clear test cache
rm -rf TestResults/
```

## Additional Test Types

### Contract Tests (Future)
- API contract validation
- Service interface compatibility
- Message schema validation

### Chaos Tests (Future)
- Fault injection
- Network partition simulation
- Resource exhaustion testing

### Load Tests (Implemented)
- NBomber scenarios
- Throughput testing
- Latency under load
- Resource utilization

## Metrics and Reporting

- **Unit Test Coverage**: Target 80%+
- **Integration Test Coverage**: All critical paths
- **Performance Benchmarks**: Sub-millisecond for critical paths
- **Security Tests**: 100% pass rate required
- **Load Tests**: Meet latency SLAs under load

## Contributing

When adding new tests:
1. Follow existing patterns and base classes
2. Add to appropriate test project
3. Update this README if adding new test categories
4. Ensure CI/CD pipeline includes new tests
5. Maintain test quality and readability
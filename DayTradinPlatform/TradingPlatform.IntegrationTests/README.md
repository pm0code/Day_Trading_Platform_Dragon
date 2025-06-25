# TradingPlatform Integration Tests

This project contains comprehensive integration tests for the Trading Platform, including end-to-end workflow tests, component integration tests, and infrastructure validation.

## Overview

The integration tests use containerized dependencies (Redis, PostgreSQL) via Testcontainers to ensure consistent and isolated test environments.

## Test Categories

### 1. Messaging Integration Tests
- **RedisMessageBusIntegrationTests**: Tests for the Redis-based message queue implementation
  - Publish/Subscribe functionality
  - Multiple consumer groups
  - Priority-based routing
  - Retry mechanisms
  - Stream metrics

### 2. Golden Rules Engine Tests
- **GoldenRulesEngineIntegrationTests**: Tests for the 12 Golden Rules trading discipline engine
  - Trade evaluation with all 12 rules
  - Risk limit enforcement
  - Compliance tracking
  - Session reporting
  - Rule configuration updates

### 3. Strategy Integration Tests
- **StrategyIntegrationTests**: Tests for trading strategy implementations
  - MomentumStrategy signal generation
  - GapStrategy pattern detection
  - Multi-strategy orchestration
  - Performance metrics tracking
  - Concurrent execution safety

### 4. End-to-End Workflow Tests
- **TradingWorkflowIntegrationTests**: Complete trading workflow validation
  - Signal generation → Validation → Risk Assessment → Execution
  - Multi-strategy concurrent processing
  - Risk management integration
  - Message queue resilience

## Running the Tests

### Prerequisites
- Docker Desktop or Docker Engine installed
- .NET 8.0 SDK
- Sufficient system resources for containers

### Running All Tests
```bash
dotnet test TradingPlatform.IntegrationTests.csproj
```

### Running Specific Test Categories
```bash
# Run only messaging tests
dotnet test --filter "FullyQualifiedName~Messaging"

# Run only Golden Rules tests
dotnet test --filter "FullyQualifiedName~GoldenRules"

# Run only end-to-end tests
dotnet test --filter "FullyQualifiedName~EndToEnd"
```

### Running with Detailed Output
```bash
dotnet test -v detailed --logger "console;verbosity=detailed"
```

## Test Infrastructure

### IntegrationTestFixture
Base fixture that provides:
- Redis container management
- PostgreSQL container management
- Service provider configuration
- Dependency injection setup

### Test Collections
Tests are organized into collections to share expensive resources:
- `[Collection("Integration Tests")]` - Shares the main integration test fixture

### Containerized Dependencies
- **Redis**: Used for message queue and caching
- **PostgreSQL**: Used for persistent storage (when needed)

## Writing New Integration Tests

### Basic Test Structure
```csharp
[Collection("Integration Tests")]
public class MyIntegrationTests : IClassFixture<MyTestFixture>
{
    private readonly MyTestFixture _fixture;

    public MyIntegrationTests(MyTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest_ShouldWork()
    {
        // Arrange
        var service = _fixture.GetRequiredService<IMyService>();
        
        // Act
        var result = await service.DoSomethingAsync();
        
        // Assert
        result.Should().NotBeNull();
    }
}
```

### Custom Test Fixture
```csharp
public class MyTestFixture : IntegrationTestFixture
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Add your test-specific services
        services.AddSingleton<IMyService, MyService>();
    }
}
```

## Test Data

### Market Data Generation
Tests use realistic market data with controlled parameters:
- Price movements (1-5% typical)
- Volume patterns (10M-100M shares)
- Volatility ranges (1-5%)
- RSI values (30-70 normal range)

### Position Context
Standard test positions with:
- $100,000 account balance
- 4:1 margin ($400,000 buying power)
- Controlled P&L scenarios
- Realistic trade counts

## Performance Considerations

### Container Startup
- Containers are shared within test collections
- First test in collection incurs startup cost (~2-5 seconds)
- Subsequent tests reuse running containers

### Parallel Execution
- Tests within same collection run sequentially
- Different collections can run in parallel
- Use `[Collection]` attribute to control parallelism

### Resource Cleanup
- Containers are automatically disposed after tests
- Use `IAsyncLifetime` for async cleanup
- Implement proper cancellation token handling

## Troubleshooting

### Container Issues
```bash
# Check running containers
docker ps

# Clean up orphaned containers
docker container prune

# Reset Docker (if needed)
docker system prune -a
```

### Test Timeouts
- Default timeout: 30 seconds per test
- Adjust with `[Fact(Timeout = 60000)]` for longer tests
- Use proper CancellationToken handling

### Port Conflicts
- Testcontainers assigns random ports automatically
- No manual port configuration needed
- Check for other services using standard ports

## CI/CD Integration

### GitHub Actions
```yaml
- name: Run Integration Tests
  run: dotnet test TradingPlatform.IntegrationTests.csproj --logger GitHubActions
```

### Azure DevOps
```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
    projects: '**/TradingPlatform.IntegrationTests.csproj'
    arguments: '--logger trx --collect:"XPlat Code Coverage"'
```

## Best Practices

1. **Test Isolation**: Each test should be independent
2. **Data Cleanup**: Don't rely on test execution order
3. **Realistic Scenarios**: Use production-like data and conditions
4. **Error Handling**: Test both success and failure paths
5. **Performance**: Keep individual tests under 10 seconds
6. **Logging**: Use test output for debugging
7. **Assertions**: Use FluentAssertions for readable tests
8. **Async/Await**: Properly handle async operations

## Coverage Goals

- **Unit Tests**: 80%+ code coverage
- **Integration Tests**: Cover all critical paths
- **End-to-End Tests**: Cover main user workflows
- **Performance Tests**: Separate project for load testing
# TradingPlatform Chaos Tests

Chaos engineering tests for validating the resilience and fault tolerance of the Trading Platform under various failure scenarios.

## Overview

Chaos Tests systematically inject failures and abnormal conditions into the trading platform to ensure it can:
- Recover gracefully from failures
- Maintain data consistency under stress
- Degrade gracefully under resource constraints
- Handle cascading failures
- Protect against data corruption

## Chaos Scenarios

### 1. Network Failures
- **Network Partitions**: Simulates loss of connectivity between services
- **High Latency**: Injects delays in network operations
- **Packet Loss**: Simulates unreliable network conditions
- **Connection Timeouts**: Tests timeout handling and retry logic

### 2. Resource Exhaustion
- **CPU Stress**: Simulates high CPU usage scenarios
- **Memory Pressure**: Tests behavior under memory constraints
- **Thread Pool Exhaustion**: Validates thread management
- **I/O Bottlenecks**: Simulates slow disk/network I/O

### 3. Service Failures
- **Service Crashes**: Tests recovery from sudden service termination
- **Cascading Failures**: Validates failure isolation
- **Slow Services**: Tests timeout and circuit breaker patterns
- **Byzantine Failures**: Tests handling of corrupted responses

### 4. Data Anomalies
- **Poison Messages**: Tests handling of malformed data
- **Clock Skew**: Validates time synchronization handling
- **Data Corruption**: Tests data validation and recovery
- **Duplicate Messages**: Validates idempotency

## Test Structure

```
TradingPlatform.ChaosTests/
├── Framework/
│   └── ChaosTestBase.cs          # Base class with chaos injection utilities
├── Scenarios/
│   ├── MessageQueueChaosTests.cs # Message queue resilience tests
│   ├── DataIngestionChaosTests.cs # Data provider failure tests
│   └── OrderExecutionChaosTests.cs # Order processing chaos tests
└── Resilience/
    ├── TradingWorkflowResilienceTests.cs # End-to-end resilience
    └── SystemRecoveryTests.cs    # Recovery time validation
```

## Key Technologies

- **Polly**: Resilience and transient-fault-handling library
- **Simmy**: Chaos engineering library (Polly extension)
- **Testcontainers**: For simulating infrastructure failures
- **xUnit**: Test framework with parallel execution support

## Running Chaos Tests

### Prerequisites
- Docker Desktop or Docker Engine
- .NET 8.0 SDK
- Sufficient system resources (chaos tests are resource-intensive)

### Running All Chaos Tests
```bash
dotnet test TradingPlatform.ChaosTests.csproj --logger "console;verbosity=detailed"
```

### Running Specific Scenarios
```bash
# Message queue chaos only
dotnet test --filter "FullyQualifiedName~MessageQueueChaos"

# Resilience tests only
dotnet test --filter "FullyQualifiedName~Resilience"
```

### Running with Chaos Metrics
```bash
dotnet test --collect:"XPlat Code Coverage" --settings chaos.runsettings
```

## Chaos Injection Patterns

### 1. Exception Injection
```csharp
var chaosPolicy = CreateExceptionChaosPolicy<T>(
    injectionRate: 0.1, // 10% of operations fail
    exceptionFactory: (context, ct) => new TimeoutException()
);
```

### 2. Latency Injection
```csharp
var latencyPolicy = CreateLatencyChaosPolicy<T>(
    injectionRate: 0.2, // 20% of operations delayed
    latency: TimeSpan.FromSeconds(5)
);
```

### 3. Result Manipulation
```csharp
var resultPolicy = CreateResultChaosPolicy<T>(
    injectionRate: 0.05, // 5% return corrupted results
    resultFactory: (context, ct) => CreateCorruptedResult()
);
```

### 4. Behavior Injection
```csharp
var behaviorPolicy = CreateBehaviorChaosPolicy(
    injectionRate: 0.1,
    behavior: async (context, ct) => await SimulateResourceExhaustion()
);
```

## Resilience Validation

### Success Criteria
- **Recovery Time**: System recovers within SLA (typically < 30 seconds)
- **Data Consistency**: No data loss or corruption
- **Degradation**: Graceful degradation under stress
- **Error Rate**: Acceptable error rate under chaos (< 10%)

### Metrics Collected
- Success/Failure rates
- Latency percentiles (P50, P95, P99)
- Recovery time objectives (RTO)
- Resource utilization during chaos
- Error propagation patterns

## Writing New Chaos Tests

### Basic Template
```csharp
[Fact]
public async Task Service_UnderChaos_MaintainsConsistency()
{
    // Arrange - Setup service and chaos policy
    var service = CreateService();
    var chaosPolicy = CreateExceptionChaosPolicy<Result>(
        injectionRate: 0.2,
        exceptionFactory: (ctx, ct) => new NetworkException()
    );

    // Act - Execute operations with chaos
    var results = new List<Result>();
    for (int i = 0; i < 100; i++)
    {
        var result = await chaosPolicy.ExecuteAsync(
            async () => await service.ProcessAsync()
        );
        results.Add(result);
    }

    // Assert - Validate resilience
    var successRate = results.Count(r => r.IsSuccess) / (double)results.Count;
    successRate.Should().BeGreaterThan(0.8); // 80% success despite 20% chaos
}
```

### Advanced Scenarios
```csharp
[Fact]
public async Task System_WithCascadingFailures_RecoversProperly()
{
    // Test cascading failure scenarios
    // Validate failure isolation
    // Measure recovery time
    // Ensure no data corruption
}
```

## Best Practices

1. **Reproducibility**: Use fixed random seeds for consistent results
2. **Isolation**: Run chaos tests separately from other tests
3. **Monitoring**: Collect detailed metrics during chaos
4. **Gradual Chaos**: Start with low injection rates, increase gradually
5. **Real Scenarios**: Base chaos on actual production incidents
6. **Documentation**: Document failure modes and recovery strategies

## Safety Considerations

- Chaos tests should NEVER run against production
- Use dedicated test environments
- Monitor resource usage during tests
- Have cleanup procedures for failed tests
- Set appropriate timeouts to prevent hanging tests

## Continuous Chaos Testing

### CI/CD Integration
```yaml
- name: Run Chaos Tests
  run: |
    dotnet test TradingPlatform.ChaosTests.csproj \
      --configuration Release \
      --logger "trx;LogFileName=chaos-results.trx" \
      --collect:"XPlat Code Coverage"
  timeout-minutes: 30
```

### Scheduled Chaos
- Run chaos tests nightly
- Increase chaos intensity over time
- Rotate through different failure scenarios
- Alert on resilience degradation

## Interpreting Results

### Success Indicators
- ✅ System recovers automatically
- ✅ No data loss or corruption
- ✅ Performance degrades gracefully
- ✅ Errors are properly isolated

### Failure Indicators
- ❌ System fails to recover
- ❌ Data inconsistencies detected
- ❌ Cascading failures occur
- ❌ Performance cliff under stress

## Future Enhancements

1. **Game Days**: Scheduled chaos experiments
2. **Chaos Mesh Integration**: Kubernetes chaos testing
3. **Automated Chaos**: Self-adjusting chaos based on metrics
4. **Chaos Dashboard**: Real-time chaos visualization
5. **Failure Library**: Catalog of failure scenarios
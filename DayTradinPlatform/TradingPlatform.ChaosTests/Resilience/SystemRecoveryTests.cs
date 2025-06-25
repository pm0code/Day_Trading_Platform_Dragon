using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TradingPlatform.ChaosTests.Framework;
using TradingPlatform.Core.Interfaces;
using TradingPlatform.Core.Models;
using TradingPlatform.DataIngestion.Services;
using TradingPlatform.Messaging.Services;
using TradingPlatform.PaperTrading.Services;
using TradingPlatform.RiskManagement.Services;
using TradingPlatform.StrategyEngine.Strategies;
using Xunit;
using Xunit.Abstractions;

namespace TradingPlatform.ChaosTests.Resilience
{
    [Collection("Chaos Tests")]
    public class SystemRecoveryTests : ChaosTestBase
    {
        private readonly ITradingLogger _logger;
        private readonly ICanonicalMessageQueue _messageQueue;

        public SystemRecoveryTests(ITestOutputHelper output) : base(output)
        {
            _logger = GetRequiredService<ITradingLogger>();
            _messageQueue = GetRequiredService<ICanonicalMessageQueue>();
        }

        [Fact]
        public async Task System_AfterCriticalServiceFailure_RecoverToOperationalState()
        {
            // Arrange
            var services = new Dictionary<string, ICanonicalService>
            {
                ["DataIngestion"] = GetRequiredService<IDataIngestionService>() as ICanonicalService,
                ["MessageQueue"] = _messageQueue as ICanonicalService,
                ["RiskCalculator"] = new RiskCalculatorCanonical(_logger),
                ["Strategy"] = new MomentumStrategyCanonical(_logger),
                ["Execution"] = new OrderExecutionEngineCanonical(_logger, _messageQueue)
            };

            var recoveryMetrics = new RecoveryMetrics();

            // Initialize all services
            foreach (var service in services.Values.Where(s => s != null))
            {
                await service.InitializeAsync(CancellationToken.None);
                await service.StartAsync(CancellationToken.None);
            }

            // Act - Simulate critical service failure
            Output.WriteLine("Simulating critical service failures...");
            
            // Phase 1: Fail critical services
            var criticalServices = new[] { "MessageQueue", "DataIngestion" };
            foreach (var serviceName in criticalServices)
            {
                if (services[serviceName] != null)
                {
                    await services[serviceName].StopAsync(CancellationToken.None);
                    recoveryMetrics.FailureTimestamps[serviceName] = DateTime.UtcNow;
                    Output.WriteLine($"{serviceName} failed at {DateTime.UtcNow:HH:mm:ss.fff}");
                }
            }

            // Wait for failure propagation
            await Task.Delay(2000);

            // Check system health during failure
            var unhealthyCount = 0;
            foreach (var (name, service) in services)
            {
                if (service != null)
                {
                    var health = await service.CheckHealthAsync(CancellationToken.None);
                    if (!health.IsHealthy)
                    {
                        unhealthyCount++;
                        Output.WriteLine($"{name} is unhealthy: {health.Details}");
                    }
                }
            }

            unhealthyCount.Should().BeGreaterThan(0, "Some services should be unhealthy during failure");

            // Phase 2: Begin recovery
            Output.WriteLine("\nBeginning system recovery...");
            var recoveryStartTime = DateTime.UtcNow;

            // Restart services in dependency order
            var recoveryOrder = new[] { "MessageQueue", "DataIngestion", "RiskCalculator", "Strategy", "Execution" };
            
            foreach (var serviceName in recoveryOrder)
            {
                if (services[serviceName] != null && services[serviceName].State != ServiceState.Running)
                {
                    var serviceRecoveryStart = DateTime.UtcNow;
                    
                    try
                    {
                        var startResult = await services[serviceName].StartAsync(CancellationToken.None);
                        
                        if (startResult.IsSuccess)
                        {
                            var recoveryTime = DateTime.UtcNow - serviceRecoveryStart;
                            recoveryMetrics.RecoveryTimes[serviceName] = recoveryTime;
                            recoveryMetrics.RecoveryTimestamps[serviceName] = DateTime.UtcNow;
                            Output.WriteLine($"{serviceName} recovered in {recoveryTime.TotalMilliseconds:F0}ms");
                        }
                        else
                        {
                            Output.WriteLine($"{serviceName} recovery failed: {startResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Output.WriteLine($"{serviceName} recovery error: {ex.Message}");
                    }
                    
                    // Small delay between service restarts
                    await Task.Delay(100);
                }
            }

            var totalRecoveryTime = DateTime.UtcNow - recoveryStartTime;

            // Phase 3: Verify system health after recovery
            Output.WriteLine("\nVerifying system health...");
            await Task.Delay(1000); // Allow services to stabilize

            var healthyCount = 0;
            foreach (var (name, service) in services)
            {
                if (service != null)
                {
                    var health = await service.CheckHealthAsync(CancellationToken.None);
                    if (health.IsHealthy)
                    {
                        healthyCount++;
                        Output.WriteLine($"✓ {name} is healthy");
                    }
                    else
                    {
                        Output.WriteLine($"✗ {name} is still unhealthy: {health.Details}");
                    }
                }
            }

            // Assert
            healthyCount.Should().Be(services.Count(s => s.Value != null), "All services should be healthy after recovery");
            totalRecoveryTime.Should().BeLessThan(TimeSpan.FromSeconds(30), "System should recover within 30 seconds");
            
            Output.WriteLine($"\nTotal recovery time: {totalRecoveryTime.TotalSeconds:F2} seconds");
            Output.WriteLine($"Recovery sequence completed successfully");
        }

        [Fact]
        public async Task System_WithRollingFailures_MaintainsPartialAvailability()
        {
            // Arrange
            var services = new List<ICanonicalService>
            {
                new MomentumStrategyCanonical(_logger),
                new GapStrategyCanonical(_logger),
                new VolumeStrategyCanonical(_logger),
                new RiskCalculatorCanonical(_logger)
            };

            foreach (var service in services)
            {
                await service.InitializeAsync(CancellationToken.None);
                await service.StartAsync(CancellationToken.None);
            }

            var availabilityMetrics = new AvailabilityMetrics();
            var measurementDuration = TimeSpan.FromSeconds(20);
            var measurementInterval = TimeSpan.FromMilliseconds(500);

            // Act - Create rolling failures
            var failureTask = Task.Run(async () =>
            {
                var failureInterval = TimeSpan.FromSeconds(2);
                var failureCount = 0;

                while (failureCount < 5)
                {
                    // Fail a random service
                    var serviceToFail = services[ChaosRandom.Next(services.Count)];
                    if (serviceToFail.State == ServiceState.Running)
                    {
                        await serviceToFail.StopAsync(CancellationToken.None);
                        Output.WriteLine($"Failed {serviceToFail.GetType().Name} at {DateTime.UtcNow:HH:mm:ss.fff}");
                        failureCount++;
                    }

                    await Task.Delay(failureInterval);

                    // Attempt to restart previously failed services
                    foreach (var service in services.Where(s => s.State != ServiceState.Running))
                    {
                        var restartResult = await service.StartAsync(CancellationToken.None);
                        if (restartResult.IsSuccess)
                        {
                            Output.WriteLine($"Restarted {service.GetType().Name} at {DateTime.UtcNow:HH:mm:ss.fff}");
                        }
                    }
                }
            });

            // Monitor availability
            var monitoringTask = Task.Run(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                while (stopwatch.Elapsed < measurementDuration)
                {
                    var healthyServices = 0;
                    var totalServices = services.Count;

                    foreach (var service in services)
                    {
                        try
                        {
                            var health = await service.CheckHealthAsync(CancellationToken.None);
                            if (health.IsHealthy)
                            {
                                healthyServices++;
                            }
                        }
                        catch
                        {
                            // Service is not responding
                        }
                    }

                    var availability = (double)healthyServices / totalServices;
                    availabilityMetrics.Measurements.Add((stopwatch.Elapsed, availability));

                    await Task.Delay(measurementInterval);
                }
            });

            await Task.WhenAll(failureTask, monitoringTask);

            // Calculate metrics
            var avgAvailability = availabilityMetrics.Measurements.Average(m => m.availability);
            var minAvailability = availabilityMetrics.Measurements.Min(m => m.availability);
            var downtimePeriods = availabilityMetrics.Measurements.Count(m => m.availability < 0.5);

            // Assert
            avgAvailability.Should().BeGreaterThan(0.6, "Average availability should be above 60%");
            minAvailability.Should().BeGreaterThan(0.25, "At least 25% of services should always be available");
            
            Output.WriteLine($"\nAvailability metrics during rolling failures:");
            Output.WriteLine($"Average availability: {avgAvailability:P2}");
            Output.WriteLine($"Minimum availability: {minAvailability:P2}");
            Output.WriteLine($"Downtime periods (< 50%): {downtimePeriods}");
        }

        [Fact]
        public async Task System_AfterDataCorruption_RestoresDataIntegrity()
        {
            // Arrange
            var dataStore = new InMemoryDataStore();
            var originalData = GenerateTestData(100);
            
            // Store original data
            foreach (var data in originalData)
            {
                await dataStore.StoreAsync(data.Symbol, data);
            }

            // Calculate original checksums
            var originalChecksums = new Dictionary<string, string>();
            foreach (var data in originalData)
            {
                originalChecksums[data.Symbol] = CalculateChecksum(data);
            }

            // Act - Corrupt data
            Output.WriteLine("Corrupting stored data...");
            var corruptedSymbols = new List<string>();
            var corruptionRate = 0.3; // 30% corruption

            foreach (var data in originalData)
            {
                if (ChaosRandom.NextDouble() < corruptionRate)
                {
                    // Corrupt the data
                    var corrupted = new MarketData
                    {
                        Symbol = data.Symbol,
                        Timestamp = data.Timestamp,
                        Open = data.Open * -1, // Negative prices
                        High = data.Low, // Inverted high/low
                        Low = data.High,
                        Close = decimal.MaxValue, // Extreme value
                        Volume = -999999 // Negative volume
                    };
                    
                    await dataStore.StoreAsync(data.Symbol, corrupted);
                    corruptedSymbols.Add(data.Symbol);
                }
            }

            Output.WriteLine($"Corrupted {corruptedSymbols.Count} out of {originalData.Count} records");

            // Recovery process
            Output.WriteLine("\nBeginning data recovery...");
            var recoveryStartTime = DateTime.UtcNow;
            var recoveredCount = 0;
            var validationErrors = new List<string>();

            // Validate and recover data
            foreach (var symbol in originalData.Select(d => d.Symbol))
            {
                var storedData = await dataStore.GetAsync<MarketData>(symbol);
                
                if (!ValidateMarketData(storedData))
                {
                    validationErrors.Add($"{symbol}: {GetValidationError(storedData)}");
                    
                    // Attempt recovery from backup or recalculation
                    var recoveredData = await RecoverMarketData(symbol, originalData.First(d => d.Symbol == symbol));
                    
                    if (ValidateMarketData(recoveredData))
                    {
                        await dataStore.StoreAsync(symbol, recoveredData);
                        recoveredCount++;
                    }
                }
            }

            var recoveryTime = DateTime.UtcNow - recoveryStartTime;

            // Verify data integrity after recovery
            var integrityChecksPassed = 0;
            foreach (var data in originalData)
            {
                var storedData = await dataStore.GetAsync<MarketData>(data.Symbol);
                if (ValidateMarketData(storedData))
                {
                    integrityChecksPassed++;
                }
            }

            // Assert
            integrityChecksPassed.Should().Be(originalData.Count, "All data should pass integrity checks after recovery");
            recoveredCount.Should().Be(corruptedSymbols.Count, "All corrupted data should be recovered");
            recoveryTime.Should().BeLessThan(TimeSpan.FromSeconds(5), "Data recovery should be fast");
            
            Output.WriteLine($"\nData recovery completed:");
            Output.WriteLine($"Validation errors found: {validationErrors.Count}");
            Output.WriteLine($"Records recovered: {recoveredCount}");
            Output.WriteLine($"Recovery time: {recoveryTime.TotalMilliseconds:F0}ms");
            Output.WriteLine($"Final integrity check: {integrityChecksPassed}/{originalData.Count} passed");
        }

        [Fact]
        public async Task System_UnderMemoryPressure_PerformsGracefulDegradation()
        {
            // Arrange
            var services = new List<ICanonicalService>
            {
                new MomentumStrategyCanonical(_logger),
                new OrderExecutionEngineCanonical(_logger, _messageQueue)
            };

            foreach (var service in services)
            {
                await service.InitializeAsync(CancellationToken.None);
                await service.StartAsync(CancellationToken.None);
            }

            var performanceMetrics = new PerformanceMetrics();
            var normalOperationTime = TimeSpan.FromSeconds(5);
            var pressureOperationTime = TimeSpan.FromSeconds(10);

            // Act - Phase 1: Normal operation
            Output.WriteLine("Phase 1: Measuring normal operation performance...");
            var normalPhaseTask = MeasurePerformance(
                services,
                normalOperationTime,
                performanceMetrics,
                "Normal");

            await normalPhaseTask;

            var normalThroughput = performanceMetrics.GetThroughput("Normal");
            var normalLatency = performanceMetrics.GetAverageLatency("Normal");

            Output.WriteLine($"Normal operation: {normalThroughput:F2} ops/sec, {normalLatency:F2}ms avg latency");

            // Phase 2: Operation under memory pressure
            Output.WriteLine("\nPhase 2: Applying memory pressure...");
            var memoryPressureTask = SimulateResourceExhaustion(
                cpuStressThreads: 2,
                memoryMB: 1500,
                duration: pressureOperationTime);

            var pressurePhaseTask = MeasurePerformance(
                services,
                pressureOperationTime,
                performanceMetrics,
                "Pressure");

            await Task.WhenAll(memoryPressureTask, pressurePhaseTask);

            var pressureThroughput = performanceMetrics.GetThroughput("Pressure");
            var pressureLatency = performanceMetrics.GetAverageLatency("Pressure");

            Output.WriteLine($"Under pressure: {pressureThroughput:F2} ops/sec, {pressureLatency:F2}ms avg latency");

            // Phase 3: Recovery after pressure
            Output.WriteLine("\nPhase 3: Measuring recovery...");
            await Task.Delay(2000); // Allow system to recover

            var recoveryPhaseTask = MeasurePerformance(
                services,
                normalOperationTime,
                performanceMetrics,
                "Recovery");

            await recoveryPhaseTask;

            var recoveryThroughput = performanceMetrics.GetThroughput("Recovery");
            var recoveryLatency = performanceMetrics.GetAverageLatency("Recovery");

            Output.WriteLine($"After recovery: {recoveryThroughput:F2} ops/sec, {recoveryLatency:F2}ms avg latency");

            // Clean up
            foreach (var service in services)
            {
                await service.StopAsync(CancellationToken.None);
            }

            // Assert
            pressureThroughput.Should().BeGreaterThan(normalThroughput * 0.3, "System should maintain at least 30% throughput under pressure");
            pressureLatency.Should().BeLessThan(normalLatency * 5, "Latency should not increase more than 5x under pressure");
            recoveryThroughput.Should().BeGreaterThan(normalThroughput * 0.8, "System should recover to at least 80% of normal throughput");
            
            var degradationPercent = (1 - pressureThroughput / normalThroughput) * 100;
            var recoveryPercent = (recoveryThroughput / normalThroughput) * 100;
            
            Output.WriteLine($"\nPerformance summary:");
            Output.WriteLine($"Degradation under pressure: {degradationPercent:F1}%");
            Output.WriteLine($"Recovery level: {recoveryPercent:F1}%");
        }

        private async Task MeasurePerformance(
            List<ICanonicalService> services,
            TimeSpan duration,
            PerformanceMetrics metrics,
            string phase)
        {
            var endTime = DateTime.UtcNow + duration;
            var operations = 0;

            while (DateTime.UtcNow < endTime)
            {
                var opStart = DateTime.UtcNow;

                try
                {
                    // Simulate operations
                    foreach (var service in services)
                    {
                        var health = await service.CheckHealthAsync(CancellationToken.None);
                        if (health.IsHealthy)
                        {
                            operations++;
                        }
                    }

                    var opLatency = (DateTime.UtcNow - opStart).TotalMilliseconds;
                    metrics.RecordOperation(phase, opLatency);
                }
                catch
                {
                    // Operation failed
                }

                await Task.Delay(10); // Small delay between operations
            }
        }

        private List<MarketData> GenerateTestData(int count)
        {
            var data = new List<MarketData>();
            var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN", "TSLA" };

            for (int i = 0; i < count; i++)
            {
                data.Add(new MarketData
                {
                    Symbol = symbols[i % symbols.Length],
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    Open = 100m + (i % 20),
                    High = 105m + (i % 20),
                    Low = 95m + (i % 20),
                    Close = 102m + (i % 20),
                    Volume = 1000000 + (i * 1000)
                });
            }

            return data;
        }

        private string CalculateChecksum(MarketData data)
        {
            var dataString = $"{data.Symbol}{data.Timestamp:O}{data.Open}{data.High}{data.Low}{data.Close}{data.Volume}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(dataString));
        }

        private bool ValidateMarketData(MarketData data)
        {
            if (data == null) return false;
            if (string.IsNullOrEmpty(data.Symbol)) return false;
            if (data.Open <= 0 || data.High <= 0 || data.Low <= 0 || data.Close <= 0) return false;
            if (data.Volume < 0) return false;
            if (data.High < data.Low) return false;
            if (data.Open > data.High || data.Open < data.Low) return false;
            if (data.Close > data.High || data.Close < data.Low) return false;
            return true;
        }

        private string GetValidationError(MarketData data)
        {
            if (data == null) return "Null data";
            if (string.IsNullOrEmpty(data.Symbol)) return "Missing symbol";
            if (data.Open <= 0 || data.Close <= 0) return "Invalid prices";
            if (data.Volume < 0) return "Negative volume";
            if (data.High < data.Low) return "High < Low";
            return "Unknown error";
        }

        private async Task<MarketData> RecoverMarketData(string symbol, MarketData original)
        {
            // Simulate recovery process
            await Task.Delay(10);
            
            // Return valid data (in real scenario, this would fetch from backup or recalculate)
            return new MarketData
            {
                Symbol = symbol,
                Timestamp = original.Timestamp,
                Open = Math.Abs(original.Open),
                High = Math.Max(Math.Abs(original.High), Math.Abs(original.Low)),
                Low = Math.Min(Math.Abs(original.High), Math.Abs(original.Low)),
                Close = Math.Abs(original.Close),
                Volume = Math.Abs(original.Volume)
            };
        }

        private class RecoveryMetrics
        {
            public Dictionary<string, DateTime> FailureTimestamps { get; } = new();
            public Dictionary<string, DateTime> RecoveryTimestamps { get; } = new();
            public Dictionary<string, TimeSpan> RecoveryTimes { get; } = new();
        }

        private class AvailabilityMetrics
        {
            public List<(TimeSpan elapsed, double availability)> Measurements { get; } = new();
        }

        private class PerformanceMetrics
        {
            private readonly Dictionary<string, List<double>> _latencies = new();
            private readonly Dictionary<string, int> _operationCounts = new();
            private readonly Dictionary<string, DateTime> _phaseStartTimes = new();

            public void RecordOperation(string phase, double latencyMs)
            {
                lock (_latencies)
                {
                    if (!_latencies.ContainsKey(phase))
                    {
                        _latencies[phase] = new List<double>();
                        _operationCounts[phase] = 0;
                        _phaseStartTimes[phase] = DateTime.UtcNow;
                    }

                    _latencies[phase].Add(latencyMs);
                    _operationCounts[phase]++;
                }
            }

            public double GetThroughput(string phase)
            {
                lock (_latencies)
                {
                    if (!_operationCounts.ContainsKey(phase)) return 0;
                    
                    var duration = DateTime.UtcNow - _phaseStartTimes[phase];
                    return _operationCounts[phase] / duration.TotalSeconds;
                }
            }

            public double GetAverageLatency(string phase)
            {
                lock (_latencies)
                {
                    if (!_latencies.ContainsKey(phase) || !_latencies[phase].Any()) return 0;
                    return _latencies[phase].Average();
                }
            }
        }

        private class InMemoryDataStore
        {
            private readonly Dictionary<string, object> _store = new();

            public Task StoreAsync<T>(string key, T value)
            {
                lock (_store)
                {
                    _store[key] = value!;
                }
                return Task.CompletedTask;
            }

            public Task<T> GetAsync<T>(string key)
            {
                lock (_store)
                {
                    if (_store.TryGetValue(key, out var value))
                    {
                        return Task.FromResult((T)value);
                    }
                    return Task.FromResult(default(T)!);
                }
            }
        }

        private class VolumeStrategyCanonical : CanonicalStrategyBase
        {
            public VolumeStrategyCanonical(ITradingLogger logger) : base(logger, "VolumeStrategy")
            {
            }

            public override Task<TradingResult<TradingSignal>> ExecuteStrategyAsync(
                string symbol,
                MarketData currentData,
                List<MarketData>? historicalData,
                CancellationToken cancellationToken)
            {
                // Simple volume-based strategy
                var signal = new TradingSignal
                {
                    Symbol = symbol,
                    SignalType = currentData.Volume > 10000000 ? SignalType.Buy : SignalType.Hold,
                    Strength = 0.5,
                    Price = currentData.Close,
                    Quantity = 100,
                    Timestamp = DateTime.UtcNow,
                    Source = "VolumeStrategy"
                };

                return Task.FromResult(TradingResult<TradingSignal>.Success(signal));
            }

            protected override Task<TradingResult> OnInitializeAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(TradingResult.Success());
            }

            protected override Task<TradingResult> OnStartAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(TradingResult.Success());
            }

            protected override Task<TradingResult> OnStopAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(TradingResult.Success());
            }
        }
    }
}
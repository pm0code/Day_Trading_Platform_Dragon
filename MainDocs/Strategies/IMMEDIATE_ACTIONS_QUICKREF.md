# Day Trading Platform - Immediate Actions Quick Reference

## 🔴 TODAY'S PRIORITIES (January 30, 2025)

### 1. Complete Financial Precision Fixes (CRITICAL)
```bash
# Navigate to ML module
cd DayTradinPlatform/TradingPlatform.ML

# Files to fix:
- Data/MLDatasetBuilder.cs
- Ranking/StockSelectionAPI.cs  
- Recognition/PatternRecognitionAPI.cs

# Replace all double/float with decimal
# Update Math.* calls to DecimalMathCanonical.*
# Run tests after each file
```

### 2. Fix ApiRateLimiter Memory Leak
```csharp
// In ApiRateLimiter.cs:
// REMOVE: Cache keys with DateTime.Now.ToString("yyyy-MM-dd-HH-mm")
// ADD: Sliding window with fixed-size circular buffer
// REPLACE: .Result and .Wait() with proper async/await
// ADD: Jitter to delays: delay * (1 + Random.Next(-20, 20) / 100.0)
```

### 3. Start Canonical Service Migration
```csharp
// Create CanonicalExecutionService.cs
public class CanonicalExecutionService : CanonicalServiceBase, IOrderExecutionService
{
    // Migrate from OrderExecutionEngine
    // Follow pattern from CanonicalMarketDataService
    // Implement Initialize(), Start(), Stop()
    // Add health checks and metrics
}
```

## 📋 This Week's Checklist

### Monday-Tuesday
- [ ] Complete ML financial precision (3 files)
- [ ] Fix ApiRateLimiter issues
- [ ] Add unit tests for fixed components

### Wednesday-Thursday  
- [ ] Migrate OrderExecutionEngine → CanonicalExecutionService
- [ ] Migrate PortfolioManager → CanonicalPortfolioService
- [ ] Update dependency injection

### Friday
- [ ] Implement secure configuration (remove hardcoded keys)
- [ ] Set up basic authentication framework
- [ ] Create service orchestration
- [ ] Enhance TradingLogOrchestrator with MCP standards:
  - Add SCREAMING_SNAKE_CASE event codes
  - Implement operation tracking pattern
  - Add child logger support
  - Zero console.log enforcement

## 🚀 Quick Commands

```bash
# Run tests
cd DayTradinPlatform
dotnet test

# Check for precision issues
rg "double|float" --type cs | grep -v "//\|TradingResult<double>"

# Find TODOs
rg "TODO|FIXME|HACK" --type cs

# Build solution
dotnet build --configuration Release

# Run benchmarks
cd TradingPlatform.Benchmarks
dotnet run -c Release
```

## ⚡ Performance Targets

- Order Latency: < 100 microseconds
- Throughput: 10,000+ msg/sec
- Memory: Zero allocation in hot paths
- Precision: System.Decimal everywhere

## 🛡️ Security Checklist

- [ ] No API keys in source code
- [ ] Use secure configuration provider
- [ ] Implement authentication
- [ ] Add authorization checks
- [ ] Enable audit logging
- [ ] Encrypt sensitive data

## 📊 Component Status

### Keep Custom (Performance Critical)
- ✅ LockFreeQueue (25-50ns)
- ✅ DecimalMath (precision required)
- ✅ HighPerformancePool (50-100ns)
- ⚠️ ApiRateLimiter (fix issues)

### Replace (Non-Critical Only)
- 🔄 Config → Options Pattern
- 🔄 HTTP Retry → Polly (market data only)
- 🔄 Logging → ETW (zero allocation)

### Build New
- 🔨 FIX Protocol Engine
- 🔨 REST API Controllers
- 🔨 WebSocket Feeds
- 🔨 JWT Authentication

## 📅 Timeline to Production

- Week 1-2: Core fixes & security
- Week 3-4: Trading features
- Week 5-6: APIs & real-time
- Week 7-8: Testing & docs
- Week 9-10: Deploy to production

## 🎯 Definition of Done

1. All financial calculations use decimal
2. Zero critical security vulnerabilities
3. 80% unit test coverage
4. API documentation complete
5. Performance benchmarks pass
6. Deployment guide ready

---

**Remember**: Fix critical issues first, then features, then nice-to-haves!
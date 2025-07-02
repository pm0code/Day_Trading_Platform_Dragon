# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Structure

This is a C# .NET 8.0 day trading platform solution with a modular architecture consisting of four main projects:

- **TradingPlatform.Core**: Core domain models, interfaces, and financial mathematics utilities
- **TradingPlatform.DataIngestion**: Market data providers (AlphaVantage, Finnhub), rate limiting, caching, and data aggregation
- **TradingPlatform.Screening**: Stock screening engines, criteria evaluators, alerts, and technical indicators
- **TradingPlatform.Utilities**: Shared utilities and Roslyn scripting support

**Note**: This repository is edited exclusively in VS Code; all builds, tests and git operations happen through the IDE.

## Build Commands

**Navigate to solution directory first:**
```bash
cd DayTradinPlatform
```

**Common build commands:**
```bash
# Restore packages
dotnet restore

# Build solution (Debug)
dotnet build

# Build solution (Release)
dotnet build --configuration Release

# Run application
dotnet run

# Clean build artifacts
dotnet clean
```

**Platform-specific builds (solution is configured for x64):**
```bash
dotnet build --configuration Release --runtime win-x64
```

## Architecture Overview

### Data Flow Architecture
1. **Data Ingestion Layer**: Pulls market data from external APIs (AlphaVantage, Finnhub) with rate limiting and caching
2. **Core Domain Layer**: Contains financial models and mathematical calculations using `System.Decimal` for precision
3. **Screening Engine**: Evaluates stocks against multiple criteria (price, volume, volatility, gaps, news)
4. **Alert System**: Provides notifications based on screening results

### Key Design Patterns
- **Dependency Injection**: All services registered in `Program.cs` using Microsoft.Extensions.DependencyInjection
- **Provider Pattern**: Market data providers implement common interfaces for different data sources
- **Rate Limiting**: API calls are throttled using custom rate limiter implementations
- **Caching**: Market data is cached to reduce API calls and improve performance

### Financial Precision Standards
- **Critical**: All monetary values and financial calculations MUST use `System.Decimal` type, never `double` or `float`
- Financial calculation standards are documented in `TradingPlatform.Core/Documentation/FinancialCalculationStandards.md`
- Custom math utilities should be created for `decimal` types when `System.Math` functions return `double`

## Key Interfaces

- `IMarketDataProvider`: Contract for market data sources
- `IDataIngestionService`: Data collection orchestration
- `IScreeningEngine`: Stock screening logic
- `IAlertService`: Alert and notification management
- `IRateLimiter`: API rate limiting
- `IMarketDataAggregator`: Combines data from multiple sources

## Configuration

External API integrations require configuration for:
- AlphaVantage API keys and endpoints
- Finnhub API keys and endpoints  
- Rate limiting parameters
- Cache settings

## Testing

**Phase 1A COMPLETED**: TradingPlatform.Tests project established with comprehensive financial math validation.

Current Testing Status:
- ✅ **TradingPlatform.Tests**: xUnit framework with 28 financial math tests (100% pass rate)
- ✅ **Financial Precision**: All calculations validated using System.Decimal compliance
- ✅ **Core Dependencies**: Clean modular architecture with resolved circular dependencies

To expand testing:
1. Add tests for DataIngestion providers (AlphaVantage, Finnhub)
2. Add tests for Screening engines and criteria evaluators
3. Add performance tests for ultra-low latency requirements (< 50ms targets for Enterprise configuration)
4. Add GPU acceleration tests for CUDA-enabled components
5. Add multi-monitor UI responsiveness tests (6+ displays)

## Dependencies

Key external dependencies:
- **Newtonsoft.Json**: JSON serialization
- **Serilog**: Structured logging
- **RestSharp**: HTTP client for API calls
- **System.Reactive**: Reactive programming
- **Microsoft.Extensions.***: Dependency injection, caching, logging
- **Microsoft.CodeAnalysis**: Roslyn compiler services for utilities

## MainDocs

Key documentation files in the MainDocs directory:

- **Day Trading Stock Recommendation Platform - PRD.md**: Product Requirements Document defining platform specifications and features
- **FinancialCalculationStandards.md**: Detailed standards for financial calculations and decimal precision requirements
- **High_Performance_ Stock_ Day_ Trading_ Platform.md.txt**: Comprehensive framework for ultra-low latency trading platform (< 100μs targets)
- **Professional Multi-Screen Trading Systems_ Architecture.md**: Multi-screen trading system architecture and design patterns

## Target Hardware Configuration

**PRIMARY TARGET: Professional Configuration (4 Monitors)**
- CPU: Intel i7-13700K or AMD Ryzen 7 7700X
- RAM: 32GB DDR4-3600 or DDR5-5600
- GPU: NVIDIA RTX 4060 or AMD RX 7600
- Storage: 1TB NVMe SSD + 2TB backup drive
- Monitors: 4 × 27-32" displays (4K primary, 1440p secondary)
- Network: Wired Ethernet (1Gbps minimum) + backup connection
- UPS: Professional-grade power protection
- Target Use Case: Advanced day trading with scalability
- Performance Targets: <50ms latency, 10,000+ messages/second
- **Expandable**: Architecture supports scaling to 6+ monitors (Enterprise configuration)

## Project Architecture

**CRITICAL**: See ARCHITECTURE.md for comprehensive performance requirements and implementation strategy based on high-performance trading platform framework. Key requirements:
- Sub-millisecond execution targets (< 100 microseconds order-to-wire)
- FIX protocol implementation for direct market access
- CPU core affinity and memory optimization for ultra-low latency
- TimescaleDB for microsecond-precision time-series data
- System.Decimal precision compliance validated in Phase 1A testing

### MainDocs – Trading "Golden Rules"

- **The_Complete_Day_Trading_Reference_Guide_ Golden_Rules.md**: Comprehensive reference guide covering psychological frameworks, technical methodologies, risk management strategies, and the fundamental golden rules for successful day trading operations
- **The_12_Golden_Rulesof_Day_Trading.md**: Synthesized framework of 12 core trading principles including capital preservation, trading discipline, loss management, and systematic approaches based on multiple authoritative sources

## Financial Precision Standards

**CRITICAL REMINDER**: ALL monetary values must use System.Decimal; never double/float.

• Follow every rule in the two 'Golden Rules' guides; treat violations as test failures.

## VS Code Workflow

- **Ctrl + Esc**: Quick command palette access
- **Alt + Ctrl + K**: Git operations and source control
- **Integrated Terminal**: Use dotnet CLI build commands directly in VS Code
- **Build Tasks**: Configure tasks.json for automated dotnet build/restore/clean operations
- **Debugging**: F5 to start debugging, breakpoints for financial calculation validation

## Development Notes

- Solution targets .NET 8.0 and x64 platform exclusively
- Uses nullable reference types (`<Nullable>enable`)
- All projects use implicit usings
- The `TradingPlatform.Utilities` project includes PowerShell scripts for service registration automation

## Additional Memories

• Every time you want to run a PS script on DRAGON, open a PS window with admin rights so I can see the execution of those commands. Do not close that window. I'll do it after my review.
• Remember that Windows does not have rsync. use scp instead
• All work has to be done on DRAGON at IP address 192.168.1.35, as this is a Windows project and the current machine is Ubuntu Linux.
• When working on the Holistic Architecture Instruction Set document in MainDocs/V2.x/, follow the comprehensive systems analysis protocol with meticulous attention to architectural integrity and multi-dimensional analysis

## Project Directory Structure (CRITICAL - DO NOT CONFUSE)
• PROJECT ROOT: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform` (note the typo in folder name)
• CLAUDE.md: `/home/nader/my_projects/C#/DayTradingPlatform/CLAUDE.md`
• SOLUTION FILE: `/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/DayTradingPlatform.sln`
• JOURNALS: `/home/nader/my_projects/C#/DayTradingPlatform/Journals`
• RESEARCH DOCS: `/home/nader/my_projects/C#/DayTradingPlatform/ResearchDocs`
• SCRIPTS: `/home/nader/my_projects/C#/DayTradingPlatform/scripts`
• MISC FILES: `/home/nader/my_projects/C#/DayTradingPlatform/MiscFiles` (for temporary/noise files)
• DO NOT create noise files in project root - use MiscFiles directory

## Machine Credentials

• DRAGON is a Windows 11 X64 machine @ 192.168.1.35
• RYZEN is an Ubuntu Linux machine @ 192.168.1.36
• For both machines:
  - User: admin
  - Password: 1qwertyuio0

## Model Guidance

• /model opus: Always use Opus for complex code generation, architectural design, and detailed technical tasks requiring high precision and advanced reasoning

## 🔴 CRITICAL MANDATE: MCP Real-Time Monitoring

**ABSOLUTE REQUIREMENT**: The MCP Code Analyzer MUST be running at ALL times during development. This is non-negotiable.

### MCP Monitoring Checklist:
1. **Before ANY coding session**: Start MCP file watcher (`./scripts/mcp-file-watcher.sh`)
2. **During development**: Verify MCP is catching issues in real-time
3. **Before EVERY commit**: Ensure pre-commit hook runs MCP analysis
4. **In VS Code**: Use MCP tasks for current file analysis
5. **CI/CD**: MCP analysis must pass before any merge

### To Start MCP Monitoring:
```bash
# Terminal 1: Start file watcher
./scripts/mcp-file-watcher.sh

# Terminal 2: Start MCP server (if using VS Code integration)
cd /home/nader/my_projects/C#/mcp-code-analyzer && npm run start
```

**FAILURE TO RUN MCP = TECHNICAL DEBT ACCUMULATION = PROJECT FAILURE**

## MANDATORY Standard Development Workflow

**CRITICAL**: The following workflow from `MainDocs/Coding Standard Development Workflow - Clean.md` is MANDATORY for ALL code work:

### 1. Canonical Service Implementation (MANDATORY)
- **Reuse Existing Services**: Always use existing canonical implementations (data access, messaging, config, auth, logging, error reporting, caching, validation, notifications, security, telemetry, background tasks)
- **Avoid Duplication**: Never create new implementations where canonical versions exist
- **Pattern Adherence**: All code must follow established architectural patterns and coding standards
- **Documentation**: Ensure all canonical services are well-documented and discoverable

### 2. Development Standards (MANDATORY)
- **Consistent Environments**: Standardized IDE settings, SDK versions, containerization
- **Project Structure**: Follow standardized layouts, naming conventions, directory structures
- **Build/Deployment**: Standardized scripts, dependency resolution, deployment pipelines

### 3. Comprehensive Documentation (MANDATORY)
- **Code-level**: Clear inline comments for complex logic, functions, classes, APIs
- **Project-level**: READMEs, architectural diagrams, design docs, API specs, deployment guides
- **Regular Updates**: Keep documentation synchronized with code changes

### 4. Static Analysis & Code Quality (MANDATORY)
- **Roslyn Analyzers**: Microsoft.CodeAnalysis.NetAnalyzers, StyleCop Analyzers, custom analyzers
- **SonarLint**: Real-time code quality and security detection
- **Zero-Warning Policy**: Maintain zero warnings from all analysis tools
- **Metrics Tracking**: Track cyclomatic complexity, maintainability index, technical debt

### 5. Error Handling & Logging (MANDATORY)
- **Canonical Error Pattern**: Standardized error handling across all code
- **Contextual Errors**: Detailed messages, exception objects, operational context, troubleshooting hints
- **No Silent Failures**: Every error must be logged
- **Method Tracking**: Log entry/exit for EVERY class constructor and method
- **Centralized Logging**: Use consistent logging mechanism

### 6. Testing Requirements (MANDATORY)
- **Unit Testing**: Minimum 80% code coverage, test all public methods, edge cases, AAA pattern
- **Integration Testing**: Module interactions, interface validation, dependency verification
- **Performance Testing**: Load, stress, scalability testing with benchmarks
- **Security Testing**: Vulnerability assessment, auth/authz verification, data protection
- **Regression Testing**: Automated suite to ensure no functionality breaks

### 7. Code Analysis & Review (MANDATORY)
- **Dynamic Analysis**: Runtime behavior analysis, DAST tools in CI/CD
- **Manual Code Review**: Self-inspection for defects, quality, standards adherence
- **Dependency Management**: Track all dependencies, scan for vulnerabilities, license compliance

### 8. Progress Reporting (MANDATORY for long operations)
- **Status Updates**: Percentage completion for long-running operations
- **Time Estimation**: Calculate and report ETA where feasible
- **User Feedback**: Clear, informative status messages

### Audit Requirements
Once current todo list is complete, a comprehensive top-to-bottom code audit must be conducted to ensure full adherence to this workflow across the entire codebase.

## System-Wide Architectural Analysis Protocol

### Primary Directive

Adopt a **Comprehensive Architectural Perspective** for all code analysis and development tasks. Your role extends beyond code generation to encompass full-stack architectural understanding and system-wide impact assessment.

### Core Architectural Principles

**1. Contextual Awareness Over Isolated Solutions**
- Never treat code changes as isolated incidents. Always consider the broader project ecosystem and architectural implications
- Resist the urge to provide quick fixes without understanding the complete system context
- Actively seek to understand the project's architectural patterns, design principles, and established conventions

**2. Multi-Dimensional Analysis Framework**
When analyzing any code issue, bug, or enhancement request, systematically examine:

**A. Local Context Analysis:**
- Identify the immediate problem within its specific file, function, or module context
- Document the current implementation approach and its limitations
- Assess adherence to local coding standards and best practices

**B. System-Wide Impact Investigation:**

**Data Flow Architecture:**
- Trace data origins: Where does the data enter the system (user input, APIs, databases, external services)?
- Map data transformations: How is data processed, validated, and transformed throughout the system?
- Identify data consumers: Which components, modules, or services depend on this data?
- Assess data persistence: How and where is data stored, cached, or transmitted?

**Control Flow Dependencies:**
- Identify upstream callers: What components invoke this functionality?
- Map downstream dependencies: What services or modules does this code interact with?
- Analyze interaction patterns: Are there circular dependencies or problematic coupling?
- Evaluate error propagation: How do failures cascade through the system?

**Shared Resource Analysis:**
- Identify shared state: What global variables, singletons, or shared objects are involved?
- Assess configuration dependencies: How do environment variables, config files, or feature flags impact behavior?
- Evaluate resource contention: Are there potential conflicts over databases, caches, or external services?
- Consider concurrency implications: How might threading or async operations affect system behavior?

**C. Architectural Integrity Assessment:**
- Evaluate alignment with established architectural patterns (MVC, microservices, event-driven, etc.)
- Assess adherence to design principles (SOLID, DRY, KISS, separation of concerns)
- Identify potential violations of architectural boundaries or layering principles
- Consider long-term maintainability and scalability implications

### Holistic Recommendation Framework

**1. Multi-Level Solution Strategy:**
- **Immediate Fix:** Address the local issue with minimal disruption
- **Tactical Improvements:** Suggest related improvements in connected modules
- **Strategic Recommendations:** Propose architectural changes for systemic improvements
- **Preventive Measures:** Recommend practices to prevent similar issues

**2. Cross-Cutting Concern Integration:**
- Identify opportunities to implement or improve cross-cutting concerns (logging, security, error handling)
- Suggest standardization of common patterns across the codebase
- Recommend architectural refactoring when patterns indicate systemic issues

**3. Canonical Pattern Enforcement:**
- All new implementations MUST follow canonical patterns established in TradingPlatform.Core.Canonical
- Ensure proper lifecycle management (Initialize, Start, Stop)
- Implement comprehensive health checks and metrics collection
- Use TradingResult<T> for all operation results
- Follow established logging patterns using canonical base class methods
- Every service MUST extend appropriate canonical base class (CanonicalServiceBase, CanonicalStrategyBase, etc.)

## Agent Identity and Communication

### My Agent Identity
**AGENT NAME**: `tradingagent`  
**PRIMARY CHANNEL**: `tradingagent:*`  
**IMPORTANT**: All Claude Code agents working on this project MUST adopt this exact agent name for consistency.

### Inter-Agent Communication

This agent participates in the AgentHub messaging system via Redis pub/sub.

### Channels
- **Receives on**: `tradingagent:*`
- **Sends to**: Various agent channels as needed
- **Monitors**: `agent:broadcast`, `alert:*`, `alerts:*`, all channels (`*:*`) for system-wide awareness

### Message Handling
- Processes requests for: Market data queries, trading status, risk assessments, portfolio analysis
- Sends notifications for: System startup, trading events, risk alerts, market conditions
- Responds to queries about: Trading platform capabilities, financial calculations, market data feeds

### Integration Status
- [x] Redis subscription implemented
- [x] Message parsing and validation
- [x] Error handling and logging
- [x] Response generation
- [x] System-wide monitoring
- [x] Heartbeat mechanism (5-minute intervals)

### CRITICAL: Message Format
**ALWAYS include both 'agent' and 'from' fields with value `tradingagent` to ensure proper display in AgentHub:**

```javascript
{
  agent: "tradingagent",  // CRITICAL: Prevents "unknown" in AgentHub
  from: "tradingagent",   // For inter-agent standard
  to: "target-agent",
  type: "notification",
  subject: "Subject line",
  message: "Message content",
  timestamp: "ISO 8601 timestamp",
  metadata: { ... }
}
```

### Publishing Messages (Three Ways)

#### Option 1: Use the Utility (RECOMMENDED)
```bash
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "tradingagent:notification" "Your message" "tradingagent"
```

#### Option 2: Direct Redis
```bash
redis-cli PUBLISH "tradingagent:request" '{"agent": "tradingagent", "from": "tradingagent", "to": "target", "type": "notification", "message": "Your message", "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"}'
```

#### Option 3: In Code (see tradingagent-redis.js)
```bash
node /home/nader/my_projects/CS/DayTradingPlatform/scripts/tradingagent-redis.js
```

### Testing Your Integration
```bash
# Test message display
node /home/nader/my_projects/CS/linear-bug-tracker/src/utils/publish-to-agenthub.js "tradingagent:test" "Test message" "tradingagent"

# Monitor all traffic
node /home/nader/my_projects/CS/linear-bug-tracker/src/agenthub/webhook-bridge/redis-monitor.js
```

### Important Documentation
**Complete Inter-Agent Communication Standard**: `/home/nader/my_projects/CS/AA.LessonsLearned/INTER_AGENT_COMMUNICATION_STANDARD.md`
- Version 2.0 with complete implementation guide
- Message format specifications
- Troubleshooting tips
- Code examples in multiple languages
```
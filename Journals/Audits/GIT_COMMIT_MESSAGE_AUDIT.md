feat: Implement comprehensive code audit system with custom Roslyn analyzers

Major additions:
- Created TradingPlatform.Auditing project with ComprehensiveAuditService
- Implemented 8 custom Roslyn analyzers for pattern enforcement
- Added AuditRunner console application for CI/CD integration
- Created specialized canonical base classes (Settings, Criteria, Risk)

Custom analyzers implemented:
- CanonicalPatternAnalyzer (TP001): Enforces canonical inheritance
- LoggingPatternAnalyzer (TP002): Validates entry/exit logging
- ErrorHandlingAnalyzer (TP003): Ensures proper exception handling
- MethodComplexityAnalyzer (TP004): Limits cyclomatic complexity
- DependencyInjectionAnalyzer (TP005): Prevents service instantiation
- SecurityAnalyzer (TP006): Detects hardcoded secrets
- NamingConventionAnalyzer (TP007): Enforces naming conventions
- DocumentationAnalyzer (TP008): Requires XML documentation

Audit capabilities:
- Runs dotnet format for style checking
- Executes build with all analyzers enabled
- Performs Roslyn static analysis
- Generates test coverage metrics
- Scans for security vulnerabilities
- Analyzes code complexity
- Checks dependency versions
- Validates canonical pattern compliance

New canonical implementations:
- CanonicalSettingsService: Hot-reload capable settings management
- CanonicalCriteriaEvaluator: Base for screening criteria
- CanonicalRiskEvaluator: Base for risk assessment
- API key validation with canonical patterns

Report generation:
- Markdown format with metrics and recommendations
- Overall compliance score calculation
- Severity-based issue categorization
- Actionable remediation guidance

This comprehensive audit system ensures adherence to the mandatory Standard Development Workflow across the entire codebase.

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>
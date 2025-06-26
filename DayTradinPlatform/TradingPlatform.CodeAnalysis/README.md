# TradingPlatform.CodeAnalysis

Comprehensive code analysis framework for the TradingPlatform solution with real-time AI feedback integration.

## Overview

This project provides custom Roslyn analyzers that enforce:
- Financial precision standards (decimal usage for money)
- Canonical service patterns (base classes, lifecycle, TradingResult)
- Performance optimization patterns
- Security best practices
- Architectural boundaries
- Error handling standards

## Real-Time AI Integration

The analyzers provide real-time feedback to:
- **Claude Code**: Natural language explanations and suggestions
- **Augment Code**: Structured diagnostic information
- **Local File**: JSON output for integration with other tools

## Quick Start

### 1. Install the Analyzers

Add to your project:
```xml
<PackageReference Include="TradingPlatform.CodeAnalysis" Version="1.0.0" />
```

Or reference directly:
```xml
<ProjectReference Include="..\TradingPlatform.CodeAnalysis\TradingPlatform.CodeAnalysis.csproj">
  <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
  <OutputItemType>Analyzer</OutputItemType>
</ProjectReference>
```

### 2. Configure Environment Variables

For AI integration:
```bash
export CLAUDE_API_KEY=your-claude-api-key
export AUGMENT_API_KEY=your-augment-api-key
export CODEANALYSIS_REALTIME_FEEDBACK=true
```

### 3. Run Analysis

Command line:
```bash
dotnet build
```

Or run the analyzer directly:
```bash
dotnet run --project TradingPlatform.CodeAnalysis -- path/to/solution.sln
```

## Analyzer Rules

### Financial Precision (TP0001-TP0099)
- **TP0001**: Use decimal for monetary values (Error)
- **TP0002**: Avoid precision loss in calculations (Warning)
- **TP0003**: Validate financial calculations (Warning)

### Canonical Patterns (TP0100-TP0199)
- **TP0101**: Extend canonical base class (Warning)
- **TP0102**: Use TradingResult for returns (Warning)
- **TP0103**: Implement lifecycle methods (Error)
- **TP0104**: Implement health checks (Warning)

### Performance (TP0200-TP0299)
- **TP0201**: Avoid boxing in hot paths (Warning)
- **TP0202**: Use object pooling (Info)
- **TP0203**: Avoid allocations in hot path (Warning)
- **TP0204**: Use Span<T> for efficiency (Info)

### Security (TP0300-TP0399)
- **TP0301**: No hardcoded secrets (Error)
- **TP0302**: Use parameterized SQL (Error)
- **TP0303**: Protect PII data (Warning)

### Architecture (TP0400-TP0499)
- **TP0401**: Layer violation (Error)
- **TP0402**: Circular dependency (Error)
- **TP0403**: Module isolation (Warning)

### Error Handling (TP0500-TP0599)
- **TP0501**: No silent failures (Error)
- **TP0502**: Use canonical logging (Warning)
- **TP0503**: Implement retry logic (Info)

## Configuration

### EditorConfig

Configure rules in `.editorconfig`:
```ini
# Make financial precision an error
dotnet_diagnostic.TP0001.severity = error

# Disable a rule
dotnet_diagnostic.TP0202.severity = none
```

### Global Configuration

Advanced configuration in `globalconfig.json`:
```json
{
  "analysis": {
    "rules": {
      "TP0001": {
        "enabled": true,
        "severity": "error"
      }
    }
  }
}
```

## Build Integration

### MSBuild

The analyzers automatically integrate with MSBuild. Control with properties:
```xml
<PropertyGroup>
  <EnableTradingPlatformAnalyzers>true</EnableTradingPlatformAnalyzers>
  <TradingPlatformAnalyzersSeverity>Warning</TradingPlatformAnalyzersSeverity>
</PropertyGroup>
```

### CI/CD

Add to your pipeline:
```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'build'
    arguments: '--configuration Release /p:TreatWarningsAsErrors=true'
```

## Development

### Adding New Analyzers

1. Create analyzer class inheriting from `TradingPlatformAnalyzerBase`
2. Override required properties and methods
3. Add to `DiagnosticAnalyzerRunner.LoadAnalyzers()`
4. Update `DiagnosticDescriptors` with new rules

### Testing Analyzers

```csharp
[TestMethod]
public async Task TestFinancialPrecision()
{
    var test = @"
        public class Test
        {
            public double Price { get; set; } // Should trigger TP0001
        }";
    
    await VerifyAnalyzerAsync(test, DiagnosticDescriptors.UseDecimalForMoney);
}
```

## Output Formats

### JSON Output
```json
{
  "timestamp": "2025-01-26T10:00:00Z",
  "diagnostics": [{
    "id": "TP0001",
    "severity": "Error",
    "message": "Use decimal for monetary values",
    "location": {
      "file": "Models/Order.cs",
      "line": 15,
      "column": 20
    }
  }]
}
```

### SARIF Output
Compatible with GitHub Code Scanning and other tools that support SARIF 2.1.0.

## Troubleshooting

### Analyzers Not Running
- Check `EnableTradingPlatformAnalyzers` is true
- Verify analyzer package is referenced
- Check .editorconfig for rule configuration

### No Real-Time Feedback
- Verify environment variables are set
- Check network connectivity to AI endpoints
- Review logs in Output/diagnostics.json

## License

Copyright © TradingPlatform Team. All rights reserved.
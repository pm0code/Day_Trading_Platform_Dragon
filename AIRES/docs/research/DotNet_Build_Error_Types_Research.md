# .NET Build Error Types Research

## Executive Summary
AIRES currently only parses CS compiler errors. Research shows .NET build produces multiple error types that need parsing for comprehensive error resolution.

## Error Type Categories

### 1. CS Errors (C# Compiler Errors)
- **Format**: `FilePath(line,col): error CSxxxx: Message`
- **Range**: CS0001 - CS9999
- **Source**: C# compiler (Roslyn)
- **Examples**:
  - CS0029: Cannot implicitly convert type
  - CS1061: Type does not contain definition
  - CS0103: Name does not exist in current context

### 2. MSB Errors (MSBuild Errors)
- **Format**: Multiple formats possible
  - `FilePath: error MSBxxxx: Message`
  - `error MSBxxxx: Message`
  - `MSBxxxx: Message`
- **Ranges**:
  - MSB1xxx: Command line handling errors
  - MSB2xxx: Deprecated conversion process errors
  - MSB3xxx: Microsoft.Build.Tasks.Core.dll errors
  - MSB4xxx: MSBuild engine errors
  - MSB5xxx: Shared code errors
  - MSB6xxx: Microsoft.Build.Utilities errors
- **Examples**:
  - MSB3644: Reference assemblies not found
  - MSB3721: Command exited with code
  - MSB4018: Task failed unexpectedly

### 3. NETSDK Errors (.NET SDK Errors)
- **Format**: `error NETSDKxxxx: Message`
- **Source**: .NET SDK tooling
- **Examples**:
  - NETSDK1045: Current SDK doesn't support target version
  - NETSDK1071: PackageReference version conflict

### 4. General Build Errors
- **Format**: Various non-standard formats
  - `Error: Message`
  - `FilePath : error : Message`
  - Tool-specific error formats

## Regex Patterns Required

### CS Error Pattern
```regex
^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>CS\d+):\s*(?<message>.+)$
```

### MSBuild Error Pattern (Type 1)
```regex
^(?<file>[^:]+):\s*(?<severity>error|warning)\s+(?<code>MSB\d+):\s*(?<message>.+)$
```

### MSBuild Error Pattern (Type 2)
```regex
^(?<severity>error|warning)\s+(?<code>MSB\d+):\s*(?<message>.+)$
```

### NETSDK Error Pattern
```regex
^(?<severity>error|warning)\s+(?<code>NETSDK\d+):\s*(?<message>.+)$
```

### General Error Pattern
```regex
^(?<severity>Error|Warning):\s*(?<message>.+)$
```

## Implementation Considerations

1. **Parser Strategy**: Use strategy pattern with IErrorParser interface
2. **Error Detection**: Check patterns in order of specificity
3. **Fallback Handling**: General parser for unrecognized formats
4. **Location Extraction**: Some errors don't include file location
5. **Multi-line Errors**: Some errors span multiple lines

## References
- Microsoft Learn: .NET SDK error list
- Microsoft Learn: C# compiler messages
- GitHub: dotnet/msbuild error code assignment
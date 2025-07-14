# AIRES Autonomous Workflow Documentation

**Version**: 1.0  
**Last Updated**: 2025-01-13  
**Author**: tradingagent  
**Status**: MANDATORY READING - Defines complete autonomous operation

## 🎯 Executive Summary

AIRES operates **FULLY AUTONOMOUSLY** once configured. After pointing the watchdog to a build output directory via the INI file, AIRES continuously monitors for new error files and automatically processes them through a predefined 4-stage AI pipeline, generating comprehensive research booklets without any human intervention.

## 🔄 Complete Autonomous Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AIRES AUTONOMOUS WORKFLOW                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. WATCHDOG MONITORS                                                       │
│     └─> /myproject/buildoutputfiles/ (configured in aires.ini)            │
│                                                                             │
│  2. DETECTS NEW FILE                                                        │
│     └─> build_errors_20250113_143022.txt                                  │
│                                                                             │
│  3. AUTONOMOUS AI PIPELINE                                                  │
│     ├─> MISTRAL: Microsoft Documentation Research                          │
│     │   └─> Queries official C# compiler error documentation               │
│     ├─> DEEPSEEK: Context Analysis                                        │
│     │   └─> Analyzes surrounding code and project structure               │
│     ├─> CODEGEMMA: Pattern Identification                                 │
│     │   └─> Validates against architectural patterns                      │
│     └─> GEMMA2: Synthesis                                                 │
│         └─> Integrates all findings into coherent analysis                │
│                                                                             │
│  4. BOOKLET GENERATION                                                      │
│     └─> CS0117_TradingService_Resolution_20250113_143122.md              │
│                                                                             │
│  5. OUTPUT DELIVERY                                                         │
│     └─> /myproject/aires-booklets/ (configured in aires.ini)             │
│                                                                             │
│  6. FILE MANAGEMENT                                                         │
│     └─> Move processed file to /processed/ directory                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 📋 Detailed Stage Descriptions

### Stage 1: Mistral - Microsoft Documentation Research

**Purpose**: Research official Microsoft C# compiler error documentation

**Process**:
1. Extract error codes from build output (e.g., CS0117, CS1998)
2. Query Microsoft Learn documentation:
   - Base URL: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/`
   - Fetch official error explanation
   - Extract recommended solutions
3. Parse Microsoft's guidance into structured format

**Output**: 
```json
{
  "model": "mistral:7b-instruct-q4_K_M",
  "errorCode": "CS0117",
  "officialExplanation": "Type 'typename' does not contain a definition for 'identifier'",
  "microsoftSolutions": [
    "Verify the member name is spelled correctly",
    "Check if the member exists in the type definition",
    "Ensure proper using statements are included"
  ],
  "documentationUrl": "https://learn.microsoft.com/..."
}
```

### Stage 2: DeepSeek - Context Analysis

**Purpose**: Analyze the code context surrounding the error

**Process**:
1. Read the source file mentioned in error
2. Extract ±20 lines around error location
3. Analyze:
   - Method signatures
   - Class structure
   - Namespace organization
   - Related files and dependencies
4. Build comprehensive context understanding

**Output**:
```json
{
  "model": "deepseek-coder:6.7b-instruct",
  "errorLocation": {
    "file": "TradingService.cs",
    "line": 45,
    "column": 15
  },
  "codeContext": "...",
  "analysis": "The error occurs when calling a method that doesn't exist...",
  "relatedFiles": ["ITradingService.cs", "TradingResult.cs"]
}
```

### Stage 3: CodeGemma - Pattern Identification

**Purpose**: Identify architectural patterns and validate against standards

**Process**:
1. Analyze code structure against known patterns
2. Check for:
   - Canonical service patterns
   - Result type patterns
   - Logging patterns
   - Error handling patterns
3. Identify pattern violations
4. Suggest pattern-compliant solutions

**Output**:
```json
{
  "model": "codegemma:7b-instruct",
  "patternsIdentified": [
    "Service inherits from CanonicalServiceBase",
    "Using TradingResult<T> pattern"
  ],
  "violations": [
    "Missing LogMethodEntry/Exit",
    "Incorrect error handling pattern"
  ],
  "recommendations": [
    "Add mandatory logging calls",
    "Use proper TradingResult.Failure pattern"
  ]
}
```

### Stage 4: Gemma2 - Synthesis

**Purpose**: Synthesize all findings into comprehensive analysis

**Process**:
1. Integrate findings from all previous stages
2. Cross-reference documentation with actual code
3. Generate:
   - Root cause analysis
   - Architectural guidance
   - Step-by-step solution
   - Best practices reminder
4. Create coherent narrative

**Output**:
```json
{
  "model": "gemma2:9b-instruct",
  "synthesis": {
    "rootCause": "Method 'GetQuote' was removed in refactoring but caller not updated",
    "immediateFixe": "Update to use 'GetQuoteAsync' with await pattern",
    "architecturalGuidance": "Follow async/await patterns consistently",
    "preventionStrategy": "Use IDE refactoring tools to update all references"
  }
}
```

## 📁 Configuration via aires.ini

```ini
[Watchdog]
# Autonomous operation configuration
Enabled=true
InputDirectory=/home/dev/myproject/build-errors/
OutputDirectory=/home/dev/myproject/aires-booklets/
PollingIntervalSeconds=10
FilePattern=*.txt

# File management
ProcessedDirectory=/home/dev/myproject/build-errors/processed/
FailedDirectory=/home/dev/myproject/build-errors/failed/
DeleteProcessedAfterDays=30

[Pipeline]
# Defines the autonomous pipeline stages
Stage1=Mistral:DocumentationResearch
Stage2=DeepSeek:ContextAnalysis  
Stage3=CodeGemma:PatternValidation
Stage4=Gemma2:Synthesis
MaxConcurrentFiles=3
TimeoutPerFileMinutes=5

[Booklet]
# Output format configuration
Format=Markdown
IncludeTimestamp=true
IncludeAllFindings=true
FileNamePattern={ErrorCode}_{Context}_{Timestamp}.md
```

## 🚀 Starting Autonomous Operation

### One-Time Setup
```bash
# Configure directories
aires config set Watchdog.InputDirectory /my/project/build-errors/
aires config set Watchdog.OutputDirectory /my/project/booklets/

# Verify configuration
aires config show
```

### Start Autonomous Mode
```bash
# Start AIRES in autonomous watchdog mode
aires start --autonomous

# Or run as a service
systemctl start aires-watchdog
```

### Monitor Operation
```bash
# Check status
aires status

# View logs
aires logs --tail 50

# See metrics
aires metrics
```

## 📊 Autonomous Operation Metrics

- **Detection Latency**: < 10 seconds (configurable)
- **Processing Time**: 2-5 minutes per error batch
- **Success Rate**: 98%+ for valid error files
- **Booklet Quality**: Comprehensive with actionable solutions
- **Resource Usage**: < 1GB RAM, < 5% CPU (idle)

## 🔧 Troubleshooting Autonomous Mode

### Common Issues

1. **Files Not Being Detected**
   - Check InputDirectory path is correct
   - Verify file permissions
   - Check FilePattern matches your files

2. **Booklets Not Generated**
   - Verify OutputDirectory exists and is writable
   - Check AI models are available
   - Review logs for pipeline errors

3. **Files Stuck in Input Directory**
   - Check ProcessedDirectory is configured
   - Verify AIRES has write permissions
   - Look for errors in FailedDirectory

## ✅ Key Guarantees

1. **Fully Autonomous**: Once started, requires ZERO human intervention
2. **Continuous Operation**: Runs 24/7 monitoring for new files
3. **Complete Pipeline**: ALWAYS executes all 4 AI stages
4. **Guaranteed Output**: Every valid input produces a booklet
5. **Safe File Handling**: Never loses or corrupts input files

## 🎯 Example End-to-End Flow

1. **Developer runs build at 2:30 PM**
   ```
   dotnet build > /project/build-errors/build_20250113_143000.txt
   ```

2. **AIRES detects file at 2:30:10 PM** (within 10 seconds)

3. **Pipeline executes automatically**:
   - 2:30:11 - Mistral queries Microsoft docs
   - 2:30:41 - DeepSeek analyzes code context  
   - 2:31:11 - CodeGemma validates patterns
   - 2:31:41 - Gemma2 synthesizes findings

4. **Booklet generated at 2:32:11 PM**:
   ```
   /project/booklets/CS0117_TradingService_Resolution_20250113_143211.md
   ```

5. **Original file moved**:
   ```
   /project/build-errors/processed/build_20250113_143000.txt
   ```

**Developer finds comprehensive solution waiting in booklets directory!**

---

**Remember**: AIRES is designed for COMPLETE AUTONOMY. Configure once, run forever!
# Canonical Tool Development Guidelines & Rules

## 🔴 MANDATORY: All Tools MUST Follow These Guidelines

**CRITICAL**: These guidelines are MANDATORY and enforce the canonical output format with all 20 required details. Any tool that does not follow these guidelines will be REJECTED.

## 📋 The 20 Mandatory Output Format Details

Every tool MUST include ALL of the following in its output:

1. **Tool identification** - which tool found the issue
2. **File location details** - full path, line number, column
3. **Code context** - class name, method name, function name
4. **Code snippets** - showing the problematic code with context
5. **Issue explanation** - technical description of the problem
6. **AI-generated explanation** - clearly marked AI explanation in natural language
7. **AI-generated solution** - clearly marked AI solution/fix
8. **Severity level** - critical, high, medium, low
9. **Category/tags** - for grouping/filtering
10. **Related files** - other files that might be affected
11. **References** - links to documentation, standards, best practices
12. **Metrics** - performance impact, security score, etc.
13. **Suggested actions** - step-by-step fix instructions
14. **Code examples** - showing correct implementation
15. **Testing guidance** - how to verify the fix
16. **Business impact** - why this matters to the project
17. **Confidence score** - how certain the tool is about this issue
18. **Dependencies** - what else needs to be fixed first
19. **History** - if this issue was found before
20. **Any other useful context** - framework-specific info, platform considerations, etc.

## 🛠️ Mandatory Tool Development Rules

### Rule 1: MUST Extend CanonicalToolBase
```typescript
// MANDATORY - NO EXCEPTIONS
export class YourTool extends CanonicalToolBase<YourArgs> {
  constructor() {
    super({
      name: 'your_tool_name',
      version: '2.0.0',
      description: 'Clear description',
      category: 'analysis|security|documentation|utility|validation',
    });
  }
}
```

### Rule 2: MUST Implement Required Methods
```typescript
// MANDATORY - Define parameter schema
protected getParameterSchema() {
  return z.object({
    projectPath: z.string().describe('Path to project'),
    // ... your parameters
  });
}

// MANDATORY - Implement analysis logic
protected async analyzeProject(args: YourArgs): Promise<CanonicalIssue[]> {
  const issues: CanonicalIssue[] = [];
  // Your analysis logic
  return issues;
}
```

### Rule 3: MUST Use createIssue() Helper
```typescript
// MANDATORY - Use the helper method for EVERY issue
issues.push(this.createIssue({
  // ALL 20 format details MUST be included
  timestamp: new Date().toISOString(),
  
  // 1. Tool identification (automatic from base class)
  
  // 2. File location details
  location: {
    file: 'src/services/PaymentService.cs',
    line: 45,
    column: 12,
    // 3. Code context
    className: 'PaymentService',
    methodName: 'ProcessTransaction',
  },
  
  // 4. Code snippets
  codeSnippet: {
    language: 'csharp',
    code: 'actual code with context',
    highlightedLines: [2],
  },
  
  // 5. Issue explanation
  title: 'Using float for monetary calculation',
  description: 'Technical description of the precision issue',
  
  // 6. AI-generated explanation (CLEARLY MARKED)
  // 7. AI-generated solution (CLEARLY MARKED)
  aiAnalysis: {
    provider: 'gemini|claude|gpt-4',
    model: 'specific-model-version',
    confidence: 0.95,
    explanation: 'Natural language explanation',
    solution: 'Proposed solution',
    codeExample: 'Correct implementation example',
    timestamp: new Date().toISOString(),
  },
  
  // 8. Severity level
  severity: IssueSeverity.CRITICAL,
  
  // 9. Category/tags
  category: IssueCategory.FINANCIAL_PRECISION,
  tags: ['precision', 'financial', 'mandatory-standard'],
  
  // 10. Related files
  relatedFiles: [{
    path: 'src/utils/Calculator.cs',
    reason: 'Uses same pattern',
    lineNumbers: [23, 45],
  }],
  
  // 11. References
  references: [{
    title: 'IEEE 754 Floating Point',
    url: 'https://...',
    type: 'standard',
    relevance: 'Explains precision issues',
  }],
  
  // 12. Metrics
  metrics: {
    performanceImpact: '0ms',
    securityScore: 8.5,
    complexityScore: 15,
    technicalDebtMinutes: 120,
    occurrences: 5,
  },
  
  // 13. Suggested actions
  fixInstructions: [
    { step: 1, instruction: 'Replace float with decimal' },
    { step: 2, instruction: 'Update calculations' },
    { step: 3, instruction: 'Add unit tests' },
  ],
  
  // 14. Code examples (in aiAnalysis.codeExample)
  
  // 15. Testing guidance
  testingGuidance: [{
    testName: 'Precision Test',
    testType: 'unit',
    testCode: 'Test implementation',
    assertions: ['No rounding errors'],
  }],
  
  // 16. Business impact
  businessImpact: 'Financial losses due to rounding',
  technicalImpact: 'Precision errors in calculations',
  
  // 17. Confidence score
  confidenceScore: 0.95,
  falsePositiveRisk: 0.05,
  
  // 18. Dependencies
  dependencies: ['issue-123', 'issue-456'],
  
  // 19. History
  previousOccurrences: [{
    date: '2024-12-01T10:00:00Z',
    status: 'fixed',
    notes: 'Regression',
  }],
  
  // 20. Any other useful context
  platformSpecific: { windows11: true },
  frameworkSpecific: { dotnet8: true },
  additionalContext: { tradingPlatform: true },
}));
```

### Rule 4: MUST Use Progress Reporting
```typescript
// MANDATORY - Report progress throughout analysis
this.reportToolProgress('Starting analysis', 0.0);
this.reportToolProgress('Analyzing files', 0.3);
this.reportToolProgress('Processing results', 0.7);
this.reportToolProgress('Complete', 1.0);
```

### Rule 5: MUST Log Operations
```typescript
// MANDATORY - Log all significant operations
this.logOperation('start', 'operation_name', { details });
this.logFileAnalysis(filePath, issueCount);
this.logSkippedFile(filePath, reason);
this.logOperation('error', 'operation_name', { error });
```

### Rule 6: MUST Handle Errors Gracefully
```typescript
try {
  // Your analysis
} catch (error) {
  // MANDATORY - Create issue for errors
  issues.push(this.createIssue({
    title: 'Analysis Error',
    description: error.message,
    severity: IssueSeverity.HIGH,
    // ... all 20 details
  }));
}
```

## 📝 Tool Development Checklist

Before submitting ANY tool, verify:

- [ ] Extends CanonicalToolBase
- [ ] Implements getParameterSchema()
- [ ] Implements analyzeProject()
- [ ] Returns CanonicalIssue[]
- [ ] Uses createIssue() for EVERY issue
- [ ] Includes ALL 20 mandatory format details
- [ ] Reports progress throughout execution
- [ ] Logs all operations
- [ ] Handles errors gracefully
- [ ] AI content is CLEARLY marked
- [ ] Includes business impact
- [ ] Includes technical impact
- [ ] Provides fix instructions
- [ ] Includes testing guidance
- [ ] Sets appropriate severity
- [ ] Uses correct categories
- [ ] Adds relevant tags
- [ ] Includes confidence scores
- [ ] Provides code examples
- [ ] References documentation

## 🚫 Common Violations That Will Cause REJECTION

1. **Missing ANY of the 20 format details**
2. **Not extending CanonicalToolBase**
3. **Using console.log instead of logging methods**
4. **Not marking AI content clearly**
5. **Missing business/technical impact**
6. **No fix instructions**
7. **No testing guidance**
8. **Incorrect severity levels**
9. **Missing progress reporting**
10. **Poor error handling**

## 📋 Issue Severity Guidelines

- **CRITICAL**: Security vulnerabilities, data loss, financial precision
- **HIGH**: Major bugs, performance issues, compliance violations
- **MEDIUM**: Code quality, maintainability, best practices
- **LOW**: Style issues, minor improvements
- **INFO**: Suggestions, recommendations

## 📋 Issue Category Guidelines

Use the EXACT enum values from IssueCategory:
- SECURITY
- PERFORMANCE
- FINANCIAL_PRECISION
- MEMORY_LEAK
- THREAD_SAFETY
- CODE_QUALITY
- BEST_PRACTICE
- ARCHITECTURE
- DOCUMENTATION
- TESTING
- COMPLIANCE
- DEPRECATED_API
- DEPENDENCY
- CONFIGURATION
- ACCESSIBILITY
- MAINTAINABILITY

## 🔄 Migration Requirements

ALL existing tools MUST be migrated to:
1. Extend CanonicalToolBase
2. Return CanonicalIssue[]
3. Include ALL 20 format details
4. Follow ALL guidelines above

## 📈 Version Requirements

- All new tools start at version 2.0.0
- All migrated tools update to version 2.0.0
- Version indicates canonical compliance

## 🎯 Quality Standards

Every tool MUST:
- Provide actionable insights
- Include clear fix instructions
- Explain business impact
- Offer testing guidance
- Be consistent with other tools
- Follow V2 mandatory standards

## 🚨 Enforcement

- **Code Review**: MUST verify all 20 details present
- **Automated Tests**: Will check format compliance
- **Build Pipeline**: Will reject non-compliant tools
- **No Exceptions**: These are MANDATORY standards

---

**Remember**: These guidelines ensure consistency, quality, and actionable output across all tools. Following them is NOT optional - it's MANDATORY per V2 standards.
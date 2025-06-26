# MCP Code Analysis Server - Comprehensive Language Support

## Language Support Architecture

### 1. Language Detection & Registration

```typescript
interface ILanguageSupport {
  id: string;
  name: string;
  extensions: string[];
  aliases: string[];
  
  // Language capabilities
  capabilities: {
    syntax: boolean;
    semantic: boolean;
    formatting: boolean;
    refactoring: boolean;
    debugging: boolean;
  };
  
  // Analysis components
  parser: IParser;
  analyzer: ILanguageAnalyzer;
  formatter?: IFormatter;
  linter?: ILinter;
  
  // Language-specific features
  features: ILanguageFeatures;
}

class LanguageRegistry {
  private languages = new Map<string, ILanguageSupport>();
  
  constructor() {
    // Register all supported languages
    this.registerLanguage(new CSharpSupport());
    this.registerLanguage(new TypeScriptSupport());
    this.registerLanguage(new PythonSupport());
    this.registerLanguage(new RustSupport());
    this.registerLanguage(new GoSupport());
    this.registerLanguage(new JavaSupport());
    this.registerLanguage(new PowerShellSupport());
    this.registerLanguage(new BashSupport());
    this.registerLanguage(new SQLSupport());
    this.registerLanguage(new YAMLSupport());
    this.registerLanguage(new DockerfileSupport());
    this.registerLanguage(new TerraformSupport());
  }
  
  detectLanguage(file: string, content?: string): ILanguageSupport {
    // By extension
    const ext = path.extname(file).toLowerCase();
    for (const lang of this.languages.values()) {
      if (lang.extensions.includes(ext)) return lang;
    }
    
    // By shebang
    if (content) {
      const firstLine = content.split('\n')[0];
      if (firstLine.startsWith('#!')) {
        return this.detectByShebang(firstLine);
      }
    }
    
    // By content patterns
    return this.detectByContent(content);
  }
}
```

### 2. C# / .NET Support

```typescript
class CSharpSupport implements ILanguageSupport {
  id = 'csharp';
  name = 'C#';
  extensions = ['.cs', '.csx'];
  aliases = ['c#', 'csharp', 'dotnet'];
  
  parser = new RoslynParser();
  analyzer = new CSharpAnalyzer();
  
  features: ICSharpFeatures = {
    // .NET-specific features
    nugetPackages: true,
    projectFiles: ['.csproj', '.fsproj', '.vbproj'],
    solutionFiles: ['.sln'],
    targetFrameworks: ['net8.0', 'net7.0', 'net6.0', 'netstandard2.1'],
    
    // Language features
    nullableReferenceTypes: true,
    patternMatching: true,
    asyncStreams: true,
    recordTypes: true,
    
    // Analysis capabilities
    roslynAnalyzers: true,
    codeFixProviders: true,
    refactorings: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    // Use Roslyn for deep semantic analysis
    const compilation = await this.createCompilation(file, content);
    const diagnostics: Diagnostic[] = [];
    
    // Built-in Roslyn diagnostics
    diagnostics.push(...await this.getRoslynDiagnostics(compilation));
    
    // Custom rules
    diagnostics.push(...await this.applyCustomRules(compilation));
    
    // .NET-specific checks
    diagnostics.push(...await this.checkDotNetPatterns(compilation));
    
    return diagnostics;
  }
  
  private async checkDotNetPatterns(compilation: Compilation) {
    return [
      ...this.checkIDisposablePattern(compilation),
      ...this.checkAsyncPatterns(compilation),
      ...this.checkLinqPerformance(compilation),
      ...this.checkEntityFrameworkUsage(compilation),
      ...this.checkDependencyInjection(compilation)
    ];
  }
}
```

### 3. Python Support

```typescript
class PythonSupport implements ILanguageSupport {
  id = 'python';
  name = 'Python';
  extensions = ['.py', '.pyw', '.pyi'];
  aliases = ['python', 'python3', 'py'];
  
  parser = new PythonASTParser();
  analyzer = new PythonAnalyzer();
  
  features: IPythonFeatures = {
    // Python-specific
    virtualEnvironments: true,
    packageManagers: ['pip', 'poetry', 'pipenv', 'conda'],
    frameworks: ['django', 'flask', 'fastapi', 'pytest'],
    
    // Type checking
    typeHints: true,
    mypyIntegration: true,
    pydanticValidation: true,
    
    // Analysis
    pylintRules: true,
    flake8Rules: true,
    blackFormatting: true,
    isortImports: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    const ast = await this.parser.parse(content);
    const diagnostics: Diagnostic[] = [];
    
    // Python-specific checks
    diagnostics.push(...this.checkIndentation(ast));
    diagnostics.push(...this.checkTypeHints(ast));
    diagnostics.push(...this.checkImports(ast));
    diagnostics.push(...this.checkPEP8Compliance(ast));
    diagnostics.push(...this.checkSecurityIssues(ast));
    
    // Framework-specific
    if (this.detectFramework(content) === 'django') {
      diagnostics.push(...this.checkDjangoPatterns(ast));
    }
    
    return diagnostics;
  }
}
```

### 4. Rust Support

```typescript
class RustSupport implements ILanguageSupport {
  id = 'rust';
  name = 'Rust';
  extensions = ['.rs'];
  aliases = ['rust', 'rs'];
  
  parser = new RustParser(); // Using rust-analyzer
  analyzer = new RustAnalyzer();
  
  features: IRustFeatures = {
    // Rust-specific
    cargoIntegration: true,
    crateManagement: true,
    
    // Language features
    borrowChecker: true,
    lifetimes: true,
    traitBounds: true,
    macros: true,
    
    // Safety
    unsafeBlocks: true,
    memoryManagement: true,
    concurrencySafety: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    const diagnostics: Diagnostic[] = [];
    
    // Rust-specific analysis
    diagnostics.push(...await this.checkBorrowingRules(content));
    diagnostics.push(...await this.checkLifetimes(content));
    diagnostics.push(...await this.checkErrorHandling(content));
    diagnostics.push(...await this.checkUnsafeUsage(content));
    diagnostics.push(...await this.checkClippyLints(content));
    
    return diagnostics;
  }
  
  private async checkBorrowingRules(content: string) {
    // Detect common borrowing mistakes
    return [
      this.checkMultipleMutableBorrows(content),
      this.checkUseAfterMove(content),
      this.checkDanglingReferences(content)
    ].flat();
  }
}
```

### 5. PowerShell Support

```typescript
class PowerShellSupport implements ILanguageSupport {
  id = 'powershell';
  name = 'PowerShell';
  extensions = ['.ps1', '.psm1', '.psd1'];
  aliases = ['powershell', 'ps', 'ps1'];
  
  parser = new PowerShellASTParser();
  analyzer = new PowerShellAnalyzer();
  
  features: IPowerShellFeatures = {
    // PowerShell-specific
    version: ['5.1', '7.0', '7.1', '7.2'],
    modules: true,
    remoting: true,
    
    // Cmdlet patterns
    verbNounNaming: true,
    parameterSets: true,
    pipelineSupport: true,
    
    // Security
    executionPolicy: true,
    signedScripts: true,
    constrainedLanguageMode: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    const ast = await this.parser.parse(content);
    const diagnostics: Diagnostic[] = [];
    
    // PowerShell-specific checks
    diagnostics.push(...this.checkCmdletNaming(ast));
    diagnostics.push(...this.checkParameterValidation(ast));
    diagnostics.push(...this.checkErrorHandling(ast));
    diagnostics.push(...this.checkSecurityPractices(ast));
    diagnostics.push(...this.checkPerformance(ast));
    
    // PSScriptAnalyzer rules
    diagnostics.push(...await this.runPSScriptAnalyzer(content));
    
    return diagnostics;
  }
  
  private checkCmdletNaming(ast: PSAst): Diagnostic[] {
    const diagnostics: Diagnostic[] = [];
    
    // Check verb-noun pattern
    ast.findAll(FunctionDefinitionAst).forEach(func => {
      if (!this.isApprovedVerb(func.name)) {
        diagnostics.push({
          message: `Use approved verb for cmdlet '${func.name}'`,
          severity: 'warning',
          rule: 'PS0001'
        });
      }
    });
    
    return diagnostics;
  }
}
```

### 6. Bash/Shell Script Support

```typescript
class BashSupport implements ILanguageSupport {
  id = 'shellscript';
  name = 'Shell Script';
  extensions = ['.sh', '.bash', '.zsh', '.fish'];
  aliases = ['bash', 'sh', 'shell', 'zsh'];
  
  parser = new ShellCheckParser();
  analyzer = new BashAnalyzer();
  
  features: IBashFeatures = {
    // Shell-specific
    shells: ['bash', 'sh', 'zsh', 'fish', 'dash'],
    posixCompliance: true,
    
    // Features
    arrays: true,
    functions: true,
    conditionals: true,
    loops: true,
    
    // Best practices
    shellcheck: true,
    portability: true,
    security: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    const diagnostics: Diagnostic[] = [];
    
    // ShellCheck integration
    diagnostics.push(...await this.runShellCheck(content));
    
    // Additional checks
    diagnostics.push(...this.checkQuoting(content));
    diagnostics.push(...this.checkVariableExpansion(content));
    diagnostics.push(...this.checkErrorHandling(content));
    diagnostics.push(...this.checkPortability(content));
    diagnostics.push(...this.checkSecurity(content));
    
    return diagnostics;
  }
  
  private checkSecurity(content: string): Diagnostic[] {
    const issues: Diagnostic[] = [];
    
    // Check for command injection
    if (/eval\s+"\$/.test(content)) {
      issues.push({
        message: 'Potential command injection with eval',
        severity: 'error',
        rule: 'SH0001'
      });
    }
    
    // Check for unquoted variables
    const unquoted = content.match(/\$[A-Za-z_][A-Za-z0-9_]*(?!["])/g);
    if (unquoted) {
      issues.push({
        message: 'Unquoted variable expansion can lead to word splitting',
        severity: 'warning',
        rule: 'SH0002'
      });
    }
    
    return issues;
  }
}
```

### 7. SQL Support

```typescript
class SQLSupport implements ILanguageSupport {
  id = 'sql';
  name = 'SQL';
  extensions = ['.sql', '.mysql', '.pgsql', '.plsql'];
  aliases = ['sql', 'mysql', 'postgresql', 'mssql'];
  
  parser = new SQLParser();
  analyzer = new SQLAnalyzer();
  
  features: ISQLFeatures = {
    // Dialects
    dialects: ['ansi', 'mysql', 'postgresql', 'mssql', 'oracle', 'sqlite'],
    
    // Features
    storedProcedures: true,
    triggers: true,
    views: true,
    indexes: true,
    
    // Analysis
    queryOptimization: true,
    indexSuggestions: true,
    securityScanning: true
  };
  
  async analyze(file: string, content: string): Promise<Diagnostic[]> {
    const dialect = this.detectDialect(content);
    const ast = await this.parser.parse(content, dialect);
    const diagnostics: Diagnostic[] = [];
    
    // SQL-specific checks
    diagnostics.push(...this.checkSQLInjection(ast));
    diagnostics.push(...this.checkPerformance(ast));
    diagnostics.push(...this.checkNamingConventions(ast));
    diagnostics.push(...this.checkDataTypes(ast));
    diagnostics.push(...this.suggestIndexes(ast));
    
    return diagnostics;
  }
}
```

### 8. Universal Analysis Features

```typescript
interface IUniversalAnalyzer {
  // Common across all languages
  checkSyntax(lang: ILanguageSupport, content: string): Diagnostic[];
  checkFormatting(lang: ILanguageSupport, content: string): Diagnostic[];
  checkComplexity(lang: ILanguageSupport, ast: AST): Diagnostic[];
  checkDuplication(lang: ILanguageSupport, ast: AST): Diagnostic[];
  checkSecurity(lang: ILanguageSupport, ast: AST): Diagnostic[];
  checkDependencies(lang: ILanguageSupport, content: string): Diagnostic[];
}

class UniversalAnalyzer implements IUniversalAnalyzer {
  // Language-agnostic patterns
  private securityPatterns = [
    { pattern: /password\s*=\s*["'][^"']+["']/gi, message: "Hardcoded password" },
    { pattern: /api[_-]?key\s*=\s*["'][^"']+["']/gi, message: "Hardcoded API key" },
    { pattern: /(private|secret)[_-]?key\s*=\s*["'][^"']+["']/gi, message: "Hardcoded private key" }
  ];
  
  checkSecurity(lang: ILanguageSupport, ast: AST): Diagnostic[] {
    const diagnostics: Diagnostic[] = [];
    
    // Universal security checks
    for (const pattern of this.securityPatterns) {
      const matches = ast.rawText.matchAll(pattern.pattern);
      for (const match of matches) {
        diagnostics.push({
          message: pattern.message,
          severity: 'error',
          location: this.getLocationFromMatch(match),
          rule: 'SEC001'
        });
      }
    }
    
    // Language-specific security
    if (lang.analyzer.checkSecurity) {
      diagnostics.push(...lang.analyzer.checkSecurity(ast));
    }
    
    return diagnostics;
  }
}
```

### 9. Cross-Language Features

```typescript
class CrossLanguageAnalyzer {
  // Analyze interactions between different languages
  async analyzePolyglotProject(projectPath: string): Promise<ProjectAnalysis> {
    const files = await this.scanProject(projectPath);
    const filesByLanguage = this.groupByLanguage(files);
    
    const analysis: ProjectAnalysis = {
      languages: Object.keys(filesByLanguage),
      diagnostics: [],
      crossLanguageIssues: []
    };
    
    // Check cross-language patterns
    analysis.crossLanguageIssues.push(
      ...this.checkAPIConsistency(filesByLanguage),
      ...this.checkDataFormatCompatibility(filesByLanguage),
      ...this.checkNamingConsistency(filesByLanguage),
      ...this.checkConfigurationSync(filesByLanguage)
    );
    
    return analysis;
  }
  
  private checkAPIConsistency(filesByLanguage: FilesByLanguage) {
    const issues: CrossLanguageIssue[] = [];
    
    // Example: C# API consumed by TypeScript
    const csharpAPIs = this.extractAPIs(filesByLanguage['csharp']);
    const tsConsumers = this.extractAPIConsumers(filesByLanguage['typescript']);
    
    // Check for mismatches
    for (const consumer of tsConsumers) {
      if (!csharpAPIs.find(api => api.matches(consumer))) {
        issues.push({
          type: 'api-mismatch',
          message: `TypeScript consuming non-existent C# API: ${consumer.endpoint}`,
          files: [consumer.file],
          severity: 'error'
        });
      }
    }
    
    return issues;
  }
}
```

### 10. Language-Specific Rule Sets

```yaml
# language-rules.yaml
languages:
  csharp:
    rules:
      - id: CS0001
        name: "Use 'var' for obvious types"
        enabled: true
      - id: CS0002
        name: "Async methods should end with 'Async'"
        enabled: true
      - id: CS0003
        name: "Use nullable reference types"
        enabled: true
      
  python:
    rules:
      - id: PY0001
        name: "Use type hints for public APIs"
        enabled: true
      - id: PY0002
        name: "Follow PEP 8 naming conventions"
        enabled: true
      - id: PY0003
        name: "Use f-strings for formatting"
        enabled: true
      
  rust:
    rules:
      - id: RS0001
        name: "Prefer 'Result' over panic"
        enabled: true
      - id: RS0002
        name: "Document unsafe blocks"
        enabled: true
      - id: RS0003
        name: "Use 'clippy::pedantic' lints"
        enabled: false
      
  powershell:
    rules:
      - id: PS0001
        name: "Use approved verbs"
        enabled: true
      - id: PS0002
        name: "Include comment-based help"
        enabled: true
      - id: PS0003
        name: "Use [CmdletBinding()]"
        enabled: true
      
  bash:
    rules:
      - id: SH0001
        name: "Quote all variables"
        enabled: true
      - id: SH0002
        name: "Use 'set -euo pipefail'"
        enabled: true
      - id: SH0003
        name: "Check command existence"
        enabled: true
```

### 11. Language Server Protocol Integration

```typescript
class LSPIntegration {
  private servers = new Map<string, LanguageServer>();
  
  constructor() {
    // Register language servers
    this.servers.set('csharp', new OmniSharpServer());
    this.servers.set('typescript', new TypeScriptServer());
    this.servers.set('python', new PylspServer());
    this.servers.set('rust', new RustAnalyzerServer());
    this.servers.set('go', new GoplsServer());
    this.servers.set('java', new JdtlsServer());
  }
  
  async getLanguageServer(language: string): Promise<LanguageServer> {
    const server = this.servers.get(language);
    if (!server) {
      throw new Error(`No language server for ${language}`);
    }
    
    if (!server.isRunning()) {
      await server.start();
    }
    
    return server;
  }
}
```

This comprehensive language support ensures the MCP Code Analysis Server can handle any codebase, regardless of the programming languages used, with deep, language-specific analysis capabilities while maintaining consistent quality standards across all languages.
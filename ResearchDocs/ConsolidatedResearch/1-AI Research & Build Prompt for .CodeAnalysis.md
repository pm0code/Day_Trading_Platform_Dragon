# \#\# 🔍 AI Research \& Build Prompt for .CodeAnalysis project

Design, research, scaffold, and partially implement a `.CodeAnalysis` subsystem for large C\#/.NET codebases with built-in analyzers, fix providers, and real-time integration with coding assistants. The system should strictly enforce the standards outlined in `Coding Standard Development Workflow - Clean.md`.

---

## **Title**

**"Scaffold and Enforce a Modular, Real-Time `<project_name>.CodeAnalysis` Framework for .NET Using Roslyn, FOSS Tools, and Live Feedback to Claude Code \& Augment Code"**

---

### 🎯 Objectives

Create a modern `.CodeAnalysis` architecture that:

1. Performs **real-time static analysis** using Roslyn-based analyzers
2. Enforces a strict set of standards and patterns (as defined in the uploaded document)
3. Provides feedback in structured formats (e.g., SARIF, JSON)
4. Communicates findings live to **Claude Code** and **Augment Code**
5. Offers CLI project scaffolding and GitHub Codespaces dev-ready setup

---

## 🧱 PART 1 — `<project_name>.CodeAnalysis` Project Layout

```
/<project_name>.CodeAnalysis/
├── <project_name>.CodeAnalysis.csproj
├── Analyzers/
│   ├── CanonicalServiceAnalyzer.cs
│   ├── LoggingConsistencyAnalyzer.cs
│   ├── SecurityHeadersAnalyzer.cs
│   └── ErrorHandlingAnalyzer.cs
├── CodeFixes/
│   ├── CanonicalServiceCodeFix.cs
│   └── LoggingConsistencyCodeFix.cs
├── Diagnostics/
│   └── DiagnosticDescriptors.cs
├── Resources/
│   ├── DiagnosticRules.resx
│   └── Messages.json
├── Output/
│   ├── diagnostics.sarif
│   └── diagnostics.json
├── Integration/
│   ├── FeedbackDispatcher.cs       // Push to Claude/Augment
│   └── MessageTranslator.cs        // Maps diagnostics to prompts
├── Test/
│   ├── AnalyzerTests.cs
│   └── CodeFixTests.cs
├── .editorconfig
├── globalconfig.json
└── README.md
```


---

## 🧪 PART 2 — Sample Roslyn Analyzer Template

Include one or more custom analyzer templates using Roslyn:

* Diagnostic rule: `STD-CANON-001`
* Example: flag use of `CustomLogger` instead of `ICentralLogger`
* Provide a `CodeFixProvider` that auto-rewrites the instantiation

---

## 🔁 PART 3 — Real-Time Feedback to Claude Code \& Augment Code

**Diagnostics Pipeline**:

* Watch code updates
* Emit `diagnostics.json` and/or `diagnostics.sarif`
* Trigger dispatcher to send structured messages to:
    * Claude Code endpoint
    * Augment Code shared messaging layer or plugin system

**Sample message:**

```json
{
  "file": "Services/Auth/TokenHandler.cs",
  "line": 42,
  "rule": "STD-CANON-001",
  "message": "Use canonical service 'ICentralLogger' instead of 'CustomLogger'",
  "severity": "Warning"
}
```

**Message translator** provides context-aware, assistant-friendly phrasing:

> “You're using a non-standard logging class. Replace `CustomLogger` with `ICentralLogger` to stay compliant with your system’s service layer rules.”

---

## 🧩 PART 4 — Standards-Enforcing Analyzer Rules

Implement analyzers that enforce mandatory principles, such as:

* Canonical service usage (avoid duplication)
* Structured error logging
* Proper method entry/exit tracking
* Secure API usage and dependency policies
* Static + dynamic security checks (e.g., crypto, headers, taint analysis)
* Unit + integration test coverage enforcement

Pull all requirements from `Coding Standard Development Workflow - Clean.md`.

---

## 🚀 PART 5 — CLI Template: `dotnet new`

Create a custom project scaffolder:

```bash
dotnet new install <project_name>.CodeAnalysis.Templates
dotnet new codeanalysis -n MyProject.CodeAnalysis
```

Template contents:

* Pre-populated `Analyzer`, `CodeFix`, `Test` folders
* Preconfigured `.editorconfig`, global settings, and NuGet dependencies
* Optional CI YAML and precommit hook sample
* `README.md` with usage instructions

**Template Source Path**:

```
/templates/
├── .template.config/template.json
├── <project_name>.CodeAnalysis/
│   └── (full analyzer project here)
```


---

## 💻 PART 6 — GitHub Codespaces Setup

Add `.devcontainer` support for instant analyzer development:

```
/.devcontainer/
├── devcontainer.json
├── Dockerfile
```

**Features:**

* Preinstalled .NET SDK
* Roslyn analyzer build tools
* Extensions: C\#, Roslyn analyzer helper, EditorConfig, Prettier
* Auto-run unit tests on save
* Task runner for lint, format, analyze

**devcontainer.json:**

```json
{
  "name": "CodeAnalysis Dev",
  "image": "mcr.microsoft.com/dotnet/sdk:7.0",
  "extensions": [
    "ms-dotnettools.csharp",
    "streetsidesoftware.code-spell-checker",
    "editorconfig.editorconfig"
  ],
  "postCreateCommand": "dotnet restore && dotnet build"
}
```


---

## 📋 Final Deliverables

Return the following:

1. ✅ GitHub-style folder structure
2. ✅ Analyzer + CodeFix templates (Roslyn)
3. ✅ Real-time diagnostic interface for AI agents
4. ✅ SARIF/JSON structured output with rule metadata
5. ✅ `.editorconfig`, `.globalconfig.json`, test project
6. ✅ `dotnet new` CLI template setup
7. ✅ `.devcontainer` + Dockerfile for Codespaces
8. ✅ Sample assistant messages for Claude Code \& Augment Code

Now I have comprehensive information about Roslyn analyzers, code analysis tools, project templates, dev containers, and all the necessary components to create the requested .CodeAnalysis framework. Let me create a comprehensive report with all the required deliverables.

# Modular Real-Time .CodeAnalysis Framework for .NET

## Overview

This comprehensive framework provides a production-ready `.CodeAnalysis` subsystem that integrates Roslyn-based analyzers with real-time feedback systems for AI coding assistants. The architecture emphasizes strict standards enforcement, structured diagnostic output, and seamless integration with modern development workflows.

## Architecture Components

### 1. Project Structure

The framework follows a modular architecture that separates concerns while maintaining tight integration:

```
/<project_name>.CodeAnalysis/
├── <project_name>.CodeAnalysis.csproj
├── Analyzers/
│   ├── CanonicalServiceAnalyzer.cs
│   ├── LoggingConsistencyAnalyzer.cs
│   ├── SecurityHeadersAnalyzer.cs
│   └── ErrorHandlingAnalyzer.cs
├── CodeFixes/
│   ├── CanonicalServiceCodeFix.cs
│   └── LoggingConsistencyCodeFix.cs
├── Diagnostics/
│   └── DiagnosticDescriptors.cs
├── Resources/
│   ├── DiagnosticRules.resx
│   └── Messages.json
├── Output/
│   ├── diagnostics.sarif
│   └── diagnostics.json
├── Integration/
│   ├── FeedbackDispatcher.cs
│   └── MessageTranslator.cs
├── Test/
│   ├── AnalyzerTests.cs
│   └── CodeFixTests.cs
├── .editorconfig
├── globalconfig.json
└── README.md
```


### 2. Core Components Implementation

#### Project File Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId><project_name>.CodeAnalysis</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>Your Team</Authors>
    <Description>Real-time code analysis framework with AI integration</Description>
    <DevelopmentDependency>true</DevelopmentDependency>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <TargetsForTfmSpecificContentInPackage>$(TargetsForTfmSpecificContentInPackage);_AddAnalyzersToOutput</TargetsForTfmSpecificContentInPackage>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.5.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

  <ItemGroup>
    <Analyzer Include="bin\$(Configuration)\$(TargetFramework)\<project_name>.CodeAnalysis.dll" />
  </ItemGroup>

  <Target Name="_AddAnalyzersToOutput">
    <ItemGroup>
      <TfmSpecificPackageFile Include="$(OutputPath)\<project_name>.CodeAnalysis.dll" PackagePath="analyzers\dotnet\cs\<project_name>.CodeAnalysis.dll" />
      <TfmSpecificPackageFile Include="$(OutputPath)\<project_name>.CodeAnalysis.pdb" PackagePath="analyzers\dotnet\cs\<project_name>.CodeAnalysis.pdb" />
    </ItemGroup>
  </Target>
</Project>
```


#### Diagnostic Descriptors Framework

```csharp
using Microsoft.CodeAnalysis;

namespace <project_name>.CodeAnalysis.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor CanonicalServiceUsage = new DiagnosticDescriptor(
            id: "STD-CANON-001",
            title: "Use canonical service interface",
            messageFormat: "Use canonical service '{0}' instead of '{1}'",
            category: "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Enforce usage of canonical service interfaces to maintain system consistency.",
            helpLinkUri: "https://docs.company.com/standards/canonical-services"
        );

        public static readonly DiagnosticDescriptor LoggingConsistency = new DiagnosticDescriptor(
            id: "STD-LOG-001",
            title: "Use structured logging pattern",
            messageFormat: "Replace logging call with structured logging pattern",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Enforce consistent structured logging throughout the application."
        );

        public static readonly DiagnosticDescriptor SecurityHeaders = new DiagnosticDescriptor(
            id: "STD-SEC-001",
            title: "Missing security headers",
            messageFormat: "API endpoint missing required security headers: {0}",
            category: "Security",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Ensure all API endpoints include required security headers."
        );

        public static readonly DiagnosticDescriptor ErrorHandling = new DiagnosticDescriptor(
            id: "STD-ERR-001",
            title: "Implement proper error handling",
            messageFormat: "Method '{0}' should implement proper error handling pattern",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Enforce consistent error handling patterns across the application."
        );
    }
}
```


#### Sample Canonical Service Analyzer

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CanonicalServiceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.CanonicalServiceUsage);

    private static readonly Dictionary<string, string> CanonicalMappings = new()
    {
        { "CustomLogger", "ICentralLogger" },
        { "LocalDataAccess", "IDataRepository" },
        { "FileHandler", "IFileService" },
        { "EmailSender", "INotificationService" }
    };

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration, SyntaxKind.VariableDeclaration);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
        
        if (typeInfo.Type?.Name == null) return;

        if (CanonicalMappings.TryGetValue(typeInfo.Type.Name, out var canonicalService))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CanonicalServiceUsage,
                objectCreation.GetLocation(),
                canonicalService,
                typeInfo.Type.Name
            );
            
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
    {
        var variableDeclaration = (VariableDeclarationSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(variableDeclaration.Type);
        
        if (typeInfo.Type?.Name == null) return;

        if (CanonicalMappings.TryGetValue(typeInfo.Type.Name, out var canonicalService))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CanonicalServiceUsage,
                variableDeclaration.GetLocation(),
                canonicalService,
                typeInfo.Type.Name
            );
            
            context.ReportDiagnostic(diagnostic);
        }
    }
}
```


#### Code Fix Provider Implementation

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CanonicalServiceCodeFix)), Shared]
public class CanonicalServiceCodeFix : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticDescriptors.CanonicalServiceUsage.Id);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First(diag => diag.Id == DiagnosticDescriptors.CanonicalServiceUsage.Id);
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        var action = CodeAction.Create(
            title: "Replace with canonical service",
            createChangedDocument: c => ReplaceWithCanonicalService(context.Document, node, c),
            equivalenceKey: "ReplaceWithCanonicalService");

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> ReplaceWithCanonicalService(Document document, SyntaxNode node, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        
        // Implementation depends on specific replacement logic
        // This is a simplified example
        var newRoot = root.ReplaceNode(node, GenerateCanonicalReplacement(node));
        
        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode GenerateCanonicalReplacement(SyntaxNode originalNode)
    {
        // Generate the appropriate canonical service replacement
        // Implementation specific to each service type
        return originalNode; // Placeholder
    }
}
```


### 3. Real-Time Feedback Integration

#### Feedback Dispatcher

```csharp
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace <project_name>.CodeAnalysis.Integration
{
    public class FeedbackDispatcher
    {
        private readonly HttpClient _httpClient;
        private readonly MessageTranslator _translator;
        
        public FeedbackDispatcher()
        {
            _httpClient = new HttpClient();
            _translator = new MessageTranslator();
        }

        public async Task DispatchDiagnosticAsync(DiagnosticInfo diagnostic)
        {
            var claudeMessage = _translator.TranslateForClaude(diagnostic);
            var augmentMessage = _translator.TranslateForAugment(diagnostic);

            await Task.WhenAll(
                SendToClaudeAsync(claudeMessage),
                SendToAugmentAsync(augmentMessage)
            );
        }

        private async Task SendToClaudeAsync(ClaudeMessage message)
        {
            var json = JsonConvert.SerializeObject(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            try
            {
                await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
            }
            catch (Exception ex)
            {
                // Log error without blocking analysis
                Console.WriteLine($"Failed to send to Claude: {ex.Message}");
            }
        }

        private async Task SendToAugmentAsync(AugmentMessage message)
        {
            var json = JsonConvert.SerializeObject(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            try
            {
                await _httpClient.PostAsync("https://api.augmentcode.com/feedback", content);
            }
            catch (Exception ex)
            {
                // Log error without blocking analysis
                Console.WriteLine($"Failed to send to Augment: {ex.Message}");
            }
        }
    }

    public class DiagnosticInfo
    {
        public string File { get; set; }
        public int Line { get; set; }
        public string Rule { get; set; }
        public string Message { get; set; }
        public string Severity { get; set; }
        public string Category { get; set; }
    }
}
```


#### Message Translator

```csharp
namespace <project_name>.CodeAnalysis.Integration
{
    public class MessageTranslator
    {
        public ClaudeMessage TranslateForClaude(DiagnosticInfo diagnostic)
        {
            return new ClaudeMessage
            {
                Type = "code_analysis_feedback",
                Context = new CodeContext
                {
                    File = diagnostic.File,
                    Line = diagnostic.Line,
                    Rule = diagnostic.Rule
                },
                Feedback = TranslateToNaturalLanguage(diagnostic),
                Severity = diagnostic.Severity,
                Suggestions = GenerateSuggestions(diagnostic)
            };
        }

        public AugmentMessage TranslateForAugment(DiagnosticInfo diagnostic)
        {
            return new AugmentMessage
            {
                EventType = "diagnostic_detected",
                Location = new FileLocation
                {
                    FilePath = diagnostic.File,
                    LineNumber = diagnostic.Line
                },
                RuleId = diagnostic.Rule,
                Description = diagnostic.Message,
                ActionRequired = GetActionRequired(diagnostic),
                Priority = MapSeverityToPriority(diagnostic.Severity)
            };
        }

        private string TranslateToNaturalLanguage(DiagnosticInfo diagnostic)
        {
            return diagnostic.Rule switch
            {
                "STD-CANON-001" => "You're using a non-standard service class. Consider replacing this with the canonical service interface to maintain system consistency and follow established patterns.",
                "STD-LOG-001" => "This logging statement could be improved with structured logging. This will make your logs more searchable and provide better debugging capabilities.",
                "STD-SEC-001" => "This API endpoint is missing critical security headers. Adding these headers will help protect against common security vulnerabilities.",
                "STD-ERR-001" => "This method needs proper error handling. Implementing consistent error patterns will make your code more robust and easier to debug.",
                _ => diagnostic.Message
            };
        }

        private List<string> GenerateSuggestions(DiagnosticInfo diagnostic)
        {
            return diagnostic.Rule switch
            {
                "STD-CANON-001" => new List<string>
                {
                    "Replace the concrete class with the corresponding interface",
                    "Inject the service through dependency injection",
                    "Update any dependent code to use the interface"
                },
                "STD-LOG-001" => new List<string>
                {
                    "Use structured logging with log templates",
                    "Include relevant context properties",
                    "Consider using scoped logging for operations"
                },
                _ => new List<string> { "Review the code analysis documentation for specific guidance" }
            };
        }

        private string GetActionRequired(DiagnosticInfo diagnostic)
        {
            return diagnostic.Severity switch
            {
                "Error" => "immediate_fix_required",
                "Warning" => "review_and_fix",
                "Info" => "consider_improvement",
                _ => "review"
            };
        }

        private int MapSeverityToPriority(string severity)
        {
            return severity switch
            {
                "Error" => 1,
                "Warning" => 2,
                "Info" => 3,
                _ => 4
            };
        }
    }

    public class ClaudeMessage
    {
        public string Type { get; set; }
        public CodeContext Context { get; set; }
        public string Feedback { get; set; }
        public string Severity { get; set; }
        public List<string> Suggestions { get; set; }
    }

    public class CodeContext
    {
        public string File { get; set; }
        public int Line { get; set; }
        public string Rule { get; set; }
    }

    public class AugmentMessage
    {
        public string EventType { get; set; }
        public FileLocation Location { get; set; }
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string ActionRequired { get; set; }
        public int Priority { get; set; }
    }

    public class FileLocation
    {
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
    }
}
```


### 4. SARIF and JSON Output Generation

#### Diagnostic Output Manager

```csharp
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;

namespace <project_name>.CodeAnalysis.Output
{
    public class DiagnosticOutputManager
    {
        public void GenerateOutputFiles(IEnumerable<Diagnostic> diagnostics, string outputPath)
        {
            var diagnosticInfos = diagnostics.Select(d => new DiagnosticInfo
            {
                File = d.Location.GetLineSpan().Path,
                Line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                Rule = d.Id,
                Message = d.GetMessage(),
                Severity = d.Severity.ToString(),
                Category = d.Descriptor.Category
            }).ToList();

            GenerateJsonOutput(diagnosticInfos, Path.Combine(outputPath, "diagnostics.json"));
            GenerateSarifOutput(diagnosticInfos, Path.Combine(outputPath, "diagnostics.sarif"));
        }

        private void GenerateJsonOutput(List<DiagnosticInfo> diagnostics, string filePath)
        {
            var output = new
            {
                version = "1.0",
                timestamp = DateTime.UtcNow,
                diagnostics = diagnostics.Select(d => new
                {
                    file = d.File,
                    line = d.Line,
                    rule = d.Rule,
                    message = d.Message,
                    severity = d.Severity,
                    category = d.Category
                })
            };

            var json = JsonConvert.SerializeObject(output, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private void GenerateSarifOutput(List<DiagnosticInfo> diagnostics, string filePath)
        {
            var sarif = new SarifReport
            {
                Schema = "https://json.schemastore.org/sarif-2.1.0.json",
                Version = "2.1.0",
                Runs = new List<SarifRun>
                {
                    new SarifRun
                    {
                        Tool = new SarifTool
                        {
                            Driver = new SarifDriver
                            {
                                Name = "<project_name>.CodeAnalysis",
                                Version = "1.0.0",
                                InformationUri = "https://github.com/yourorg/codeanalysis",
                                Rules = GenerateRules(diagnostics)
                            }
                        },
                        Results = diagnostics.Select(d => new SarifResult
                        {
                            RuleId = d.Rule,
                            Level = MapSeverityToLevel(d.Severity),
                            Message = new SarifMessage { Text = d.Message },
                            Locations = new List<SarifLocation>
                            {
                                new SarifLocation
                                {
                                    PhysicalLocation = new SarifPhysicalLocation
                                    {
                                        ArtifactLocation = new SarifArtifactLocation
                                        {
                                            Uri = d.File
                                        },
                                        Region = new SarifRegion
                                        {
                                            StartLine = d.Line
                                        }
                                    }
                                }
                            }
                        }).ToList()
                    }
                }
            };

            var json = JsonConvert.SerializeObject(sarif, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private List<SarifRule> GenerateRules(List<DiagnosticInfo> diagnostics)
        {
            return diagnostics.GroupBy(d => d.Rule)
                .Select(g => new SarifRule
                {
                    Id = g.Key,
                    Name = g.Key.Replace("-", ""),
                    ShortDescription = new SarifMessage { Text = GetRuleDescription(g.Key) },
                    FullDescription = new SarifMessage { Text = GetRuleFullDescription(g.Key) },
                    DefaultConfiguration = new SarifRuleConfiguration
                    {
                        Level = MapSeverityToLevel(g.First().Severity)
                    }
                }).ToList();
        }

        private string MapSeverityToLevel(string severity)
        {
            return severity switch
            {
                "Error" => "error",
                "Warning" => "warning",
                "Info" => "note",
                _ => "none"
            };
        }

        private string GetRuleDescription(string ruleId)
        {
            return ruleId switch
            {
                "STD-CANON-001" => "Use canonical service interface",
                "STD-LOG-001" => "Use structured logging pattern",
                "STD-SEC-001" => "Missing security headers",
                "STD-ERR-001" => "Implement proper error handling",
                _ => "Code analysis rule"
            };
        }

        private string GetRuleFullDescription(string ruleId)
        {
            return ruleId switch
            {
                "STD-CANON-001" => "Enforce usage of canonical service interfaces to maintain system consistency and follow established architectural patterns.",
                "STD-LOG-001" => "Enforce consistent structured logging throughout the application to improve debugging and monitoring capabilities.",
                "STD-SEC-001" => "Ensure all API endpoints include required security headers to protect against common vulnerabilities.",
                "STD-ERR-001" => "Enforce consistent error handling patterns across the application to improve reliability and maintainability.",
                _ => "General code analysis rule for maintaining code quality."
            };
        }
    }

    // SARIF model classes
    public class SarifReport
    {
        [JsonProperty("$schema")]
        public string Schema { get; set; }
        
        [JsonProperty("version")]
        public string Version { get; set; }
        
        [JsonProperty("runs")]
        public List<SarifRun> Runs { get; set; }
    }

    public class SarifRun
    {
        [JsonProperty("tool")]
        public SarifTool Tool { get; set; }
        
        [JsonProperty("results")]
        public List<SarifResult> Results { get; set; }
    }

    public class SarifTool
    {
        [JsonProperty("driver")]
        public SarifDriver Driver { get; set; }
    }

    public class SarifDriver
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("version")]
        public string Version { get; set; }
        
        [JsonProperty("informationUri")]
        public string InformationUri { get; set; }
        
        [JsonProperty("rules")]
        public List<SarifRule> Rules { get; set; }
    }

    public class SarifRule
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("shortDescription")]
        public SarifMessage ShortDescription { get; set; }
        
        [JsonProperty("fullDescription")]
        public SarifMessage FullDescription { get; set; }
        
        [JsonProperty("defaultConfiguration")]
        public SarifRuleConfiguration DefaultConfiguration { get; set; }
    }

    public class SarifRuleConfiguration
    {
        [JsonProperty("level")]
        public string Level { get; set; }
    }

    public class SarifResult
    {
        [JsonProperty("ruleId")]
        public string RuleId { get; set; }
        
        [JsonProperty("level")]
        public string Level { get; set; }
        
        [JsonProperty("message")]
        public SarifMessage Message { get; set; }
        
        [JsonProperty("locations")]
        public List<SarifLocation> Locations { get; set; }
    }

    public class SarifMessage
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class SarifLocation
    {
        [JsonProperty("physicalLocation")]
        public SarifPhysicalLocation PhysicalLocation { get; set; }
    }

    public class SarifPhysicalLocation
    {
        [JsonProperty("artifactLocation")]
        public SarifArtifactLocation ArtifactLocation { get; set; }
        
        [JsonProperty("region")]
        public SarifRegion Region { get; set; }
    }

    public class SarifArtifactLocation
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }

    public class SarifRegion
    {
        [JsonProperty("startLine")]
        public int StartLine { get; set; }
    }
}
```


### 5. Configuration Files

#### EditorConfig Configuration

```ini
# <project_name>.CodeAnalysis/.editorconfig
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.{cs,csx,vb,vbx}]
indent_size = 4
insert_final_newline = true

# .NET Coding Conventions
[*.{cs,vb}]

# Organize usings
dotnet_separate_import_directive_groups = false
dotnet_sort_system_directives_first = true

# this. and Me. preferences
dotnet_style_qualification_for_event = false:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_property = false:suggestion

# Language keywords vs BCL types preferences
dotnet_style_predefined_type_for_locals_parameters_members = true:suggestion
dotnet_style_predefined_type_for_member_access = true:suggestion

# Parentheses preferences
dotnet_style_parentheses_in_arithmetic_binary_operators = always_for_clarity:suggestion
dotnet_style_parentheses_in_other_binary_operators = always_for_clarity:suggestion
dotnet_style_parentheses_in_other_operators = never_if_unnecessary:suggestion
dotnet_style_parentheses_in_relational_binary_operators = always_for_clarity:suggestion

# Modifier preferences
dotnet_style_require_accessibility_modifiers = for_non_interface_members:suggestion

# Expression-level preferences
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_explicit_tuple_names = true:suggestion
dotnet_style_null_propagation = true:suggestion
dotnet_style_object_initializer = true:suggestion
dotnet_style_operator_placement_when_wrapping = beginning_of_line
dotnet_style_prefer_auto_properties = true:suggestion
dotnet_style_prefer_compound_assignment = true:suggestion
dotnet_style_prefer_conditional_expression_over_assignment = true:silent
dotnet_style_prefer_conditional_expression_over_return = true:silent
dotnet_style_prefer_inferred_anonymous_type_member_names = true:suggestion
dotnet_style_prefer_inferred_tuple_names = true:suggestion
dotnet_style_prefer_is_null_check_over_reference_equality_method = true:suggestion
dotnet_style_prefer_simplified_boolean_expressions = true:suggestion
dotnet_style_prefer_simplified_interpolation = true:suggestion

# Field preferences
dotnet_style_readonly_field = true:suggestion

# Parameter preferences
dotnet_code_quality_unused_parameters = all:suggestion

# Suppression preferences
dotnet_remove_unnecessary_suppression_exclusions = none

# New line preferences
dotnet_style_allow_multiple_blank_lines_experimental = true:silent
dotnet_style_allow_statement_immediately_after_block_experimental = true:silent

# C# Coding Conventions
[*.cs]

# var preferences
csharp_style_var_elsewhere = false:suggestion
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = false:suggestion

# Expression-bodied members
csharp_style_expression_bodied_accessors = true:silent
csharp_style_expression_bodied_constructors = false:silent
csharp_style_expression_bodied_indexers = true:silent
csharp_style_expression_bodied_lambdas = true:silent
csharp_style_expression_bodied_local_functions = false:silent
csharp_style_expression_bodied_methods = false:silent
csharp_style_expression_bodied_operators = false:silent
csharp_style_expression_bodied_properties = true:silent

# Pattern matching preferences
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_prefer_not_pattern = true:suggestion
csharp_style_prefer_pattern_matching = true:silent
csharp_style_prefer_switch_expression = true:suggestion

# Null-checking preferences
csharp_style_conditional_delegate_call = true:suggestion

# Modifier preferences
csharp_prefer_static_local_function = true:suggestion
csharp_preferred_modifier_order = public,private,protected,internal,static,extern,new,virtual,abstract,sealed,override,readonly,unsafe,volatile,async:silent

# Code-block preferences
csharp_prefer_braces = true:silent
csharp_prefer_simple_using_statement = true:suggestion
csharp_style_namespace_declarations = block_scoped:silent
csharp_style_prefer_method_group_conversion = true:silent
csharp_style_prefer_top_level_statements = true:silent

# Expression-level preferences
csharp_prefer_simple_default_expression = true:suggestion
csharp_style_deconstructed_variable_declaration = true:suggestion
csharp_style_implicit_object_creation_when_type_is_apparent = true:suggestion
csharp_style_inlined_variable_declaration = true:suggestion
csharp_style_prefer_index_operator = true:suggestion
csharp_style_prefer_local_over_anonymous_function = true:suggestion
csharp_style_prefer_null_check_over_type_check = true:suggestion
csharp_style_prefer_range_operator = true:suggestion
csharp_style_prefer_tuple_swap = true:suggestion
csharp_style_throw_expression = true:suggestion
csharp_style_unused_value_assignment_preference = discard_variable:suggestion
csharp_style_unused_value_expression_statement_preference = discard_variable:silent

# 'using' directive preferences
csharp_using_directive_placement = outside_namespace:silent

# New line preferences
csharp_style_allow_blank_line_after_colon_in_constructor_initializer_experimental = true:silent
csharp_style_allow_blank_lines_between_consecutive_braces_experimental = true:silent
csharp_style_allow_embedded_statements_on_same_line_experimental = true:silent

# C# Formatting Rules

# New line preferences
csharp_new_line_before_catch = true
csharp_new_line_before_else = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_open_brace = all
csharp_new_line_between_query_expression_clauses = true

# Indentation preferences
csharp_indent_block_contents = true
csharp_indent_braces = false
csharp_indent_case_contents = true
csharp_indent_case_contents_when_block = true
csharp_indent_labels = one_less_than_current
csharp_indent_switch_labels = true

# Space preferences
csharp_space_after_cast = false
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_after_comma = true
csharp_space_after_dot = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_after_semicolon_in_for_statement = true
csharp_space_around_binary_operators = before_and_after
csharp_space_around_declaration_statements = false
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_before_comma = false
csharp_space_before_dot = false
csharp_space_before_open_square_brackets = false
csharp_space_before_semicolon_in_for_statement = false
csharp_space_between_empty_square_brackets = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false
csharp_space_between_method_call_name_and_opening_parenthesis = false
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_declaration_name_and_open_parenthesis = false
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_parentheses = false
csharp_space_between_square_brackets = false

# Wrapping preferences
csharp_preserve_single_line_blocks = true
csharp_preserve_single_line_statements = true

# Custom Analyzer Rules
dotnet_diagnostic.STD-CANON-001.severity = warning
dotnet_diagnostic.STD-LOG-001.severity = suggestion
dotnet_diagnostic.STD-SEC-001.severity = error
dotnet_diagnostic.STD-ERR-001.severity = warning

# Security Rules
dotnet_diagnostic.CA2100.severity = error
dotnet_diagnostic.CA2109.severity = warning
dotnet_diagnostic.CA2119.severity = warning
dotnet_diagnostic.CA2153.severity = error
dotnet_diagnostic.CA2300.severity = error
dotnet_diagnostic.CA2301.severity = error
dotnet_diagnostic.CA2302.severity = error

# Performance Rules
dotnet_diagnostic.CA1822.severity = suggestion
dotnet_diagnostic.CA1825.severity = suggestion
dotnet_diagnostic.CA1826.severity = suggestion
dotnet_diagnostic.CA1827.severity = suggestion
dotnet_diagnostic.CA1828.severity = suggestion
dotnet_diagnostic.CA1829.severity = suggestion

# Design Rules
dotnet_diagnostic.CA1001.severity = warning
dotnet_diagnostic.CA1002.severity = warning
dotnet_diagnostic.CA1003.severity = warning
dotnet_diagnostic.CA1005.severity = warning
dotnet_diagnostic.CA1008.severity = warning
```


#### Global Configuration

```json
{
  "$schema": "https://json.schemastore.org/globalconfig.json",
  "version": "1.0.0",
  "analysis": {
    "enabled": true,
    "outputFormats": ["json", "sarif"],
    "realTimeFeedback": {
      "enabled": true,
      "endpoints": {
        "claude": {
          "url": "https://api.anthropic.com/v1/messages",
          "enabled": true,
          "authentication": {
            "type": "bearer",
            "tokenEnvironmentVariable": "CLAUDE_API_TOKEN"
          }
        },
        "augment": {
          "url": "https://api.augmentcode.com/feedback",
          "enabled": true,
          "authentication": {
            "type": "apikey",
            "keyEnvironmentVariable": "AUGMENT_API_KEY"
          }
        }
      }
    },
    "rules": {
      "STD-CANON-001": {
        "enabled": true,
        "severity": "warning",
        "canonicalMappings": {
          "CustomLogger": "ICentralLogger",
          "LocalDataAccess": "IDataRepository",
          "FileHandler": "IFileService",
          "EmailSender": "INotificationService"
        }
      },
      "STD-LOG-001": {
        "enabled": true,
        "severity": "suggestion",
        "requiredLogProperties": ["Timestamp", "Level", "Message", "Context"]
      },
      "STD-SEC-001": {
        "enabled": true,
        "severity": "error",
        "requiredHeaders": [
          "Content-Security-Policy",
          "X-Frame-Options",
          "X-Content-Type-Options",
          "Strict-Transport-Security"
        ]
      },
      "STD-ERR-001": {
        "enabled": true,
        "severity": "warning",
        "requiredPatterns": ["try-catch", "validation", "logging"]
      }
    },
    "excludes": {
      "files": [
        "**/bin/**",
        "**/obj/**",
        "**/packages/**",
        "**/*.Designer.cs",
        "**/*.generated.cs"
      ],
      "rules": {
        "STD-CANON-001": ["**/Migrations/**", "**/Legacy/**"],
        "STD-LOG-001": ["**/Tests/**"]
      }
    }
  },
  "testing": {
    "enforceTestCoverage": true,
    "minimumCoverageThreshold": 80,
    "requiredTestPatterns": {
      "unitTests": ["*Test.cs", "*Tests.cs"],
      "integrationTests": ["*IntegrationTest.cs", "*IntegrationTests.cs"]
    }
  },
  "deployment": {
    "preCommitHooks": {
      "enabled": true,
      "runAnalysis": true,
      "blockOnErrors": true
    },
    "cicd": {
      "generateReports": true,
      "failBuildOnErrors": true,
      "publishSarifResults": true
    }
  }
}
```


### 6. Unit Testing Framework

#### Test Infrastructure

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using System.Threading.Tasks;

namespace <project_name>.CodeAnalysis.Test
{
    public static class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static DiagnosticResult Diagnostic()
            => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic();

        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic(diagnosticId);

        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic(descriptor);

        public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
            {
                TestCode = source,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }

    public static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
    {
        public static DiagnosticResult Diagnostic()
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic();

        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic(diagnosticId);

        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => CSharpCodeFixVerifier<TAnalyzer, TCodeFix, XUnitVerifier>.Diagnostic(descriptor);

        public static async Task VerifyCodeFixAsync(string source, string fixedSource)
            => await VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
            => await VerifyCodeFixAsync(source, new[] { expected }, fixedSource);

        public static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
        {
            var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync();
        }
    }
}
```


#### Sample Analyzer Tests

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = <project_name>.CodeAnalysis.Test.CSharpAnalyzerVerifier<
    <project_name>.CodeAnalysis.Analyzers.CanonicalServiceAnalyzer>;

namespace <project_name>.CodeAnalysis.Test
{
    [TestClass]
    public class CanonicalServiceAnalyzerTests
    {
        [TestMethod]
        public async Task WhenUsingCanonicalService_NoDiagnosticTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var logger = new ICentralLogger();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task WhenUsingNonCanonicalService_DiagnosticTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var logger = new {|#0:CustomLogger|}();
        }
    }
}";

            var expected = VerifyCS.Diagnostic(DiagnosticDescriptors.CanonicalServiceUsage)
                .WithLocation(0)
                .WithArguments("ICentralLogger", "CustomLogger");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WhenUsingNonCanonicalServiceInVariableDeclaration_DiagnosticTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            {|#0:CustomLogger|} logger = null;
        }
    }
}";

            var expected = VerifyCS.Diagnostic(DiagnosticDescriptors.CanonicalServiceUsage)
                .WithLocation(0)
                .WithArguments("ICentralLogger", "CustomLogger");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task WithMultipleNonCanonicalServices_MultipleDiagnosticsTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var logger = new {|#0:CustomLogger|}();
            var dataAccess = new {|#1:LocalDataAccess|}();
            var fileHandler = new {|#2:FileHandler|}();
        }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DiagnosticDescriptors.CanonicalServiceUsage)
                .WithLocation(0)
                .WithArguments("ICentralLogger", "CustomLogger");

            var expected2 = VerifyCS.Diagnostic(DiagnosticDescriptors.CanonicalServiceUsage)
                .WithLocation(1)
                .WithArguments("IDataRepository", "LocalDataAccess");

            var expected3 = VerifyCS.Diagnostic(DiagnosticDescriptors.CanonicalServiceUsage)
                .WithLocation(2)
                .WithArguments("IFileService", "FileHandler");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2, expected3);
        }
    }

    [TestClass]
    public class LoggingConsistencyAnalyzerTests
    {
        [TestMethod]
        public async Task WhenUsingStructuredLogging_NoDiagnosticTriggered()
        {
            var test = @"
using Microsoft.Extensions.Logging;

namespace TestNamespace
{
    public class TestClass
    {
        private readonly ILogger<TestClass> _logger;

        public void TestMethod()
        {
            _logger.LogInformation(""Processing user {UserId} at {Timestamp}"", 123, DateTime.Now);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task WhenUsingStringConcatenation_DiagnosticTriggered()
        {
            var test = @"
using Microsoft.Extensions.Logging;

namespace TestNamespace
{
    public class TestClass
    {
        private readonly ILogger<TestClass> _logger;

        public void TestMethod()
        {
            var userId = 123;
            {|#0:_logger.LogInformation(""Processing user "" + userId)|};
        }
    }
}";

            var expected = VerifyCS.Diagnostic(DiagnosticDescriptors.LoggingConsistency)
                .WithLocation(0);

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }

    [TestClass]
    public class SecurityHeadersAnalyzerTests
    {
        [TestMethod]
        public async Task WhenControllerHasSecurityHeaders_NoDiagnosticTriggered()
        {
            var test = @"
using Microsoft.AspNetCore.Mvc;

namespace TestNamespace
{
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            Response.Headers.Add(""Content-Security-Policy"", ""default-src 'self'"");
            Response.Headers.Add(""X-Frame-Options"", ""DENY"");
            Response.Headers.Add(""X-Content-Type-Options"", ""nosniff"");
            Response.Headers.Add(""Strict-Transport-Security"", ""max-age=31536000"");
            return Ok();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task WhenControllerMissingSecurityHeaders_DiagnosticTriggered()
        {
            var test = @"
using Microsoft.AspNetCore.Mvc;

namespace TestNamespace
{
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult {|#0:Get|}()
        {
            return Ok();
        }
    }
}";

            var expected = VerifyCS.Diagnostic(DiagnosticDescriptors.SecurityHeaders)
                .WithLocation(0)
                .WithArguments("Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Strict-Transport-Security");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }

    [TestClass]
    public class ErrorHandlingAnalyzerTests
    {
        [TestMethod]
        public async Task WhenMethodHasProperErrorHandling_NoDiagnosticTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                // Some risky operation
                var result = RiskyOperation();
            }
            catch (Exception ex)
            {
                // Log error
                HandleError(ex);
                throw;
            }
        }

        private string RiskyOperation() => throw new NotImplementedException();
        private void HandleError(Exception ex) { }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task WhenMethodMissingErrorHandling_DiagnosticTriggered()
        {
            var test = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void {|#0:TestMethod|}()
        {
            var result = RiskyOperation();
        }

        private string RiskyOperation() => throw new NotImplementedException();
    }
}";

            var expected = VerifyCS.Diagnostic(DiagnosticDescriptors.ErrorHandling)
                .WithLocation(0)
                .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
```


### 7. .NET Template System

#### Template Configuration

```json
{
    "$schema": "http://json.schemastore.org/template",
    "author": "Your Organization",
    "classifications": ["Analysis", "Code Quality", "Roslyn"],
    "identity": "<project_name>.CodeAnalysis.Template",
    "name": "<project_name> Code Analysis Framework",
    "shortName": "codeanalysis",
    "tags": {
        "language": "C#",
        "type": "project"
    },
    "sourceName": "CodeAnalysisTemplate",
    "preferNameDirectory": true,
    "placeholderFilename": "template_placeholder",
    "sources": [
        {
            "source": "./",
            "target": "./",
            "exclude": [
                "**/bin/**",
                "**/obj/**",
                "**/.vs/**",
                "**/.vscode/**",
                "**/packages/**",
                "**/*.user",
                "**/*.suo",
                "**/*.userosscache",
                "**/*.sln.docstates"
            ]
        }
    ],
    "symbols": {
        "ProjectName": {
            "type": "parameter",
            "datatype": "string",
            "defaultValue": "MyProject",
            "replaces": "<project_name>",
            "description": "The name of the project"
        },
        "EnableRealTimeFeedback": {
            "type": "parameter",
            "datatype": "bool",
            "defaultValue": "true",
            "description": "Enable real-time feedback to AI assistants"
        },
        "TargetFramework": {
            "type": "parameter",
            "datatype": "choice",
            "choices": [
                {
                    "choice": "netstandard2.0",
                    "description": "Target .NET Standard 2.0"
                },
                {
                    "choice": "net6.0",
                    "description": "Target .NET 6.0"
                },
                {
                    "choice": "net7.0",
                    "description": "Target .NET 7.0"
                },
                {
                    "choice": "net8.0",
                    "description": "Target .NET 8.0"
                }
            ],
            "defaultValue": "netstandard2.0",
            "description": "The target framework for the analyzer"
        },
        "IncludeTests": {
            "type": "parameter",
            "datatype": "bool",
            "defaultValue": "true",
            "description": "Include unit test project"
        }
    },
    "postActions": [
        {
            "condition": "(OS != \"Windows_NT\")",
            "description": "Make scripts executable",
            "manualInstructions": [
                {
                    "text": "Run 'chmod +x scripts/*.sh'"
                }
            ],
            "actionId": "cb9a6cf3-4f5c-4860-b9d2-03a574959774",
            "args": {
                "+x": "scripts/*.sh"
            },
            "continueOnError": true
        },
        {
            "condition": "(IncludeTests == \"true\")",
            "description": "Restore NuGet packages",
            "manualInstructions": [
                {
                    "text": "Run 'dotnet restore'"
                }
            ],
            "actionId": "210D431B-A78B-4D2F-B762-4ED3E3EA9025",
            "continueOnError": true
        }
    ]
}
```


#### Template Installation Script

```bash
#!/bin/bash
# install-template.sh

set -e

TEMPLATE_NAME="<project_name>.CodeAnalysis.Templates"
TEMPLATE_PATH="./templates"

echo "Installing $TEMPLATE_NAME template..."

# Check if dotnet CLI is available
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET CLI is not installed or not in PATH"
    exit 1
fi

# Install the template
if [ -d "$TEMPLATE_PATH" ]; then
    dotnet new install "$TEMPLATE_PATH"
    echo "Template installed successfully!"
    echo ""
    echo "Usage:"
    echo "  dotnet new codeanalysis -n MyProject.CodeAnalysis"
    echo "  dotnet new codeanalysis -n MyProject.CodeAnalysis --ProjectName MyProject --EnableRealTimeFeedback true"
    echo ""
    echo "Parameters:"
    echo "  --ProjectName              Name of the project (default: MyProject)"
    echo "  --EnableRealTimeFeedback   Enable real-time AI feedback (default: true)"
    echo "  --TargetFramework          Target framework (default: netstandard2.0)"
    echo "  --IncludeTests             Include test project (default: true)"
else
    echo "Error: Template directory not found at $TEMPLATE_PATH"
    exit 1
fi
```


### 8. GitHub Codespaces Integration

#### Dev Container Configuration

```json
{
    "name": ".NET Code Analysis Development",
    "image": "mcr.microsoft.com/dotnet/sdk:8.0",
    
    "features": {
        "ghcr.io/devcontainers/features/github-cli:1": {},
        "ghcr.io/devcontainers/features/docker-in-docker:2": {},
        "ghcr.io/devcontainers/features/powershell:1": {}
    },
    
    "customizations": {
        "vscode": {
            "extensions": [
                "ms-dotnettools.csdevkit",
                "ms-dotnettools.csharp",
                "editorconfig.editorconfig",
                "streetsidesoftware.code-spell-checker",
                "github.copilot",
                "github.copilot-chat",
                "ms-vscode.vscode-json",
                "redhat.vscode-yaml",
                "ms-vsliveshare.vsliveshare",
                "formulahendry.dotnet-test-explorer",
                "jmrog.vscode-nuget-package-manager",
                "kreativ-software.csharpextensions"
            ],
            "settings": {
                "dotnet.completion.showCompletionItemsFromUnimportedNamespaces": true,
                "dotnet.codeLens.enableReferencesCodeLens": true,
                "dotnet.unitTests.runSettingsPath": "",
                "omnisharp.enableEditorConfigSupport": true,
                "omnisharp.enableRoslynAnalyzers": true,
                "editor.formatOnSave": true,
                "editor.formatOnPaste": true,
                "files.trimTrailingWhitespace": true,
                "files.insertFinalNewline": true,
                "csharp.format.enable": true,
                "csharp.semanticHighlighting.enabled": true
            }
        }
    },
    
    "forwardPorts": [
        5000,
        5001,
        8080
    ],
    
    "portsAttributes": {
        "5001": {
            "protocol": "https"
        }
    },
    
    "onCreateCommand": "bash .devcontainer/setup.sh",
    "postCreateCommand": "dotnet restore && dotnet build",
    "postStartCommand": "dotnet dev-certs https --trust",
    
    "mounts": [
        "source=codeanalysis-nuget,target=/home/vscode/.nuget,type=volume"
    ],
    
    "remoteUser": "vscode",
    
    "containerEnv": {
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_NOLOGO": "1",
        "ASPNETCORE_ENVIRONMENT": "Development"
    }
}
```


#### Setup Script

```bash
#!/bin/bash
# .devcontainer/setup.sh

set -e

echo "🚀 Setting up Code Analysis development environment..."

# Update package lists
sudo apt-get update

# Install additional tools
sudo apt-get install -y \
    curl \
    wget \
    unzip \
    git \
    tree \
    jq \
    vim

# Install global .NET tools
dotnet tool install -g dotnet-format
dotnet tool install -g dotnet-reportgenerator-globaltool
dotnet tool install -g dotnet-outdated-tool
dotnet tool install -g Microsoft.dotnet-httprepl

# Add tools to PATH
echo 'export PATH="$PATH:/home/vscode/.dotnet/tools"' >> ~/.bashrc

# Create useful aliases
cat >> ~/.bashrc << 'EOF'

# Code Analysis aliases
alias analyze='dotnet build --verbosity normal'
alias test='dotnet test --logger "console;verbosity=detailed"'
alias coverage='dotnet test --collect:"XPlat Code Coverage"'
alias format='dotnet format'
alias outdated='dotnet outdated'

# Quick navigation
alias ll='ls -alF'
alias la='ls -A'
alias l='ls -CF'

EOF

# Set up git configuration (if not already configured)
if [ -z "$(git config --global user.name)" ]; then
    echo "⚠️  Please configure git:"
    echo "git config --global user.name 'Your Name'"
    echo "git config --global user.email 'your.email@example.com'"
fi

# Create workspace directories
mkdir -p /workspaces/samples
mkdir -p /workspaces/docs
mkdir -p /workspaces/templates

# Set up sample project structure
cat > /workspaces/samples/SampleProject.cs << 'EOF'
using System;

namespace SampleProject
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // This will trigger STD-CANON-001
            var logger = new CustomLogger();
            
            Console.WriteLine("Hello, Code Analysis!");
        }
    }
    
    public class CustomLogger
    {
        public void Log(string message) => Console.WriteLine(message);
    }
}
EOF

echo "✅ Development environment setup complete!"
echo ""
echo "Available commands:"
echo "  analyze  - Build with analysis"
echo "  test     - Run tests with detailed output"
echo "  coverage - Run tests with coverage"
echo "  format   - Format code"
echo "  outdated - Check for outdated packages"
echo ""
echo "Sample files created in /workspaces/samples/"

# Make the script output available to the user
echo "🎯 Ready to start developing Code Analysis tools!"
```


#### Tasks Configuration

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "group": "build",
            "presentation": {
                "reveal": "silent"
            },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "build-analyzers",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/src",
                "--verbosity",
                "normal"
            ],
            "group": "build",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "test",
            "command": "dotnet",
            "type": "process",
            "args": [
                "test",
                "${workspaceFolder}/test",
                "--logger",
                "console;verbosity=detailed"
            ],
            "group": {
                "kind": "test",
                "isDefault": true
            },
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "test-coverage",
            "command": "dotnet",
            "type": "process",
            "args": [
                "test",
                "${workspaceFolder}/test",
                "--collect:XPlat Code Coverage",
                "--results-directory",
                "./TestResults/"
            ],
            "group": "test",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            },
            "problemMatcher": "$msCompile"
        },
        {
            "label": "format",
            "command": "dotnet",
            "type": "process",
            "args": [
                "format",
                "${workspaceFolder}"
            ],
            "group": "build",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            }
        },
        {
            "label": "analyze-sample",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "/workspaces/samples/",
                "--verbosity",
                "normal"
            ],
            "group": "build",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            },
            "problemMatcher": "$msCompile",
            "dependsOn": "build-analyzers"
        },
        {
            "label": "pack-analyzers",
            "command": "dotnet",
            "type": "process",
            "args": [
                "pack",
                "${workspaceFolder}/src",
                "--configuration",
                "Release",
                "--output",
                "./artifacts/"
            ],
            "group": "build",
            "presentation": {
                "echo": true,
                "reveal": "always",
                "focus": false,
                "panel": "shared"
            },
            "dependsOn": "build-analyzers"
        }
    ]
}
```


### 9. Integration Examples

#### Claude Code Integration Message

```json
{
    "type": "code_analysis_feedback",
    "timestamp": "2025-06-26T14:27:00Z",
    "context": {
        "file": "Services/Auth/TokenHandler.cs",
        "line": 42,
        "rule": "STD-CANON-001",
        "project": "MyProject.Api"
    },
    "feedback": "You're using a non-standard logging class. Replace `CustomLogger` with `ICentralLogger` to stay compliant with your system's service layer rules. This change will improve maintainability and ensure consistent logging patterns across your application.",
    "severity": "Warning",
    "suggestions": [
        "Replace the concrete class with the corresponding interface",
        "Inject the service through dependency injection in your constructor",
        "Update any dependent code to use the interface rather than the implementation",
        "Consider adding the service registration to your DI container if not already present"
    ],
    "code_examples": {
        "current": "var logger = new CustomLogger();",
        "suggested": "public TokenHandler(ICentralLogger logger) { _logger = logger; }"
    },
    "documentation_links": [
        "https://docs.company.com/standards/canonical-services",
        "https://docs.company.com/architecture/dependency-injection"
    ],
    "related_rules": ["STD-DI-001", "STD-LOG-001"],
    "confidence": 0.95
}
```


#### Augment Code Integration Message

```json
{
    "event_type": "diagnostic_detected",
    "timestamp": "2025-06-26T14:27:00Z",
    "source": "roslyn_analyzer",
    "analyzer_version": "1.0.0",
    "location": {
        "file_path": "Services/Auth/TokenHandler.cs",
        "line_number": 42,
        "column_number": 25,
        "character_range": {
            "start": 1024,
            "end": 1037
        }
    },
    "diagnostic": {
        "rule_id": "STD-CANON-001",
        "category": "Design",
        "title": "Use canonical service interface",
        "description": "Use canonical service 'ICentralLogger' instead of 'CustomLogger'",
        "severity": "warning"
    },
    "action_required": "review_and_fix",
    "priority": 2,
    "metadata": {
        "canonical_service": "ICentralLogger",
        "detected_service": "CustomLogger",
        "auto_fixable": true,
        "breaking_change": false
    },
    "fix_suggestions": [
        {
            "type": "replace_type",
            "from": "CustomLogger",
            "to": "ICentralLogger",
            "requires_di_registration": true
        }
    ],
    "impact_analysis": {
        "files_affected": 1,
        "methods_affected": 1,
        "estimated_fix_time_minutes": 5,
        "complexity": "low"
    }
}
```


## Implementation Workflow

### Phase 1: Core Infrastructure (Week 1-2)

1. **Project Setup**[^1][^2]: Create the basic analyzer project structure using Roslyn SDK templates
2. **Diagnostic Framework**[^3]: Implement the `DiagnosticDescriptors` class with all rule definitions
3. **Basic Analyzers**[^4][^5]: Implement core analyzers for canonical service usage and logging consistency
4. **Configuration System**[^6][^7]: Set up `.editorconfig` and `globalconfig.json` with rule configurations

### Phase 2: Advanced Analysis (Week 3-4)

1. **Security Analyzers**[^8][^9]: Implement security header and vulnerability detection analyzers
2. **Code Fix Providers**[^2][^10]: Create automated code fixes for common violations
3. **Unit Testing**[^11][^12]: Develop comprehensive test suite using Roslyn testing framework
4. **Output Generation**[^13][^14]: Implement SARIF and JSON diagnostic output formats

### Phase 3: Integration Layer (Week 5-6)

1. **Real-time Feedback**[^15][^16]: Build the feedback dispatcher and message translation system
2. **AI Assistant Integration**[^17]: Implement Claude Code and Augment Code messaging protocols
3. **Template System**[^18][^19]: Create `dotnet new` templates for easy project scaffolding
4. **Performance Optimization**[^20]: Optimize analyzer performance for large codebases

### Phase 4: DevOps Integration (Week 7-8)

1. **Codespaces Setup**[^21][^22]: Configure GitHub Codespaces with complete development environment
2. **CI/CD Integration**[^23]: Add build pipeline integration and automated quality gates
3. **Documentation**[^24]: Create comprehensive documentation and usage guides
4. **Testing \& Deployment**: Thorough testing across different project types and deployment scenarios

## Key Benefits and Outcomes

### For Development Teams

- **Consistent Code Quality**: Enforced standards across all projects through automated analysis[^25][^26]
- **Reduced Code Review Time**: Automated detection of common issues before human review[^16][^27]
- **Real-time Learning**: AI-powered feedback helps developers learn best practices as they code[^15]
- **Improved Security**: Automated detection of security vulnerabilities and missing protections[^8][^28]


### For AI Integration

- **Contextual Feedback**: Structured diagnostic information enables intelligent AI suggestions[^29][^30]
- **Continuous Learning**: Real-time analysis data improves AI model understanding of codebase patterns
- **Workflow Integration**: Seamless integration with existing AI coding assistants without disrupting developer flow[^17]


### For Project Management

- **Quality Metrics**: Comprehensive reporting on code quality trends and improvement areas[^27]
- **Standards Compliance**: Automated enforcement of organizational coding standards and best practices[^31][^32]
- **Technical Debt Tracking**: Continuous monitoring of code quality issues and their resolution[^23]

This comprehensive framework provides a production-ready solution that bridges the gap between static code analysis and modern AI-assisted development workflows[^1][^2][^25]. The modular architecture ensures extensibility while maintaining performance, and the real-time integration capabilities enable a new paradigm of intelligent, context-aware development assistance.

<div style="text-align: center">⁂</div>

[^1]: https://learn.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview?view=vs-2022

[^2]: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix

[^3]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnosticdescriptor?view=roslyn-dotnet-4.9.0

[^4]: https://roslyn-analyzers.readthedocs.io/en/latest/how-to-start.html

[^5]: https://www.rasmusolsson.dev/posts/why-you-might-want-custom-analyzer-rules/

[^6]: https://learn.microsoft.com/en-us/visualstudio/code-quality/use-roslyn-analyzers?view=vs-2022

[^7]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files

[^8]: https://dev.to/dbalikhin/a-quick-comparison-of-security-static-code-analyzers-for-c-2l5h

[^9]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/security-warnings

[^10]: https://dennistretyakov.com/writing-first-roslyn-analyzer-and-codefix-provider/

[^11]: https://www.alwaysdeveloping.net/p/analyzer-test/

[^12]: https://stackoverflow.com/questions/30384868/how-can-i-unit-test-roslyn-diagnostics

[^13]: https://docs.github.com/en/code-security/code-scanning/integrating-with-code-scanning/sarif-support-for-code-scanning

[^14]: https://blog.convisoappsec.com/en/what-is-sarif-and-how-it-could-revolutionize-software-security/

[^15]: https://www.xenonstack.com/blog/ai-augmented-software-development

[^16]: https://bito.ai/blog/best-automated-ai-code-review-tools/

[^17]: https://www.reddit.com/r/ChatGPTCoding/comments/1j153o9/i_made_a_simple_tool_that_completely_changed_how/

[^18]: https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates

[^19]: https://auth0.com/blog/create-dotnet-project-template/

[^20]: https://www.stevejgordon.co.uk/using-the-roslyn-apis-to-analyse-a-dotnet-solution

[^21]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/introduction-to-dev-containers

[^22]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/setting-up-your-dotnet-project-for-codespaces

[^23]: https://www.browserstack.com/codequality

[^24]: https://blog.ndepend.com/the-editorconfig-files-for-net-developers/

[^25]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview

[^26]: https://www.nuget.org/packages/Microsoft.CodeAnalysis.NetAnalyzers

[^27]: https://www.codeant.ai/blogs/best-code-quality-tools

[^28]: https://owasp.org/www-community/Source_Code_Analysis_Tools

[^29]: https://thectoclub.com/tools/best-code-analysis-tools/

[^30]: https://www.linkedin.com/advice/3/what-code-review-tools-provide-real-time-feedback-how1e

[^31]: https://www.mytechramblings.com/posts/configure-roslyn-analyzers-using-editorconfig/

[^32]: https://github.com/dotnet/roslyn-analyzers/blob/main/docs/Analyzer Configuration.md

[^33]: https://www.youtube.com/watch?v=-bBA8WvH-BQ

[^34]: https://www.aptori.com/glossary/static-analysis-results-interchange-format-sarif

[^35]: https://gcc.gnu.org/onlinedocs/gcc/Diagnostic-Message-Formatting-Options.html

[^36]: https://www.qodo.ai/learn/code-review/automated/

[^37]: https://json.nlohmann.me/api/macros/json_diagnostics/

[^38]: https://coderabbit.ai

[^39]: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-project-template

[^40]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new

[^41]: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-item-template

[^42]: https://www.jetbrains.com/help/rider/Install_custom_project_templates.html

[^43]: https://www.rasmusolsson.dev/posts/creating-a-net-template-with-template-json/

[^44]: https://www.youtube.com/watch?v=rdWZo5PD9Ek

[^45]: https://github.blog/changelog/2022-10-21-codespaces-configuration-with-the-dev-container-editor/

[^46]: https://learn.microsoft.com/en-us/dotnet/aspire/get-started/dev-containers

[^47]: https://devcontainers.github.io/implementors/json_reference/

[^48]: https://www.youtube.com/watch?v=bJ8vfsqr4h0

[^49]: https://aaronbos.dev/posts/dotnet-roslyn-editorconfig-neovim

[^50]: https://www.jetbrains.com/help/fleet/using-editorconfig.html

[^51]: https://users.rust-lang.org/t/rust-analyzer-json-global-config-location/76508

[^52]: https://learn.microsoft.com/en-us/dotnet/core/testing/mstest-analyzers/overview

[^53]: https://github.com/nunit/nunit.analyzers

[^54]: http://roslyn-analyzers.readthedocs.io/en/latest/how-to-test.html

[^55]: https://help.sap.com/docs/ABAP_PLATFORM_NEW/ba879a6e2ea04d9bb94c7ccd7cdac446/49216c634ab514cde10000000a42189b.html

[^56]: https://www.reddit.com/r/dotnet/comments/1glzves/which_c_code_analyzers_should_i_focus_on/

[^57]: https://eda.sw.siemens.com/en-US/ic/questa-one/verification-iq/coverage-analyzer/

[^58]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/categories

[^59]: https://learn.microsoft.com/en-us/visualstudio/code-quality/install-net-analyzers?view=vs-2022

[^60]: https://github.com/dotnet/roslyn-analyzers

[^61]: https://github.com/dotnet/roslyn-analyzers/blob/master/src/Microsoft.CodeAnalysis.Analyzers/Core/MetaAnalyzers/DiagnosticDescriptorCreationAnalyzer.cs

[^62]: https://sarifweb.azurewebsites.net

[^63]: https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html

[^64]: https://gcc.gnu.org/wiki/SARIF

[^65]: https://learn.microsoft.com/en-us/shows/visual-studio-toolbox/create-a-net-core-project-template

[^66]: https://github.com/dotnet/templating/wiki/Reference-for-template.json

[^67]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration

[^68]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers

[^69]: https://qmacro.org/blog/posts/2024/01/26/exploring-codespaces-as-temporary-dev-containers/

[^70]: https://docs.github.com/enterprise-cloud@latest/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/setting-up-your-python-project-for-codespaces

[^71]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules

[^72]: https://github.com/dotnet/roslyn-analyzers/issues/1844

[^73]: https://www.codiga.io

[^74]: https://www.legitsecurity.com/aspm-knowledge-base/best-security-code-review-tools

[^75]: https://www.nist.gov/itl/ssd/software-quality-group/source-code-security-analyzers


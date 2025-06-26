## 🔍 AI Research & Build Prompt for .CodeAnalysis project

Design, research, scaffold, and partially implement a `.CodeAnalysis` subsystem for large C#/.NET codebases with built-in analyzers, fix providers, and real-time integration with coding assistants. The system should strictly enforce the standards outlined in `Coding Standard Development Workflow - Clean.md`.

---

## **Title**

**"Scaffold and Enforce a Modular, Real-Time `<project_name>.CodeAnalysis` Framework for .NET Using Roslyn, FOSS Tools, and Live Feedback to Claude Code & Augment Code"**

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

## 🔁 PART 3 — Real-Time Feedback to Claude Code & Augment Code

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
* Extensions: C#, Roslyn analyzer helper, EditorConfig, Prettier
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
8. ✅ Sample assistant messages for Claude Code & Augment Code
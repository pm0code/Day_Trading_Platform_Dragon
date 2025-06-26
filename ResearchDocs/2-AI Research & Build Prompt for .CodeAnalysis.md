.CodeAnalyzer Implementation Workflow

Project Setup: Create the basic analyzer project structure using Roslyn SDK templates

Diagnostic Framework: Implement the DiagnosticDescriptors class with all rule definitions

Basic Analyzers: Implement core analyzers for canonical service usage and logging consistency

Configuration System: Set up .editorconfig and globalconfig.json with rule configurations

Phase 2: Advanced Analysis (Week 3-4)
Security Analyzers: Implement security header and vulnerability detection analyzers

Code Fix Providers: Create automated code fixes for common violations

Unit Testing: Develop comprehensive test suite using Roslyn testing framework

Output Generation: Implement SARIF and JSON diagnostic output formats

Phase 3: Integration Layer (Week 5-6)
Real-time Feedback: Build the feedback dispatcher and message translation system

AI Assistant Integration: Implement Claude Code and Augment Code messaging protocols

Template System: Create dotnet new templates for easy project scaffolding

Performance Optimization: Optimize analyzer performance for large codebases

Phase 4: DevOps Integration (Week 7-8)
Codespaces Setup: Configure GitHub Codespaces with complete development environment

CI/CD Integration: Add build pipeline integration and automated quality gates

Documentation: Create comprehensive documentation and usage guides

Testing \& Deployment: Thorough testing across different project types and deployment scenarios

Now I have comprehensive information about all the tools, packages, and resources needed for implementing the .CodeAnalysis framework. Let me create a detailed continuation report with specific products, libraries, URLs, and instructions for each implementation phase.

# Detailed Implementation Resources for .CodeAnalysis Framework

## Implementation Workflow with Specific Tools and Resources

### Phase 1: Core Infrastructure (Week 1-2)

#### Project Setup: Create the basic analyzer project structure using Roslyn SDK templates

**Required NuGet Packages:**

- **Microsoft.CodeAnalysis.CSharp** (Version 4.14.0): The core Roslyn C\# analyzer package[^1]
    - Installation: `dotnet add package Microsoft.CodeAnalysis.CSharp --version 4.14.0`
    - URL: https://www.nuget.org/packages/microsoft.codeanalysis.csharp/
- **Microsoft.CodeAnalysis.Analyzers** (Version 4.14.0): Rules for correct usage of Roslyn APIs[^2]
    - Installation: `dotnet add package Microsoft.CodeAnalysis.Analyzers --version 4.14.0`
    - URL: https://www.nuget.org/packages/microsoft.codeanalysis.analyzers/

**Template Options:**

- **Community.RoslynTemplates** (Version 2.0.0): Collection of Roslyn templates[^3]
    - Installation: `dotnet new install Community.RoslynTemplates::2.0.0`
    - URL: https://www.nuget.org/packages/Community.RoslynTemplates
- **Manual Project Creation**: For modern .NET projects targeting .NET 8+[^4]
    - Reference: https://github.com/dotnet/roslyn-analyzers
    - Documentation: https://learn.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview

**Instructions for AI System:**

1. Install .NET 8 SDK or later from https://dotnet.microsoft.com/download
2. Create new project: `dotnet new classlib -n YourProject.CodeAnalysis --framework netstandard2.0`
3. Add required NuGet packages using the commands above
4. Configure project file according to Roslyn analyzer packaging requirements[^5]

#### Diagnostic Framework: Implement the DiagnosticDescriptors class with all rule definitions

**Configuration Tools:**

- **EditorConfig Support**: Built into .NET SDK 5.0+[^6]
    - Reference: https://learn.microsoft.com/en-us/visualstudio/code-quality/install-net-analyzers
    - Configure rules using `.editorconfig` file with `dotnet_diagnostic.RuleId.severity` syntax
- **GlobalConfig.json**: Custom configuration format for complex rule settings
    - JSON Schema: Use https://json.schemastore.org/globalconfig.json for validation
    - Implementation example in DotNetProjectFile.Analyzers[^7]

**Instructions for AI System:**

1. Create `DiagnosticDescriptors.cs` with static readonly DiagnosticDescriptor fields
2. Use diagnostic IDs in format: `STD-{CATEGORY}-{NUMBER}` (e.g., STD-CANON-001)
3. Configure severity levels: Error, Warning, Info, Hidden, None[^8]
4. Set up `.editorconfig` file with rule configurations

#### Basic Analyzers: Implement core analyzers for canonical service usage and logging consistency

**Testing Framework:**

- **Microsoft.CodeAnalysis.CSharp.Analyzer.Testing** (Version 1.1.2): Official Roslyn testing framework[^9]
    - Installation: `dotnet add package Microsoft.CodeAnalysis.CSharp.Analyzer.Testing --version 1.1.2`
    - URL: https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Analyzer.Testing
- **Additional Package Source Required**: Add Azure DevOps NuGet feed[^10]
    - Add to project: `<RestoreAdditionalProjectSources>https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json</RestoreAdditionalProjectSources>`

**Instructions for AI System:**

1. Implement `DiagnosticAnalyzer` classes inheriting from base class
2. Override `SupportedDiagnostics` and `Initialize` methods
3. Use `SyntaxNodeAnalysisContext` for syntax tree analysis
4. Test using `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` pattern[^10]

#### Configuration System: Set up .editorconfig and globalconfig.json with rule configurations

**Standard Configuration Files:**

- **.editorconfig**: Industry standard configuration format[^6]
    - Schema: https://editorconfig.org/
    - .NET-specific rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/
- **Global Analyzer Config**: MSBuild integration for analyzer configuration[^7]
    - Use `<GlobalAnalyzerConfigFiles>` MSBuild property
    - Reference: https://github.com/dotnet-project-file-analyzers/dotnet-project-file-analyzers

**Instructions for AI System:**

1. Create `.editorconfig` file in repository root
2. Configure analyzer rules using `dotnet_diagnostic.RULE_ID.severity = level` format
3. Set up `globalconfig.json` for complex custom rule configurations
4. Use `<GlobalAnalyzerConfigFiles Include="globalconfig.json" />` in project files

### Phase 2: Advanced Analysis (Week 3-4)

#### Security Analyzers: Implement security header and vulnerability detection analyzers

**Security Analysis Tools:**

- **Microsoft.CodeAnalysis.NetAnalyzers**: Built-in security rules (CA2xxx series)[^4]
    - Included with .NET 5+ SDK by default
    - Documentation: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/
- **Security Code Scan**: Additional security analyzer
    - GitHub: https://github.com/security-code-scan/security-code-scan
    - NuGet: `SecurityCodeScan.VS2019`

**Instructions for AI System:**

1. Enable built-in security analyzers with `<EnableNETAnalyzers>true</EnableNETAnalyzers>`
2. Configure security rules in `.editorconfig`: `dotnet_diagnostic.CA2100.severity = error`
3. Implement custom security analyzers for API security headers
4. Use `ISymbol` analysis for vulnerability pattern detection

#### Code Fix Providers: Create automated code fixes for common violations

**Code Fix Framework:**

- **Microsoft.CodeAnalysis.CSharp.CodeFix.Testing** (Version 1.1.2): Testing framework for code fixes[^10]
    - Installation: `dotnet add package Microsoft.CodeAnalysis.CSharp.CodeFix.Testing --version 1.1.2`
    - Requires same Azure DevOps package source as analyzer testing

**Instructions for AI System:**

1. Implement `CodeFixProvider` classes with `[ExportCodeFixProvider]` attribute
2. Override `FixableDiagnosticIds` and `RegisterCodeFixesAsync` methods
3. Use `CodeAction.Create` to define fix actions
4. Test using `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>` pattern

#### Unit Testing: Develop comprehensive test suite using Roslyn testing framework

**Testing Packages:**

- **Microsoft.CodeAnalysis.Analyzer.Testing** (Version 1.1.2): Base testing framework[^11]
- **Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.MSTest** (Version 1.1.2): MSTest integration[^12]
- **XUnit Alternative**: Replace MSTest with XUnit for cross-platform compatibility

**Instructions for AI System:**

1. Create separate test project targeting .NET 8
2. Install testing framework packages
3. Use `[Fact]` or `[TestMethod]` attributes for test methods
4. Follow pattern: `await VerifyCS.VerifyAnalyzerAsync(testCode, expectedDiagnostics)`

#### Output Generation: Implement SARIF and JSON diagnostic output formats

**SARIF Tools:**

- **Sarif.Sdk** (Version 4.5.4): Microsoft SARIF SDK for .NET[^13]
    - Installation: `dotnet add package Sarif.Sdk --version 4.5.4`
    - URL: https://www.nuget.org/packages/Sarif.Sdk/
    - GitHub: https://github.com/microsoft/sarif-sdk[^14]

**SARIF Specification:**

- **Schema**: https://json.schemastore.org/sarif-2.1.0.json[^15]
- **GitHub Integration**: Native SARIF support for code scanning[^15]
- **Documentation**: https://docs.github.com/en/code-security/code-scanning/integrating-with-code-scanning/sarif-support-for-code-scanning

**Instructions for AI System:**

1. Install Sarif.Sdk NuGet package
2. Use `Microsoft.CodeAnalysis.Sarif` namespace for object model
3. Create `SarifLog` objects with `Run` collections
4. Export to both SARIF 2.1.0 and custom JSON formats
5. Validate output against JSON schema before publishing

### Phase 3: Integration Layer (Week 5-6)

#### Real-time Feedback: Build the feedback dispatcher and message translation system

**HTTP Client Libraries:**

- **System.Net.Http**: Built into .NET (no additional package needed)
- **Newtonsoft.Json**: For JSON serialization
    - Installation: `dotnet add package Newtonsoft.Json`
- **System.Text.Json**: Alternative built-in JSON library (.NET Core 3.0+)

**Instructions for AI System:**

1. Implement `HttpClient` for webhook delivery
2. Use `async/await` pattern for non-blocking HTTP calls
3. Implement retry logic with exponential backoff[^16]
4. Configure timeout and circuit breaker patterns
5. Use structured logging for webhook delivery tracking

#### AI Assistant Integration: Implement Claude Code and Augment Code messaging protocols

**Claude API Integration:**

- **Anthropic API Documentation**: https://docs.anthropic.com/en/home[^17]
- **API Endpoint**: `https://api.anthropic.com/v1/messages`
- **Authentication**: Bearer token in `x-api-key` header
- **SDK**: No official .NET SDK, use HttpClient directly

**Augment Code Integration:**

- **API Documentation**: https://developers.augment.com/api[^18]
- **Authentication**: OAuth 2.0 with client credentials flow
- **Endpoint**: `https://webservice.augment.com/rest/v1/`
- **Product Information**: https://www.augmentcode.com/product[^19]

**Webhook Tools:**

- **Sync Webhooks**: Third-party webhook management service[^20]
- **Custom Implementation**: Use ASP.NET Core minimal APIs for webhook receivers

**Instructions for AI System:**

1. Register with Anthropic and Augment Code for API access
2. Store API keys in environment variables or Azure Key Vault
3. Implement separate message formats for each AI assistant
4. Use JSON serialization for message payloads
5. Implement webhook signature validation for security[^16]
6. Set up retry mechanisms for failed deliveries

#### Template System: Create dotnet new templates for easy project scaffolding

**Template Engine:**

- **Microsoft.TemplateEngine.Authoring.Templates**: Official template authoring package[^21]
    - Installation: `dotnet new install Microsoft.TemplateEngine.Authoring.Templates`
    - Documentation: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-template-package

**Template Configuration:**

- **template.json Schema**: http://json.schemastore.org/template[^22]
- **Documentation**: https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates
- **Examples**: https://github.com/dotnet/templating (official examples)[^22]

**Instructions for AI System:**

1. Create template project: `dotnet new templatepack -n "YourCompany.CodeAnalysis.Templates"`
2. Structure templates in `content/` folder with `.template.config/template.json`
3. Configure template parameters using symbols section[^23]
4. Package with `dotnet pack` command
5. Install locally: `dotnet new install ./path/to/template/`
6. Publish to NuGet for distribution[^24]

#### Performance Optimization: Optimize analyzer performance for large codebases

**Performance Tools:**

- **dotnet-counters**: Built-in performance monitoring tool
- **PerfView**: Microsoft performance analysis tool
- **BenchmarkDotNet**: Micro-benchmarking library for .NET

**Optimization Techniques:**

- Enable concurrent execution: `context.EnableConcurrentExecution()`
- Filter generated code: `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`
- Cache expensive computations using `ConcurrentDictionary`
- Use syntax filters to reduce analysis scope

**Instructions for AI System:**

1. Profile analyzer performance using PerfView or dotnet-counters
2. Implement concurrent analysis where possible
3. Cache compilation-level analysis results
4. Use syntax node filters to reduce analysis scope
5. Benchmark performance improvements using large test projects

### Phase 4: DevOps Integration (Week 7-8)

#### Codespaces Setup: Configure GitHub Codespaces with complete development environment

**Dev Container Tools:**

- **VS Code Dev Containers Extension**: Install from VS Code marketplace[^25]
- **Dev Container CLI**: `npm install -g @devcontainers/cli`
- **Docker**: Required for local development container testing

**Container Configuration:**

- **Base Images**: Use `mcr.microsoft.com/dotnet/sdk:8.0` for .NET development[^26]
- **Dev Container Features**: Browse at https://containers.dev/features
- **Documentation**: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces[^26]

**Instructions for AI System:**

1. Create `.devcontainer/devcontainer.json` file in repository root
2. Configure base image: `"image": "mcr.microsoft.com/dotnet/sdk:8.0"`
3. Add VS Code extensions for C\# development
4. Install required tools using `postCreateCommand`
5. Configure port forwarding for development servers
6. Test locally using VS Code Dev Containers extension

#### CI/CD Integration: Add build pipeline integration and automated quality gates

**GitHub Actions:**

- **Workflow Templates**: https://docs.github.com/en/actions/about-github-actions[^27]
- **Starter Workflows**: https://github.com/actions/starter-workflows
- **Marketplace**: https://github.com/marketplace/actions/

**Azure DevOps:**

- **Pipeline Documentation**: https://learn.microsoft.com/en-us/azure/devops/pipelines/[^28]
- **Build Automation**: https://learn.microsoft.com/en-us/dynamics365/fin-ops-core/dev-itpro/dev-tools/hosted-build-automation[^29]

**Required Actions/Tasks:**

- **actions/checkout@v4**: Code checkout
- **actions/setup-dotnet@v3**: .NET SDK setup
- **coverlet.msbuild**: Code coverage collection
- **ReportGenerator**: Coverage report generation

**Instructions for AI System:**

1. Create `.github/workflows/ci.yml` for GitHub Actions[^30]
2. Configure triggers: `on: [push, pull_request]`
3. Set up multi-stage pipeline: build, test, analyze, publish
4. Integrate SARIF upload using `github/codeql-action/upload-sarif`
5. Configure build status checks for pull requests
6. Set up artifact publishing for NuGet packages

#### Documentation: Create comprehensive documentation and usage guides

**Documentation Tools:**

- **DocFX**: Microsoft documentation generation tool
    - Installation: `dotnet tool install -g docfx`
    - GitHub: https://github.com/dotnet/docfx
- **GitHub Pages**: Free hosting for documentation sites
- **GitBook**: Alternative documentation platform
- **MkDocs**: Python-based documentation generator

**Instructions for AI System:**

1. Install DocFX: `dotnet tool install -g docfx`
2. Initialize documentation: `docfx init`
3. Configure `docfx.json` for project structure
4. Write markdown documentation in `articles/` folder
5. Generate API documentation from XML comments
6. Deploy to GitHub Pages using GitHub Actions

#### Testing \& Deployment: Thorough testing across different project types and deployment scenarios

**Testing Tools:**

- **coverlet.collector**: Code coverage data collection[^31]
    - Installation: `dotnet add package coverlet.collector`
- **ReportGenerator**: Coverage report generation[^31]
    - Installation: `dotnet tool install -g dotnet-reportgenerator-globaltool`

**Deployment Platforms:**

- **NuGet.org**: Public package repository
- **Azure DevOps Artifacts**: Private package feeds
- **GitHub Packages**: Integrated package hosting
- **MyGet**: Alternative package hosting service

**Testing Configuration:**

- **tests.runsettings**: Test execution configuration[^31]
- **SonarQube Integration**: Code quality analysis[^32]
- **Mutation Testing**: Using Stryker.NET for test quality validation

**Instructions for AI System:**

1. Create `tests.runsettings` file for coverage configuration
2. Install coverage tools: `dotnet tool install -g dotnet-reportgenerator-globaltool`
3. Run tests with coverage: `dotnet test --settings tests.runsettings`
4. Generate reports: `reportgenerator -reports:"**\TestResults\**\coverage.cobertura.xml" -targetdir:"coverage"`
5. Set up automated testing across .NET versions (6, 7, 8)
6. Configure deployment pipelines for different environments
7. Implement semantic versioning for package releases
8. Set up automated security scanning with GitHub Advanced Security

## Summary of Key Resources

### Essential NuGet Packages

- Microsoft.CodeAnalysis.CSharp (4.14.0)
- Microsoft.CodeAnalysis.Analyzers (4.14.0)
- Microsoft.CodeAnalysis.CSharp.Analyzer.Testing (1.1.2)
- Sarif.Sdk (4.5.4)
- Newtonsoft.Json (latest)


### Development Tools

- .NET 8 SDK
- Visual Studio 2022 or VS Code with C\# extension
- Docker Desktop for container development
- Git for version control


### API Endpoints

- Claude API: https://api.anthropic.com/v1/messages
- Augment Code API: https://webservice.augment.com/rest/v1/
- GitHub API: https://api.github.com/


### Documentation Resources

- Roslyn Analyzers: https://github.com/dotnet/roslyn-analyzers
- SARIF Specification: https://docs.github.com/en/code-security/code-scanning/integrating-with-code-scanning/sarif-support-for-code-scanning
- .NET Templates: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-template-package
- GitHub Codespaces: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces

This comprehensive resource guide provides the AI system with specific tools, packages, URLs, and step-by-step instructions needed to implement each phase of the .CodeAnalysis framework successfully.

<div style="text-align: center">⁂</div>

[^1]: https://www.nuget.org/packages/microsoft.codeanalysis.csharp/

[^2]: https://www.nuget.org/packages/microsoft.codeanalysis.analyzers/

[^3]: https://www.nuget.org/packages/Community.RoslynTemplates

[^4]: https://github.com/dotnet/roslyn-analyzers

[^5]: https://aaronstannard.com/roslyn-nuget/

[^6]: https://learn.microsoft.com/en-us/visualstudio/code-quality/install-net-analyzers?view=vs-2022

[^7]: https://github.com/dotnet-project-file-analyzers/dotnet-project-file-analyzers

[^8]: https://learn.microsoft.com/en-us/visualstudio/code-quality/roslyn-analyzers-overview?view=vs-2022

[^9]: https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Analyzer.Testing

[^10]: https://www.meziantou.net/how-to-test-a-roslyn-analyzer.htm

[^11]: https://www.nuget.org/packages/Microsoft.CodeAnalysis.Analyzer.Testing/

[^12]: https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Analyzer.Testing.MSTest/

[^13]: https://www.nuget.org/packages/Sarif.Sdk/

[^14]: https://github.com/microsoft/sarif-sdk

[^15]: https://docs.github.com/en/code-security/code-scanning/integrating-with-code-scanning/sarif-support-for-code-scanning

[^16]: https://www.nexusflowinnovations.com/blog/webhook-implementation-strategies-ai-assistants

[^17]: https://docs.anthropic.com/en/home

[^18]: https://developers.augment.com/api

[^19]: https://www.augmentcode.com/product

[^20]: https://syncwebhooks.com/ai-assistant/

[^21]: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-template-package

[^22]: https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates

[^23]: https://www.rasmusolsson.dev/posts/creating-a-net-template-with-template-json/

[^24]: https://www.loginradius.com/blog/engineering/using-nuget-to-publish-net-packages

[^25]: https://code.visualstudio.com/docs/devcontainers/create-dev-container

[^26]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/introduction-to-dev-containers

[^27]: https://docs.github.com/articles/getting-started-with-github-actions

[^28]: https://www.encorebusiness.com/blog/build-automation-using-azure-pipeline-microsoft-hosted-agent/

[^29]: https://learn.microsoft.com/en-us/dynamics365/fin-ops-core/dev-itpro/dev-tools/hosted-build-automation

[^30]: https://github.blog/enterprise-software/ci-cd/build-ci-cd-pipeline-github-actions-four-steps/

[^31]: https://knowyourtoolset.com/2024/01/coverage-reports/

[^32]: https://docs.sonarsource.com/sonarqube-server/9.8/analyzing-source-code/test-coverage/dotnet-test-coverage/

[^33]: https://stackoverflow.com/questions/56684895/roslyn-restore-nugets-packages-folder

[^34]: https://www.reddit.com/r/dotnet/comments/1i24iec/is_there_an_updated_implementation_for_writing/

[^35]: https://learn.microsoft.com/en-us/answers/questions/357531/does-microsoft-codeanalysis-csharp-nuget-package-c

[^36]: https://stackoverflow.com/questions/73740603/is-microsoft-codeanalysis-csharp-codestyle-still-needed-for-a-net-6-project

[^37]: https://www.jetbrains.com/help/rider/Using_NET_Compiler_Analyzers.html

[^38]: https://roslyn-analyzers.readthedocs.io/en/latest/how-to-start.html

[^39]: https://stackoverflow.com/questions/72663279/upgrading-microsoft-codeanalysis-csharp-for-roslyn-analyzer

[^40]: https://stackoverflow.com/questions/62638455/analyzer-with-code-fix-project-template-is-broken

[^41]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview

[^42]: https://bandit.readthedocs.io/en/latest/formatters/sarif.html

[^43]: https://www.nist.gov/document/samate-document-source-code-security-analysis-tool-functional-specification-version-10-nist

[^44]: https://stackoverflow.com/questions/78943908/how-to-convert-json-value-into-sarif-format

[^45]: https://pipedream.com/apps/anthropic

[^46]: https://docs.aws.amazon.com/bedrock/latest/userguide/model-parameters-anthropic-claude-messages.html

[^47]: https://learn.microsoft.com/en-us/dotnet/core/tutorials/cli-templates-create-project-template

[^48]: https://www.jetbrains.com/help/rider/Install_custom_project_templates.html

[^49]: https://www.reddit.com/r/dotnet/comments/1731gp6/create_nuget_package_and_publish_in_private_server/

[^50]: https://github.com/dotnet/templating/wiki/Reference-for-template.json/42630f82c912b4e890cb4c1a45f640689b73ad5a

[^51]: https://github.blog/changelog/2022-10-21-codespaces-configuration-with-the-dev-container-editor/

[^52]: https://dev.to/pwd9000/introduction-to-github-codespaces-building-your-first-dev-container-69l

[^53]: https://www.youtube.com/watch?v=0H2miBK_gAk

[^54]: https://docs.github.com/enterprise-cloud@latest/codespaces/setting-up-your-project-for-codespaces/adding-a-dev-container-configuration/setting-up-your-python-project-for-codespaces

[^55]: https://code.visualstudio.com/docs/devcontainers/containers

[^56]: https://docs.docker.com/get-started/introduction/develop-with-containers/

[^57]: https://www.youtube.com/watch?v=a5qkPEod9ng

[^58]: https://docs.github.com/en/actions/about-github-actions/about-continuous-integration-with-github-actions

[^59]: https://github.com/dotnet/roslyn-sdk

[^60]: http://roslyn-analyzers.readthedocs.io/en/latest/create-nuget-package.html

[^61]: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/

[^62]: https://www.nuget.org/packages/Microsoft.CodeAnalysis/

[^63]: https://github.com/dotnet/roslyn-sdk/blob/main/src/Microsoft.CodeAnalysis.Testing/README.md

[^64]: https://sarifweb.azurewebsites.net

[^65]: https://github.com/microsoft/sarif-tools

[^66]: https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html

[^67]: https://github.com/databricks-industry-solutions/security-analysis-tool

[^68]: https://www.anthropic.com/api

[^69]: https://docs.anthropic.com/en/docs/get-started

[^70]: https://support.anthropic.com/en/articles/10168395-setting-up-integrations-on-claude-ai

[^71]: https://stackoverflow.com/questions/58325232/how-do-i-ship-multiple-dotnet-new-templates-inside-a-single-nuget-package

[^72]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new

[^73]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers/adding-features-to-a-devcontainer-file

[^74]: https://docs.github.com/en/codespaces/setting-up-your-project-for-codespaces/configuring-dev-containers

[^75]: https://www.reddit.com/r/devops/comments/18ic2zx/seeking_opinions_on_github_actions_for_cicd/

[^76]: https://github.com/readme/guides/sothebys-github-actions


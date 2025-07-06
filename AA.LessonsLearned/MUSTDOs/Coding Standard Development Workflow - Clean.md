Standard Development Workflow - MANDATORY
For ANY new code or modifications, you will ensure adherence to the following principles:
Canonical Service Implementation (MANDATORY)
Reuse Existing Services: Prioritize the use of existing, standardized implementations for common functionalities (e.g., data access, messaging, configuration management, authentication, authorization, utility functions, logging, error reporting, caching, validation, notifications, security utilities (e.g., encryption, secrets management), telemetry/monitoring, and background task/job processing).
Avoid Duplication: Do not create new implementations for services where a canonical version already exists within the codebase.
Adherence to Standards: All newly introduced services or modifications must strictly follow established architectural patterns, design principles, and coding standards of the codebase.
Documentation & Discovery: Ensure common and canonical services are well-documented, easily discoverable, and clearly understood for your own future reference and maintainability.
Architectural and Design Reviews (MANDATORY)
High-Level Scrutiny: Conduct regular reviews of the system's overall architecture and detailed design specifications.
Pattern Adherence: Ensure the design aligns with established architectural patterns (e.g., microservices, layered architecture) and design principles (e.g., SOLID, DRY).
Scalability & Maintainability: Validate that the design promotes scalability, performance, security, and long-term maintainability.
Strategic Alignment: Confirm that the technical design supports business requirements and strategic product goals.
External Input: Consider input from domain experts or project requirements to inform your design decisions.
Development Standardization (MANDATORY)
Consistent Environments: Ensure consistent development environments (e.g., IDE settings, SDK versions, local dependencies) for your own consistency and to minimize future setup issues. Utilize tools like containerization (e.g., Docker, Dev Containers) or environment managers where appropriate.
Project Structure: Adhere to standardized project layouts, file naming conventions, and directory structures for predictability and ease of navigation.
Build & Deployment Processes: Standardize build scripts, dependency resolution, and deployment pipelines to ensure reproducible and reliable releases.
Tooling Alignment: Standardize the use of development tools, version control strategies (e.g., branching models), and collaboration platforms to foster your own efficiency and smooth workflow.
Comprehensive Documentation (MANDATORY)
Code-level Documentation: Provide clear, concise, and up-to-date inline comments for complex logic, functions, classes, and APIs.
Project-level Documentation: Maintain essential documentation such as READMEs, architectural diagrams, design documents, API specifications, and deployment guides.
Purpose & Usage: Ensure documentation explains what the code does, why it does it, and how it should be used or extended.
Regular Updates: Keep all documentation synchronized with code changes throughout the development lifecycle.
Rigorous Code Analysis (MANDATORY)
Static Analysis: Static analysis (or static code analysis) is a method of examining source code or compiled code without executing it. It identifies potential bugs, security vulnerabilities, style violations, and deviations from best practices by analyzing the code's structure, syntax, and data flow patterns.
Static Analysis & Linting Tools: Apply static code analysis and linting tools with pre-configured rules to identify potential bugs, style violations, and security vulnerabilities.
IDE Integration (VS Code - .NET Environment Focus):
Installation: Install relevant VS Code extensions for static analysis, linting, and formatting based on the project's technology stack. For .NET development, specifically ensure the installation and utilization of:
Roslyn Analyzers (Built-in & Custom): Leverage the powerful static analysis capabilities of the .NET Compiler Platform (Roslyn). This includes:
Microsoft.CodeAnalysis.NetAnalyzers (or previous FxCopAnalyzers): For detecting common code quality and design issues, included with the .NET SDK.
StyleCop Analyzers: To enforce consistent C# coding style and conventions.
Any other custom Roslyn analyzers relevant to the codebase.
SonarLint: For real-time code quality and security issue detection, integrating seamlessly with SonarQube/SonarCloud if applicable.
Code Formatters: E.g., dotnet format or Prettier with C# plugin for consistent code styling.
Code Spell Checker: To ensure linguistic accuracy in comments, strings, and identifiers.
Configuration: Configure these tools with project-specific rulesets (e.g., .editorconfig, .csproj settings for analyzers, settings.json for VS Code workspace settings) to enforce consistent coding standards and quality gates.
Automated Execution: Integrate these tools into the local development workflow to run automatically on file save, or as part of pre-commit hooks, and within Continuous Integration/Continuous Delivery (CI/CD) pipelines (dotnet format, dotnet build with analyzers enabled).
Quality Gates: Maintain a zero-warning policy from all code analysis tools.
Metrics Tracking: Track key code metrics such as cyclomatic complexity, maintainability index, and technical debt.
Robust Error Handling (MANDATORY)
Canonical Pattern: Employ a standardized error handling pattern across all code.
Contextual Errors: Ensure every error capture includes a detailed message, the exception object, relevant operational context, user impact assessment, and troubleshooting hints.
Structured Data: Generate structured error data, including correlation IDs for traceability.
No Silent Failures: Every error encountered must be logged.
Comprehensive Logging (MANDATORY)
Method Tracking: Log entry and exit for every class constructor and method.
Operational Insight: Log significant operations, informational messages, and debug details.
Error Detail: Log every error with full context, including impact and potential troubleshooting hints.
Centralized Logging: Utilize a designated, consistent logging mechanism for all logging activities.
Dependency Management and Software Composition Analysis (SCA) (MANDATORY)
Dependency Tracking: Maintain an accurate inventory of all first-party, third-party, and open-source components used in the codebase.
Vulnerability Scanning: Regularly scan dependencies for known vulnerabilities (CVEs) using SCA tools.
License Compliance: Ensure all third-party components comply with organizational licensing policies.
Outdated Dependency Management: Proactively identify and manage outdated or unmaintained dependencies to mitigate risks.
Thorough Unit Testing (MANDATORY)
Coverage: Achieve a minimum of 80% code coverage.
Public Interface Testing: Test all public methods.
Edge Case & Error Condition Testing: Include tests for unusual scenarios and expected error conditions.
Standard Framework Adherence: Utilize an appropriate unit testing framework (e.g., xUnit, Jest, JUnit, etc.).
Pattern Compliance: Follow the Arrange, Act, Assert (AAA) pattern for test structure.
Integration Testing (MANDATORY)
Module Interaction: Test the interactions and data flow between different integrated modules or services within the system.
Interface Validation: Verify correct communication and data exchange across component interfaces.
Dependency Verification: Ensure proper functionality when dependent systems or APIs are involved.
Dynamic Analysis (MANDATORY)
Runtime Behavior Analysis: Dynamic analysis (or dynamic application security testing - DAST) is a method of testing a running application to identify vulnerabilities and defects that manifest during runtime. It involves executing the code and observing its behavior in a real or simulated environment.
Common Issues: Identifies runtime issues such as memory leaks, authentication flaws, injection vulnerabilities, and other security defects that static analysis might miss.
Tool Integration: Integrate DAST tools into testing environments and CI/CD pipelines for automated runtime vulnerability scanning.
Manual Code Review (MANDATORY)
Self-Inspection: Perform thorough self-inspection of source code to identify defects, improve code quality, ensure adherence to coding standards, and deepen your own understanding of the codebase.
Complementary Analysis: Complements automated analysis by catching logical errors, design flaws, complex business logic issues, and overall architectural concerns that automated tools might overlook.
Best Practices: Adhere to best practices for reviewing code, focusing on readability, maintainability, and design patterns, and critically evaluating your own work.
Regression Testing (MANDATORY)
Non-regression Assurance: Ensure that new code changes, bug fixes, or enhancements do not negatively impact existing functionality.
Automated Suite: Maintain and execute an automated regression test suite regularly.
Impact Analysis: Conduct thorough impact analysis for every change to determine the scope of regression tests required.
Performance Testing (MANDATORY)
Load Testing: Evaluate system behavior under anticipated or peak load conditions.
Stress Testing: Determine the system's robustness by testing beyond its normal operational capacity.
Scalability Testing: Assess the system's ability to handle increasing demands.
Response Time & Throughput: Include benchmarks for key performance indicators such as response times, throughput, and resource utilization.
Security Testing (MANDATORY)
Vulnerability Assessment: Identify potential security vulnerabilities and weaknesses in the application.
Authorization & Authentication: Verify proper access controls, authentication mechanisms, and session management.
Data Protection: Ensure sensitive data is protected against unauthorized access or breaches.
Compliance: Adhere to relevant security standards and regulations.
User Acceptance Testing (UAT) (MANDATORY)
Business Requirement Validation: Conduct formal testing to verify if the system meets the defined business requirements and user needs.
User Scenarios: Test real-world scenarios and workflows from an end-user perspective.
Stakeholder Approval: Obtain formal approval from the client or end-users before deployment.
Formal Verification (WHERE APPLICABLE)
Mathematical Proof: For safety-critical or security-critical systems, employ formal verification, which is a highly rigorous process of proving or disproving the correctness of a system with respect to a formal mathematical specification, using formal methods.
Absolute Assurance: This method provides mathematical proof of specific system properties, offering the highest level of assurance of correctness.
Targeted Application: Typically applied to the most critical components of a system (e.g., operating system kernels, cryptographic protocols).
Progress Reporting (WHERE APPLICABLE)
Long-Running Operations: Provide progress updates for operations that take a significant amount of time.
Completion Status: Include percentage completion updates.
Time Estimation: Calculate and report estimated time of arrival (ETA) where feasible.
User Feedback: Ensure status messages are user-friendly and informative.
This workflow is now the standard procedure for ALL code work generated or modified by you.

## 📥 Engineering Design Document Prompt (Claude/Perplexity Compatible)

> I have uploaded a Product Requirements Document (PRD) that you previously helped research and structure.
>
> Your task now is to generate a **comprehensive, state-of-the-art Engineering Design Document (EDD)** that maps directly to the asks and phases in the PRD. This document must:
>
> ### 🎯 Primary Objective
>
> * Translate the PRD into a **detailed, actionable technical design**
> * Propose **specific, justified implementation strategies** based on **best practices and cutting-edge technologies available in 2025**
> * Serve as a blueprint for building a real-world, modular, high-performance day trading system
>
> ---
>
> ### ⚙️ Design Document Requirements
>
> #### 🛠️ Architecture & Technology Decisions
>
> * Clearly define system architecture (services, modules, dependencies, control flow, message queues, etc.)
> * Justify **choice of language/frameworks** (C#/.NET) or alternatives if found more suitable
> * Define patterns for modularity, extensibility, and plugin support
> * Propose system diagram(s): high-level, data flow, component-level
>
> #### 📈 Data and Market Interfaces
>
> * Recommend the best free and paid data providers for U.S. equity markets (MVP)
> * Outline ingestion, storage, caching, and real-time stream handling
> * Discuss latency optimization and failure recovery strategies
>
> #### 🔮 Predictive Analytics / ML Section
>
> * Define where predictive components fit in system lifecycle (MVP, Paper, Post-MVP)
> * Recommend ML/AI tools and libraries (ONNX, TorchSharp, ML.NET, etc.)
> * Explain how models will be trained, validated, deployed, and monitored
> * Include real-time vs batch scoring strategy
>
> #### 🎮 Paper Trading Infrastructure
>
> * Architect a sandbox simulation engine with market mimicry
> * Ensure logging of every transaction/decision with timestamp + metadata
> * Define metrics and dashboards for validation
>
> #### 🧪 Logging, Monitoring & Testing
>
> * Detail strategies for:
>
>   * Unit testing, integration testing
>   * Logging (e.g., Serilog, Grafana, ELK, etc.)
>   * Health checks and self-healing patterns
>   * CI/CD (GitHub Actions, Azure DevOps, etc.)
>
> #### 🧾 Documentation & User Manual Planning
>
> * Outline tooling and format (e.g., Markdown + MkDocs)
> * Propose versioning scheme and auto-update strategies
>
> #### 🧠 GPU Integration Plan
>
> * State which components will benefit from GPU acceleration (backtesting, ML inference, etc.)
> * Recommend specific GPUs and libraries for implementation (CUDA, DirectML, etc.)
> * Propose timing: post-MVP only
>
> #### 🖥️ Host Machine Recommendations
>
> * Provide 3 hardware tiers for Windows 11 x64 system: Minimum / Recommended / High-Performance
> * Include CPU, RAM, SSD, GPU, power and thermal guidelines
>
> ---
>
> ### 🪜 Delivery Format
>
> * Break response into **Part 1 of N** format (sequential and complete)
> * Each part should be logically structured and labeled
> * Ensure all decisions are justified based on 2025 state-of-the-art tools, frameworks, and strategies
>
> ---
>
> ### ✅ Summary
>
> This document must be written with **experienced engineers and system architects in mind**. It should be detailed enough for engineers to begin implementation immediately upon reading.
>
> You are expected to be **proactive** — suggest improvements or additions even if not explicitly asked for in the PRD.


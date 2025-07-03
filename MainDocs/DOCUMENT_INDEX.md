# Day Trading Platform - Document Index
**Updated**: 2025-01-30  
**Purpose**: Master index of all documentation locations after cleanup

## Directory Structure Overview

```
DayTradingPlatform/
├── README.md                    # Main project documentation
├── CLAUDE.md                    # AI assistant instructions
├── MainDocs/                    # Project documentation
│   ├── Strategies/             # Planning and strategy documents
│   ├── Technical/              # Technical documentation
│   └── V2.x/                   # Version 2 documentation
├── ResearchDocs/               # Research and analysis
│   └── Analysis/               # Benchmark and performance analysis
├── Journals/                   # Development logs and sessions
│   └── Audits/                 # Audit reports and progress tracking
├── MiscFiles/                  # Temporary and working files
└── scripts/                    # Automation scripts
```

## Document Locations by Category

### 📋 Strategy & Planning Documents
**Location**: `MainDocs/Strategies/`

- `MASTER_TODO_LIST.md` - **Central TODO tracker with current task status** 🎯
- `MASTER_ACTION_PLAN_2025.md` - 8-week roadmap to production deployment
- `EXECUTION_PLAN.md` - Detailed implementation execution plan
- `IMMEDIATE_ACTIONS.md` - Current priority action items
- `IMMEDIATE_ACTIONS_QUICKREF.md` - Quick reference for daily tasks
- `COMPREHENSIVE_TESTING_STRATEGY.md` - Three-layer testing framework (unit/integration/E2E)
- `LOGGING_IMPLEMENTATION_PLAN.md` - MCP-compliant logging implementation
- `MIGRATION_PLAN_SAFE_ADOPTIONS.md` - Non-critical path migration strategy

### 🔬 Research & Analysis Documents
**Location**: `ResearchDocs/` and `ResearchDocs/Analysis/`

#### Performance Analysis
- `Analysis/BENCHMARK_RESULTS_ANALYSIS.md` - Custom vs standard library benchmarks
- `Analysis/DISRUPTOR_BENCHMARK_ANALYSIS.md` - Disruptor.NET performance analysis
- `Analysis/NON_STANDARD_IMPLEMENTATIONS_REPORT.md` - Custom implementation audit

#### Technical Research
- `STANDARD_LIBRARY_CANDIDATES_RESEARCH.md` - HFT-compatible library evaluation
- `COMPREHENSIVE_VIOLATION_PRIORITY_PLAN.md` - Code violation prioritization
- `RAPM_Implementation_Guide.md` - Risk-Adjusted Performance Measurement
- `Modern Portfolio Optimization & Risk Modeling in Quantitative Finance.md`
- `Multi-Screen Layout and Information Architecture for Day Trading Platforms.md`

### 🛠️ Technical Documentation
**Location**: `MainDocs/Technical/`

- `BASELINE.md` - Project baseline configuration
- `TESTING-GUIDE.md` - Comprehensive testing guidelines
- `README-Docker.md` - Docker setup and deployment

### 📊 Development Journals
**Location**: `Journals/`

- Session logs with timestamp format: `YYYY-MM-DD_description.md`
- `2025-01-30_comprehensive_analysis_and_planning.md` - Latest planning session
- `2025-01-30_critical_fixes_and_file_organization.md` - ApiRateLimiter fixes and cleanup
- `2025-01-30_comprehensive_testing_implementation.md` - Testing framework implementation

### 🔍 Audit Reports
**Location**: `Journals/Audits/`

- `GIT_COMMIT_MESSAGE_AUDIT.md` - Git history audit
- `INDEX_UPDATE_2025-06-24_AUDIT.md` - Index update audit
- `MCP_ANALYSIS_REPORT.md` - MCP code analysis findings
- `MCP_FIXES_PROGRESS.md` - MCP violation fix tracking
- `MCP_NULL_REFERENCE_FALSE_POSITIVES.md` - False positive analysis

### 📚 Project Requirements & Design
**Location**: `MainDocs/`

- `Day Trading Stock Recommendation Platform - PRD.md` - Product requirements
- `FinancialCalculationStandards.md` - Decimal precision standards
- `High_Performance_Stock_Day_Trading_Platform.md.txt` - Ultra-low latency specs
- `Professional Multi-Screen Trading Systems_Architecture.md` - Multi-monitor design
- `The_Complete_Day_Trading_Reference_Guide_Golden_Rules.md` - Trading principles

### 🔧 Source Code Documentation
**Location**: Within respective project directories

- `/DayTradinPlatform/` - Main solution directory
- `/DayTradinPlatform/TradingPlatform.Tests.Core/Canonical/` - Test framework base classes
- `/scripts/` - Automation and utility scripts

## Quick Access Guide

### For Daily Development:
1. **Today's Tasks**: `MainDocs/Strategies/IMMEDIATE_ACTIONS_QUICKREF.md`
2. **Testing Guide**: `MainDocs/Strategies/COMPREHENSIVE_TESTING_STRATEGY.md`
3. **Action Plan**: `MainDocs/Strategies/MASTER_ACTION_PLAN_2025.md`

### For Architecture Decisions:
1. **Performance Requirements**: `MainDocs/High_Performance_Stock_Day_Trading_Platform.md.txt`
2. **System Architecture**: `MainDocs/Professional Multi-Screen Trading Systems_Architecture.md`
3. **Financial Standards**: `MainDocs/FinancialCalculationStandards.md`

### For Implementation:
1. **Benchmark Results**: `ResearchDocs/Analysis/BENCHMARK_RESULTS_ANALYSIS.md`
2. **Migration Plan**: `MainDocs/Strategies/MIGRATION_PLAN_SAFE_ADOPTIONS.md`
3. **Logging Design**: `MainDocs/Strategies/LOGGING_IMPLEMENTATION_PLAN.md`

## Search Tips

- Strategy documents: Look in `MainDocs/Strategies/`
- Research findings: Check `ResearchDocs/` and `ResearchDocs/Analysis/`
- Session logs: Browse `Journals/` by date
- Technical specs: Find in `MainDocs/` or `MainDocs/Technical/`
- Audit reports: Located in `Journals/Audits/`

---

*Note: This index is current as of the cleanup performed on 2025-01-30. Update this document when adding new documentation or reorganizing the structure.*
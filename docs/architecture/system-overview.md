# System Architecture Overview

## Three-Project Ecosystem

This document provides a comprehensive overview of the integrated system architecture comprising:
- **Day Trading Platform** (C# .NET 8.0)
- **MCP Code Analyzer** (TypeScript/Node.js)
- **Linear Bug Tracker** (TypeScript/React)

## Architecture Diagram

```mermaid
graph TB
    subgraph "Development Tools Layer"
        MCP[MCP Code Analyzer<br/>TypeScript/Node.js<br/>Port: 3000<br/>24x7 Operation]
        Linear[Linear Bug Tracker<br/>TypeScript/React<br/>Backend: 3001<br/>Frontend: 4000]
    end
    
    subgraph "Trading System Layer"
        DTP[Day Trading Platform<br/>C# .NET 8.0<br/>Windows x64<br/>Ultra-low latency]
        TA[TradingAgent Service<br/>Node.js<br/>Redis Pub/Sub]
    end
    
    subgraph "Infrastructure Layer"
        Redis[(Redis<br/>Pub/Sub Messaging<br/>Port: 6379)]
        FS[File System<br/>Monitoring<br/>Real-time]
        SQLite1[(SQLite<br/>MCP Data<br/>Analysis Results)]
        SQLite2[(SQLite<br/>Linear Data<br/>Issues/Projects)]
        SharedConfig[Shared Config<br/>Types & Schemas<br/>Integration Contracts]
    end
    
    subgraph "External Services"
        AV[AlphaVantage API<br/>Market Data]
        FH[Finnhub API<br/>Real-time Quotes]
        MM[Mattermost<br/>AgentHub Channels]
    end
    
    %% File System Monitoring
    MCP -->|Monitors Files| FS
    FS -->|C# Files| DTP
    FS -->|TS/JS Files| Linear
    
    %% Redis Communication
    MCP -->|Publishes Analysis| Redis
    Linear -->|Subscribes/Publishes| Redis
    TA -->|Pub/Sub Messages| Redis
    Redis -->|Webhook Bridge| MM
    
    %% Database Connections
    MCP --> SQLite1
    Linear --> SQLite2
    
    %% External API Connections
    DTP --> AV
    DTP --> FH
    
    %% Component Relationships
    TA -.->|Component of| DTP
    SharedConfig -.->|Used by| MCP
    SharedConfig -.->|Used by| Linear
    SharedConfig -.->|Used by| TA
    
    %% Styling
    classDef trading fill:#f9f,stroke:#333,stroke-width:4px
    classDef tools fill:#bbf,stroke:#333,stroke-width:2px
    classDef infra fill:#bfb,stroke:#333,stroke-width:2px
    classDef external fill:#fbb,stroke:#333,stroke-width:2px
    
    class DTP,TA trading
    class MCP,Linear tools
    class Redis,FS,SQLite1,SQLite2,SharedConfig infra
    class AV,FH,MM external
```

## Component Details

### 1. Day Trading Platform (Core Business Application)
- **Technology**: C# .NET 8.0, Windows x64
- **Purpose**: High-performance financial trading system
- **Key Features**:
  - Ultra-low latency (<100μs targets)
  - System.Decimal for financial precision
  - Multi-monitor support (4-6 displays)
  - Rate-limited market data ingestion
  - Canonical service patterns

### 2. MCP Code Analyzer (Code Quality Guardian)
- **Technology**: TypeScript, Node.js, MCP SDK
- **Purpose**: Real-time code analysis and quality enforcement
- **Key Features**:
  - 25 specialized analysis tools
  - Financial precision validation
  - Security scanning (Trivy, Gitleaks)
  - 24x7 mandatory operation
  - Multi-language support (C#, TypeScript)

### 3. Linear Bug Tracker (Development Management)
- **Technology**: TypeScript, React, GraphQL
- **Purpose**: Multi-project issue tracking and management
- **Key Features**:
  - Full-text search capabilities
  - File attachment support
  - JWT authentication
  - PM2 for high availability
  - Auto-issue creation from MCP

### 4. TradingAgent (Communication Service)
- **Technology**: Node.js
- **Purpose**: Inter-agent communication for Trading Platform
- **Key Features**:
  - Redis pub/sub messaging
  - System-wide channel monitoring
  - AgentHub integration
  - 5-minute heartbeat

## Communication Patterns

### Redis Pub/Sub Channels
```
tradingagent:*  → Trading platform messages
mcp:*          → Code analysis results
linear:*       → Bug tracking updates
agent:*        → General agent communication
alert:*        → System-wide alerts
```

### Message Flow Examples

1. **Code Analysis Flow**:
   ```
   File Change → MCP Detects → Analysis → Redis → Linear (auto-issue) → Mattermost
   ```

2. **Trading Alert Flow**:
   ```
   Market Event → Trading Platform → TradingAgent → Redis → All Subscribers → Mattermost
   ```

3. **Bug Report Flow**:
   ```
   Linear UI → GraphQL → Backend → Redis → MCP (if needed) → Response
   ```

## Integration Points

### Shared Configuration
- Location: `/home/nader/my_projects/CS/shared-config`
- Contents: Message types, schemas, error codes, channel definitions
- Usage: NPM package for TypeScript projects, JSON references for C#

### File System Monitoring
- MCP monitors both project directories
- Real-time detection of code changes
- Automatic analysis triggers

### Database Integration
- MCP: SQLite for analysis results and metrics
- Linear: SQLite for issues, projects, and attachments
- No direct database sharing (message-based integration)

## Deployment Architecture

### Process Management
- **PM2** manages Node.js services:
  - webhook-bridge (Redis to Mattermost)
  - mcp-server
  - linear-backend
  - tradingagent (optional)

### Network Topology
- All services run on localhost (development)
- Redis on port 6379
- MCP on port 3000
- Linear backend on port 3001
- Linear frontend on port 4000

### Security Considerations
- JWT authentication for Linear
- Redis password protection (production)
- Isolated execution environments
- No direct cross-project file access

## Benefits of Separation

1. **Technology Optimization**: Each project uses optimal stack
2. **Independent Scaling**: Services scale based on individual needs
3. **Risk Isolation**: Trading platform isolated from tools
4. **Clear Boundaries**: Well-defined interfaces via messages
5. **Deployment Flexibility**: Deploy independently

## Future Enhancements

1. **Message Queue**: Add RabbitMQ for guaranteed delivery
2. **Service Mesh**: Implement for production deployments
3. **Monitoring**: Add Prometheus/Grafana stack
4. **API Gateway**: Centralize external access
5. **Container Orchestration**: Kubernetes for production

---

*Last Updated: 2025-01-30*  
*Version: 1.0*
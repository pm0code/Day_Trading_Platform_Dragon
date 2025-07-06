# Unified 24x7 High Availability Implementation Guide

**Date**: 2025-01-03  
**Purpose**: Comprehensive guide for implementing 24x7 high availability across all agent projects  
**Scope**: Ubuntu 24.x, systemd, PM2, and open-source monitoring tools

## Table of Contents

1. [Core Principles](#core-principles)
2. [Architecture Overview](#architecture-overview)
3. [Service Management](#service-management)
4. [Self-Healing Mechanisms](#self-healing-mechanisms)
5. [Monitoring and Observability](#monitoring-and-observability)
6. [Alerting and Notifications](#alerting-and-notifications)
7. [Security and Hardening](#security-and-hardening)
8. [Implementation Checklist](#implementation-checklist)
9. [Tool Selection Matrix](#tool-selection-matrix)
10. [Common Patterns](#common-patterns)

## Core Principles

### 1. Use What's Already There
- **systemd** is the primary service manager on Ubuntu
- **PM2** for Node.js applications (already in use)
- **journald** for centralized logging
- **Redis** for coordination and state management

### 2. Defense in Depth
- Multiple layers of monitoring and recovery
- Circuit breakers to prevent cascade failures
- Resource limits to prevent single service from affecting others

### 3. Observable by Default
- Structured logging to journald
- Metrics exposed via Prometheus format
- Health endpoints for all services

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Application Layer                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │   Service   │  │   Service   │  │   Service   │        │
│  │   (PM2)     │  │  (systemd)  │  │   (Docker)  │        │
│  └─────────────┘  └─────────────┘  └─────────────┘        │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────┐
│                    Management Layer                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │ systemd  │  │   PM2    │  │  Monit   │  │ Watchdog │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────┐
│                  Monitoring Layer                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │Prometheus│  │ journald │  │ rsyslog  │  │  Loki    │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
└─────────────────────────┬───────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────┐
│                 Visualization Layer                          │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                 │
│  │ Grafana  │  │  Kibana  │  │Dashboard │                 │
│  └──────────┘  └──────────┘  └──────────┘                 │
└─────────────────────────────────────────────────────────────┘
```

## Service Management

### systemd Service Template

```ini
[Unit]
Description=%i Service
After=network.target redis.service
StartLimitIntervalSec=300
StartLimitBurst=5
OnFailure=recovery@%i.service

[Service]
Type=simple
User=nader
Group=nader
WorkingDirectory=/home/nader/my_projects/CS/%i
Environment="NODE_ENV=production"
ExecStartPre=/bin/bash -c 'test -f .env || exit 0'
ExecStart=/usr/bin/node server.js
Restart=always
RestartSec=10

# Resource Limits
MemoryMax=2G
CPUQuota=200%
LimitNOFILE=65536

# Security
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=read-only

# Logging
StandardOutput=journal
StandardError=journal
SyslogIdentifier=%i

[Install]
WantedBy=multi-user.target
```

### PM2 Ecosystem Template

```javascript
module.exports = {
  apps: [{
    name: 'service-name',
    script: './server.js',
    instances: 2,
    exec_mode: 'cluster',
    
    // Memory Management
    max_memory_restart: '1G',
    
    // Auto-restart
    autorestart: true,
    watch: false,
    max_restarts: 10,
    min_uptime: '10s',
    
    // Logs
    error_file: './logs/error.log',
    out_file: './logs/out.log',
    merge_logs: true,
    time: true,
    
    // Environment
    env: {
      NODE_ENV: 'production',
      PORT: 3000
    },
    
    // Graceful shutdown
    kill_timeout: 5000,
    listen_timeout: 3000,
    
    // Crash recovery
    exp_backoff_restart_delay: 100
  }]
};
```

## Self-Healing Mechanisms

### 1. Circuit Breaker Pattern

```typescript
class CircuitBreaker {
  private state: 'CLOSED' | 'OPEN' | 'HALF_OPEN' = 'CLOSED';
  private failures = 0;
  private lastFailureTime = 0;
  
  constructor(
    private failureThreshold: number,
    private resetTimeout: number
  ) {}
  
  async execute<T>(fn: () => Promise<T>): Promise<T> {
    if (this.state === 'OPEN') {
      if (Date.now() - this.lastFailureTime > this.resetTimeout) {
        this.state = 'HALF_OPEN';
      } else {
        throw new Error('Circuit breaker is OPEN');
      }
    }
    
    try {
      const result = await fn();
      this.onSuccess();
      return result;
    } catch (error) {
      this.onFailure();
      throw error;
    }
  }
  
  private onSuccess() {
    this.failures = 0;
    if (this.state === 'HALF_OPEN') {
      this.state = 'CLOSED';
    }
  }
  
  private onFailure() {
    this.failures++;
    this.lastFailureTime = Date.now();
    if (this.failures >= this.failureThreshold) {
      this.state = 'OPEN';
    }
  }
}
```

### 2. Health Check Endpoints

Every service MUST expose:
- `/health` - Basic liveness check
- `/health/ready` - Readiness check (dependencies)
- `/metrics` - Prometheus metrics

### 3. Recovery Actions

```bash
#!/bin/bash
# recovery.sh - Generic recovery script

SERVICE=$1
MAX_ATTEMPTS=3
ATTEMPT=0

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" | systemd-cat -t "recovery-$SERVICE"
}

while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    log "Recovery attempt $((ATTEMPT + 1)) for $SERVICE"
    
    # Service-specific recovery
    case $SERVICE in
        redis)
            systemctl restart redis
            sleep 5
            redis-cli ping && break
            ;;
        *)
            systemctl restart "$SERVICE"
            sleep 10
            systemctl is-active "$SERVICE" && break
            ;;
    esac
    
    ATTEMPT=$((ATTEMPT + 1))
done

if [ $ATTEMPT -eq $MAX_ATTEMPTS ]; then
    log "CRITICAL: Failed to recover $SERVICE after $MAX_ATTEMPTS attempts"
    # Send alert
    redis-cli PUBLISH "alert:critical" "{\"service\":\"$SERVICE\",\"status\":\"failed\"}"
fi
```

## Monitoring and Observability

### 1. Structured Logging

All logs MUST include:
- Timestamp (ISO 8601)
- Level (ERROR, WARN, INFO, DEBUG)
- Service name
- Correlation ID (for request tracing)
- Structured data (JSON)

Example:
```json
{
  "timestamp": "2025-01-03T10:30:45.123Z",
  "level": "ERROR",
  "service": "mcp-analyzer",
  "correlationId": "abc123",
  "message": "Failed to connect to Redis",
  "error": {
    "code": "ECONNREFUSED",
    "host": "localhost",
    "port": 6379
  }
}
```

### 2. Prometheus Metrics

Standard metrics every service MUST expose:

```prometheus
# Service Info
service_info{version="1.0.0",commit="abc123"} 1

# Request metrics
http_requests_total{method="GET",status="200"} 1234
http_request_duration_seconds{method="GET",quantile="0.95"} 0.123

# Process metrics
process_cpu_seconds_total 123.45
process_resident_memory_bytes 12345678
process_open_fds 42

# Custom metrics
circuit_breaker_state{name="redis",state="closed"} 1
health_check_status{check="database"} 1
```

### 3. Log Aggregation

#### Using journald (Recommended for single server)
```bash
# View logs for a service
journalctl -u mcp-analyzer -f

# Export logs for analysis
journalctl -u mcp-analyzer --since "1 hour ago" -o json > logs.json

# Forward to rsyslog
echo 'if $programname == "mcp-analyzer" then /var/log/mcp/analyzer.log' > /etc/rsyslog.d/10-mcp.conf
```

#### Using Loki (For advanced search)
```yaml
# promtail config
server:
  http_listen_port: 9080

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://localhost:3100/loki/api/v1/push

scrape_configs:
  - job_name: journal
    journal:
      json: true
      max_age: 12h
      labels:
        job: systemd-journal
    relabel_configs:
      - source_labels: ['__journal__systemd_unit']
        target_label: 'unit'
```

## Alerting and Notifications

### 1. Alert Rules

```yaml
# prometheus/alerts.yml
groups:
  - name: ServiceAlerts
    rules:
      - alert: ServiceDown
        expr: up == 0
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Service {{ $labels.job }} is down"
          
      - alert: HighMemoryUsage
        expr: process_resident_memory_bytes > 2e9
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage in {{ $labels.job }}"
          
      - alert: CircuitBreakerOpen
        expr: circuit_breaker_state{state="open"} == 1
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Circuit breaker {{ $labels.name }} is open"
```

### 2. Notification Channels

```yaml
# alertmanager.yml
route:
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'default'
  
receivers:
  - name: 'default'
    webhook_configs:
      - url: 'http://localhost:3000/alerts'
        send_resolved: true
```

## Security and Hardening

### 1. systemd Security Options

```ini
[Service]
# User/Group
User=service-user
Group=service-group

# Filesystem
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=/var/lib/service

# Network
RestrictAddressFamilies=AF_INET AF_INET6
IPAccounting=true

# Capabilities
NoNewPrivileges=true
CapabilityBoundingSet=
AmbientCapabilities=

# System calls
SystemCallFilter=@system-service
SystemCallErrorNumber=EPERM
```

### 2. Resource Limits

```bash
# /etc/security/limits.d/services.conf
service-user soft nofile 65536
service-user hard nofile 65536
service-user soft nproc 4096
service-user hard nproc 4096
```

### 3. Firewall Rules

```bash
# UFW rules
ufw allow from 127.0.0.1 to any port 3456 comment "MCP Analyzer"
ufw allow from 10.0.0.0/8 to any port 9090 comment "Prometheus"
ufw allow from 10.0.0.0/8 to any port 3000 comment "Grafana"
```

## Implementation Checklist

### For Each Service:

- [ ] Create systemd service file
- [ ] Set resource limits (Memory, CPU, file descriptors)
- [ ] Implement health check endpoints
- [ ] Add Prometheus metrics
- [ ] Configure structured logging
- [ ] Create recovery script
- [ ] Set up monitoring alerts
- [ ] Document runbook

### System-Wide:

- [ ] Install and configure Prometheus
- [ ] Install and configure Grafana
- [ ] Set up Alertmanager
- [ ] Configure log rotation
- [ ] Enable journald persistence
- [ ] Set up backup strategy
- [ ] Create disaster recovery plan
- [ ] Test failover scenarios

## Tool Selection Matrix

| Function | Recommended | Alternative | When to Use |
|----------|-------------|-------------|-------------|
| Process Management | systemd | PM2 | systemd for system services, PM2 for Node.js |
| Monitoring | Prometheus | Telegraf | Prometheus for pull-based, Telegraf for push |
| Logs | journald | rsyslog | journald for local, rsyslog for remote |
| Dashboards | Grafana | Custom | Grafana for standard, custom for specific needs |
| Alerts | Alertmanager | Monit | Alertmanager for complex rules, Monit for simple |
| Backup | restic | rsync | restic for encrypted, rsync for simple |

## Common Patterns

### 1. Graceful Shutdown

```javascript
const shutdown = async (signal) => {
  logger.info(`Received ${signal}, shutting down gracefully...`);
  
  // Stop accepting new requests
  server.close();
  
  // Wait for existing requests to complete
  await new Promise(resolve => server.on('close', resolve));
  
  // Close database connections
  await db.close();
  
  // Close Redis connections
  await redis.quit();
  
  process.exit(0);
};

process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT', () => shutdown('SIGINT'));
```

### 2. Health Check Implementation

```javascript
app.get('/health', (req, res) => {
  res.json({ status: 'ok', timestamp: new Date().toISOString() });
});

app.get('/health/ready', async (req, res) => {
  const checks = {
    database: await checkDatabase(),
    redis: await checkRedis(),
    disk: await checkDiskSpace(),
    memory: checkMemory()
  };
  
  const allHealthy = Object.values(checks).every(check => check);
  
  res.status(allHealthy ? 200 : 503).json({
    status: allHealthy ? 'ready' : 'not ready',
    checks
  });
});
```

### 3. Metrics Collection

```javascript
const promClient = require('prom-client');
const register = new promClient.Registry();

// Default metrics
promClient.collectDefaultMetrics({ register });

// Custom metrics
const httpDuration = new promClient.Histogram({
  name: 'http_request_duration_seconds',
  help: 'Duration of HTTP requests in seconds',
  labelNames: ['method', 'route', 'status'],
  registers: [register]
});

// Middleware
app.use((req, res, next) => {
  const end = httpDuration.startTimer();
  res.on('finish', () => {
    end({ method: req.method, route: req.route?.path || 'unknown', status: res.statusCode });
  });
  next();
});

app.get('/metrics', async (req, res) => {
  res.set('Content-Type', register.contentType);
  res.end(await register.metrics());
});
```

## Summary

This guide provides a comprehensive approach to achieving 24x7 high availability using:
- Native Linux tools (systemd, journald)
- Proven patterns (circuit breakers, health checks)
- Open-source monitoring (Prometheus, Grafana)
- Defense-in-depth security

All agents should follow these patterns to ensure consistent, reliable, and observable services.
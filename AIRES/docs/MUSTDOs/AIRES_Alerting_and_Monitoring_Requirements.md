# AIRES Alerting and Monitoring Requirements

**MANDATORY**: AIRES must implement comprehensive alerting and monitoring to ensure system health visibility and immediate notification of issues.

## 🚨 Alert Categories

### 1. **Critical Alerts** (Immediate Action Required)
- **AI Service Unavailable**: Any AI model (Mistral, DeepSeek, CodeGemma, Gemma2) unreachable
- **Configuration Missing**: aires.ini not found or invalid
- **Disk Space Critical**: < 100MB available in output directory
- **Memory Pressure**: > 90% memory usage
- **Unhandled Exceptions**: Any uncaught exception in pipeline

### 2. **Warning Alerts** (Attention Needed)
- **AI Service Slow**: Response time > 60s from any model
- **High Error Rate**: > 20% of files failing to process
- **Configuration Issues**: Invalid settings detected
- **Disk Space Low**: < 500MB available
- **Memory High**: > 70% memory usage

### 3. **Informational Alerts**
- **Processing Complete**: Booklet successfully generated
- **Watchdog Started/Stopped**: Service lifecycle events
- **Configuration Reloaded**: Settings changes detected
- **Daily Summary**: Processing statistics

## 📡 Alert Channels

### 1. **Console Output** (Always Active)
```csharp
// Structured console alerts with severity levels
[CRITICAL] 2025-07-13 14:32:15 - Ollama service unavailable at http://localhost:11434
[WARNING]  2025-07-13 14:32:16 - AI response time exceeded threshold: 65s
[INFO]     2025-07-13 14:32:45 - Booklet generated: CS0246_Resolution_20250713_143245.md
```

### 2. **Log Files** (Always Active)
- Location: `./logs/aires-{date}.log`
- Structured JSON format for parsing
- Automatic rotation daily
- 30-day retention

### 3. **Windows Event Log** (For System Integration)
```csharp
// Write to Windows Application Event Log
EventLog.WriteEntry("AIRES", message, EventLogEntryType.Error, eventId);
```

### 4. **File-Based Alerts** (For Agent Monitoring)
```
Location: ./alerts/
Format: {severity}_{timestamp}_{alert_type}.json
Content: {
  "timestamp": "2025-07-13T14:32:15Z",
  "severity": "CRITICAL",
  "component": "OllamaClient",
  "message": "Service unavailable",
  "details": { ... },
  "suggested_action": "Check if Ollama is running"
}
```

### 5. **Health Check Endpoint** (For External Monitoring)
```
GET /health
Response: {
  "status": "healthy|degraded|unhealthy",
  "timestamp": "2025-07-13T14:32:15Z",
  "components": {
    "ollama": "healthy",
    "filesystem": "healthy",
    "memory": "healthy"
  },
  "metrics": {
    "uptime_seconds": 3600,
    "files_processed": 42,
    "errors_last_hour": 0
  }
}
```

## 🔔 Alert Implementation Requirements

### 1. **Alert Service Interface**
```csharp
public interface IAIRESAlertingService
{
    Task RaiseAlertAsync(AlertSeverity severity, string component, string message, Dictionary<string, object>? details = null);
    Task<HealthCheckResult> GetHealthStatusAsync();
    Task<AlertStatistics> GetAlertStatisticsAsync(TimeSpan period);
}
```

### 2. **Alert Severity Levels**
```csharp
public enum AlertSeverity
{
    Critical = 1,  // System failure, immediate action required
    Warning = 2,   // Degraded performance, attention needed
    Info = 3,      // Normal operations, FYI
    Debug = 4      // Detailed diagnostic information
}
```

### 3. **Alert Throttling**
- Same alert type: Maximum 1 per minute
- Burst protection: Maximum 10 alerts per minute total
- Summary mode: Aggregate similar alerts hourly

### 4. **Alert Persistence**
```csharp
public class AlertRecord
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Component { get; set; }
    public string Message { get; set; }
    public Dictionary<string, object> Details { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
```

## 📊 Monitoring Metrics

### 1. **System Metrics**
- CPU usage percentage
- Memory usage (MB)
- Disk space available (MB)
- Thread count
- Handle count

### 2. **Application Metrics**
- Files processed per hour
- Average processing time
- Success/failure ratio
- AI service response times
- Booklets generated count

### 3. **Error Metrics**
- Exceptions per hour
- Failed file processing count
- AI service timeout count
- Configuration errors count

## 🎯 Alert Rules Engine

### Rule Examples:
```yaml
rules:
  - name: "AI Service Critical"
    condition: "ollama.consecutive_failures > 3"
    severity: "critical"
    message: "Ollama service has failed 3 times consecutively"
    action: "notify_all_channels"
    
  - name: "High Memory Usage"
    condition: "system.memory_percent > 90"
    severity: "warning"
    message: "Memory usage exceeds 90%"
    cooldown_minutes: 5
    
  - name: "Processing Success"
    condition: "pipeline.booklet_generated"
    severity: "info"
    message: "Booklet successfully generated"
    channels: ["log", "file"]
```

## 🔌 Integration with AIRES Components

### 1. **AIRESServiceBase Enhancement**
```csharp
protected virtual void LogAlert(AlertSeverity severity, string message, Exception? ex = null)
{
    // Log to standard logger
    LogError(message, ex);
    
    // Raise alert through alerting service
    _alertingService.RaiseAlertAsync(severity, ServiceName, message, GetAlertDetails(ex));
}
```

### 2. **CLI Integration**
- `aires alerts list` - Show recent alerts
- `aires alerts ack <id>` - Acknowledge an alert
- `aires health` - Show system health status

### 3. **Watchdog Integration**
- Monitor alert directory for critical alerts
- Auto-restart on repeated failures
- Email notification for critical alerts (future)

## 📈 Monitoring Dashboard (Future)

### Planned Metrics Display:
1. Real-time processing status
2. AI service health indicators
3. Resource usage graphs
4. Alert history timeline
5. Success/failure trends

## 🚑 Incident Response

### Alert Response Matrix:
| Severity | Response Time | Action Required |
|----------|---------------|-----------------|
| Critical | Immediate | Stop processing, notify operator |
| Warning | < 30 minutes | Investigate, plan remediation |
| Info | As needed | Review in daily summary |

### Escalation Path:
1. Console + Log (always)
2. Alert file creation (for agents)
3. Windows Event Log (for system admins)
4. Future: Email/SMS/Webhook

## 📝 Implementation Checklist

- [ ] Create IAIRESAlertingService interface
- [ ] Implement FileBasedAlertingService
- [ ] Integrate with AIRESServiceBase
- [ ] Add health check endpoint
- [ ] Create alert file watcher for agents
- [ ] Implement alert throttling
- [ ] Add Windows Event Log integration
- [ ] Create CLI alert commands
- [ ] Write alert rule engine
- [ ] Add metrics collection
- [ ] Create alert unit tests
- [ ] Document alert response procedures

---

**Note**: This alerting system ensures AIRES can communicate its status effectively to both human operators and AI agents, enabling proactive issue resolution and system health monitoring.
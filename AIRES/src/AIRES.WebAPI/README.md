# AIRES Health Monitoring API

## Overview
The AIRES Health Monitoring API provides REST endpoints for monitoring the health and status of all AIRES components. This API is designed for integration with external monitoring tools like Prometheus, Grafana, or custom dashboards.

## Running the API

### Using the provided script:
```bash
./scripts/run-health-api.sh
```

### Or manually:
```bash
cd src/AIRES.WebAPI
dotnet run
```

## Available Endpoints

### Health Endpoints

#### GET /api/health
Comprehensive health check of all AIRES components.

**Response:**
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "totalDuration": 123,
  "timestamp": "2025-01-14T12:00:00Z",
  "summary": {
    "total": 10,
    "healthy": 8,
    "degraded": 1,
    "unhealthy": 1
  },
  "components": [
    {
      "name": "AIResearchOrchestratorService",
      "status": "Healthy",
      "category": "Core Service",
      "duration": 45,
      "checkedAt": "2025-01-14T12:00:00Z",
      "diagnostics": {...}
    }
  ]
}
```

#### GET /api/health/live
Liveness probe - simple check if the API is running.

**Response:**
```json
{
  "status": "Alive",
  "timestamp": "2025-01-14T12:00:00Z",
  "service": "AIRES"
}
```

#### GET /api/health/ready
Readiness probe - checks if critical components are ready.

**Response:**
```json
{
  "status": "Ready|NotReady",
  "timestamp": "2025-01-14T12:00:00Z",
  "criticalComponents": [...]
}
```

#### GET /api/health/{componentName}
Get health status for a specific component.

**Example:** `/api/health/AIResearchOrchestratorService`

### Documentation

#### GET /
Root endpoint with API information and available endpoints.

#### GET /swagger
Swagger UI for interactive API documentation.

## Configuration

The API reads configuration from:
1. `appsettings.json` - API-specific settings
2. `aires.ini` - AIRES system configuration
3. Environment variables with `AIRES_` prefix

### Default Ports
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

## Integration Examples

### Prometheus Configuration
```yaml
scrape_configs:
  - job_name: 'aires-health'
    scrape_interval: 30s
    metrics_path: '/api/health'
    static_configs:
      - targets: ['localhost:5000']
```

### Kubernetes Probes
```yaml
livenessProbe:
  httpGet:
    path: /api/health/live
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 30

readinessProbe:
  httpGet:
    path: /api/health/ready
    port: 5000
  initialDelaySeconds: 15
  periodSeconds: 10
```

### PowerShell Monitoring Script
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/health"
if ($response.status -ne "Healthy") {
    Write-Warning "AIRES is not healthy: $($response.status)"
}
```

## Health Status Meanings

- **Healthy**: All components are functioning normally (HTTP 200)
- **Degraded**: Some non-critical issues detected, but service is operational (HTTP 503)
- **Unhealthy**: Critical issues detected, service may not function properly (HTTP 503)

**Important**: Both Degraded and Unhealthy states return HTTP 503 to ensure monitoring systems 
trigger alerts BEFORE complete failure. A degraded system needs attention!

## CORS Policy

The API allows cross-origin requests from any origin to facilitate integration with monitoring dashboards. In production, you should restrict this to specific origins.

## Security Considerations

1. The health endpoints expose system information - ensure proper network security
2. Consider implementing authentication for production deployments
3. Use HTTPS in production environments
4. Restrict CORS origins in production

## Troubleshooting

### API won't start
- Check if port 5000/5001 is already in use
- Ensure aires.ini is accessible
- Check logs in `logs/aires-api-*.log`

### Health checks timing out
- Increase timeout values in appsettings.json
- Check if Ollama service is running
- Verify network connectivity to AI services
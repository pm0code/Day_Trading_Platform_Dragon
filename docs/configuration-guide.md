# Configuration Guide

## Configuration Files

### Location

Configuration files are searched in the following order:

1. `./project.config.json` (Project directory)
2. `~/.project/config.json` (User home)
3. `/etc/project/config.json` (System-wide)

### Format

Configuration uses JSON format:

```json
{
  "version": "2.0",
  "settings": {
    "theme": "dark",
    "language": "en",
    "autoSave": true
  },
  "features": {
    "analytics": true,
    "telemetry": false,
    "updates": "auto"
  }
}
```

## Configuration Options

### Core Settings

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `theme` | string | "light" | UI theme (light/dark/auto) |
| `language` | string | "en" | Interface language |
| `logLevel` | string | "info" | Logging verbosity |
| `timeout` | number | 30000 | Operation timeout (ms) |

### Feature Flags

| Feature | Type | Default | Description |
|---------|------|---------|-------------|
| `analytics` | boolean | true | Enable usage analytics |
| `telemetry` | boolean | true | Send crash reports |
| `updates` | string | "notify" | Update behavior |
| `experimental` | boolean | false | Enable beta features |

### Advanced Options

```json
{
  "advanced": {
    "parallelism": 4,
    "cacheSize": "1GB",
    "memoryLimit": "4GB",
    "network": {
      "proxy": "http://proxy:8080",
      "timeout": 60000,
      "retries": 3
    }
  }
}
```

## Environment Variables

Override configuration with environment variables:

```bash
export PROJECT_THEME=dark
export PROJECT_LOG_LEVEL=debug
export PROJECT_TIMEOUT=60000
```

## Command Line Arguments

Override any setting via CLI:

```bash
project --theme=dark --log-level=debug
```

## Profiles

Create multiple configuration profiles:

### Development Profile
```json
{
  "name": "development",
  "extends": "default",
  "settings": {
    "logLevel": "debug",
    "cache": false
  }
}
```

### Production Profile
```json
{
  "name": "production",
  "extends": "default",
  "settings": {
    "logLevel": "error",
    "optimize": true
  }
}
```

## Validation

Configuration is validated on startup:

```bash
project config validate
```

## Migration

Migrate from old configuration:

```bash
project config migrate
```

## Best Practices

1. **Version Control**: Commit project config, exclude user config
2. **Secrets**: Use environment variables for sensitive data
3. **Documentation**: Document custom settings
4. **Validation**: Test configuration changes
5. **Backups**: Keep configuration backups

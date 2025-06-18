# DRAGON Remote Build System
**Day Trading Platform - Hybrid Development Environment**

## Overview
This system allows you to maintain full control from your Linux development environment while building Windows-specific components on the DRAGON system. The approach provides the best of both worlds: Linux development productivity with Windows build capabilities.

## Architecture

```
┌─────────────────────┐    ┌─────────────────────┐
│   Linux Dev Env    │    │   DRAGON (Windows)  │
│                     │    │                     │
│ ┌─────────────────┐ │    │ ┌─────────────────┐ │
│ │ Source Control  │ │    │ │ Windows Builds  │ │
│ │ Code Editing    │ │◄──►│ │ WinUI 3 App     │ │
│ │ Git Management │ │    │ │ GPU Detection   │ │
│ │ CI/CD Control   │ │    │ │ Hardware Access │ │
│ └─────────────────┘ │    │ └─────────────────┘ │
└─────────────────────┘    └─────────────────────┘
       Claude Code              DRAGON Hardware
```

## Quick Start

### 1. Initial Setup
```bash
# Set DRAGON connection details
export DRAGON_HOST="192.168.1.100"  # Your DRAGON IP
export DRAGON_USER="nader"           # Your Windows username

# Test connectivity and setup environment
./scripts/dragon-remote-build.sh setup
```

### 2. Daily Development Workflow
```bash
# Sync changes and build
./scripts/dragon-remote-build.sh build

# Build and run tests
./scripts/dragon-remote-build.sh test

# Full pipeline (build, test, publish)
./scripts/dragon-remote-build.sh publish
```

### 3. Monitoring and Status
```bash
# Check build status
./scripts/dragon-remote-build.sh status

# Monitor system health
./scripts/dragon-remote-build.sh health
```

## Available Commands

| Command | Description | Use Case |
|---------|-------------|----------|
| `check` | Test connectivity and build status | Quick health check |
| `setup` | Initial DRAGON environment setup | First-time setup |
| `sync` | Sync project files to DRAGON | Manual sync |
| `build` | Build Windows components | Development builds |
| `test` | Build and run all tests | Validation |
| `publish` | Full pipeline with artifacts | Release preparation |
| `status` | Show current build status | Status monitoring |
| `health` | System health monitoring | Performance check |
| `full` | Complete setup and build | Full automation |

## Benefits of This Approach

### Linux Development Environment
- **Full Claude Code Integration**: Complete VS Code integration with AI assistance
- **Superior Git Integration**: Linux-native Git performance and tooling
- **Package Management**: Native Linux package managers and containerization
- **Shell Scripting**: Powerful bash scripting for automation
- **SSH/Remote Tools**: Native remote management capabilities

### Windows Build Environment
- **Hardware-Specific Builds**: Direct access to RTX 4070 Ti + RTX 3060 Ti
- **WinUI 3 Compilation**: Native Windows UI framework support
- **GPU Detection**: Real hardware detection without emulation
- **Windows APIs**: Direct access to Windows Management Instrumentation
- **Performance Testing**: Real-world performance on target hardware

## Project Structure on DRAGON

```
C:\dev\trading-platform\DayTradinPlatform\
├── TradingPlatform.DisplayManagement\     # ✅ Builds on both platforms
├── TradingPlatform.TradingApp\            # 🪟 Windows-only (WinUI 3)
├── TradingPlatform.Core\                  # ✅ Cross-platform
├── TradingPlatform.Testing\               # ✅ Cross-platform tests
├── scripts\
│   ├── setup-dragon-development.ps1      # 🪟 DRAGON environment setup
│   ├── build-windows-components.ps1      # 🪟 Windows build automation
│   └── dragon-remote-build.sh            # 🐧 Linux orchestration
└── publish\                              # 📦 Deployment artifacts
```

## Environment Variables

Set these in your Linux environment for seamless operation:

```bash
# Required
export DRAGON_HOST="192.168.1.100"        # DRAGON IP address
export DRAGON_USER="nader"                # Windows username

# Optional
export DRAGON_PROJECT_PATH="C:/dev/trading-platform/DayTradinPlatform"
export BUILD_CONFIG="Release"             # Release or Debug
export BUILD_RUNTIME="win-x64"           # Target runtime
```

## SSH Key Setup

For passwordless operation, set up SSH key authentication:

```bash
# Generate SSH key if needed
ssh-keygen -t rsa -b 4096 -C "your_email@example.com"

# Copy key to DRAGON
ssh-copy-id nader@192.168.1.100

# Test connection
ssh nader@192.168.1.100 "echo 'DRAGON connection successful'"
```

## Build Outputs

### Platform-Independent Components
These build on both Linux and Windows:
- **TradingPlatform.Core**: ✅ Core trading logic
- **TradingPlatform.DisplayManagement**: ✅ Session detection, GPU models
- **TradingPlatform.DataIngestion**: ✅ Market data providers
- **TradingPlatform.Testing**: ✅ Financial math tests

### Windows-Specific Components
These require DRAGON for compilation:
- **TradingPlatform.TradingApp**: 🪟 WinUI 3 application
- **GPU Hardware Detection**: 🪟 WMI-based RTX detection
- **Windows Session Management**: 🪟 RDP/Console detection
- **Multi-Monitor APIs**: 🪟 Windows display management

## Performance Considerations

### DRAGON System Optimization
- **GPU Temperature Monitoring**: Built-in health checks
- **Memory Usage Tracking**: Automatic system monitoring
- **Build Performance**: Local NVMe storage for fast builds
- **Network Optimization**: Efficient file synchronization

### Development Workflow
- **Incremental Builds**: Only changed files are synced
- **Parallel Execution**: Multiple build targets in parallel
- **Caching**: Build artifacts cached on DRAGON
- **Artifact Retrieval**: Only final binaries downloaded

## Troubleshooting

### Connection Issues
```bash
# Test basic connectivity
ping 192.168.1.100

# Test SSH
ssh -v nader@192.168.1.100

# Check firewall
./scripts/dragon-remote-build.sh check
```

### Build Issues
```bash
# Clean and rebuild
./scripts/dragon-remote-build.sh build

# Check DRAGON status
./scripts/dragon-remote-build.sh status

# Monitor system health
./scripts/dragon-remote-build.sh health
```

### Performance Issues
```bash
# Monitor DRAGON resources
./scripts/dragon-remote-build.sh health

# Check GPU temperatures
# (Built into health monitoring)
```

## Integration with Development Workflow

### Git Hooks
Add this to your `.git/hooks/pre-push`:
```bash
#!/bin/bash
# Ensure Windows build passes before push
./scripts/dragon-remote-build.sh test
```

### VS Code Tasks
Add to `.vscode/tasks.json`:
```json
{
    "label": "Build on DRAGON",
    "type": "shell",
    "command": "./scripts/dragon-remote-build.sh",
    "args": ["build"],
    "group": "build"
}
```

### CI/CD Integration
```yaml
# Example GitHub Actions integration
- name: Build Windows Components
  run: |
    ./scripts/dragon-remote-build.sh publish
    # Upload artifacts from ./artifacts/
```

## Security Considerations

- **SSH Key Authentication**: Passwordless but secure access
- **Network Isolation**: DRAGON on private network segment
- **File Permissions**: Proper Windows ACL configuration
- **Firewall Rules**: Minimal port exposure (SSH only)

This hybrid approach provides the optimal development experience while ensuring all Windows-specific components build and test correctly on the target DRAGON hardware.
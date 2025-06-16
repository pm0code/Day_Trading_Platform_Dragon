# Trading Platform CI/CD Setup Guide
## Ubuntu Development → DRAGON Windows 11 Testing

This guide sets up a comprehensive CI/CD pipeline for cross-platform development where **Ubuntu serves as the development environment** and **DRAGON (Windows 11) serves as the target testing platform** for ultra-low latency trading applications.

## 🏗️ Architecture Overview

```
┌─────────────────┐    SSH/Git     ┌─────────────────┐
│ Ubuntu Dev      │ ────────────── │ DRAGON Win11    │
│ - VS Code       │                │ - Self-hosted   │
│ - Claude Code   │   GitHub       │   Runner        │
│ - Git           │   Actions      │ - .NET 8.0      │
│ - Docker        │                │ - Performance   │
└─────────────────┘                │   Testing       │
        │                          └─────────────────┘
        │                                   │
        └──────────── GitHub ──────────────┘
                   (Orchestration)
```

## 🚀 Quick Setup

### 1. Ubuntu Development Machine (Current)
```bash
# Already configured with development environment
# Verify prerequisites
dotnet --version  # Should be 8.0.x
git --version
docker --version
```

### 2. DRAGON Windows 11 Machine Setup
```powershell
# Run as Administrator on DRAGON machine
# Download and execute the setup script
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/YOUR_ORG/YOUR_REPO/main/scripts/setup-dragon-runner.ps1" -OutFile "setup-dragon-runner.ps1"

# Execute with your GitHub token and repository
.\setup-dragon-runner.ps1 -GitHubToken "YOUR_GITHUB_TOKEN" -GitHubRepo "YOUR_ORG/YOUR_REPO"
```

### 3. Ubuntu → DRAGON Connection Setup
```bash
# On Ubuntu development machine
cd /home/nader/my_projects/C#/DayTradingPlatform
chmod +x scripts/dragon-connect.sh

# Initial setup (generates SSH keys and configures connection)
./scripts/dragon-connect.sh setup

# Test the connection
./scripts/dragon-connect.sh test
```

## 📋 Detailed Setup Instructions

### Step 1: DRAGON Machine Preparation

#### Prerequisites
- **DRAGON Machine**: Windows 11 Pro/Enterprise (x64)
- **Hardware**: Intel i9-14900K, 32GB+ RAM, Mellanox 10GbE
- **Network**: Accessible from Ubuntu development machine
- **Permissions**: Administrator access for initial setup

#### Setup Process
1. **Download setup script** to DRAGON machine
2. **Run PowerShell as Administrator**
3. **Execute setup script** with GitHub repository details
4. **Configure SSH keys** (manual step required)
5. **Verify .NET 8.0 SDK** installation
6. **Test GitHub Actions Runner** registration

```powershell
# Example setup command
.\setup-dragon-runner.ps1 `
    -GitHubToken "ghp_YOUR_TOKEN_HERE" `
    -GitHubRepo "your-org/day-trading-platform" `
    -RunnerName "dragon-runner" `
    -RunnerLabels "windows-runner,dragon,windows-11,x64,trading"
```

### Step 2: SSH Key Configuration

#### Ubuntu Side (Development Machine)
```bash
# Generate SSH key pair for DRAGON connection
ssh-keygen -t ed25519 -f ~/.ssh/dragon_key -N ""

# Display public key for copying
cat ~/.ssh/dragon_key.pub
```

#### DRAGON Side (Windows 11 Machine)
```powershell
# Create SSH directory
New-Item -ItemType Directory -Path $env:USERPROFILE\.ssh -Force

# Add public key to authorized_keys (replace with actual public key)
Add-Content -Path $env:USERPROFILE\.ssh\authorized_keys -Value "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG... ubuntu-dev-to-dragon"

# Set proper permissions
icacls $env:USERPROFILE\.ssh\authorized_keys /inheritance:r /grant:r "$($env:USERNAME):F"
```

### Step 3: Test Cross-Platform Pipeline

#### Full Test Cycle
```bash
# Deploy code, build, and test on DRAGON
./scripts/dragon-connect.sh full-test
```

#### Individual Operations
```bash
# Test connectivity
./scripts/dragon-connect.sh test

# Deploy code only
./scripts/dragon-connect.sh deploy

# Build on DRAGON
./scripts/dragon-connect.sh build

# Run tests on DRAGON
./scripts/dragon-connect.sh test-run

# Collect performance telemetry
./scripts/dragon-connect.sh telemetry
```

## 🔄 CI/CD Workflows

### 1. Main CI/CD Pipeline
**File**: `.github/workflows/trading-platform-ci.yml`

**Triggers**:
- Push to `main` or `develop` branches
- Pull requests to `main`
- Manual workflow dispatch

**Flow**:
1. **Ubuntu Build**: Restore, build, test with coverage
2. **Package**: Create source artifact for Windows testing
3. **DRAGON Testing**: Deploy to Windows, build, performance test
4. **Telemetry**: Collect system metrics and performance data
5. **Reporting**: Generate comprehensive build reports

### 2. Security Scanning
**File**: `.github/workflows/security-scan.yml`

**Features**:
- Daily security vulnerability scans
- Dependency license checking
- SonarCloud code quality analysis
- Automated dependency updates

### 3. Deployment Pipeline
**File**: `.github/workflows/deployment.yml`

**Environments**:
- **Staging**: Automated deployment after successful CI
- **Production**: Manual approval required

## 📊 Performance Monitoring

### Real-time Monitoring on DRAGON
```bash
# Start performance monitoring (sends data to Ubuntu)
./scripts/dragon-connect.sh monitor-start

# Stop monitoring
./scripts/dragon-connect.sh monitor-stop
```

### Performance Targets
- **Order Execution**: <100μs
- **Market Data Processing**: <50μs
- **Strategy Execution**: <45ms
- **Risk Checks**: <10ms

### Telemetry Data
- CPU usage and core utilization
- Memory consumption and availability
- Network throughput (10GbE optimization)
- Disk I/O performance
- Application-specific metrics

## 🐛 Debugging & Development

### Remote Development with VS Code
```bash
# Setup remote debugging session
./scripts/dragon-connect.sh debug

# Or use VS Code Remote-SSH extension
code --remote ssh-remote+dragon C:\BuildWorkspace
```

### Direct SSH Access
```bash
# Open SSH session to DRAGON
./scripts/dragon-connect.sh ssh
```

### Log Collection
```bash
# Collect latest telemetry and logs
./scripts/dragon-connect.sh telemetry

# View collected data
ls -la /tmp/dragon-telemetry/
```

## 🔧 Configuration

### Environment Variables

#### Ubuntu Development Machine
```bash
export DRAGON_HOST="192.168.1.100"  # DRAGON machine IP
export DRAGON_USER="trader"          # Windows username
```

#### GitHub Repository Secrets
Required secrets for CI/CD pipeline:
- `DRAGON_HOST`: IP address of DRAGON machine
- `DRAGON_USER`: Username for DRAGON machine  
- `DRAGON_SSH_KEY`: Private SSH key for authentication
- `SONAR_TOKEN`: SonarCloud authentication token

### Network Configuration

#### Ubuntu Firewall
```bash
# Allow SSH connections from DRAGON
sudo ufw allow from DRAGON_IP to any port 22
```

#### DRAGON Firewall
```powershell
# Allow SSH from Ubuntu (configured by setup script)
New-NetFirewallRule -DisplayName "Allow SSH from Ubuntu" -Direction Inbound -Protocol TCP -LocalPort 22 -RemoteAddress UBUNTU_IP -Action Allow
```

## 🚨 Troubleshooting

### Common Issues

#### SSH Connection Failed
```bash
# Test basic connectivity
ping $DRAGON_HOST

# Test SSH with verbose output
ssh -v -i ~/.ssh/dragon_key $DRAGON_USER@$DRAGON_HOST

# Check SSH key permissions
chmod 600 ~/.ssh/dragon_key
```

#### GitHub Actions Runner Not Responding
```powershell
# Check runner service status on DRAGON
Get-Service -Name "actions.runner.*"

# Restart runner service
Restart-Service -Name "actions.runner.*"

# Check runner logs
Get-Content "C:\actions-runner\_diag\*.log" -Tail 50
```

#### Build Failures on DRAGON
```bash
# Check build logs
./scripts/dragon-connect.sh ssh
cd C:\BuildWorkspace
dotnet build --verbosity diagnostic
```

#### Performance Issues
```powershell
# Check system resources on DRAGON
Get-Process | Sort-Object CPU -Descending | Select-Object -First 10
Get-Counter "\Processor(_Total)\% Processor Time"
Get-Counter "\Memory\Available MBytes"
```

### Log Locations

#### Ubuntu Development Machine
- CI/CD logs: GitHub Actions interface
- Connection logs: Terminal output
- Telemetry: `/tmp/dragon-telemetry/`

#### DRAGON Windows 11 Machine
- Runner logs: `C:\actions-runner\_diag\`
- Build logs: `C:\BuildWorkspace\`
- Performance data: `C:\PerformanceData\`
- Application logs: `C:\TradingLogs\`

## 📈 Performance Optimization

### DRAGON Machine Optimization
The setup script automatically configures:
- High-performance power profile
- Disabled unnecessary Windows services
- Network optimizations for Mellanox 10GbE
- Memory and CPU optimizations for trading
- Jumbo frames (9000 MTU) for network performance

### CI/CD Pipeline Optimization
- **Caching**: NuGet packages cached between builds
- **Parallel execution**: Multiple test suites run concurrently
- **Incremental builds**: Only changed projects rebuilt
- **Artifact management**: Efficient storage and retention

## 🎯 Next Steps

1. **Complete DRAGON setup** using the provided scripts
2. **Test SSH connectivity** between Ubuntu and DRAGON
3. **Run initial CI/CD pipeline** to verify integration
4. **Configure monitoring dashboards** for performance tracking
5. **Set up alerting** for build failures and performance issues

## 📞 Support

### Quick Commands Reference
```bash
# Setup and test
./scripts/dragon-connect.sh setup
./scripts/dragon-connect.sh test

# Development workflow
./scripts/dragon-connect.sh deploy
./scripts/dragon-connect.sh build
./scripts/dragon-connect.sh test-run

# Monitoring
./scripts/dragon-connect.sh monitor-start
./scripts/dragon-connect.sh telemetry

# Debugging
./scripts/dragon-connect.sh debug
./scripts/dragon-connect.sh ssh
```

This setup provides a production-ready CI/CD pipeline specifically designed for ultra-low latency trading platform development with cross-platform testing capabilities.
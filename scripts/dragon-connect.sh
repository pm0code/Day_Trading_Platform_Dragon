#!/bin/bash
# DRAGON Connection Manager for Ubuntu Development Machine
# Manages SSH connectivity and remote operations with DRAGON Windows 11 machine

set -e

# Configuration
DRAGON_HOST="${DRAGON_HOST:-192.168.1.35}"
DRAGON_USER="${DRAGON_USER:-admin}"
DRAGON_KEY="$HOME/.ssh/dragon_key"
PROJECT_PATH="/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
DRAGON_WORKSPACE="D:\\BuildWorkspace"
TELEMETRY_DIR="/tmp/dragon-telemetry"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${CYAN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log "🔍 Checking prerequisites..."
    
    # Check if SSH key exists
    if [ ! -f "$DRAGON_KEY" ]; then
        warning "SSH key not found at $DRAGON_KEY"
        log "Generating SSH key for DRAGON connection..."
        ssh-keygen -t ed25519 -f "$DRAGON_KEY" -N "" -C "ubuntu-dev-to-dragon"
        success "SSH key generated: $DRAGON_KEY"
        log "📋 Copy the following public key to DRAGON machine:"
        echo ""
        cat "${DRAGON_KEY}.pub"
        echo ""
        warning "Add this key to DRAGON machine: %USERPROFILE%\\.ssh\\authorized_keys"
        warning "Then set permissions: icacls \"%USERPROFILE%\\.ssh\\authorized_keys\" /inheritance:r /grant:r \"%USERNAME%:F\""
        exit 0
    fi
    
    # Check SSH connectivity
    if ! ssh -o ConnectTimeout=5 -o BatchMode=yes -i "$DRAGON_KEY" "$DRAGON_USER@$DRAGON_HOST" "echo 'Connection test'" &>/dev/null; then
        error "Cannot connect to DRAGON machine at $DRAGON_HOST"
        log "Please ensure:"
        log "1. DRAGON machine is powered on and accessible"
        log "2. SSH service is running on DRAGON"
        log "3. Public key is properly installed on DRAGON"
        log "4. Firewall allows SSH connections"
        exit 1
    fi
    
    success "Prerequisites check passed"
}

# Setup SSH config
setup_ssh_config() {
    log "🔧 Setting up SSH configuration..."
    
    # Create SSH config entry for DRAGON
    SSH_CONFIG="$HOME/.ssh/config"
    
    # Remove existing DRAGON config if present
    if grep -q "Host dragon" "$SSH_CONFIG" 2>/dev/null; then
        sed -i '/Host dragon/,/^$/d' "$SSH_CONFIG"
    fi
    
    # Add DRAGON SSH configuration
    cat >> "$SSH_CONFIG" << EOF

# DRAGON Windows 11 Trading Platform Target
Host dragon
    HostName $DRAGON_HOST
    User $DRAGON_USER
    IdentityFile $DRAGON_KEY
    IdentitiesOnly yes
    PasswordAuthentication no
    PubkeyAuthentication yes
    ServerAliveInterval 60
    ServerAliveCountMax 3
    Compression yes
    ForwardAgent no
EOF
    
    chmod 600 "$SSH_CONFIG"
    success "SSH configuration updated"
}

# Test DRAGON connectivity
test_connectivity() {
    log "🔌 Testing DRAGON connectivity..."
    
    # Basic connectivity test
    if ssh dragon "echo 'DRAGON connection successful'" 2>/dev/null; then
        success "✅ SSH connection to DRAGON working"
    else
        error "❌ SSH connection to DRAGON failed"
        return 1
    fi
    
    # Test .NET availability
    log "Checking .NET 8.0 availability on DRAGON..."
    DOTNET_VERSION=$(ssh dragon "dotnet --version" 2>/dev/null || echo "not found")
    if [[ $DOTNET_VERSION == 8.* ]]; then
        success "✅ .NET 8.0 available on DRAGON: $DOTNET_VERSION"
    else
        error "❌ .NET 8.0 not found on DRAGON: $DOTNET_VERSION"
        return 1
    fi
    
    # Test GitHub Actions Runner
    log "Checking GitHub Actions Runner on DRAGON..."
    if ssh dragon "Get-Service -Name 'actions.runner.*' | Where-Object { \$_.Status -eq 'Running' }" 2>/dev/null | grep -q "Running"; then
        success "✅ GitHub Actions Runner is running on DRAGON"
    else
        warning "⚠️  GitHub Actions Runner status unclear on DRAGON"
    fi
    
    # Get system information
    log "Retrieving DRAGON system information..."
    SYSTEM_INFO=$(ssh dragon "Get-ComputerInfo | Select-Object WindowsProductName,TotalPhysicalMemory,CsProcessors | ConvertTo-Json" 2>/dev/null)
    if [ -n "$SYSTEM_INFO" ]; then
        success "✅ DRAGON system information retrieved"
        echo "$SYSTEM_INFO" | jq '.' 2>/dev/null || echo "$SYSTEM_INFO"
    fi
}

# Deploy code to DRAGON for testing
deploy_code() {
    log "🚀 Deploying code to DRAGON for testing..."
    
    # BuildWorkspace directory already exists on DRAGON at D:\BuildWorkspace
    
    # Deploy code to DRAGON using scp (simple and reliable)
    log "Copying code to DRAGON..."
    
    # Copy only the essential project files to D:\BuildWorkspace
    log "Copying .NET solution files..."
    scp -i "$DRAGON_KEY" -o "StrictHostKeyChecking=no" \
        "$PROJECT_PATH"/*.sln \
        "$PROJECT_PATH"/*.md \
        "$PROJECT_PATH"/Dockerfile* \
        "$DRAGON_USER@$DRAGON_HOST:$DRAGON_WORKSPACE/" 2>/dev/null || true
    
    log "Copying project directories..."
    scp -r -i "$DRAGON_KEY" -o "StrictHostKeyChecking=no" \
        "$PROJECT_PATH"/TradingPlatform.* \
        "$DRAGON_USER@$DRAGON_HOST:$DRAGON_WORKSPACE/" 2>/dev/null || true
    
    success "Code deployed to DRAGON: $DRAGON_WORKSPACE"
}

# Build solution on DRAGON
build_on_dragon() {
    log "🔨 Building solution on DRAGON..."
    
    # Build the solution
    ssh dragon "
        Set-Location '$DRAGON_WORKSPACE'
        Write-Host '🔄 Restoring packages...'
        dotnet restore DayTradinPlatform.sln
        
        Write-Host '🔨 Building solution...'
        dotnet build DayTradinPlatform.sln --configuration Release --verbosity minimal
        
        if (\$LASTEXITCODE -eq 0) {
            Write-Host '✅ Build successful'
        } else {
            Write-Error '❌ Build failed'
            exit 1
        }
    "
    
    if [ $? -eq 0 ]; then
        success "✅ Build completed successfully on DRAGON"
    else
        error "❌ Build failed on DRAGON"
        return 1
    fi
}

# Run tests on DRAGON
test_on_dragon() {
    log "🧪 Running tests on DRAGON..."
    
    ssh dragon "
        Set-Location '$DRAGON_WORKSPACE'
        Write-Host '🧪 Running tests...'
        
        dotnet test DayTradinPlatform.sln \
            --configuration Release \
            --no-build \
            --logger 'trx;LogFileName=dragon-test-results.trx' \
            --results-directory 'TestResults' \
            --collect:'XPlat Code Coverage'
        
        if (\$LASTEXITCODE -eq 0) {
            Write-Host '✅ All tests passed'
        } else {
            Write-Warning '⚠️  Some tests failed'
        }
        
        # Display test summary
        if (Test-Path 'TestResults\\dragon-test-results.trx') {
            Write-Host '📊 Test Results Summary:'
            [xml]\$testResults = Get-Content 'TestResults\\dragon-test-results.trx'
            \$counters = \$testResults.TestRun.ResultSummary.Counters
            Write-Host \"Total: \$(\$counters.total), Passed: \$(\$counters.passed), Failed: \$(\$counters.failed)\"
        }
    "
}

# Collect telemetry from DRAGON
collect_telemetry() {
    log "📊 Collecting telemetry from DRAGON..."
    
    # Create telemetry directory
    mkdir -p "$TELEMETRY_DIR"
    
    # Collect performance data
    ssh dragon "
        \$perfData = @{
            Timestamp = Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ'
            ComputerName = \$env:COMPUTERNAME
            OS = (Get-CimInstance Win32_OperatingSystem).Caption
            CPU = @{
                Name = (Get-CimInstance Win32_Processor).Name
                Cores = (Get-CimInstance Win32_ComputerSystem).NumberOfProcessors
                Usage = [math]::Round((Get-Counter '\Processor(_Total)\% Processor Time').CounterSamples[0].CookedValue, 2)
            }
            Memory = @{
                TotalGB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
                AvailableGB = [math]::Round((Get-Counter '\Memory\Available MBytes').CounterSamples[0].CookedValue / 1024, 2)
            }
            Network = @()
        }
        
        # Get network adapter information
        Get-NetAdapter | Where-Object { \$_.Status -eq 'Up' } | ForEach-Object {
            \$perfData.Network += @{
                Name = \$_.Name
                LinkSpeed = \$_.LinkSpeed
                InterfaceDescription = \$_.InterfaceDescription
            }
        }
        
        \$perfData | ConvertTo-Json -Depth 3
    " > "$TELEMETRY_DIR/dragon-telemetry-$(date +%Y%m%d-%H%M%S).json"
    
    success "Telemetry collected to $TELEMETRY_DIR"
}

# Start performance monitoring on DRAGON
start_monitoring() {
    log "📈 Starting performance monitoring on DRAGON..."
    
    ssh dragon "
        Start-Process powershell -ArgumentList '-File C:\TradingLogs\dragon-monitor.ps1 -UbuntuHost $(hostname -I | awk '{print $1}') -SshUser $USER' -WindowStyle Hidden
    " 2>/dev/null
    
    success "Performance monitoring started on DRAGON"
}

# Stop performance monitoring on DRAGON
stop_monitoring() {
    log "🛑 Stopping performance monitoring on DRAGON..."
    
    ssh dragon "
        Get-Process | Where-Object { \$_.ProcessName -eq 'powershell' -and \$_.CommandLine -like '*dragon-monitor*' } | Stop-Process -Force
    " 2>/dev/null
    
    success "Performance monitoring stopped on DRAGON"
}

# Remote debugging session
debug_session() {
    log "🐛 Starting remote debugging session with DRAGON..."
    
    deploy_code
    
    ssh dragon "
        Set-Location '$DRAGON_WORKSPACE'
        Write-Host '🐛 Debug session ready'
        Write-Host 'Use Visual Studio Code Remote-SSH extension to connect'
        Write-Host 'Or run: code --remote ssh-remote+dragon $DRAGON_WORKSPACE'
    "
}

# Show help
show_help() {
    echo "DRAGON Connection Manager - Ubuntu to Windows 11 Trading Platform"
    echo ""
    echo "Usage: $0 [COMMAND]"
    echo ""
    echo "Commands:"
    echo "  setup           - Setup SSH configuration and test connectivity"
    echo "  test            - Test connectivity to DRAGON machine"
    echo "  deploy          - Deploy code to DRAGON for testing"
    echo "  build           - Build solution on DRAGON"
    echo "  test-run        - Run tests on DRAGON"
    echo "  full-test       - Deploy, build, and test on DRAGON"
    echo "  telemetry       - Collect telemetry from DRAGON"
    echo "  monitor-start   - Start performance monitoring on DRAGON"
    echo "  monitor-stop    - Stop performance monitoring on DRAGON"
    echo "  debug           - Start remote debugging session"
    echo "  ssh             - Open SSH connection to DRAGON"
    echo "  help            - Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  DRAGON_HOST     - DRAGON machine hostname/IP (default: dragon.local)"
    echo "  DRAGON_USER     - Username on DRAGON machine (default: current user)"
    echo ""
    echo "Examples:"
    echo "  $0 setup                    # Initial setup"
    echo "  $0 full-test               # Complete test cycle"
    echo "  DRAGON_HOST=192.168.1.100 $0 test  # Test with specific IP"
}

# Main execution
main() {
    case "${1:-help}" in
        "setup")
            check_prerequisites
            setup_ssh_config
            test_connectivity
            ;;
        "test")
            test_connectivity
            ;;
        "deploy")
            check_prerequisites
            deploy_code
            ;;
        "build")
            check_prerequisites
            build_on_dragon
            ;;
        "test-run")
            check_prerequisites
            test_on_dragon
            ;;
        "full-test")
            check_prerequisites
            deploy_code
            build_on_dragon
            test_on_dragon
            collect_telemetry
            ;;
        "telemetry")
            check_prerequisites
            collect_telemetry
            ;;
        "monitor-start")
            check_prerequisites
            start_monitoring
            ;;
        "monitor-stop")
            check_prerequisites
            stop_monitoring
            ;;
        "debug")
            check_prerequisites
            debug_session
            ;;
        "ssh")
            check_prerequisites
            log "🔗 Opening SSH connection to DRAGON..."
            ssh dragon
            ;;
        "help"|*)
            show_help
            ;;
    esac
}

# Execute main function
main "$@"

#!/bin/bash
# DRAGON Remote Build Orchestration Script
# Controls Windows builds from Linux development environment
# Day Trading Platform - Centralized Build Management

set -e

# Configuration
DRAGON_HOST="${DRAGON_HOST:-192.168.1.100}"  # Set your DRAGON IP
DRAGON_USER="${DRAGON_USER:-nader}"
DRAGON_PROJECT_PATH="${DRAGON_PROJECT_PATH:-C:/dev/trading-platform/DayTradinPlatform}"
BUILD_CONFIG="${BUILD_CONFIG:-Release}"
BUILD_RUNTIME="${BUILD_RUNTIME:-win-x64}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[DRAGON]${NC} $1"
}

print_success() {
    echo -e "${GREEN}✅${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠️${NC} $1"
}

print_error() {
    echo -e "${RED}❌${NC} $1"
}

print_header() {
    echo -e "${CYAN}🐉 $1${NC}"
    echo -e "${CYAN}$(printf '=%.0s' $(seq 1 ${#1}))${NC}"
}

# Function to check if DRAGON is accessible
check_dragon_connection() {
    print_status "Checking connection to DRAGON ($DRAGON_HOST)..."
    
    if ping -c 1 -W 2 "$DRAGON_HOST" >/dev/null 2>&1; then
        print_success "DRAGON is reachable"
    else
        print_error "Cannot reach DRAGON at $DRAGON_HOST"
        print_warning "Please ensure:"
        echo "  1. DRAGON is powered on and connected"
        echo "  2. SSH is enabled on DRAGON"
        echo "  3. Network connectivity is working"
        echo "  4. Firewall allows SSH connections"
        exit 1
    fi
}

# Function to test SSH connection
test_ssh_connection() {
    print_status "Testing SSH connection to DRAGON..."
    
    if ssh -o ConnectTimeout=10 -o BatchMode=yes "$DRAGON_USER@$DRAGON_HOST" exit 2>/dev/null; then
        print_success "SSH connection successful"
    else
        print_error "SSH connection failed"
        print_warning "Please ensure SSH key authentication is set up"
        echo "  Run: ssh-copy-id $DRAGON_USER@$DRAGON_HOST"
        exit 1
    fi
}

# Function to sync project files to DRAGON
sync_project_to_dragon() {
    print_status "Syncing project files to DRAGON..."
    
    # Create project directory on DRAGON if it doesn't exist
    ssh "$DRAGON_USER@$DRAGON_HOST" "mkdir -p '$DRAGON_PROJECT_PATH'"
    
    # Sync project files (excluding build artifacts and .git)
    rsync -avz --delete \
        --exclude='bin/' \
        --exclude='obj/' \
        --exclude='TestResults/' \
        --exclude='.git/' \
        --exclude='*.log' \
        --exclude='publish/' \
        ./ "$DRAGON_USER@$DRAGON_HOST:$DRAGON_PROJECT_PATH/"
    
    print_success "Project files synced to DRAGON"
}

# Function to run Windows setup if needed
setup_dragon_environment() {
    print_status "Setting up DRAGON development environment..."
    
    # Check if setup is needed
    setup_needed=$(ssh "$DRAGON_USER@$DRAGON_HOST" "
        if (Get-Command dotnet -ErrorAction SilentlyContinue) { 
            Write-Output 'false' 
        } else { 
            Write-Output 'true' 
        }
    " 2>/dev/null || echo "true")
    
    if [ "$setup_needed" = "true" ]; then
        print_warning "DRAGON environment setup required"
        ssh "$DRAGON_USER@$DRAGON_HOST" "
            Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
            cd '$DRAGON_PROJECT_PATH'
            PowerShell -File scripts/setup-dragon-development.ps1 -QuickSetup
        "
        print_success "DRAGON environment setup completed"
    else
        print_success "DRAGON environment already configured"
    fi
}

# Function to build Windows components on DRAGON
build_windows_components() {
    print_status "Building Windows components on DRAGON..."
    
    # Run the Windows build script remotely
    ssh "$DRAGON_USER@$DRAGON_HOST" "
        cd '$DRAGON_PROJECT_PATH'
        PowerShell -File scripts/build-windows-components.ps1 -Configuration '$BUILD_CONFIG' -Runtime '$BUILD_RUNTIME' -CleanFirst
    "
    
    if [ $? -eq 0 ]; then
        print_success "Windows components built successfully on DRAGON"
    else
        print_error "Windows build failed on DRAGON"
        exit 1
    fi
}

# Function to run tests on DRAGON
run_tests_on_dragon() {
    print_status "Running tests on DRAGON..."
    
    ssh "$DRAGON_USER@$DRAGON_HOST" "
        cd '$DRAGON_PROJECT_PATH'
        dotnet test TradingPlatform.Testing/TradingPlatform.Testing.csproj --configuration '$BUILD_CONFIG' --logger 'trx;LogFileName=TestResults.trx' --results-directory 'TestResults'
    "
    
    if [ $? -eq 0 ]; then
        print_success "All tests passed on DRAGON"
    else
        print_error "Tests failed on DRAGON"
        exit 1
    fi
}

# Function to publish TradingApp for deployment
publish_trading_app() {
    print_status "Publishing TradingApp for deployment..."
    
    ssh "$DRAGON_USER@$DRAGON_HOST" "
        cd '$DRAGON_PROJECT_PATH'
        PowerShell -File scripts/build-windows-components.ps1 -Configuration '$BUILD_CONFIG' -Runtime '$BUILD_RUNTIME' -PublishApp -OutputPath 'publish'
    "
    
    if [ $? -eq 0 ]; then
        print_success "TradingApp published successfully"
    else
        print_error "TradingApp publishing failed"
        exit 1
    fi
}

# Function to retrieve build artifacts
retrieve_build_artifacts() {
    print_status "Retrieving build artifacts from DRAGON..."
    
    # Create local artifacts directory
    mkdir -p "./artifacts/dragon-build-$(date +%Y%m%d-%H%M%S)"
    local artifact_dir="./artifacts/dragon-build-$(date +%Y%m%d-%H%M%S)"
    
    # Download build artifacts
    scp -r "$DRAGON_USER@$DRAGON_HOST:$DRAGON_PROJECT_PATH/publish" "$artifact_dir/" 2>/dev/null || true
    scp -r "$DRAGON_USER@$DRAGON_HOST:$DRAGON_PROJECT_PATH/TestResults" "$artifact_dir/" 2>/dev/null || true
    
    # Download specific Windows binaries
    scp "$DRAGON_USER@$DRAGON_HOST:$DRAGON_PROJECT_PATH/TradingPlatform.TradingApp/bin/$BUILD_CONFIG/net8.0-windows10.0.19041.0/$BUILD_RUNTIME/TradingPlatform.TradingApp.exe" "$artifact_dir/" 2>/dev/null || true
    
    print_success "Build artifacts saved to: $artifact_dir"
}

# Function to show build status
show_build_status() {
    print_status "Getting build status from DRAGON..."
    
    ssh "$DRAGON_USER@$DRAGON_HOST" "
        cd '$DRAGON_PROJECT_PATH'
        Write-Host '🐉 DRAGON Build Status:' -ForegroundColor Cyan
        Write-Host '========================' -ForegroundColor Cyan
        
        # Check .NET version
        \$dotnetVersion = dotnet --version 2>\$null
        if (\$dotnetVersion) {
            Write-Host '✅ .NET SDK:' \$dotnetVersion -ForegroundColor Green
        }
        
        # Check recent build outputs
        Write-Host ''
        Write-Host '📦 Recent Build Outputs:' -ForegroundColor Yellow
        
        # Check DisplayManagement
        if (Test-Path 'TradingPlatform.DisplayManagement/bin/$BUILD_CONFIG/net8.0/TradingPlatform.DisplayManagement.dll') {
            Write-Host '  ✅ TradingPlatform.DisplayManagement.dll' -ForegroundColor Green
        } else {
            Write-Host '  ❌ TradingPlatform.DisplayManagement.dll' -ForegroundColor Red
        }
        
        # Check TradingApp
        if (Test-Path 'TradingPlatform.TradingApp/bin/$BUILD_CONFIG/net8.0-windows10.0.19041.0/$BUILD_RUNTIME/TradingPlatform.TradingApp.exe') {
            Write-Host '  ✅ TradingPlatform.TradingApp.exe' -ForegroundColor Green
        } else {
            Write-Host '  ❌ TradingPlatform.TradingApp.exe' -ForegroundColor Red
        }
        
        # Check GPU detection capabilities
        Write-Host ''
        Write-Host '🎮 GPU Detection Status:' -ForegroundColor Yellow
        \$gpus = Get-CimInstance -ClassName Win32_VideoController | Where-Object { \$_.Name -notlike '*Basic*' -and \$_.Name -notlike '*Microsoft*' }
        foreach (\$gpu in \$gpus) {
            Write-Host \"  🎮 \$(\$gpu.Name)\" -ForegroundColor White
        }
        
        Write-Host ''
        Write-Host '🖥️ Session Detection:' -ForegroundColor Yellow
        \$sessionName = \$env:SESSIONNAME
        if (\$sessionName -eq 'Console') {
            Write-Host '  ✅ Direct Console Access - Full GPU capabilities available' -ForegroundColor Green
        } else {
            Write-Host \"  🌐 Remote Session (\$sessionName) - Mock services will be used\" -ForegroundColor Cyan
        }
    "
}

# Function to monitor DRAGON system health
monitor_dragon_health() {
    print_status "Monitoring DRAGON system health..."
    
    ssh "$DRAGON_USER@$DRAGON_HOST" "
        Write-Host '🏥 DRAGON System Health:' -ForegroundColor Cyan
        Write-Host '========================' -ForegroundColor Cyan
        
        # CPU Usage
        \$cpu = Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 1
        \$cpuUsage = [math]::Round(100 - \$cpu.CounterSamples[0].CookedValue, 2)
        Write-Host \"💻 CPU Usage: \$cpuUsage%\" -ForegroundColor White
        
        # Memory Usage
        \$memory = Get-CimInstance -ClassName Win32_OperatingSystem
        \$memUsage = [math]::Round(((\$memory.TotalVisibleMemorySize - \$memory.FreePhysicalMemory) / \$memory.TotalVisibleMemorySize) * 100, 2)
        Write-Host \"🧠 Memory Usage: \$memUsage%\" -ForegroundColor White
        
        # Disk Usage
        \$disk = Get-CimInstance -ClassName Win32_LogicalDisk -Filter \"DeviceID='C:'\"
        \$diskUsage = [math]::Round(((\$disk.Size - \$disk.FreeSpace) / \$disk.Size) * 100, 2)
        Write-Host \"💾 Disk Usage: \$diskUsage%\" -ForegroundColor White
        
        # GPU Temperature (if available)
        try {
            \$gpu = Get-CimInstance -Namespace root\\wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction SilentlyContinue
            if (\$gpu) {
                Write-Host '🌡️ GPU Status: Monitoring available' -ForegroundColor Green
            }
        } catch {
            Write-Host '🌡️ GPU Status: Temperature monitoring not available' -ForegroundColor Yellow
        }
    "
}

# Main script logic
main() {
    print_header "DRAGON Remote Build Orchestration"
    echo "Linux Development → Windows Build Pipeline"
    echo ""
    
    case "${1:-build}" in
        "check")
            check_dragon_connection
            test_ssh_connection
            show_build_status
            ;;
        "setup")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            setup_dragon_environment
            ;;
        "sync")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            ;;
        "build")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            build_windows_components
            show_build_status
            ;;
        "test")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            build_windows_components
            run_tests_on_dragon
            ;;
        "publish")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            build_windows_components
            run_tests_on_dragon
            publish_trading_app
            retrieve_build_artifacts
            ;;
        "status")
            check_dragon_connection
            test_ssh_connection
            show_build_status
            ;;
        "health")
            check_dragon_connection
            test_ssh_connection
            monitor_dragon_health
            ;;
        "full")
            check_dragon_connection
            test_ssh_connection
            sync_project_to_dragon
            setup_dragon_environment
            build_windows_components
            run_tests_on_dragon
            publish_trading_app
            retrieve_build_artifacts
            show_build_status
            ;;
        *)
            echo "Usage: $0 {check|setup|sync|build|test|publish|status|health|full}"
            echo ""
            echo "Commands:"
            echo "  check   - Check DRAGON connectivity and build status"
            echo "  setup   - Set up DRAGON development environment"
            echo "  sync    - Sync project files to DRAGON"
            echo "  build   - Build Windows components on DRAGON"
            echo "  test    - Build and run tests on DRAGON"
            echo "  publish - Full build, test, and publish pipeline"
            echo "  status  - Show current build status"
            echo "  health  - Monitor DRAGON system health"
            echo "  full    - Complete setup and build pipeline"
            echo ""
            echo "Environment Variables:"
            echo "  DRAGON_HOST=$DRAGON_HOST"
            echo "  DRAGON_USER=$DRAGON_USER"
            echo "  BUILD_CONFIG=$BUILD_CONFIG"
            echo "  BUILD_RUNTIME=$BUILD_RUNTIME"
            exit 1
            ;;
    esac
}

# Run main function with all arguments
main "$@"
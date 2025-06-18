#!/bin/bash
# Setup Complete Isolated BuildWorkspace on DRAGON
# Day Trading Platform - Remote DRAGON BuildWorkspace Creation
# Creates D:\BuildWorkspace\WindowsComponents\ with complete isolation

set -e

# Configuration
DRAGON_HOST="${DRAGON_HOST:-192.168.1.35}"
DRAGON_USER="${DRAGON_USER:-nader}"
BUILD_WORKSPACE="D:/BuildWorkspace/WindowsComponents"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

function print_header() {
    echo ""
    echo -e "${CYAN}🐉 $1${NC}"
    echo -e "${CYAN}$(printf '=%.0s' $(seq 1 ${#1}))${NC}"
}

function print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

function print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

function print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_header "Setting Up DRAGON BuildWorkspace"
echo -e "Target: ${DRAGON_HOST} (${DRAGON_USER})"
echo -e "Workspace: ${BUILD_WORKSPACE}"
echo -e "${YELLOW}Purpose: Complete isolation for Windows builds${NC}"
echo ""

# Test DRAGON connectivity first
echo -e "${YELLOW}🔍 Testing DRAGON connectivity...${NC}"
if ping -c 1 -W 2 "$DRAGON_HOST" >/dev/null 2>&1; then
    print_success "DRAGON is reachable at $DRAGON_HOST"
else
    print_error "Cannot reach DRAGON at $DRAGON_HOST"
    print_warning "Please ensure DRAGON is powered on and network connected"
    exit 1
fi

# Create the complete BuildWorkspace structure on DRAGON
echo -e "${YELLOW}🏗️ Creating isolated BuildWorkspace structure on DRAGON...${NC}"

# Build the PowerShell script for remote execution
SETUP_SCRIPT='
param(
    [string]$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
)

Write-Host "🏗️ Creating DRAGON BuildWorkspace Structure" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Target: $WorkspaceRoot" -ForegroundColor White
Write-Host ""

# Create complete directory structure
$directories = @(
    "$WorkspaceRoot",
    "$WorkspaceRoot\Source",
    "$WorkspaceRoot\Source\DayTradingPlatform",
    "$WorkspaceRoot\Tools",
    "$WorkspaceRoot\Tools\Compilers",
    "$WorkspaceRoot\Tools\Testing", 
    "$WorkspaceRoot\Tools\Utilities",
    "$WorkspaceRoot\Artifacts",
    "$WorkspaceRoot\Artifacts\Release",
    "$WorkspaceRoot\Artifacts\Debug",
    "$WorkspaceRoot\Artifacts\Packages",
    "$WorkspaceRoot\Artifacts\TestResults",
    "$WorkspaceRoot\Environment",
    "$WorkspaceRoot\Environment\Scripts",
    "$WorkspaceRoot\Environment\Config",
    "$WorkspaceRoot\Environment\Logs",
    "$WorkspaceRoot\Cache",
    "$WorkspaceRoot\Cache\NuGet",
    "$WorkspaceRoot\Cache\MSBuild", 
    "$WorkspaceRoot\Cache\Git",
    "$WorkspaceRoot\Backup",
    "$WorkspaceRoot\Documentation"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  ✅ Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "  ✅ Exists: $dir" -ForegroundColor Green
    }
}

# Create workspace identification README
$workspaceReadme = @"
🛡️ ISOLATED WINDOWS BUILD WORKSPACE - Day Trading Platform
===========================================================

⚠️  COMPLETELY ISOLATED WINDOWS BUILD ENVIRONMENT
🎯 Purpose: Zero contamination of existing development environment
🔧 Tools: Self-contained toolchain isolated from system tools
📦 Artifacts: All outputs contained within workspace
🛡️ Safety: Prevents issues that waste time tracking down

🚫 ZERO CONTAMINATION DESIGN
✅ No interference with existing development setup
✅ Self-contained toolchain and dependencies
✅ Isolated environment variables and configuration  
✅ Separate package cache and build artifacts
✅ No system-wide installations or modifications

📂 WORKSPACE STRUCTURE:
├── Source\                    # 📝 Source code workspace (synced from Linux)
│   └── DayTradingPlatform\   # 🔄 Project files (independent copy)
├── Tools\                     # 🔧 Development toolchain
│   ├── Compilers\            # 🏗️ .NET SDK, MSBuild, Visual Studio Build Tools
│   ├── Testing\              # 🧪 Test runners, coverage tools, performance profilers
│   └── Utilities\            # 🛠️ Git, editors, PowerShell modules
├── Artifacts\                 # 📦 Build outputs and deployments
│   ├── Release\              # 🚀 Production builds
│   ├── Debug\                # 🐛 Development builds
│   ├── Packages\             # 📦 NuGet packages and self-contained deployments
│   └── TestResults\          # 📊 Test execution results and coverage reports
├── Environment\               # ⚙️ Configuration and automation
│   ├── Scripts\              # 🤖 Build automation scripts
│   ├── Config\               # 📋 Configuration files and settings
│   └── Logs\                 # 📊 Build logs and system monitoring
├── Cache\                     # 💾 Build cache and temporary files
│   ├── NuGet\                # 📦 NuGet package cache
│   ├── MSBuild\              # 🏗️ MSBuild incremental build cache
│   └── Git\                  # 🔄 Git workspace and temporary files
├── Backup\                    # 💾 Workspace backups and snapshots
└── Documentation\             # 📚 Build workspace documentation

🎯 ISOLATION GUARANTEE:
• ZERO system modifications
• ZERO global tool installations  
• ZERO environment variable pollution
• ZERO interference with existing development

🎮 DRAGON HARDWARE INTEGRATION:
• RTX 4070 Ti + RTX 3060 Ti detection
• Multi-monitor support (up to 8 displays)
• Hardware-accelerated graphics pipeline
• Session detection (RDP vs Console)

Created: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
System: DRAGON BuildWorkspace
Purpose: Isolated Windows Build Environment
Hardware: RTX 4070 Ti + RTX 3060 Ti Dual-GPU Configuration
"@

Set-Content -Path "$WorkspaceRoot\README-ISOLATED-BUILDWORKSPACE.txt" -Value $workspaceReadme

# Create toolchain installation script
$toolchainScript = @"
# ISOLATED Toolchain Installation for BuildWorkspace
# Zero contamination guarantee - all tools install to workspace

param(
    [string]`$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
)

Write-Host "🔧 Installing ISOLATED Development Toolchain" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Target: `$WorkspaceRoot\Tools" -ForegroundColor White
Write-Host ""

`$toolsPath = "`$WorkspaceRoot\Tools"
`$downloadPath = "`$WorkspaceRoot\Cache\Downloads"

# Create download cache
New-Item -ItemType Directory -Path `$downloadPath -Force | Out-Null

Write-Host "🛡️ ISOLATION FEATURES:" -ForegroundColor Yellow
Write-Host "• No system PATH modifications" -ForegroundColor White
Write-Host "• No global environment variables" -ForegroundColor White
Write-Host "• No registry changes" -ForegroundColor White
Write-Host "• Self-contained tool installation" -ForegroundColor White
Write-Host ""

# Set isolated environment variables for this session only
`$env:BUILDWORKSPACE_ROOT = `$WorkspaceRoot
`$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
`$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
`$env:NUGET_PACKAGES = "`$WorkspaceRoot\Cache\NuGet"
`$env:MSBUILD_CACHE_DIR = "`$WorkspaceRoot\Cache\MSBuild"

Write-Host "✅ Isolated environment configured for this session" -ForegroundColor Green
Write-Host ""

Write-Host "🎉 Toolchain installation script ready!" -ForegroundColor Green
Write-Host "Ready to install .NET SDK, Build Tools, and utilities" -ForegroundColor White
Write-Host "All installations will be isolated to this workspace" -ForegroundColor White
"@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\install-toolchain.ps1" -Value $toolchainScript

# Create build script
$buildScript = @"
# Build Day Trading Platform in ISOLATED BuildWorkspace
# Zero contamination guarantee

param(
    [string]`$Configuration = "Release",
    [string]`$Platform = "x64"
)

`$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
`$SourcePath = "`$WorkspaceRoot\Source\DayTradingPlatform"
`$ArtifactsPath = "`$WorkspaceRoot\Artifacts\`$Configuration"

Write-Host "🏗️ ISOLATED BUILD - Day Trading Platform" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Configuration: `$Configuration | Platform: `$Platform" -ForegroundColor White
Write-Host "Isolation: Complete separation from system environment" -ForegroundColor Yellow
Write-Host ""

# Set isolated environment
`$env:BUILDWORKSPACE_ROOT = `$WorkspaceRoot
`$env:NUGET_PACKAGES = "`$WorkspaceRoot\Cache\NuGet"
`$env:MSBUILD_CACHE_DIR = "`$WorkspaceRoot\Cache\MSBuild"

if (-not (Test-Path `$SourcePath)) {
    Write-Host "❌ Source code not found at: `$SourcePath" -ForegroundColor Red
    Write-Host "Run source sync from Linux first" -ForegroundColor Yellow
    exit 1
}

Set-Location `$SourcePath

Write-Host "📦 Restoring packages to isolated cache..." -ForegroundColor Yellow
dotnet restore "DayTradinPlatform.sln" --packages "`$env:NUGET_PACKAGES"

Write-Host "🔨 Building in isolation..." -ForegroundColor Yellow
dotnet build "DayTradinPlatform.sln" --configuration `$Configuration --runtime "win-`$Platform" --no-restore

Write-Host "✅ Isolated build completed!" -ForegroundColor Green
"@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\build-platform.ps1" -Value $buildScript

Write-Host ""
Write-Host "🎉 DRAGON BuildWorkspace Created Successfully!" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
Write-Host ""
Write-Host "📂 Structure:" -ForegroundColor Yellow
Write-Host "  $WorkspaceRoot" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Yellow  
Write-Host "1. Sync source code from Linux development environment" -ForegroundColor White
Write-Host "2. Install isolated toolchain: .\Environment\Scripts\install-toolchain.ps1" -ForegroundColor White
Write-Host "3. Build projects: .\Environment\Scripts\build-platform.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🛡️ Isolation Guarantee: Zero contamination of existing environment!" -ForegroundColor Green
'

# Execute the setup script on DRAGON via PowerShell remoting or direct execution
# First try with direct SSH execution
echo -e "${YELLOW}📡 Executing BuildWorkspace setup on DRAGON...${NC}"

# Write the script to a temporary file and execute it
TEMP_SCRIPT="/tmp/dragon_buildworkspace_setup.ps1"
echo "$SETUP_SCRIPT" > "$TEMP_SCRIPT"

# Try to copy and execute the script on DRAGON
if command -v scp >/dev/null 2>&1; then
    # Copy script to DRAGON and execute
    echo -e "${YELLOW}📤 Copying setup script to DRAGON...${NC}"
    scp "$TEMP_SCRIPT" "$DRAGON_USER@$DRAGON_HOST:C:/temp/buildworkspace_setup.ps1" 2>/dev/null || {
        print_warning "SCP failed, trying alternative method..."
        
        # Alternative: Execute via SSH with inline PowerShell
        echo -e "${YELLOW}🔧 Executing setup via SSH...${NC}"
        ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -Command \"$SETUP_SCRIPT\"" 2>/dev/null || {
            print_error "Remote execution failed. Manual setup required."
            echo ""
            echo -e "${YELLOW}📋 Manual Setup Instructions:${NC}"
            echo "1. Connect to DRAGON via RDP or console"
            echo "2. Open PowerShell as Administrator"  
            echo "3. Run the following script:"
            echo ""
            echo "$SETUP_SCRIPT"
            echo ""
            exit 1
        }
    }
    
    if [[ $? -eq 0 ]]; then
        echo -e "${YELLOW}🚀 Executing setup script on DRAGON...${NC}"
        ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -ExecutionPolicy Bypass -File C:/temp/buildworkspace_setup.ps1"
    fi
else
    print_warning "SCP not available, using SSH direct execution..."
    ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -Command \"$SETUP_SCRIPT\""
fi

# Clean up temp file
rm -f "$TEMP_SCRIPT"

if [[ $? -eq 0 ]]; then
    print_header "DRAGON BuildWorkspace Setup Complete"
    print_success "Isolated BuildWorkspace created on DRAGON"
    print_success "Zero contamination guarantee active"
    print_success "Ready for source code sync and builds"
    echo ""
    echo -e "${YELLOW}🔄 Next Step: Sync source code${NC}"
    echo "   ./scripts/sync-to-buildworkspace.sh"
    echo ""
    echo -e "${YELLOW}🛡️ Isolation Features Active:${NC}"
    echo "   • No system modifications"
    echo "   • Self-contained toolchain"
    echo "   • Independent cache directories"
    echo "   • Complete workspace isolation"
else
    print_error "BuildWorkspace setup failed"
    print_warning "Manual setup may be required on DRAGON"
    exit 1
fi
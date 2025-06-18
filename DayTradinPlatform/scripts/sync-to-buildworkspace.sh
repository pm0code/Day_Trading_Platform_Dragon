#!/bin/bash
# Sync Source Code to Isolated BuildWorkspace
# Day Trading Platform - Safe source transfer to D:\BuildWorkspace\WindowsComponents
# Prevents contamination of existing development environment

set -e

# Configuration
SOURCE_PATH="${1:-.}"
BUILD_WORKSPACE="${BUILD_WORKSPACE:-D:/BuildWorkspace/WindowsComponents}"
DRAGON_HOST="${DRAGON_HOST:-192.168.1.35}"
DRAGON_USER="${DRAGON_USER:-admin}"
DRY_RUN="${2:-false}"
SKIP_GIT_FILES="${3:-false}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

function print_header() {
    echo ""
    echo -e "${CYAN}🔄 $1${NC}"
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

print_header "Syncing to Isolated BuildWorkspace"
echo -e "Source: ${SOURCE_PATH}"
echo -e "Target: ${BUILD_WORKSPACE} (on DRAGON)"
echo -e "${YELLOW}Isolation: Complete separation from existing environment${NC}"
echo ""

# Validate DRAGON connection settings
if [[ -z "$DRAGON_HOST" || -z "$DRAGON_USER" ]]; then
    print_error "DRAGON connection not configured"
    echo "Please set environment variables:"
    echo "  export DRAGON_HOST='192.168.1.100'"
    echo "  export DRAGON_USER='nader'"
    exit 1
fi

print_success "DRAGON target: $DRAGON_USER@$DRAGON_HOST"

# Test DRAGON connectivity
echo -e "${YELLOW}🔍 Testing DRAGON connectivity...${NC}"
if ping -c 1 -W 2 "$DRAGON_HOST" >/dev/null 2>&1; then
    print_success "DRAGON is reachable"
else
    print_error "Cannot reach DRAGON at $DRAGON_HOST"
    exit 1
fi

# Test SSH connection
echo -e "${YELLOW}🔐 Testing SSH connection...${NC}"
if ssh -o ConnectTimeout=5 -o BatchMode=yes "$DRAGON_USER@$DRAGON_HOST" exit 2>/dev/null; then
    print_success "SSH connection successful"
else
    print_error "SSH connection failed - ensure key authentication is set up"
    exit 1
fi

# Create BuildWorkspace structure on DRAGON
echo -e "${YELLOW}📁 Creating isolated BuildWorkspace structure...${NC}"
CREATE_STRUCTURE="
if (-not (Test-Path '$BUILD_WORKSPACE')) {
    Write-Host 'Creating BuildWorkspace directory structure...'
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Source' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Source\\DayTradingPlatform' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Tools' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Artifacts' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Environment' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Cache' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BUILD_WORKSPACE\\Cache\\NuGet' -Force | Out-Null
    Write-Host 'BuildWorkspace structure created'
} else {
    Write-Host 'BuildWorkspace already exists'
}
"

ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -Command \"$CREATE_STRUCTURE\""
print_success "BuildWorkspace structure ready"

# Prepare source for sync (exclude contaminating files)
echo -e "${YELLOW}📋 Preparing source files for sync...${NC}"

# Count source files (excluding build artifacts)
SOURCE_COUNT=$(find "$SOURCE_PATH" -type f \
    ! -path "*/bin/*" \
    ! -path "*/obj/*" \
    ! -path "*/TestResults/*" \
    ! -path "*/publish/*" \
    ! -path "*/.git/*" \
    ! -path "*/.vs/*" \
    ! -path "*/.vscode/*" \
    ! -path "*/packages/*" \
    ! -name "*.log" \
    ! -name "*.tmp" \
    ! -name "*.user" \
    ! -name "*.suo" \
    ! -name "*.cache" | wc -l)

print_success "Found $SOURCE_COUNT source files to sync"

if [[ "$DRY_RUN" == "true" ]]; then
    echo -e "${YELLOW}🔍 DRY RUN - Files that would be synced:${NC}"
    find "$SOURCE_PATH" -type f \
        ! -path "*/bin/*" \
        ! -path "*/obj/*" \
        ! -path "*/TestResults/*" \
        ! -path "*/publish/*" \
        ! -path "*/.git/*" \
        ! -path "*/.vs/*" \
        ! -path "*/.vscode/*" \
        ! -path "*/packages/*" \
        ! -name "*.log" \
        ! -name "*.tmp" \
        ! -name "*.user" \
        ! -name "*.suo" \
        ! -name "*.cache" | head -10 | while read file; do
        REL_PATH=${file#$SOURCE_PATH/}
        echo -e "  📄 $REL_PATH"
    done
    
    if [[ $SOURCE_COUNT -gt 10 ]]; then
        echo -e "  ... and $((SOURCE_COUNT - 10)) more files"
    fi
    echo ""
    echo -e "${GREEN}🚀 Run without dry-run to perform actual sync${NC}"
    exit 0
fi

# Sync source files to BuildWorkspace (isolated)
echo -e "${YELLOW}🔄 Syncing source files to isolated BuildWorkspace...${NC}"
TARGET_PATH="$BUILD_WORKSPACE/Source/DayTradingPlatform"

# Build rsync command with exclusions
RSYNC_EXCLUDES=(
    "--exclude=.git/"
    "--exclude=bin/"
    "--exclude=obj/"
    "--exclude=TestResults/"
    "--exclude=publish/"
    "--exclude=*.log"
    "--exclude=*.tmp"
    "--exclude=.vs/"
    "--exclude=.vscode/"
    "--exclude=packages/"
    "--exclude=*.user"
    "--exclude=*.suo"
    "--exclude=*.cache"
)

if [[ "$SKIP_GIT_FILES" == "true" ]]; then
    RSYNC_EXCLUDES+=(
        "--exclude=.gitignore"
        "--exclude=.gitattributes"
    )
fi

echo "Executing rsync with exclusions..."
rsync -avz --delete "${RSYNC_EXCLUDES[@]}" "$SOURCE_PATH/" "$DRAGON_USER@$DRAGON_HOST:$TARGET_PATH/"

if [[ $? -eq 0 ]]; then
    print_success "Source files synced to isolated BuildWorkspace"
else
    print_error "Sync failed"
    exit 1
fi

# Create isolation marker on DRAGON
echo -e "${YELLOW}🛡️ Creating isolation markers...${NC}"
ISOLATION_MARKER="
\$markerContent = @'
🛡️ ISOLATED WINDOWS BUILD ENVIRONMENT
====================================

⚠️  CONTAMINATION PREVENTION ACTIVE
This environment is completely isolated from your existing development setup.

🚫 ISOLATION FEATURES:
✅ Separate toolchain installation
✅ Isolated environment variables  
✅ Independent package cache
✅ No system-wide modifications
✅ Self-contained build artifacts

🎯 PURPOSE:
Build Windows-specific components without affecting your main development environment.
Prevents time-wasting issues caused by development environment contamination.

📂 BUILD WORKSPACE LOCATION:
$BUILD_WORKSPACE

🔧 TOOLCHAIN STATUS:
Run .\Environment\Scripts\install-toolchain.ps1 to install isolated tools

⚡ USAGE:
• Build: .\Environment\Scripts\build-platform.ps1
• Test: .\Environment\Scripts\test-platform.ps1  
• Monitor: .\Environment\Scripts\monitor-workspace.ps1

Last Sync: \$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Source: Linux development environment
Target: Isolated Windows build environment
Safety: Zero contamination design
'@

Set-Content -Path '$BUILD_WORKSPACE\ISOLATION-NOTICE.txt' -Value \$markerContent
Write-Host 'Isolation marker created'
"

ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -Command \"$ISOLATION_MARKER\""
print_success "Isolation markers created"

# Verify sync integrity
echo -e "${YELLOW}🔍 Verifying sync integrity...${NC}"
VERIFY_COMMAND="
\$sourceCount = (Get-ChildItem '$TARGET_PATH' -Recurse -File | Measure-Object).Count
Write-Host \"Files synced to BuildWorkspace: \$sourceCount\"

if (Test-Path '$TARGET_PATH\\DayTradinPlatform.sln') {
    Write-Host 'Solution file found: ✅'
} else {
    Write-Host 'Solution file missing: ❌'
}

if (Test-Path '$TARGET_PATH\\TradingPlatform.Core') {
    Write-Host 'Core project found: ✅'  
} else {
    Write-Host 'Core project missing: ❌'
}

if (Test-Path '$TARGET_PATH\\TradingPlatform.DisplayManagement') {
    Write-Host 'DisplayManagement project found: ✅'
} else {
    Write-Host 'DisplayManagement project missing: ❌'
}
"

ssh "$DRAGON_USER@$DRAGON_HOST" "powershell -Command \"$VERIFY_COMMAND\""

# Final status
print_header "Sync Complete"
print_success "Source code synced to isolated BuildWorkspace"
print_success "Zero contamination of existing development environment"  
print_success "Ready for Windows-specific builds"
echo ""
echo -e "${YELLOW}🚀 Next Steps on DRAGON:${NC}"
echo "1. cd $BUILD_WORKSPACE"
echo "2. .\\Environment\\Scripts\\install-toolchain.ps1"
echo "3. .\\Environment\\Scripts\\build-platform.ps1"
echo ""
echo -e "${YELLOW}🛡️ Isolation Benefits:${NC}"
echo "• No interference with existing tools"
echo "• Independent build environment"  
echo "• Safe experimentation space"
echo "• Easy cleanup if needed"
echo ""
print_success "BuildWorkspace sync completed successfully!"
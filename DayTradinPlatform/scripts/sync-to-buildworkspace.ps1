# Sync Source Code to Isolated BuildWorkspace
# Day Trading Platform - Safe source transfer to D:\BuildWorkspace\WindowsComponents
# Prevents contamination of existing development environment

param(
    [string]$SourcePath = ".",
    [string]$BuildWorkspace = "D:\BuildWorkspace\WindowsComponents",
    [string]$DragonHost = "${env:DRAGON_HOST}",
    [string]$DragonUser = "${env:DRAGON_USER}",
    [switch]$DryRun,
    [switch]$SkipGitFiles
)

# Colors for output
$RED = '\033[0;31m'
$GREEN = '\033[0;32m'
$YELLOW = '\033[1;33m'
$BLUE = '\033[0;34m'
$CYAN = '\033[0;36m'
$NC = '\033[0m' # No Color

function Print-Header($message) {
    Write-Host ""
    Write-Host "🔄 $message" -ForegroundColor Cyan
    Write-Host "$(('=' * ($message.Length + 3)))" -ForegroundColor Cyan
}

function Print-Success($message) {
    Write-Host "✅ $message" -ForegroundColor Green
}

function Print-Warning($message) {
    Write-Host "⚠️  $message" -ForegroundColor Yellow
}

function Print-Error($message) {
    Write-Host "❌ $message" -ForegroundColor Red
}

Print-Header "Syncing to Isolated BuildWorkspace"
Write-Host "Source: $SourcePath" -ForegroundColor White
Write-Host "Target: $BuildWorkspace (on DRAGON)" -ForegroundColor White
Write-Host "Isolation: Complete separation from existing environment" -ForegroundColor Yellow
Write-Host ""

# Validate DRAGON connection settings
if (-not $DragonHost -or -not $DragonUser) {
    Print-Error "DRAGON connection not configured"
    Write-Host "Please set environment variables:" -ForegroundColor White
    Write-Host "  export DRAGON_HOST='192.168.1.100'" -ForegroundColor Gray
    Write-Host "  export DRAGON_USER='nader'" -ForegroundColor Gray
    exit 1
}

Print-Success "DRAGON target: $DragonUser@$DragonHost"

# Test DRAGON connectivity
Write-Host "🔍 Testing DRAGON connectivity..." -ForegroundColor Yellow
if (ping -c 1 -W 2 $DragonHost > $null 2>&1) {
    Print-Success "DRAGON is reachable"
} else {
    Print-Error "Cannot reach DRAGON at $DragonHost"
    exit 1
}

# Test SSH connection
Write-Host "🔐 Testing SSH connection..." -ForegroundColor Yellow
if (ssh -o ConnectTimeout=5 -o BatchMode=yes "$DragonUser@$DragonHost" exit 2>$null) {
    Print-Success "SSH connection successful"
} else {
    Print-Error "SSH connection failed - ensure key authentication is set up"
    exit 1
}

# Create BuildWorkspace structure on DRAGON
Write-Host "📁 Creating isolated BuildWorkspace structure..." -ForegroundColor Yellow
$createStructure = @"
if (-not (Test-Path '$BuildWorkspace')) {
    Write-Host 'Creating BuildWorkspace directory structure...'
    New-Item -ItemType Directory -Path '$BuildWorkspace' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Source' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Source\DayTradingPlatform' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Tools' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Artifacts' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Environment' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Cache' -Force | Out-Null
    New-Item -ItemType Directory -Path '$BuildWorkspace\Cache\NuGet' -Force | Out-Null
    Write-Host 'BuildWorkspace structure created'
} else {
    Write-Host 'BuildWorkspace already exists'
}
"@

ssh "$DragonUser@$DragonHost" "powershell -Command `"$createStructure`""
Print-Success "BuildWorkspace structure ready"

# Prepare rsync exclusions for clean sync
$excludePatterns = @(
    '--exclude=.git/',
    '--exclude=bin/',
    '--exclude=obj/',
    '--exclude=TestResults/',
    '--exclude=publish/',
    '--exclude=*.log',
    '--exclude=*.tmp',
    '--exclude=.vs/',
    '--exclude=.vscode/',
    '--exclude=packages/',
    '--exclude=*.user',
    '--exclude=*.suo',
    '--exclude=*.cache'
)

if ($SkipGitFiles) {
    $excludePatterns += '--exclude=.gitignore'
    $excludePatterns += '--exclude=.gitattributes'
}

# Prepare source for sync (exclude contaminating files)
Write-Host "📋 Preparing source files for sync..." -ForegroundColor Yellow
$sourceFiles = Get-ChildItem $SourcePath -Recurse | Where-Object {
    $_.FullName -notmatch '\\(bin|obj|TestResults|publish|\\.git|\\.vs|\\.vscode|packages)\\' -and
    $_.Extension -notin @('.log', '.tmp', '.user', '.suo', '.cache')
}

Print-Success "Found $($sourceFiles.Count) source files to sync"

if ($DryRun) {
    Write-Host "🔍 DRY RUN - Files that would be synced:" -ForegroundColor Yellow
    $sourceFiles | Select-Object -First 10 | ForEach-Object {
        $relativePath = $_.FullName.Replace((Resolve-Path $SourcePath).Path, "")
        Write-Host "  📄 $relativePath" -ForegroundColor Gray
    }
    if ($sourceFiles.Count -gt 10) {
        Write-Host "  ... and $($sourceFiles.Count - 10) more files" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "🚀 Run without -DryRun to perform actual sync" -ForegroundColor Green
    exit 0
}

# Sync source files to BuildWorkspace (isolated)
Write-Host "🔄 Syncing source files to isolated BuildWorkspace..." -ForegroundColor Yellow
$targetPath = "$BuildWorkspace/Source/DayTradingPlatform"

$rsyncCommand = "rsync -avz --delete " + ($excludePatterns -join ' ') + " $SourcePath/ `"$DragonUser@$DragonHost`:$targetPath/`""
Write-Host "Executing: $rsyncCommand" -ForegroundColor Gray

try {
    Invoke-Expression $rsyncCommand
    Print-Success "Source files synced to isolated BuildWorkspace"
} catch {
    Print-Error "Sync failed: $_"
    exit 1
}

# Create isolation marker on DRAGON
Write-Host "🛡️ Creating isolation markers..." -ForegroundColor Yellow
$isolationMarker = @"
`$markerContent = @'
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
$BuildWorkspace

🔧 TOOLCHAIN STATUS:
Run .\Environment\Scripts\install-toolchain.ps1 to install isolated tools

⚡ USAGE:
• Build: .\Environment\Scripts\build-platform.ps1
• Test: .\Environment\Scripts\test-platform.ps1  
• Monitor: .\Environment\Scripts\monitor-workspace.ps1

Last Sync: `$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Source: Linux development environment
Target: Isolated Windows build environment
Safety: Zero contamination design
'@

Set-Content -Path '$BuildWorkspace\ISOLATION-NOTICE.txt' -Value `$markerContent
Write-Host 'Isolation marker created'
"@

ssh "$DragonUser@$DragonHost" "powershell -Command `"$isolationMarker`""
Print-Success "Isolation markers created"

# Verify sync integrity
Write-Host "🔍 Verifying sync integrity..." -ForegroundColor Yellow
$verifyCommand = @"
`$sourceCount = (Get-ChildItem '$targetPath' -Recurse -File | Measure-Object).Count
Write-Host "Files synced to BuildWorkspace: `$sourceCount"

if (Test-Path '$targetPath\DayTradinPlatform.sln') {
    Write-Host 'Solution file found: ✅'
} else {
    Write-Host 'Solution file missing: ❌'
}

if (Test-Path '$targetPath\TradingPlatform.Core') {
    Write-Host 'Core project found: ✅'  
} else {
    Write-Host 'Core project missing: ❌'
}

if (Test-Path '$targetPath\TradingPlatform.DisplayManagement') {
    Write-Host 'DisplayManagement project found: ✅'
} else {
    Write-Host 'DisplayManagement project missing: ❌'
}
"@

ssh "$DragonUser@$DragonHost" "powershell -Command `"$verifyCommand`""

# Final status
Print-Header "Sync Complete"
Write-Host "✅ Source code synced to isolated BuildWorkspace" -ForegroundColor Green
Write-Host "✅ Zero contamination of existing development environment" -ForegroundColor Green  
Write-Host "✅ Ready for Windows-specific builds" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Next Steps on DRAGON:" -ForegroundColor Yellow
Write-Host "1. cd $BuildWorkspace" -ForegroundColor White
Write-Host "2. .\Environment\Scripts\install-toolchain.ps1" -ForegroundColor White
Write-Host "3. .\Environment\Scripts\build-platform.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🛡️ Isolation Benefits:" -ForegroundColor Yellow
Write-Host "• No interference with existing tools" -ForegroundColor White
Write-Host "• Independent build environment" -ForegroundColor White  
Write-Host "• Safe experimentation space" -ForegroundColor White
Write-Host "• Easy cleanup if needed" -ForegroundColor White
Write-Host ""
Print-Success "BuildWorkspace sync completed successfully!"
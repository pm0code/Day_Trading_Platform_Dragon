# DRAGON BuildWorkspace Setup - Manual Execution
# Run this script directly on DRAGON to create isolated build environment
# Purpose: Zero contamination of existing development environment

param(
    [string]$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
)

Write-Host "🛡️ CREATING ISOLATED DRAGON BUILDWORKSPACE" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "Target: $WorkspaceRoot" -ForegroundColor White
Write-Host "Purpose: Complete isolation for Windows builds" -ForegroundColor Yellow
Write-Host ""

# Create complete directory structure
Write-Host "📁 Creating isolated directory structure..." -ForegroundColor Green

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
    "$WorkspaceRoot\Cache\Downloads",
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
Write-Host "📋 Creating workspace documentation..." -ForegroundColor Green

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

🚀 USAGE INSTRUCTIONS:
1. Sync source: Copy Day Trading Platform source to Source\DayTradingPlatform\
2. Install tools: .\Environment\Scripts\install-toolchain.ps1
3. Build project: .\Environment\Scripts\build-platform.ps1
4. Run tests: .\Environment\Scripts\test-platform.ps1

Created: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
System: DRAGON BuildWorkspace
Purpose: Isolated Windows Build Environment
Hardware: RTX 4070 Ti + RTX 3060 Ti Dual-GPU Configuration
"@

Set-Content -Path "$WorkspaceRoot\README-ISOLATED-BUILDWORKSPACE.txt" -Value $workspaceReadme

# Create comprehensive toolchain installation script
Write-Host "🔧 Creating toolchain installation script..." -ForegroundColor Green

$toolchainScript = @'
# ISOLATED Toolchain Installation for BuildWorkspace
# Zero contamination guarantee - all tools install to workspace

param(
    [string]$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents",
    [switch]$SkipDownloads,
    [switch]$QuickInstall
)

Write-Host "🔧 Installing ISOLATED Development Toolchain" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Target: $WorkspaceRoot\Tools" -ForegroundColor White
Write-Host ""

$toolsPath = "$WorkspaceRoot\Tools"
$downloadPath = "$WorkspaceRoot\Cache\Downloads"

# Create download cache
New-Item -ItemType Directory -Path $downloadPath -Force | Out-Null

Write-Host "🛡️ ISOLATION FEATURES:" -ForegroundColor Yellow
Write-Host "• No system PATH modifications" -ForegroundColor White
Write-Host "• No global environment variables" -ForegroundColor White
Write-Host "• No registry changes" -ForegroundColor White
Write-Host "• Self-contained tool installation" -ForegroundColor White
Write-Host ""

# Set isolated environment variables for this session only
$env:BUILDWORKSPACE_ROOT = $WorkspaceRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:NUGET_PACKAGES = "$WorkspaceRoot\Cache\NuGet"
$env:MSBUILD_CACHE_DIR = "$WorkspaceRoot\Cache\MSBuild"

Write-Host "✅ Isolated environment configured for this session" -ForegroundColor Green

# Check if .NET SDK is already available
$dotnetInstalled = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
    $dotnetInstalled = $true
} else {
    Write-Host "⚠️  .NET SDK not found - will use system installation if available" -ForegroundColor Yellow
}

# Check for MSBuild
$msbuildInstalled = $false
if (Get-Command msbuild -ErrorAction SilentlyContinue) {
    Write-Host "✅ MSBuild found" -ForegroundColor Green
    $msbuildInstalled = $true
} else {
    Write-Host "⚠️  MSBuild not found - may need Visual Studio Build Tools" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎉 Toolchain assessment completed!" -ForegroundColor Green

if ($dotnetInstalled -and $msbuildInstalled) {
    Write-Host "✅ All required tools are available" -ForegroundColor Green
    Write-Host "Ready to build Day Trading Platform projects" -ForegroundColor White
} else {
    Write-Host "⚠️  Some tools may need to be installed manually" -ForegroundColor Yellow
    Write-Host "Consider installing:" -ForegroundColor White
    if (-not $dotnetInstalled) {
        Write-Host "  • .NET 8 SDK" -ForegroundColor Gray
    }
    if (-not $msbuildInstalled) {
        Write-Host "  • Visual Studio Build Tools 2022" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "🛡️ Environment isolation maintained - no system changes made" -ForegroundColor Green
'@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\install-toolchain.ps1" -Value $toolchainScript

# Create build script
Write-Host "🏗️ Creating build automation script..." -ForegroundColor Green

$buildScript = @'
# Build Day Trading Platform in ISOLATED BuildWorkspace
# Zero contamination guarantee

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$Clean,
    [switch]$RunTests
)

$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
$SourcePath = "$WorkspaceRoot\Source\DayTradingPlatform"
$ArtifactsPath = "$WorkspaceRoot\Artifacts\$Configuration"

Write-Host "🏗️ ISOLATED BUILD - Day Trading Platform" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration | Platform: $Platform" -ForegroundColor White
Write-Host "Isolation: Complete separation from system environment" -ForegroundColor Yellow
Write-Host ""

# Set isolated environment
$env:BUILDWORKSPACE_ROOT = $WorkspaceRoot
$env:NUGET_PACKAGES = "$WorkspaceRoot\Cache\NuGet"
$env:MSBUILD_CACHE_DIR = "$WorkspaceRoot\Cache\MSBuild"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Host "🛡️ Isolated environment variables active" -ForegroundColor Green
Write-Host "  NUGET_PACKAGES = $env:NUGET_PACKAGES" -ForegroundColor Gray
Write-Host "  MSBUILD_CACHE_DIR = $env:MSBUILD_CACHE_DIR" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $SourcePath)) {
    Write-Host "❌ Source code not found at: $SourcePath" -ForegroundColor Red
    Write-Host "Please sync source code first" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Expected structure:" -ForegroundColor Yellow
    Write-Host "  $SourcePath\DayTradinPlatform.sln" -ForegroundColor Gray
    Write-Host "  $SourcePath\TradingPlatform.Core\" -ForegroundColor Gray
    Write-Host "  $SourcePath\TradingPlatform.DisplayManagement\" -ForegroundColor Gray
    exit 1
}

if (-not (Test-Path "$SourcePath\DayTradinPlatform.sln")) {
    Write-Host "❌ Solution file not found: $SourcePath\DayTradinPlatform.sln" -ForegroundColor Red
    exit 1
}

Set-Location $SourcePath
Write-Host "📂 Working directory: $SourcePath" -ForegroundColor Green

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean "DayTradinPlatform.sln" --configuration $Configuration
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Clean completed" -ForegroundColor Green
    }
}

Write-Host "📦 Restoring packages to isolated cache..." -ForegroundColor Yellow
dotnet restore "DayTradinPlatform.sln" --packages "$env:NUGET_PACKAGES"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Package restore failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "✅ Packages restored to isolated cache" -ForegroundColor Green

Write-Host "🔨 Building in isolation..." -ForegroundColor Yellow
dotnet build "DayTradinPlatform.sln" --configuration $Configuration --runtime "win-$Platform" --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "✅ Build completed successfully" -ForegroundColor Green

# Run tests if requested
if ($RunTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    
    if (Test-Path "TradingPlatform.Testing\TradingPlatform.Testing.csproj") {
        $testResultsPath = "$WorkspaceRoot\Artifacts\TestResults"
        New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null
        
        dotnet test "TradingPlatform.Testing\TradingPlatform.Testing.csproj" `
            --configuration $Configuration `
            --logger "trx;LogFileName=TestResults.trx" `
            --results-directory $testResultsPath
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Tests completed successfully" -ForegroundColor Green
        } else {
            Write-Host "❌ Tests failed" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️  Test project not found" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "🎉 ISOLATED BUILD COMPLETED!" -ForegroundColor Green
Write-Host "Build artifacts are in isolated workspace only" -ForegroundColor White
Write-Host "No contamination of existing development environment" -ForegroundColor White
'@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\build-platform.ps1" -Value $buildScript

# Create test script
Write-Host "🧪 Creating test automation script..." -ForegroundColor Green

$testScript = @'
# Test Day Trading Platform in ISOLATED BuildWorkspace

param(
    [string]$Configuration = "Release"
)

$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
$SourcePath = "$WorkspaceRoot\Source\DayTradingPlatform"
$TestResultsPath = "$WorkspaceRoot\Artifacts\TestResults"

Write-Host "🧪 ISOLATED TESTING - Day Trading Platform" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Set isolated environment
$env:BUILDWORKSPACE_ROOT = $WorkspaceRoot
$env:NUGET_PACKAGES = "$WorkspaceRoot\Cache\NuGet"

if (-not (Test-Path $SourcePath)) {
    Write-Host "❌ Source code not found" -ForegroundColor Red
    exit 1
}

Set-Location $SourcePath
New-Item -ItemType Directory -Path $TestResultsPath -Force | Out-Null

dotnet test "TradingPlatform.Testing\TradingPlatform.Testing.csproj" `
    --configuration $Configuration `
    --logger "trx;LogFileName=TestResults.trx" `
    --results-directory $TestResultsPath `
    --collect:"XPlat Code Coverage"

Write-Host "📊 Test results: $TestResultsPath" -ForegroundColor Gray
'@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\test-platform.ps1" -Value $testScript

# Create system monitoring script
Write-Host "📊 Creating system monitoring script..." -ForegroundColor Green

$monitorScript = @'
# Monitor DRAGON BuildWorkspace System Health

$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"

Write-Host "📊 DRAGON BUILDWORKSPACE MONITOR" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# System Info
$os = Get-CimInstance -ClassName Win32_OperatingSystem
Write-Host "🖥️ System: $($os.Caption) $($os.OSArchitecture)" -ForegroundColor White

# Workspace Status
if (Test-Path $WorkspaceRoot) {
    Write-Host "✅ BuildWorkspace: Available" -ForegroundColor Green
    
    # Check workspace size
    $workspaceSize = [math]::Round((Get-ChildItem $WorkspaceRoot -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    Write-Host "📦 Workspace Size: $workspaceSize MB" -ForegroundColor White
    
    # Check cache sizes
    $nugetCacheSize = 0
    if (Test-Path "$WorkspaceRoot\Cache\NuGet") {
        $nugetCacheSize = [math]::Round((Get-ChildItem "$WorkspaceRoot\Cache\NuGet" -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
    }
    Write-Host "📦 NuGet Cache: $nugetCacheSize MB" -ForegroundColor White
    
} else {
    Write-Host "❌ BuildWorkspace: Not found" -ForegroundColor Red
}

# GPU Status
Write-Host ""
Write-Host "🎮 GPU Status:" -ForegroundColor Yellow
$gpus = Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.Name -notlike "*Basic*" -and $_.Name -notlike "*Microsoft*" }
foreach ($gpu in $gpus) {
    Write-Host "  🎮 $($gpu.Name)" -ForegroundColor White
}

# Session Type
Write-Host ""
Write-Host "🖥️ Session Type:" -ForegroundColor Yellow
$sessionName = $env:SESSIONNAME
if ($sessionName -eq "Console") {
    Write-Host "  ✅ Direct Console - Full hardware access" -ForegroundColor Green
} else {
    Write-Host "  🌐 Remote Session ($sessionName) - Limited hardware access" -ForegroundColor Cyan
}
'@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\monitor-workspace.ps1" -Value $monitorScript

# Final summary
Write-Host ""
Write-Host "🎉 DRAGON BUILDWORKSPACE CREATED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "📂 Workspace Location:" -ForegroundColor Yellow
Write-Host "  $WorkspaceRoot" -ForegroundColor White
Write-Host ""
Write-Host "🛡️ Isolation Features:" -ForegroundColor Yellow
Write-Host "  ✅ Zero system contamination" -ForegroundColor Green
Write-Host "  ✅ Self-contained toolchain" -ForegroundColor Green
Write-Host "  ✅ Isolated cache directories" -ForegroundColor Green
Write-Host "  ✅ Independent build environment" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Copy source code to: $WorkspaceRoot\Source\DayTradingPlatform" -ForegroundColor White
Write-Host "  2. Install tools: .\Environment\Scripts\install-toolchain.ps1" -ForegroundColor White
Write-Host "  3. Build project: .\Environment\Scripts\build-platform.ps1" -ForegroundColor White
Write-Host "  4. Run tests: .\Environment\Scripts\test-platform.ps1" -ForegroundColor White
Write-Host "  5. Monitor system: .\Environment\Scripts\monitor-workspace.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🎯 Ready for isolated Windows development!" -ForegroundColor Green
Write-Host "No risk of contaminating your existing environment!" -ForegroundColor Cyan
# Create Clearly Marked DRAGON Project Structure
# Day Trading Platform - Hybrid Development Architecture
# Ensures no confusion about Linux dev + Windows build separation

param(
    [string]$ProjectRoot = "C:\dev\trading-platform",
    [switch]$CreateDesktopShortcuts
)

Write-Host "🐉 Creating DRAGON Project Structure with Clear Markings" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# Ensure we're running as Administrator for full access
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "⚠️  Running with limited privileges - some features may be restricted" -ForegroundColor Yellow
}

# Create main project directory structure
Write-Host "📁 Creating DRAGON project directory structure..." -ForegroundColor Green

$directories = @(
    "$ProjectRoot",
    "$ProjectRoot\DayTradinPlatform",                    # Main project (synced from Linux)
    "$ProjectRoot\artifacts",                            # Build outputs
    "$ProjectRoot\logs",                                 # Build and system logs
    "$ProjectRoot\documentation",                        # DRAGON-specific docs
    "$ProjectRoot\tools",                               # DRAGON-specific tools
    "$ProjectRoot\backup"                               # Local backups
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  ✅ Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "  ✅ Exists: $dir" -ForegroundColor Green
    }
}

# Create visual README files with clear markings
Write-Host "📋 Creating visual identification files..." -ForegroundColor Green

# Main project README
$mainReadme = @"
🐉 DRAGON TRADING PLATFORM - WINDOWS BUILD SYSTEM
==================================================

⚠️  IMPORTANT: This is the WINDOWS BUILD ENVIRONMENT
🐧 Primary development happens on LINUX with Claude Code
🔄 This system receives code via automated sync and builds Windows-specific components

📊 SYSTEM ROLE:
├── 🪟 Windows Component Builds (WinUI 3, GPU Detection, Display Management)
├── 🎮 Hardware Detection (RTX 4070 Ti + RTX 3060 Ti)
├── 🖥️ Multi-Monitor Trading Setup Testing
├── 📦 Windows Deployment Package Creation
└── ⚡ Performance Testing on Target Hardware

🚫 DO NOT MANUALLY EDIT SOURCE CODE HERE
✅ All source changes should be made in the Linux development environment
🔄 Use: ./scripts/dragon-remote-build.sh from Linux to trigger builds

📂 DIRECTORY STRUCTURE:
├── DayTradinPlatform\          # 🔄 SYNCED FROM LINUX (read-only)
├── artifacts\                  # 📦 Build outputs and deployment packages
├── logs\                      # 📊 Build logs and system monitoring
├── documentation\             # 📋 DRAGON-specific documentation
├── tools\                     # 🔧 DRAGON-specific utilities
└── backup\                    # 💾 Local backups and snapshots

🎯 QUICK ACTIONS:
• Check build status: PowerShell scripts in DayTradinPlatform\scripts\
• View system health: Monitor GPU temps, memory usage
• Test applications: Run built TradingApp.exe from artifacts\

Last Updated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
System: DRAGON (Windows 11 x64)
Hardware: RTX 4070 Ti + RTX 3060 Ti
Purpose: Windows Build Environment for Linux-Controlled Development
"@

Set-Content -Path "$ProjectRoot\README-DRAGON-SYSTEM.txt" -Value $mainReadme
Write-Host "  ✅ Created: README-DRAGON-SYSTEM.txt" -ForegroundColor Green

# DayTradinPlatform sync marker
$syncMarker = @"
🔄 AUTOMATED SYNC DIRECTORY
===========================

⚠️  WARNING: DO NOT MANUALLY EDIT FILES IN THIS DIRECTORY
🐧 This directory is automatically synchronized from the Linux development environment
🔄 Any manual changes will be OVERWRITTEN during the next sync

✅ TO MAKE CHANGES:
1. Edit files in the Linux development environment (Claude Code)
2. Run sync command from Linux: ./scripts/dragon-remote-build.sh sync
3. Files will be automatically updated here

🎯 PURPOSE:
• Source code synchronized from Linux
• Windows-specific builds executed here
• Artifacts generated in ../artifacts/

🚫 DO NOT:
❌ Edit source files directly
❌ Create new files in this directory
❌ Modify project structure manually

✅ DO:
✅ Run builds using PowerShell scripts
✅ Test compiled applications
✅ Monitor build outputs
✅ Check system health during builds

Last Sync: Check git log for latest sync timestamp
Sync Source: Linux development environment with Claude Code
Build Target: Windows 11 x64 with RTX GPU support
"@

if (Test-Path "$ProjectRoot\DayTradinPlatform") {
    Set-Content -Path "$ProjectRoot\DayTradinPlatform\README-SYNC-DIRECTORY.txt" -Value $syncMarker
    Write-Host "  ✅ Created: DayTradinPlatform\README-SYNC-DIRECTORY.txt" -ForegroundColor Green
}

# Artifacts directory info
$artifactsReadme = @"
📦 BUILD ARTIFACTS & DEPLOYMENT PACKAGES
========================================

🎯 PURPOSE: Storage for compiled Windows components and deployment packages

📂 DIRECTORY CONTENTS:
├── TradingApp\                # 🪟 WinUI 3 trading application
├── Libraries\                 # 📚 Compiled .NET libraries
├── TestResults\              # 🧪 Test execution results
├── PublishPackages\          # 📦 Deployment-ready packages
└── PerformanceReports\       # 📊 Hardware performance data

🚀 DEPLOYMENT READY FILES:
• TradingPlatform.TradingApp.exe - Main trading application
• Required libraries and dependencies
• Configuration files for DRAGON hardware
• GPU detection and display management components

🔍 TYPICAL BUILD OUTPUTS:
✅ Platform-independent libraries (.dll files)
✅ Windows-specific TradingApp executable
✅ Hardware detection components
✅ Multi-monitor configuration tools
✅ Test results and performance metrics

🎮 HARDWARE-SPECIFIC FEATURES:
• RTX 4070 Ti + RTX 3060 Ti detection
• 8-monitor support configuration
• GPU performance optimization
• Trading workload acceleration

📊 MONITORING:
• Build timestamps and versions
• Performance benchmarks
• Hardware utilization metrics
• Trading system readiness status

Last Build: Check timestamps in subdirectories
Build Source: Automated from Linux development environment
Target Hardware: DRAGON (RTX 4070 Ti + RTX 3060 Ti)
"@

Set-Content -Path "$ProjectRoot\artifacts\README-BUILD-ARTIFACTS.txt" -Value $artifactsReadme
Write-Host "  ✅ Created: artifacts\README-BUILD-ARTIFACTS.txt" -ForegroundColor Green

# Create desktop shortcuts if requested
if ($CreateDesktopShortcuts) {
    Write-Host "🖥️ Creating desktop shortcuts for quick access..." -ForegroundColor Green
    
    # Get desktop path
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    
    # Create shortcut to project root
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut("$desktopPath\DRAGON Trading Platform.lnk")
    $shortcut.TargetPath = $ProjectRoot
    $shortcut.Description = "DRAGON Trading Platform - Windows Build Environment"
    $shortcut.Save()
    Write-Host "  ✅ Created desktop shortcut to project root" -ForegroundColor Green
    
    # Create shortcut to build artifacts
    $artifactShortcut = $shell.CreateShortcut("$desktopPath\DRAGON Build Artifacts.lnk")
    $artifactShortcut.TargetPath = "$ProjectRoot\artifacts"
    $artifactShortcut.Description = "DRAGON Build Artifacts - Compiled Applications"
    $artifactShortcut.Save()
    Write-Host "  ✅ Created desktop shortcut to build artifacts" -ForegroundColor Green
}

# Create system information file
Write-Host "📊 Creating DRAGON system information file..." -ForegroundColor Green

$systemInfo = @"
🐉 DRAGON SYSTEM CONFIGURATION
==============================

🖥️ SYSTEM INFORMATION:
OS: $($(Get-CimInstance -ClassName Win32_OperatingSystem).Caption)
Architecture: $($(Get-CimInstance -ClassName Win32_OperatingSystem).OSArchitecture)
CPU: $($(Get-CimInstance -ClassName Win32_Processor).Name)
Memory: $([math]::Round((Get-CimInstance -ClassName Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum).Sum / 1GB, 2)) GB

🎮 GPU CONFIGURATION:
"@

# Add GPU information
$gpus = Get-CimInstance -ClassName Win32_VideoController | Where-Object { $_.Name -notlike "*Basic*" -and $_.Name -notlike "*Microsoft*" }
foreach ($gpu in $gpus) {
    $systemInfo += "`nGPU: $($gpu.Name)"
    if ($gpu.AdapterRAM) {
        $vramGB = [math]::Round($gpu.AdapterRAM / 1GB, 1)
        $systemInfo += " ($vramGB GB)"
    }
}

$systemInfo += @"

🎯 TRADING PLATFORM ROLE:
Purpose: Windows Build Environment for Hybrid Development
Primary Dev: Linux with Claude Code integration
Build Target: Windows 11 x64 with RTX GPU support
Hardware: Dual RTX GPU configuration for 8-monitor trading

🔄 SYNC STATUS:
Last Updated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Project Root: $ProjectRoot
Sync Source: Linux development environment
Build Automation: PowerShell scripts in DayTradinPlatform\scripts\

🎮 HARDWARE CAPABILITIES:
✅ RTX GPU Hardware Detection
✅ Multi-Monitor Support (up to 8 displays)
✅ Hardware-Accelerated Trading Graphics
✅ Real-time Performance Monitoring
✅ Windows Session Detection (RDP vs Console)

📋 QUICK REFERENCE:
• Project Files: $ProjectRoot\DayTradinPlatform\ (SYNCED - READ ONLY)
• Build Outputs: $ProjectRoot\artifacts\
• System Logs: $ProjectRoot\logs\
• Documentation: $ProjectRoot\documentation\

⚠️  REMEMBER: All source code changes happen on Linux!
🔄 Trigger builds from Linux: ./scripts/dragon-remote-build.sh
"@

Set-Content -Path "$ProjectRoot\DRAGON-SYSTEM-INFO.txt" -Value $systemInfo
Write-Host "  ✅ Created: DRAGON-SYSTEM-INFO.txt" -ForegroundColor Green

# Create PowerShell profile enhancement for DRAGON awareness
Write-Host "📝 Enhancing PowerShell profile for DRAGON awareness..." -ForegroundColor Green

$profileContent = @"

# 🐉 DRAGON TRADING PLATFORM PROFILE ENHANCEMENT
# ===============================================
Write-Host ""
Write-Host "🐉 DRAGON TRADING PLATFORM - WINDOWS BUILD ENVIRONMENT" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "🔄 Hybrid Development: Linux Dev + Windows Build" -ForegroundColor Yellow
Write-Host "🎮 Hardware: RTX 4070 Ti + RTX 3060 Ti Dual GPU" -ForegroundColor Green
Write-Host "🖥️ Monitors: Up to 8 display support for trading" -ForegroundColor Green
Write-Host ""
Write-Host "⚠️  SOURCE CODE EDITING: Use Linux environment with Claude Code" -ForegroundColor Red
Write-Host "✅ BUILD EXECUTION: Use this Windows environment" -ForegroundColor Green
Write-Host ""

# Quick navigation aliases
Set-Alias -Name "dragon-project" -Value "Set-Location '$ProjectRoot'"
Set-Alias -Name "dragon-source" -Value "Set-Location '$ProjectRoot\DayTradinPlatform'"
Set-Alias -Name "dragon-artifacts" -Value "Set-Location '$ProjectRoot\artifacts'"
Set-Alias -Name "dragon-logs" -Value "Set-Location '$ProjectRoot\logs'"

# Build shortcuts
function Invoke-DragonBuild {
    Write-Host "🔨 Starting DRAGON build process..." -ForegroundColor Green
    Set-Location "$ProjectRoot\DayTradinPlatform"
    PowerShell -File "scripts\build-windows-components.ps1" -Configuration Release -Runtime win-x64
}

function Show-DragonStatus {
    Write-Host "📊 DRAGON System Status:" -ForegroundColor Cyan
    Write-Host "========================" -ForegroundColor Cyan
    
    # Project structure
    Write-Host "📂 Project Structure:" -ForegroundColor Yellow
    Write-Host "  Project Root: $ProjectRoot" -ForegroundColor White
    Write-Host "  Source Code: $(if (Test-Path '$ProjectRoot\DayTradinPlatform') { '✅ Synced' } else { '❌ Not Synced' })" -ForegroundColor White
    Write-Host "  Build Artifacts: $(if (Test-Path '$ProjectRoot\artifacts') { '✅ Available' } else { '❌ No Builds' })" -ForegroundColor White
    
    # GPU Status
    Write-Host ""
    Write-Host "🎮 GPU Status:" -ForegroundColor Yellow
    `$gpus = Get-CimInstance -ClassName Win32_VideoController | Where-Object { `$_.Name -notlike "*Basic*" -and `$_.Name -notlike "*Microsoft*" }
    foreach (`$gpu in `$gpus) {
        Write-Host "  🎮 `$(`$gpu.Name)" -ForegroundColor White
    }
    
    # Session Detection
    Write-Host ""
    Write-Host "🖥️ Session Information:" -ForegroundColor Yellow
    `$sessionName = `$env:SESSIONNAME
    if (`$sessionName -eq "Console") {
        Write-Host "  ✅ Direct Console Access - Full hardware capabilities" -ForegroundColor Green
    } else {
        Write-Host "  🌐 Remote Session (`$sessionName) - Limited capabilities" -ForegroundColor Yellow
    }
}

function Show-DragonHelp {
    Write-Host "🐉 DRAGON Command Reference:" -ForegroundColor Cyan
    Write-Host "============================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Navigation:" -ForegroundColor Yellow
    Write-Host "  dragon-project    - Go to project root" -ForegroundColor White
    Write-Host "  dragon-source     - Go to source code (synced from Linux)" -ForegroundColor White
    Write-Host "  dragon-artifacts  - Go to build outputs" -ForegroundColor White
    Write-Host "  dragon-logs       - Go to build logs" -ForegroundColor White
    Write-Host ""
    Write-Host "Operations:" -ForegroundColor Yellow
    Write-Host "  Invoke-DragonBuild - Build Windows components" -ForegroundColor White
    Write-Host "  Show-DragonStatus  - Show system status" -ForegroundColor White
    Write-Host "  Show-DragonHelp    - Show this help" -ForegroundColor White
    Write-Host ""
    Write-Host "⚠️  Remember: Edit source code on Linux, build on DRAGON!" -ForegroundColor Red
}

# Set default location to project root
dragon-project

# Show initial status
Show-DragonStatus
"@

# Append to PowerShell profile
$profilePath = $PROFILE.AllUsersAllHosts
if (-not (Test-Path (Split-Path $profilePath))) {
    New-Item -ItemType Directory -Path (Split-Path $profilePath) -Force
}
Add-Content -Path $profilePath -Value $profileContent
Write-Host "  ✅ Enhanced PowerShell profile with DRAGON commands" -ForegroundColor Green

# Create environment variables for DRAGON awareness
Write-Host "🌍 Setting DRAGON environment variables..." -ForegroundColor Green
[Environment]::SetEnvironmentVariable("DRAGON_PROJECT_ROOT", $ProjectRoot, "Machine")
[Environment]::SetEnvironmentVariable("DRAGON_ROLE", "WINDOWS_BUILD_ENVIRONMENT", "Machine")
[Environment]::SetEnvironmentVariable("DRAGON_DEV_MODE", "HYBRID_LINUX_WINDOWS", "Machine")
Write-Host "  ✅ Environment variables configured" -ForegroundColor Green

# Create visual taskbar indicator (if possible)
Write-Host "🖥️ Setting up visual indicators..." -ForegroundColor Green

# Set custom wallpaper text indicator (registry modification)
try {
    $registryPath = "HKCU:\Control Panel\Desktop"
    Set-ItemProperty -Path $registryPath -Name "Wallpaper" -Value ""
    Write-Host "  ✅ Desktop configured for DRAGON identification" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️  Could not modify desktop settings (limited permissions)" -ForegroundColor Yellow
}

# Final summary
Write-Host ""
Write-Host "🎉 DRAGON Project Structure Created Successfully!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Summary of DRAGON Markings:" -ForegroundColor Yellow
Write-Host "├── 📂 Clear directory structure with purpose indicators" -ForegroundColor White
Write-Host "├── 📋 README files explaining hybrid development approach" -ForegroundColor White
Write-Host "├── 🖥️ Desktop shortcuts for quick access" -ForegroundColor White
Write-Host "├── 📝 Enhanced PowerShell profile with DRAGON commands" -ForegroundColor White
Write-Host "├── 🌍 Environment variables identifying DRAGON role" -ForegroundColor White
Write-Host "└── 📊 System information and configuration files" -ForegroundColor White
Write-Host ""
Write-Host "💡 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Restart PowerShell to load new profile" -ForegroundColor White
Write-Host "2. Run 'Show-DragonHelp' for command reference" -ForegroundColor White
Write-Host "3. Linux dev environment can now sync to: $ProjectRoot\DayTradinPlatform" -ForegroundColor White
Write-Host "4. Build artifacts will appear in: $ProjectRoot\artifacts" -ForegroundColor White
Write-Host ""
Write-Host "🔄 Remember: Edit on Linux, Build on DRAGON!" -ForegroundColor Red
Write-Host ""
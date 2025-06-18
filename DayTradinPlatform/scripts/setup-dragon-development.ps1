# DRAGON Development Environment Setup Script
# Day Trading Platform - Windows 11 x64 Development Setup
# Optimized for RTX 4070 Ti + RTX 3060 Ti dual-GPU configuration

param(
    [switch]$SkipVisualStudio,
    [switch]$SkipDotNet,
    [switch]$SkipGit,
    [switch]$QuickSetup
)

Write-Host "🐉 DRAGON Development Environment Setup" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Setting up Windows 11 x64 development environment for Day Trading Platform" -ForegroundColor White
Write-Host ""

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "❌ This script requires Administrator privileges" -ForegroundColor Red
    Write-Host "   Please run PowerShell as Administrator and try again" -ForegroundColor Yellow
    exit 1
}

# System Information
Write-Host "📊 System Information:" -ForegroundColor Green
$os = Get-CimInstance -ClassName Win32_OperatingSystem
$cpu = Get-CimInstance -ClassName Win32_Processor
$gpu = Get-CimInstance -ClassName Win32_VideoController
$memory = Get-CimInstance -ClassName Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum

Write-Host "   OS: $($os.Caption) ($($os.OSArchitecture))" -ForegroundColor White
Write-Host "   CPU: $($cpu.Name)" -ForegroundColor White
Write-Host "   Memory: $([math]::Round($memory.Sum / 1GB, 2)) GB" -ForegroundColor White
Write-Host "   GPUs:" -ForegroundColor White
foreach ($g in $gpu) {
    if ($g.Name -notlike "*Basic*" -and $g.Name -notlike "*Microsoft*") {
        Write-Host "     - $($g.Name)" -ForegroundColor White
    }
}
Write-Host ""

# Enable Windows Developer Mode
Write-Host "🔧 Enabling Windows Developer Mode..." -ForegroundColor Green
try {
    $devMode = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name "AllowDevelopmentWithoutDevLicense" -ErrorAction SilentlyContinue
    if ($devMode.AllowDevelopmentWithoutDevLicense -ne 1) {
        Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name "AllowDevelopmentWithoutDevLicense" -Value 1
        Write-Host "   ✅ Developer Mode enabled" -ForegroundColor Green
    } else {
        Write-Host "   ✅ Developer Mode already enabled" -ForegroundColor Green
    }
} catch {
    Write-Host "   ⚠️  Could not enable Developer Mode: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Install Chocolatey if not present
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "🍫 Installing Chocolatey Package Manager..." -ForegroundColor Green
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
    Write-Host "   ✅ Chocolatey installed" -ForegroundColor Green
} else {
    Write-Host "🍫 Chocolatey already installed, updating..." -ForegroundColor Green
    choco upgrade chocolatey -y
}

# Install Git
if (-not $SkipGit -and -not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "📦 Installing Git..." -ForegroundColor Green
    choco install git -y
    Write-Host "   ✅ Git installed" -ForegroundColor Green
} else {
    Write-Host "📦 Git already installed" -ForegroundColor Green
}

# Install .NET 8 SDK
if (-not $SkipDotNet) {
    Write-Host "🔷 Installing .NET 8 SDK..." -ForegroundColor Green
    choco install dotnet-8.0-sdk -y
    Write-Host "   ✅ .NET 8 SDK installed" -ForegroundColor Green
}

# Install Visual Studio 2022 Community with required workloads
if (-not $SkipVisualStudio) {
    Write-Host "🔨 Installing Visual Studio 2022 Community..." -ForegroundColor Green
    
    # Download Visual Studio Installer
    $vsInstallerPath = "$env:TEMP\vs_community.exe"
    Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vs_community.exe" -OutFile $vsInstallerPath
    
    # Install with required workloads for DRAGON trading platform
    $workloads = @(
        "Microsoft.VisualStudio.Workload.ManagedDesktop",    # .NET desktop development
        "Microsoft.VisualStudio.Workload.Universal",        # UWP development (for WinUI 3)
        "Microsoft.VisualStudio.Workload.NetWeb",           # ASP.NET and web development
        "Microsoft.VisualStudio.Component.Windows11SDK.22621" # Windows 11 SDK
    )
    
    $workloadArgs = $workloads -join " --add "
    $installArgs = "--quiet --wait --add $workloadArgs"
    
    Write-Host "   Installing Visual Studio with trading platform workloads..." -ForegroundColor White
    Start-Process -FilePath $vsInstallerPath -ArgumentList $installArgs -Wait
    Write-Host "   ✅ Visual Studio 2022 Community installed" -ForegroundColor Green
}

# Install additional development tools
Write-Host "🛠️ Installing additional development tools..." -ForegroundColor Green

$tools = @(
    "vscode",                    # Visual Studio Code
    "powershell-core",          # PowerShell 7
    "windows-terminal",         # Windows Terminal
    "7zip",                     # File compression
    "notepadplusplus",         # Text editor
    "sysinternals"             # System utilities
)

foreach ($tool in $tools) {
    try {
        choco install $tool -y --ignore-checksums
        Write-Host "   ✅ $tool installed" -ForegroundColor Green
    } catch {
        Write-Host "   ⚠️  Failed to install $tool" -ForegroundColor Yellow
    }
}

# Configure Windows for optimal trading performance
Write-Host "⚡ Configuring Windows for optimal trading performance..." -ForegroundColor Green

# Set high performance power plan
powercfg -setactive SCHEME_MIN

# Disable Windows Update automatic restarts during trading hours (9 AM - 4 PM)
$registryPath = "HKLM:\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings"
if (Test-Path $registryPath) {
    Set-ItemProperty -Path $registryPath -Name "ActiveHoursStart" -Value 9
    Set-ItemProperty -Path $registryPath -Name "ActiveHoursEnd" -Value 16
    Write-Host "   ✅ Windows Update active hours set (9 AM - 4 PM)" -ForegroundColor Green
}

# Disable Windows Defender real-time protection for development folders (user can re-enable)
Write-Host "   ⚠️  Consider adding development folders to Windows Defender exclusions" -ForegroundColor Yellow

# Configure GPU settings for dual RTX setup
Write-Host "🎮 Optimizing GPU settings for RTX 4070 Ti + RTX 3060 Ti..." -ForegroundColor Green

# Enable Hardware-accelerated GPU scheduling
$gpuSchedulingPath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"
Set-ItemProperty -Path $gpuSchedulingPath -Name "HwSchMode" -Value 2
Write-Host "   ✅ Hardware-accelerated GPU scheduling enabled" -ForegroundColor Green

# Set registry for multi-monitor optimization
$multiMonitorPath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\DCI"
if (-not (Test-Path $multiMonitorPath)) {
    New-Item -Path $multiMonitorPath -Force
}
Set-ItemProperty -Path $multiMonitorPath -Name "Timeout" -Value 0
Write-Host "   ✅ Multi-monitor display optimizations applied" -ForegroundColor Green

# Create development directories
Write-Host "📁 Creating development directory structure..." -ForegroundColor Green
$devRoot = "C:\dev"
$projectRoot = "$devRoot\trading-platform"

if (-not (Test-Path $devRoot)) {
    New-Item -ItemType Directory -Path $devRoot -Force
    Write-Host "   ✅ Created $devRoot" -ForegroundColor Green
}

if (-not (Test-Path $projectRoot)) {
    New-Item -ItemType Directory -Path $projectRoot -Force
    Write-Host "   ✅ Created $projectRoot" -ForegroundColor Green
}

# Set up environment variables
Write-Host "🌍 Setting up environment variables..." -ForegroundColor Green
[Environment]::SetEnvironmentVariable("DRAGON_DEV_ROOT", $devRoot, "Machine")
[Environment]::SetEnvironmentVariable("TRADING_PLATFORM_ROOT", $projectRoot, "Machine")
[Environment]::SetEnvironmentVariable("EnableWindowsTargeting", "true", "Machine")
Write-Host "   ✅ Environment variables configured" -ForegroundColor Green

# Create PowerShell profile for development
Write-Host "📝 Setting up PowerShell development profile..." -ForegroundColor Green
$profileContent = @"
# DRAGON Trading Platform Development Profile

Write-Host "🐉 DRAGON Development Environment Ready" -ForegroundColor Cyan

# Aliases for common tasks
Set-Alias -Name "build-trading" -Value "dotnet build --configuration Release --runtime win-x64"
Set-Alias -Name "test-trading" -Value "dotnet test --logger trx --results-directory TestResults"
Set-Alias -Name "clean-trading" -Value "dotnet clean"

# Functions
function Start-TradingPlatform {
    Set-Location `$env:TRADING_PLATFORM_ROOT
    Write-Host "📊 Switched to Trading Platform directory" -ForegroundColor Green
}

function Build-AllProjects {
    Write-Host "🔨 Building all trading platform projects..." -ForegroundColor Green
    dotnet build --configuration Release --runtime win-x64
}

function Test-AllProjects {
    Write-Host "🧪 Running all trading platform tests..." -ForegroundColor Green
    dotnet test --logger trx --results-directory TestResults
}

# Set default location
Start-TradingPlatform
"@

$profilePath = $PROFILE.AllUsersAllHosts
if (-not (Test-Path (Split-Path $profilePath))) {
    New-Item -ItemType Directory -Path (Split-Path $profilePath) -Force
}
Set-Content -Path $profilePath -Value $profileContent
Write-Host "   ✅ PowerShell profile configured" -ForegroundColor Green

# Install Windows SDK and build tools
Write-Host "🏗️ Installing Windows SDK and Build Tools..." -ForegroundColor Green
choco install windows-sdk-11-version-2004-all -y
choco install visualstudio2022buildtools -y
Write-Host "   ✅ Windows SDK and Build Tools installed" -ForegroundColor Green

# Final system check
Write-Host "" -ForegroundColor White
Write-Host "🔍 Final System Verification:" -ForegroundColor Green

# Check .NET installation
$dotnetVersion = dotnet --version 2>$null
if ($dotnetVersion) {
    Write-Host "   ✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "   ❌ .NET SDK not detected" -ForegroundColor Red
}

# Check Git installation
$gitVersion = git --version 2>$null
if ($gitVersion) {
    Write-Host "   ✅ Git: $gitVersion" -ForegroundColor Green
} else {
    Write-Host "   ❌ Git not detected" -ForegroundColor Red
}

# Check Visual Studio
$vsPath = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"
if (Test-Path $vsPath) {
    Write-Host "   ✅ Visual Studio 2022 Community installed" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Visual Studio 2022 not detected at expected location" -ForegroundColor Yellow
}

Write-Host "" -ForegroundColor White
Write-Host "🎉 DRAGON Development Environment Setup Complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "" -ForegroundColor White
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Restart your system to apply all settings" -ForegroundColor White
Write-Host "2. Clone the trading platform repository to: $projectRoot" -ForegroundColor White
Write-Host "3. Open project in Visual Studio 2022" -ForegroundColor White
Write-Host "4. Build and test the WinUI 3 trading application" -ForegroundColor White
Write-Host "5. Configure multiple monitors for trading setup" -ForegroundColor White
Write-Host "" -ForegroundColor White
Write-Host "💡 Pro Tips:" -ForegroundColor Yellow
Write-Host "• Use 'Start-TradingPlatform' to quickly navigate to project" -ForegroundColor White
Write-Host "• Use 'Build-AllProjects' for full solution build" -ForegroundColor White
Write-Host "• Monitor GPU temps during heavy development/testing" -ForegroundColor White
Write-Host "• Consider SSD optimization for faster builds" -ForegroundColor White
Write-Host "" -ForegroundColor White

if (-not $QuickSetup) {
    Write-Host "Press any key to continue..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
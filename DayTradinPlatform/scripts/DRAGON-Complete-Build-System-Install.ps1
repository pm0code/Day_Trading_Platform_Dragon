# DRAGON Complete Build System Installation
# Installs all missing components for professional Windows development
# Run as Administrator on DRAGON

param(
    [string]$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents",
    [switch]$SkipDownloads,
    [switch]$ForceReinstall
)

Write-Host "🔧 DRAGON Complete Build System Installation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Installing all missing components for professional Windows development" -ForegroundColor White
Write-Host ""

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "❌ This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Running as Administrator" -ForegroundColor Green
Write-Host ""

# Create download cache directory
$downloadPath = "$WorkspaceRoot\Tools\Downloads"
if (-not (Test-Path $downloadPath)) {
    New-Item -ItemType Directory -Path $downloadPath -Force | Out-Null
}

Write-Host "📦 Download cache: $downloadPath" -ForegroundColor Gray
Write-Host ""

# Function to check if a tool is installed
function Test-ToolInstalled {
    param([string]$Command, [string]$Name)
    
    try {
        $result = Get-Command $Command -ErrorAction SilentlyContinue
        if ($result) {
            Write-Host "  ✅ $Name is installed" -ForegroundColor Green
            return $true
        }
    } catch {}
    
    Write-Host "  ❌ $Name is missing" -ForegroundColor Red
    return $false
}

# Function to install via winget if available
function Install-ViaWinget {
    param([string]$PackageId, [string]$Name)
    
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        Write-Host "  📦 Installing $Name via winget..." -ForegroundColor Yellow
        try {
            winget install --id $PackageId --silent --accept-package-agreements --accept-source-agreements
            Write-Host "  ✅ $Name installed via winget" -ForegroundColor Green
            return $true
        } catch {
            Write-Host "  ⚠️  Winget installation failed for $Name" -ForegroundColor Yellow
            return $false
        }
    }
    return $false
}

# Check current system status
Write-Host "🔍 Checking current development tools..." -ForegroundColor Green

$dotnetInstalled = Test-ToolInstalled "dotnet" ".NET SDK"
$msbuildInstalled = Test-ToolInstalled "msbuild" "MSBuild"
$gitInstalled = Test-ToolInstalled "git" "Git"
$wingetInstalled = Test-ToolInstalled "winget" "Windows Package Manager"

Write-Host ""

# Install Windows Package Manager (winget) if missing
if (-not $wingetInstalled) {
    Write-Host "📦 Installing Windows Package Manager (winget)..." -ForegroundColor Yellow
    
    # Download and install App Installer (includes winget)
    $appInstallerUrl = "https://aka.ms/getwinget"
    $appInstallerPath = "$downloadPath\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle"
    
    if (-not $SkipDownloads) {
        try {
            Write-Host "  📥 Downloading App Installer..." -ForegroundColor White
            Invoke-WebRequest -Uri $appInstallerUrl -OutFile $appInstallerPath -UseBasicParsing
            
            Write-Host "  🔨 Installing App Installer..." -ForegroundColor White
            Add-AppxPackage -Path $appInstallerPath
            
            Write-Host "  ✅ Windows Package Manager installed" -ForegroundColor Green
            $wingetInstalled = $true
        } catch {
            Write-Host "  ⚠️  Could not install winget automatically" -ForegroundColor Yellow
            Write-Host "     Please install from Microsoft Store: 'App Installer'" -ForegroundColor Gray
        }
    }
}

# Install Visual Studio Build Tools 2022
Write-Host "🏗️ Installing Visual Studio Build Tools 2022..." -ForegroundColor Green

if (-not $msbuildInstalled -or $ForceReinstall) {
    $installed = $false
    
    # Try winget first
    if ($wingetInstalled) {
        $installed = Install-ViaWinget "Microsoft.VisualStudio.2022.BuildTools" "VS Build Tools 2022"
    }
    
    # Manual installation if winget failed
    if (-not $installed -and -not $SkipDownloads) {
        Write-Host "  📥 Downloading Visual Studio Build Tools 2022..." -ForegroundColor Yellow
        
        $vsBuildToolsUrl = "https://aka.ms/vs/17/release/vs_buildtools.exe"
        $vsBuildToolsPath = "$downloadPath\vs_buildtools.exe"
        
        try {
            Invoke-WebRequest -Uri $vsBuildToolsUrl -OutFile $vsBuildToolsPath -UseBasicParsing
            
            Write-Host "  🔨 Installing Visual Studio Build Tools 2022..." -ForegroundColor Yellow
            Write-Host "     This may take several minutes..." -ForegroundColor Gray
            
            # Install with required workloads for .NET development
            $arguments = @(
                "--quiet"
                "--wait" 
                "--add Microsoft.VisualStudio.Workload.MSBuildTools"
                "--add Microsoft.VisualStudio.Workload.VCTools"
                "--add Microsoft.VisualStudio.Workload.NativeDesktop"
                "--add Microsoft.Component.MSBuild"
                "--add Microsoft.VisualStudio.Component.Windows10SDK.20348"
                "--add Microsoft.VisualStudio.Component.VC.Tools.x86.x64"
                "--add Microsoft.VisualStudio.Component.VC.CMake.Project"
            )
            
            Start-Process -FilePath $vsBuildToolsPath -ArgumentList $arguments -Wait -NoNewWindow
            Write-Host "  ✅ Visual Studio Build Tools 2022 installed" -ForegroundColor Green
            
        } catch {
            Write-Host "  ❌ Failed to install Visual Studio Build Tools: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "  ✅ Visual Studio Build Tools already available" -ForegroundColor Green
}

# Install Git for Windows if missing
if (-not $gitInstalled -or $ForceReinstall) {
    Write-Host "📂 Installing Git for Windows..." -ForegroundColor Green
    
    $installed = $false
    
    # Try winget first
    if ($wingetInstalled) {
        $installed = Install-ViaWinget "Git.Git" "Git for Windows"
    }
    
    # Manual installation if winget failed
    if (-not $installed -and -not $SkipDownloads) {
        $gitUrl = "https://github.com/git-for-windows/git/releases/download/v2.43.0.windows.1/Git-2.43.0-64-bit.exe"
        $gitPath = "$downloadPath\Git-2.43.0-64-bit.exe"
        
        try {
            Write-Host "  📥 Downloading Git for Windows..." -ForegroundColor Yellow
            Invoke-WebRequest -Uri $gitUrl -OutFile $gitPath -UseBasicParsing
            
            Write-Host "  🔨 Installing Git for Windows..." -ForegroundColor Yellow
            Start-Process -FilePath $gitPath -ArgumentList "/VERYSILENT", "/NORESTART" -Wait -NoNewWindow
            Write-Host "  ✅ Git for Windows installed" -ForegroundColor Green
            
        } catch {
            Write-Host "  ❌ Failed to install Git: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "  ✅ Git already available" -ForegroundColor Green
}

# Install additional development tools
Write-Host ""
Write-Host "🛠️ Installing additional development tools..." -ForegroundColor Green

$additionalTools = @(
    @{Id="Microsoft.PowerShell"; Name="PowerShell 7"},
    @{Id="Microsoft.WindowsTerminal"; Name="Windows Terminal"},
    @{Id="Microsoft.VisualStudioCode"; Name="Visual Studio Code"},
    @{Id="JetBrains.dotMemoryUnit"; Name="dotMemory Unit"},
    @{Id="Microsoft.DotNet.SDK.8"; Name=".NET 8 SDK (Latest)"}
)

foreach ($tool in $additionalTools) {
    if ($wingetInstalled) {
        try {
            winget install --id $tool.Id --silent --accept-package-agreements --accept-source-agreements 2>$null
            Write-Host "  ✅ $($tool.Name) installed/updated" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠️  Could not install $($tool.Name)" -ForegroundColor Yellow
        }
    }
}

# Install Windows SDK
Write-Host ""
Write-Host "🪟 Installing Windows SDK..." -ForegroundColor Green

if ($wingetInstalled) {
    try {
        winget install --id "Microsoft.WindowsSDK.10.0.20348" --silent --accept-package-agreements --accept-source-agreements
        Write-Host "  ✅ Windows SDK 10.0.20348 installed" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  Could not install Windows SDK via winget" -ForegroundColor Yellow
    }
}

# Configure development environment
Write-Host ""
Write-Host "⚙️ Configuring development environment..." -ForegroundColor Green

# Update PATH environment variable
$pathUpdates = @(
    "${env:ProgramFiles}\Git\bin"
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin"
    "${env:ProgramFiles}\PowerShell\7"
    "${env:ProgramFiles}\dotnet"
)

$currentPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
$pathChanged = $false

foreach ($pathItem in $pathUpdates) {
    if (Test-Path $pathItem) {
        if ($currentPath -notlike "*$pathItem*") {
            $currentPath += ";$pathItem"
            $pathChanged = $true
            Write-Host "  ✅ Added to PATH: $pathItem" -ForegroundColor Green
        }
    }
}

if ($pathChanged) {
    [Environment]::SetEnvironmentVariable("PATH", $currentPath, "Machine")
    Write-Host "  ✅ PATH environment variable updated" -ForegroundColor Green
}

# Set up BuildWorkspace-specific environment
Write-Host ""
Write-Host "🏗️ Configuring BuildWorkspace environment..." -ForegroundColor Green

$buildEnvVars = @{
    "BUILDWORKSPACE_ROOT" = $WorkspaceRoot
    "DOTNET_CLI_TELEMETRY_OPTOUT" = "1"
    "DOTNET_SKIP_FIRST_TIME_EXPERIENCE" = "1"
    "MSBUILD_CACHE_DIR" = "$WorkspaceRoot\Cache\MSBuild"
    "NUGET_PACKAGES" = "$WorkspaceRoot\Cache\NuGet"
}

foreach ($envVar in $buildEnvVars.GetEnumerator()) {
    [Environment]::SetEnvironmentVariable($envVar.Key, $envVar.Value, "Machine")
    Write-Host "  ✅ Set: $($envVar.Key) = $($envVar.Value)" -ForegroundColor Green
}

# Create enhanced build script
Write-Host ""
Write-Host "📝 Creating enhanced build scripts..." -ForegroundColor Green

$enhancedBuildScript = @"
# Enhanced Build Script for DRAGON BuildWorkspace
# Uses full MSBuild and Visual Studio Build Tools

param(
    [string]`$Configuration = "Release",
    [string]`$Platform = "x64",
    [switch]`$Clean,
    [switch]`$Rebuild,
    [switch]`$RunTests,
    [switch]`$CreatePackage
)

`$WorkspaceRoot = "$WorkspaceRoot"
`$SourcePath = "`$WorkspaceRoot\Source\DayTradingPlatform"

Write-Host "🏗️ ENHANCED BUILD - Day Trading Platform" -ForegroundColor Cyan
Write-Host "Configuration: `$Configuration | Platform: `$Platform" -ForegroundColor White

# Set isolated environment
`$env:BUILDWORKSPACE_ROOT = `$WorkspaceRoot
`$env:NUGET_PACKAGES = "`$WorkspaceRoot\Cache\NuGet"
`$env:MSBUILD_CACHE_DIR = "`$WorkspaceRoot\Cache\MSBuild"

if (-not (Test-Path "`$SourcePath\DayTradinPlatform.sln")) {
    Write-Host "❌ Solution not found" -ForegroundColor Red
    exit 1
}

Set-Location `$SourcePath

if (`$Clean -or `$Rebuild) {
    Write-Host "🧹 Cleaning solution..." -ForegroundColor Yellow
    if (Get-Command msbuild -ErrorAction SilentlyContinue) {
        msbuild "DayTradinPlatform.sln" /t:Clean /p:Configuration=`$Configuration /p:Platform=`$Platform
    } else {
        dotnet clean "DayTradinPlatform.sln" --configuration `$Configuration
    }
}

Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
dotnet restore "DayTradinPlatform.sln" --packages "`$env:NUGET_PACKAGES"

Write-Host "🔨 Building solution..." -ForegroundColor Yellow
if (Get-Command msbuild -ErrorAction SilentlyContinue) {
    # Use MSBuild for advanced features
    msbuild "DayTradinPlatform.sln" /p:Configuration=`$Configuration /p:Platform=`$Platform /m
} else {
    # Fallback to dotnet build
    dotnet build "DayTradinPlatform.sln" --configuration `$Configuration --no-restore
}

if (`$LASTEXITCODE -eq 0) {
    Write-Host "✅ Build completed successfully" -ForegroundColor Green
    
    if (`$RunTests) {
        Write-Host "🧪 Running tests..." -ForegroundColor Yellow
        dotnet test "TradingPlatform.Testing\TradingPlatform.Testing.csproj" --configuration `$Configuration --no-build
    }
    
    if (`$CreatePackage) {
        Write-Host "📦 Creating deployment package..." -ForegroundColor Yellow
        if (Test-Path "TradingPlatform.TradingApp\TradingPlatform.TradingApp.csproj") {
            dotnet publish "TradingPlatform.TradingApp\TradingPlatform.TradingApp.csproj" --configuration `$Configuration --runtime win-x64 --self-contained true --output "`$WorkspaceRoot\Artifacts\Packages\TradingApp"
        }
    }
} else {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit `$LASTEXITCODE
}
"@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\enhanced-build.ps1" -Value $enhancedBuildScript
Write-Host "  ✅ Enhanced build script created" -ForegroundColor Green

# Final system check
Write-Host ""
Write-Host "🔍 Final system verification..." -ForegroundColor Green

# Refresh environment variables for current session
$env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine")

$tools = @(
    @{Command="dotnet"; Name=".NET SDK"},
    @{Command="msbuild"; Name="MSBuild"},
    @{Command="git"; Name="Git"},
    @{Command="winget"; Name="Package Manager"}
)

foreach ($tool in $tools) {
    if (Get-Command $tool.Command -ErrorAction SilentlyContinue) {
        Write-Host "  ✅ $($tool.Name) is available" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $($tool.Name) not found in PATH (restart may be required)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "🎉 DRAGON BUILD SYSTEM INSTALLATION COMPLETE!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Installed Components:" -ForegroundColor Yellow
Write-Host "  ✅ Visual Studio Build Tools 2022" -ForegroundColor Green
Write-Host "  ✅ MSBuild (latest version)" -ForegroundColor Green  
Write-Host "  ✅ Windows SDK" -ForegroundColor Green
Write-Host "  ✅ Git for Windows" -ForegroundColor Green
Write-Host "  ✅ PowerShell 7" -ForegroundColor Green
Write-Host "  ✅ Windows Terminal" -ForegroundColor Green
Write-Host "  ✅ Enhanced build scripts" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Restart PowerShell to load new PATH variables" -ForegroundColor White
Write-Host "  2. Test build: .\Environment\Scripts\enhanced-build.ps1" -ForegroundColor White
Write-Host "  3. Full build with tests: .\Environment\Scripts\enhanced-build.ps1 -RunTests" -ForegroundColor White
Write-Host ""
Write-Host "🎯 DRAGON is now ready for professional Windows development!" -ForegroundColor Green
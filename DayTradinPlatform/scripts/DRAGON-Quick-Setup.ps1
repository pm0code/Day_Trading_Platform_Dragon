# DRAGON BuildWorkspace Quick Setup
# Copy and run this entire script on DRAGON as Administrator

Write-Host "🛡️ CREATING ISOLATED DRAGON BUILDWORKSPACE" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan

$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
Write-Host "Target: $WorkspaceRoot" -ForegroundColor White
Write-Host ""

# Create directory structure
$directories = @(
    "$WorkspaceRoot", "$WorkspaceRoot\Source", "$WorkspaceRoot\Source\DayTradingPlatform",
    "$WorkspaceRoot\Tools", "$WorkspaceRoot\Artifacts", "$WorkspaceRoot\Environment",
    "$WorkspaceRoot\Environment\Scripts", "$WorkspaceRoot\Cache", "$WorkspaceRoot\Cache\NuGet",
    "$WorkspaceRoot\Cache\MSBuild", "$WorkspaceRoot\Documentation"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Write-Host "✅ Created: $dir" -ForegroundColor Green
}

# Create build script
$buildScript = @'
param([string]$Configuration = "Release")
$WorkspaceRoot = "D:\BuildWorkspace\WindowsComponents"
$SourcePath = "$WorkspaceRoot\Source\DayTradingPlatform"

Write-Host "🏗️ ISOLATED BUILD - Day Trading Platform" -ForegroundColor Cyan
$env:NUGET_PACKAGES = "$WorkspaceRoot\Cache\NuGet"
$env:MSBUILD_CACHE_DIR = "$WorkspaceRoot\Cache\MSBuild"

if (-not (Test-Path "$SourcePath\DayTradinPlatform.sln")) {
    Write-Host "❌ Source code not found. Sync from Linux first." -ForegroundColor Red
    exit 1
}

Set-Location $SourcePath
dotnet restore "DayTradinPlatform.sln" --packages "$env:NUGET_PACKAGES"
dotnet build "DayTradinPlatform.sln" --configuration $Configuration --runtime win-x64 --no-restore
Write-Host "✅ Build completed!" -ForegroundColor Green
'@

Set-Content -Path "$WorkspaceRoot\Environment\Scripts\build-platform.ps1" -Value $buildScript

Write-Host ""
Write-Host "🎉 DRAGON BUILDWORKSPACE CREATED!" -ForegroundColor Green
Write-Host "📂 Location: $WorkspaceRoot" -ForegroundColor Yellow
Write-Host "🚀 Ready for source sync and builds!" -ForegroundColor Green
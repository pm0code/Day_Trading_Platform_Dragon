#!/usr/bin/env pwsh

param(
    [string]$FinnhubKey = $env:FINNHUB_API_KEY,
    [string]$AlphaVantageKey = $env:ALPHAVANTAGE_API_KEY
)

# Colors for output
$colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
}

function Write-ColoredOutput {
    param([string]$Message, [string]$Type = "Info")
    Write-Host $Message -ForegroundColor $colors[$Type]
}

Write-ColoredOutput "=====================================" "Info"
Write-ColoredOutput " Day Trading Platform Test Harness" "Info"
Write-ColoredOutput "=====================================" "Info"
Write-Host

# Check if we're in the right directory
$solutionPath = Join-Path $PSScriptRoot ".." "DayTradinPlatform"
if (-not (Test-Path $solutionPath)) {
    Write-ColoredOutput "Error: Solution directory not found at $solutionPath" "Error"
    exit 1
}

Set-Location $solutionPath

# Check for API keys
$hasKeys = $true
if ([string]::IsNullOrWhiteSpace($FinnhubKey)) {
    Write-ColoredOutput "Warning: No Finnhub API key found" "Warning"
    Write-Host "  Set via: `$env:FINNHUB_API_KEY='your_key'"
    $hasKeys = $false
}

if ([string]::IsNullOrWhiteSpace($AlphaVantageKey)) {
    Write-ColoredOutput "Warning: No AlphaVantage API key found" "Warning"
    Write-Host "  Set via: `$env:ALPHAVANTAGE_API_KEY='your_key'"
    $hasKeys = $false
}

if (-not $hasKeys) {
    Write-Host
    Write-ColoredOutput "You can get free API keys from:" "Info"
    Write-Host "  Finnhub: https://finnhub.io/"
    Write-Host "  AlphaVantage: https://www.alphavantage.co/support/#api-key"
    Write-Host
    Write-Host "Press any key to continue with placeholder keys..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Build the solution
Write-Host
Write-ColoredOutput "Building solution..." "Info"
$buildResult = dotnet build --configuration Debug 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-ColoredOutput "Build failed!" "Error"
    Write-Host $buildResult
    exit 1
}
Write-ColoredOutput "Build succeeded!" "Success"

# Run the test harness
Write-Host
Write-ColoredOutput "Running test harness..." "Info"
Write-Host

# Set environment variables if provided
if ($FinnhubKey) {
    $env:FINNHUB_API_KEY = $FinnhubKey
}
if ($AlphaVantageKey) {
    $env:ALPHAVANTAGE_API_KEY = $AlphaVantageKey
}

# Run the test runner
dotnet run --project TradingPlatform.TestRunner --no-build

Write-Host
Write-ColoredOutput "Test harness complete!" "Success"
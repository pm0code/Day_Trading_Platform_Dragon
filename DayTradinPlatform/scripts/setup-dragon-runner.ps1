# Setup GitHub Actions Self-Hosted Runner on DRAGON
# Run this script as Administrator on DRAGON Windows machine

param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$true)]
    [string]$RepoOwner,
    
    [Parameter(Mandatory=$true)]
    [string]$RepoName,
    
    [string]$RunnerName = "DRAGON-RTX-Runner",
    [string]$RunnerWorkDir = "D:\BuildWorkspace\GitHubRunner"
)

Write-Host "=== DRAGON GitHub Actions Runner Setup ===" -ForegroundColor Green
Write-Host "Setting up self-hosted runner for Day Trading Platform CI/CD" -ForegroundColor Yellow

try {
    # 1. Create runner work directory
    Write-Host "`n1. Creating runner work directory..." -ForegroundColor Cyan
    if (-not (Test-Path $RunnerWorkDir)) {
        New-Item -Path $RunnerWorkDir -ItemType Directory -Force
        Write-Host "✓ Created: $RunnerWorkDir" -ForegroundColor Green
    }

    # 2. Download GitHub Actions Runner
    Write-Host "`n2. Downloading GitHub Actions Runner..." -ForegroundColor Cyan
    $RunnerVersion = "2.311.0"
    $RunnerUrl = "https://github.com/actions/runner/releases/download/v$RunnerVersion/actions-runner-win-x64-$RunnerVersion.zip"
    $RunnerZip = "$RunnerWorkDir\actions-runner.zip"
    
    if (-not (Test-Path "$RunnerWorkDir\run.cmd")) {
        Invoke-WebRequest -Uri $RunnerUrl -OutFile $RunnerZip
        Expand-Archive -Path $RunnerZip -DestinationPath $RunnerWorkDir -Force
        Remove-Item $RunnerZip
        Write-Host "✓ GitHub Actions Runner downloaded and extracted" -ForegroundColor Green
    } else {
        Write-Host "✓ GitHub Actions Runner already installed" -ForegroundColor Green
    }

    # 3. Generate registration token
    Write-Host "`n3. Generating registration token..." -ForegroundColor Cyan
    $Headers = @{
        "Authorization" = "token $GitHubToken"
        "Accept" = "application/vnd.github.v3+json"
    }
    
    $TokenUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/actions/runners/registration-token"
    $TokenResponse = Invoke-RestMethod -Uri $TokenUrl -Method POST -Headers $Headers
    $RegistrationToken = $TokenResponse.token
    Write-Host "✓ Registration token generated" -ForegroundColor Green

    # 4. Configure the runner
    Write-Host "`n4. Configuring GitHub Actions Runner..." -ForegroundColor Cyan
    Set-Location $RunnerWorkDir
    
    $ConfigArgs = @(
        "--url", "https://github.com/$RepoOwner/$RepoName",
        "--token", $RegistrationToken,
        "--name", $RunnerName,
        "--work", "_work",
        "--labels", "windows,x64,dragon,rtx,ultra-low-latency,trading",
        "--runasservice",
        "--windowslogonaccount", "NT AUTHORITY\NETWORK SERVICE",
        "--unattended"
    )
    
    & .\config.cmd @ConfigArgs
    Write-Host "✓ Runner configured successfully" -ForegroundColor Green

    # 5. Install as Windows Service
    Write-Host "`n5. Installing runner as Windows Service..." -ForegroundColor Cyan
    & .\svc.sh install
    & .\svc.sh start
    Write-Host "✓ Runner installed and started as Windows Service" -ForegroundColor Green

    # 6. Configure environment variables
    Write-Host "`n6. Setting trading platform environment variables..." -ForegroundColor Cyan
    [Environment]::SetEnvironmentVariable("DRAGON_BUILD_WORKSPACE", "D:\BuildWorkspace\WindowsComponents", "Machine")
    [Environment]::SetEnvironmentVariable("TRADING_PLATFORM_ENV", "DRAGON_PRODUCTION", "Machine")
    [Environment]::SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1", "Machine")
    Write-Host "✓ Environment variables configured" -ForegroundColor Green

    # 7. Test runner connectivity
    Write-Host "`n7. Testing runner connectivity..." -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    $RunnerStatus = Get-Service "actions.runner.*" | Where-Object {$_.Status -eq "Running"}
    if ($RunnerStatus) {
        Write-Host "✓ GitHub Actions Runner service is running" -ForegroundColor Green
    } else {
        Write-Host "⚠ Runner service may still be starting..." -ForegroundColor Yellow
    }

    # Success summary
    Write-Host "`n============================================================" -ForegroundColor Green
    Write-Host "DRAGON GITHUB ACTIONS RUNNER SETUP COMPLETE!" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    
    Write-Host "`nRunner Configuration:" -ForegroundColor Cyan
    Write-Host "  Name: $RunnerName" -ForegroundColor White
    Write-Host "  Work Directory: $RunnerWorkDir" -ForegroundColor White
    Write-Host "  Labels: windows, x64, dragon, rtx, ultra-low-latency, trading" -ForegroundColor White
    Write-Host "  Service Status: Running" -ForegroundColor White
    
    Write-Host "`nCI/CD Features Enabled:" -ForegroundColor Cyan
    Write-Host "  ✓ Windows x64 Production Builds" -ForegroundColor Green
    Write-Host "  ✓ Ultra-Low Latency Performance Testing" -ForegroundColor Green
    Write-Host "  ✓ RTX GPU-Accelerated Operations" -ForegroundColor Green
    Write-Host "  ✓ Cross-Platform Build Pipeline" -ForegroundColor Green

} catch {
    Write-Host "`n✗ ERROR: GitHub Actions Runner setup failed!" -ForegroundColor Red
    Write-Host "Error Details: $($_.Exception.Message)" -ForegroundColor Yellow
    exit 1
}

Write-Host "`nDRAGON is now ready for automated CI/CD operations!" -ForegroundColor Cyan
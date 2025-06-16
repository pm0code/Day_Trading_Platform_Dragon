# Setup DRAGON Windows 11 Self-Hosted Runner
# This script configures the DRAGON machine as a GitHub Actions self-hosted runner
# for ultra-low latency trading platform testing

param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubRepo,
    
    [Parameter(Mandatory=$false)]
    [string]$RunnerName = "dragon-runner",
    
    [Parameter(Mandatory=$false)]
    [string]$RunnerLabels = "windows-runner,dragon,windows-11,x64,trading"
)

Write-Host "🐉 Setting up DRAGON as GitHub Actions Self-Hosted Runner" -ForegroundColor Green
Write-Host "Target: Windows 11 X64 Ultra-Low Latency Trading Platform" -ForegroundColor Yellow

# Ensure running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

# Step 1: Install OpenSSH Server for Ubuntu connectivity
Write-Host "📡 Installing OpenSSH Server for Ubuntu development machine connectivity..." -ForegroundColor Cyan

# Install OpenSSH Server
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

# Start and configure SSH service
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'

# Configure firewall for SSH
New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22

Write-Host "✅ OpenSSH Server installed and configured" -ForegroundColor Green

# Step 2: Configure SSH Key Authentication
Write-Host "🔐 Configuring SSH key authentication..." -ForegroundColor Cyan

# Create .ssh directory
$sshDir = "$env:USERPROFILE\.ssh"
New-Item -ItemType Directory -Path $sshDir -Force

Write-Host "⚠️  Manual Step Required:" -ForegroundColor Yellow
Write-Host "1. Copy the public key from Ubuntu development machine" -ForegroundColor White
Write-Host "2. Add it to: $sshDir\authorized_keys" -ForegroundColor White
Write-Host "3. Run: icacls `"$sshDir\authorized_keys`" /inheritance:r /grant:r `"$($env:USERNAME):F`"" -ForegroundColor White

# Step 3: Install .NET 8.0 SDK
Write-Host "🔧 Installing .NET 8.0 SDK..." -ForegroundColor Cyan

$dotnetVersion = "8.0"
$dotnetInstaller = "dotnet-sdk-8.0-win-x64.exe"
$dotnetUrl = "https://download.microsoft.com/download/7/8/b/78b16d3e-f7a1-4d7e-b7e8-e5e5e4b7e6c8/dotnet-sdk-8.0.302-win-x64.exe"

# Check if .NET 8.0 is already installed
$installedVersions = dotnet --list-sdks 2>$null
if ($installedVersions -match "8\.0") {
    Write-Host "✅ .NET 8.0 SDK already installed" -ForegroundColor Green
} else {
    Write-Host "Downloading .NET 8.0 SDK..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller
    
    Write-Host "Installing .NET 8.0 SDK..." -ForegroundColor Yellow
    Start-Process -FilePath $dotnetInstaller -ArgumentList "/quiet" -Wait
    Remove-Item $dotnetInstaller
    
    # Refresh PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    
    Write-Host "✅ .NET 8.0 SDK installed" -ForegroundColor Green
}

# Step 4: Install GitHub Actions Runner
Write-Host "🏃 Installing GitHub Actions Self-Hosted Runner..." -ForegroundColor Cyan

$runnerDir = "C:\actions-runner"
New-Item -ItemType Directory -Path $runnerDir -Force
Set-Location $runnerDir

# Download latest runner
$runnerVersion = "2.311.0"  # Update to latest version as needed
$runnerZip = "actions-runner-win-x64-$runnerVersion.zip"
$runnerUrl = "https://github.com/actions/runner/releases/download/v$runnerVersion/$runnerZip"

Write-Host "Downloading GitHub Actions Runner v$runnerVersion..." -ForegroundColor Yellow
Invoke-WebRequest -Uri $runnerUrl -OutFile $runnerZip

Write-Host "Extracting runner..." -ForegroundColor Yellow
Expand-Archive -Path $runnerZip -DestinationPath . -Force
Remove-Item $runnerZip

# Configure the runner
Write-Host "Configuring runner for repository: $GitHubRepo" -ForegroundColor Yellow
$configArgs = @(
    "--url", "https://github.com/$GitHubRepo",
    "--token", $GitHubToken,
    "--name", $RunnerName,
    "--labels", $RunnerLabels,
    "--work", "_work",
    "--unattended"
)

& .\config.cmd @configArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Runner configured successfully" -ForegroundColor Green
} else {
    Write-Error "Failed to configure runner"
    exit 1
}

# Install and start as Windows service
Write-Host "Installing runner as Windows service..." -ForegroundColor Yellow
& .\svc.cmd install
& .\svc.cmd start

Write-Host "✅ GitHub Actions Runner service started" -ForegroundColor Green

# Step 5: Install additional tools for ultra-low latency testing
Write-Host "🛠️  Installing additional tools for ultra-low latency testing..." -ForegroundColor Cyan

# Install Chocolatey if not present
if (!(Get-Command choco -ErrorAction SilentlyContinue)) {
    Write-Host "Installing Chocolatey..." -ForegroundColor Yellow
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
}

# Install performance monitoring tools
$tools = @(
    "perfview",
    "sysinternals",
    "jetbrains-dotpeek",
    "git"
)

foreach ($tool in $tools) {
    Write-Host "Installing $tool..." -ForegroundColor Yellow
    choco install $tool -y --no-progress
}

# Step 6: Configure Windows for ultra-low latency
Write-Host "⚡ Configuring Windows 11 for ultra-low latency trading..." -ForegroundColor Cyan

# Disable Windows Update automatic restart
Write-Host "Configuring Windows Update..." -ForegroundColor Yellow
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU" /v NoAutoRebootWithLoggedOnUsers /t REG_DWORD /d 1 /f

# Configure power settings for high performance
Write-Host "Setting high performance power profile..." -ForegroundColor Yellow
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  # High Performance

# Disable unnecessary services for trading
Write-Host "Optimizing services for trading performance..." -ForegroundColor Yellow
$servicesToDisable = @(
    "Themes",
    "TabletInputService",
    "WSearch"  # Windows Search
)

foreach ($service in $servicesToDisable) {
    try {
        Set-Service -Name $service -StartupType Disabled -ErrorAction SilentlyContinue
        Write-Host "Disabled service: $service" -ForegroundColor Green
    } catch {
        Write-Host "Could not disable service: $service" -ForegroundColor Yellow
    }
}

# Step 7: Configure network optimizations for Mellanox 10GbE
Write-Host "🌐 Configuring network optimizations for Mellanox 10GbE..." -ForegroundColor Cyan

# Check for 10GbE adapters
$tenGigAdapters = Get-NetAdapter | Where-Object { $_.LinkSpeed -eq "10 Gbps" }
if ($tenGigAdapters) {
    Write-Host "Found 10GbE adapter(s):" -ForegroundColor Green
    $tenGigAdapters | ForEach-Object { Write-Host "  - $($_.Name): $($_.InterfaceDescription)" -ForegroundColor White }
    
    # Apply optimizations to 10GbE adapters
    foreach ($adapter in $tenGigAdapters) {
        Write-Host "Optimizing adapter: $($adapter.Name)" -ForegroundColor Yellow
        
        # Enable jumbo frames (9000 MTU)
        Set-NetAdapterAdvancedProperty -Name $adapter.Name -RegistryKeyword "*JumboPacket" -RegistryValue 9000 -ErrorAction SilentlyContinue
        
        # Optimize receive buffers
        Set-NetAdapterAdvancedProperty -Name $adapter.Name -RegistryKeyword "*ReceiveBuffers" -RegistryValue 2048 -ErrorAction SilentlyContinue
        
        # Optimize transmit buffers  
        Set-NetAdapterAdvancedProperty -Name $adapter.Name -RegistryKeyword "*TransmitBuffers" -RegistryValue 2048 -ErrorAction SilentlyContinue
        
        Write-Host "✅ Network optimizations applied to $($adapter.Name)" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  No 10GbE adapters detected" -ForegroundColor Yellow
}

# Step 8: Create monitoring and telemetry directories
Write-Host "📊 Setting up monitoring and telemetry..." -ForegroundColor Cyan

$directories = @(
    "C:\TradingLogs",
    "C:\PerformanceData", 
    "C:\TelemetryData",
    "C:\CrashDumps"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Path $dir -Force
    Write-Host "Created directory: $dir" -ForegroundColor Green
}

# Step 9: Install performance monitoring script
Write-Host "📈 Installing performance monitoring script..." -ForegroundColor Cyan

$monitorScript = @"
# DRAGON Performance Monitor for Ultra-Low Latency Trading
# Monitors system performance and sends telemetry to Ubuntu development machine

param(
    [string]`$UbuntuHost = "ubuntu-dev",
    [string]`$SshUser = "developer",
    [int]`$IntervalSeconds = 60
)

function Send-TelemetryToUbuntu {
    param([string]`$FilePath, [string]`$RemotePath = "/tmp/dragon-telemetry")
    
    try {
        scp -i ~/.ssh/dragon_key "`$FilePath" "`${SshUser}@`${UbuntuHost}:`${RemotePath}/"
        Write-Host "Uploaded `$FilePath to Ubuntu" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to upload `$FilePath: `$(`$_.Exception.Message)"
    }
}

function Collect-PerformanceData {
    `$perfData = @{
        Timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
        CPU = @{
            Usage = [math]::Round((Get-Counter "\Processor(_Total)\% Processor Time").CounterSamples[0].CookedValue, 2)
            Cores = (Get-CimInstance Win32_ComputerSystem).NumberOfProcessors
        }
        Memory = @{
            TotalGB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
            AvailableGB = [math]::Round((Get-Counter "\Memory\Available MBytes").CounterSamples[0].CookedValue / 1024, 2)
            UsagePercent = [math]::Round(((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory - (Get-Counter "\Memory\Available Bytes").CounterSamples[0].CookedValue) / (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory * 100, 2)
        }
        Network = @{}
        Disk = @{
            FreeSpaceGB = [math]::Round((Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB, 2)
        }
    }
    
    # Check network adapters
    Get-NetAdapter | Where-Object { `$_.Status -eq "Up" } | ForEach-Object {
        `$perfData.Network[`$_.Name] = @{
            LinkSpeed = `$_.LinkSpeed
            BytesReceived = (Get-Counter "\Network Interface(`$(`$_.InterfaceDescription))\Bytes Received/sec" -ErrorAction SilentlyContinue).CounterSamples[0].CookedValue
            BytesSent = (Get-Counter "\Network Interface(`$(`$_.InterfaceDescription))\Bytes Sent/sec" -ErrorAction SilentlyContinue).CounterSamples[0].CookedValue
        }
    }
    
    return `$perfData
}

Write-Host "🐉 DRAGON Performance Monitor Started" -ForegroundColor Green
Write-Host "Monitoring interval: `$IntervalSeconds seconds" -ForegroundColor Yellow
Write-Host "Ubuntu target: `$UbuntuHost" -ForegroundColor Yellow

while (`$true) {
    try {
        `$perfData = Collect-PerformanceData
        `$filename = "dragon-perf-`$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
        `$filepath = "C:\PerformanceData\`$filename"
        
        `$perfData | ConvertTo-Json -Depth 3 | Out-File -FilePath `$filepath
        
        Write-Host "`$(Get-Date): Performance data collected - CPU: `$(`$perfData.CPU.Usage)%, Memory: `$(`$perfData.Memory.UsagePercent)%" -ForegroundColor Cyan
        
        Send-TelemetryToUbuntu -FilePath `$filepath
        
        Start-Sleep -Seconds `$IntervalSeconds
    } catch {
        Write-Error "Monitoring error: `$(`$_.Exception.Message)"
        Start-Sleep -Seconds 30
    }
}
"@

$monitorScript | Out-File -FilePath "C:\TradingLogs\dragon-monitor.ps1" -Encoding UTF8

Write-Host "✅ Performance monitoring script installed" -ForegroundColor Green

# Step 10: Final validation
Write-Host "🔍 Performing final validation..." -ForegroundColor Cyan

# Check .NET installation
$dotnetInfo = dotnet --info
Write-Host "✅ .NET SDK installed:" -ForegroundColor Green
Write-Host ($dotnetInfo | Select-String "Version:" | Select-Object -First 1) -ForegroundColor White

# Check GitHub Actions Runner service
$runnerService = Get-Service -Name "actions.runner.*" -ErrorAction SilentlyContinue
if ($runnerService -and $runnerService.Status -eq "Running") {
    Write-Host "✅ GitHub Actions Runner service is running" -ForegroundColor Green
} else {
    Write-Host "⚠️  GitHub Actions Runner service status unclear" -ForegroundColor Yellow
}

# Check SSH service
$sshService = Get-Service -Name "sshd" -ErrorAction SilentlyContinue
if ($sshService -and $sshService.Status -eq "Running") {
    Write-Host "✅ SSH service is running" -ForegroundColor Green
} else {
    Write-Host "❌ SSH service is not running" -ForegroundColor Red
}

Write-Host ""
Write-Host "🎉 DRAGON Windows 11 Self-Hosted Runner Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Configure SSH key authentication from Ubuntu machine" -ForegroundColor White
Write-Host "2. Test SSH connectivity: ssh $env:USERNAME@$env:COMPUTERNAME" -ForegroundColor White
Write-Host "3. Verify runner appears in GitHub repository settings" -ForegroundColor White
Write-Host "4. Run a test CI/CD pipeline to validate setup" -ForegroundColor White
Write-Host ""
Write-Host "Performance Monitoring:" -ForegroundColor Yellow
Write-Host "Start monitoring: powershell -File C:\TradingLogs\dragon-monitor.ps1" -ForegroundColor White
Write-Host ""
Write-Host "🐉 DRAGON is ready for ultra-low latency trading platform testing!" -ForegroundColor Green
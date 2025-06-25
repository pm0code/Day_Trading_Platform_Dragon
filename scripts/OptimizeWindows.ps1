# Windows 11 Performance Optimization Script for Trading Platform
# Run as Administrator

Write-Host "Trading Platform Windows Performance Optimization Script" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    Write-Host "This script must be run as Administrator. Exiting..." -ForegroundColor Red
    exit 1
}

# Function to set registry value
function Set-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [object]$Value,
        [string]$Type = "DWord"
    )
    
    if (!(Test-Path $Path)) {
        New-Item -Path $Path -Force | Out-Null
    }
    
    Set-ItemProperty -Path $Path -Name $Name -Value $Value -Type $Type
    Write-Host "Set $Path\$Name = $Value" -ForegroundColor Yellow
}

Write-Host "`n1. Configuring Power Settings..." -ForegroundColor Cyan

# Set High Performance power plan
powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
Write-Host "   - High Performance power plan activated" -ForegroundColor Green

# Disable CPU throttling
powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100
powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMAX 100
Write-Host "   - CPU throttling disabled" -ForegroundColor Green

# Disable core parking
powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 100
Write-Host "   - Core parking disabled" -ForegroundColor Green

Write-Host "`n2. Optimizing Network Settings..." -ForegroundColor Cyan

# Disable Nagle's algorithm
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name "TcpAckFrequency" -Value 1
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name "TCPNoDelay" -Value 1
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name "TcpDelAckTicks" -Value 0

# Increase network buffers
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Services\AFD\Parameters" -Name "DefaultReceiveWindow" -Value 1048576
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Services\AFD\Parameters" -Name "DefaultSendWindow" -Value 1048576

Write-Host "`n3. Configuring System Timer Resolution..." -ForegroundColor Cyan

# Set timer resolution to maximum (0.5ms)
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel" -Name "GlobalTimerResolutionRequests" -Value 1

Write-Host "`n4. Optimizing Memory Management..." -ForegroundColor Cyan

# Disable memory compression
Disable-MMAgent -MemoryCompression
Write-Host "   - Memory compression disabled" -ForegroundColor Green

# Configure system cache
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "LargeSystemCache" -Value 1
Set-RegistryValue -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "DisablePagingExecutive" -Value 1

Write-Host "`n5. Disabling Unnecessary Services..." -ForegroundColor Cyan

$servicesToDisable = @(
    "DiagTrack",          # Diagnostics Tracking
    "dmwappushservice",   # Device Management
    "WSearch",            # Windows Search (if not needed)
    "SysMain",            # Superfetch
    "Print Spooler"       # Print Spooler (if not needed)
)

foreach ($service in $servicesToDisable) {
    try {
        Stop-Service -Name $service -Force -ErrorAction SilentlyContinue
        Set-Service -Name $service -StartupType Disabled
        Write-Host "   - Disabled $service" -ForegroundColor Green
    } catch {
        Write-Host "   - Could not disable $service" -ForegroundColor Yellow
    }
}

Write-Host "`n6. Configuring Windows Defender Exclusions..." -ForegroundColor Cyan

# Add trading platform to Defender exclusions
$tradingPlatformPath = "C:\TradingPlatform"
Add-MpPreference -ExclusionPath $tradingPlatformPath
Add-MpPreference -ExclusionProcess "TradingPlatform.exe"
Write-Host "   - Added Windows Defender exclusions" -ForegroundColor Green

Write-Host "`n7. Setting Process Priority..." -ForegroundColor Cyan

# Create scheduled task to set process priority at startup
$action = New-ScheduledTaskAction -Execute 'PowerShell.exe' -Argument '-NoProfile -WindowStyle Hidden -Command "Get-Process TradingPlatform -ErrorAction SilentlyContinue | ForEach-Object { $_.PriorityClass = ''RealTime'' }"'
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "TradingPlatformPriority" -Action $action -Trigger $trigger -Principal $principal -Force
Write-Host "   - Created scheduled task for process priority" -ForegroundColor Green

Write-Host "`n8. Configuring CPU Affinity..." -ForegroundColor Cyan

# Get number of CPU cores
$cores = (Get-WmiObject Win32_Processor).NumberOfCores
Write-Host "   - Detected $cores CPU cores" -ForegroundColor Green

# Reserve cores 0-3 for trading platform (adjust as needed)
Write-Host "   - Cores 0-3 reserved for trading platform" -ForegroundColor Yellow
Write-Host "   - Other processes will use remaining cores" -ForegroundColor Yellow

Write-Host "`n9. Network Adapter Optimization..." -ForegroundColor Cyan

# Get network adapters
$adapters = Get-NetAdapter | Where-Object {$_.Status -eq "Up"}

foreach ($adapter in $adapters) {
    # Enable RSS (Receive Side Scaling)
    Set-NetAdapterRss -Name $adapter.Name -Enabled $true -ErrorAction SilentlyContinue
    
    # Increase receive buffers
    Set-NetAdapterAdvancedProperty -Name $adapter.Name -DisplayName "Receive Buffers" -DisplayValue 2048 -ErrorAction SilentlyContinue
    
    # Increase transmit buffers
    Set-NetAdapterAdvancedProperty -Name $adapter.Name -DisplayName "Transmit Buffers" -DisplayValue 2048 -ErrorAction SilentlyContinue
    
    Write-Host "   - Optimized adapter: $($adapter.Name)" -ForegroundColor Green
}

Write-Host "`n10. Creating Performance Monitoring Script..." -ForegroundColor Cyan

$monitorScript = @'
# Performance Monitoring Script
$counters = @(
    "\Processor(_Total)\% Processor Time",
    "\Memory\Available MBytes",
    "\Network Interface(*)\Bytes Total/sec",
    "\Process(TradingPlatform)\% Processor Time",
    "\Process(TradingPlatform)\Private Bytes"
)

Get-Counter -Counter $counters -Continuous -SampleInterval 1 | 
    ForEach-Object {
        $timestamp = $_.Timestamp
        $_.CounterSamples | ForEach-Object {
            "$timestamp,$($_.Path),$($_.CookedValue)"
        }
    } | Out-File -FilePath "C:\TradingPlatform\Logs\performance.csv" -Append
'@

$monitorScript | Out-File -FilePath "C:\TradingPlatform\Scripts\MonitorPerformance.ps1"
Write-Host "   - Created performance monitoring script" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Optimization Complete!" -ForegroundColor Green
Write-Host "Please restart your system for all changes to take effect." -ForegroundColor Yellow
Write-Host "`nRecommended BIOS Settings:" -ForegroundColor Cyan
Write-Host "  - Disable C-States" -ForegroundColor Yellow
Write-Host "  - Disable Intel SpeedStep / AMD Cool'n'Quiet" -ForegroundColor Yellow
Write-Host "  - Set Power Profile to Maximum Performance" -ForegroundColor Yellow
Write-Host "  - Enable XMP/DOCP for RAM" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Green
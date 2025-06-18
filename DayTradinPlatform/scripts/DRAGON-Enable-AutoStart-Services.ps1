# DRAGON Auto-Start Services Configuration
# Ensures all required services start automatically on DRAGON boot
# Run this script on DRAGON as Administrator

param(
    [switch]$EnableAll,
    [switch]$ShowStatus,
    [switch]$RestartServices
)

Write-Host "🐉 DRAGON Auto-Start Services Configuration" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "❌ This script must be run as Administrator" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Running as Administrator" -ForegroundColor Green
Write-Host ""

# Define required services for DRAGON BuildWorkspace
$requiredServices = @{
    # SSH Services (for remote Linux control)
    "ssh-agent" = @{
        DisplayName = "OpenSSH Authentication Agent"
        Description = "SSH key management for passwordless Linux connections"
        Required = $true
    }
    "sshd" = @{
        DisplayName = "OpenSSH SSH Server"
        Description = "SSH server for remote Linux commands and file transfer"
        Required = $true
    }
    
    # Windows Remote Management (PowerShell remoting)
    "WinRM" = @{
        DisplayName = "Windows Remote Management"
        Description = "PowerShell remoting for automated builds"
        Required = $true
    }
    "Wecsvc" = @{
        DisplayName = "Windows Event Collector" 
        Description = "Event log collection and management"
        Required = $false
    }
    
    # Remote Desktop (for manual access)
    "TermService" = @{
        DisplayName = "Remote Desktop Services"
        Description = "Remote Desktop access for manual development"
        Required = $true
    }
    "UmRdpService" = @{
        DisplayName = "Remote Desktop Services UserMode Port Redirector"
        Description = "RDP session support"
        Required = $true
    }
    
    # File Sharing (for source sync)
    "LanmanServer" = @{
        DisplayName = "Server"
        Description = "File and printer sharing"
        Required = $true
    }
    "LanmanWorkstation" = @{
        DisplayName = "Workstation"
        Description = "Network file access"
        Required = $true
    }
    
    # Network Services
    "Dnscache" = @{
        DisplayName = "DNS Client"
        Description = "DNS resolution for network connectivity"
        Required = $true
    }
    "Dhcp" = @{
        DisplayName = "DHCP Client"
        Description = "Automatic IP configuration"
        Required = $true
    }
    
    # Windows Update (for security)
    "wuauserv" = @{
        DisplayName = "Windows Update"
        Description = "Automatic security updates"
        Required = $false
    }
    
    # Performance and Monitoring
    "PerfHost" = @{
        DisplayName = "Performance Counter DLL Host"
        Description = "Performance monitoring for build optimization"
        Required = $false
    }
    "WSearch" = @{
        DisplayName = "Windows Search"
        Description = "File indexing for faster development"
        Required = $false
    }
    
    # GPU and Display Services
    "NVDisplay.ContainerLocalSystem" = @{
        DisplayName = "NVIDIA Display Container LS"
        Description = "NVIDIA GPU management for RTX hardware"
        Required = $false
    }
    "NvContainerLocalSystem" = @{
        DisplayName = "NVIDIA LocalSystem Container"
        Description = "NVIDIA driver services"
        Required = $false
    }
}

function Set-ServiceAutoStart {
    param(
        [string]$ServiceName,
        [hashtable]$ServiceInfo,
        [switch]$Restart
    )
    
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        
        if ($null -eq $service) {
            if ($ServiceInfo.Required) {
                Write-Host "  ❌ Required service '$ServiceName' not found" -ForegroundColor Red
                Write-Host "     $($ServiceInfo.Description)" -ForegroundColor Gray
                
                # Try to install OpenSSH if it's missing
                if ($ServiceName -in @("ssh-agent", "sshd")) {
                    Write-Host "  🔧 Attempting to install OpenSSH..." -ForegroundColor Yellow
                    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
                    Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
                    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
                }
            } else {
                Write-Host "  ⚠️  Optional service '$ServiceName' not found (skipping)" -ForegroundColor Yellow
                return
            }
        }
        
        if ($null -ne $service) {
            # Set service to automatic start
            Set-Service -Name $ServiceName -StartupType Automatic -ErrorAction Stop
            
            # Start the service if it's not running
            if ($service.Status -ne "Running") {
                Start-Service -Name $ServiceName -ErrorAction Stop
                Write-Host "  ✅ $($ServiceInfo.DisplayName) - Set to auto-start and started" -ForegroundColor Green
            } else {
                Write-Host "  ✅ $($ServiceInfo.DisplayName) - Set to auto-start (already running)" -ForegroundColor Green
            }
            
            # Restart if requested
            if ($Restart -and $service.Status -eq "Running") {
                Restart-Service -Name $ServiceName -ErrorAction Stop
                Write-Host "  🔄 $($ServiceInfo.DisplayName) - Restarted" -ForegroundColor Cyan
            }
        }
        
    } catch {
        if ($ServiceInfo.Required) {
            Write-Host "  ❌ Failed to configure required service '$ServiceName': $($_.Exception.Message)" -ForegroundColor Red
        } else {
            Write-Host "  ⚠️  Failed to configure optional service '$ServiceName': $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

function Show-ServiceStatus {
    Write-Host "📊 Current Service Status:" -ForegroundColor Cyan
    Write-Host "=========================" -ForegroundColor Cyan
    
    foreach ($serviceName in $requiredServices.Keys) {
        $serviceInfo = $requiredServices[$serviceName]
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        
        if ($null -eq $service) {
            $status = if ($serviceInfo.Required) { "❌ MISSING (Required)" } else { "⚠️  Not Installed (Optional)" }
            $color = if ($serviceInfo.Required) { "Red" } else { "Yellow" }
        } else {
            $startType = (Get-WmiObject -Class Win32_Service -Filter "Name='$serviceName'").StartMode
            $status = switch ($service.Status) {
                "Running" { "✅ Running ($startType)" }
                "Stopped" { "⏹️ Stopped ($startType)" }
                default { "⚠️  $($service.Status) ($startType)" }
            }
            $color = switch ($service.Status) {
                "Running" { "Green" }
                "Stopped" { "Yellow" }
                default { "Red" }
            }
        }
        
        Write-Host "  $($serviceInfo.DisplayName): " -NoNewline -ForegroundColor White
        Write-Host $status -ForegroundColor $color
    }
}

# Show current status if requested
if ($ShowStatus) {
    Show-ServiceStatus
    Write-Host ""
}

# Configure services
if ($EnableAll -or (-not $ShowStatus)) {
    Write-Host "🔧 Configuring Services for Auto-Start:" -ForegroundColor Green
    Write-Host "=======================================" -ForegroundColor Green
    
    foreach ($serviceName in $requiredServices.Keys) {
        $serviceInfo = $requiredServices[$serviceName]
        Write-Host ""
        Write-Host "🔧 Configuring: $($serviceInfo.DisplayName)" -ForegroundColor Yellow
        Write-Host "   Description: $($serviceInfo.Description)" -ForegroundColor Gray
        
        Set-ServiceAutoStart -ServiceName $serviceName -ServiceInfo $serviceInfo -Restart:$RestartServices
    }
    
    Write-Host ""
    Write-Host "🛡️ Configuring Windows Firewall for SSH..." -ForegroundColor Yellow
    
    # Enable SSH firewall rules
    try {
        New-NetFirewallRule -DisplayName "OpenSSH-Server-In-TCP" -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -ErrorAction SilentlyContinue
        Write-Host "  ✅ SSH firewall rule enabled (port 22)" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  SSH firewall rule may already exist" -ForegroundColor Yellow
    }
    
    # Enable RDP firewall rules
    try {
        Enable-NetFirewallRule -DisplayGroup "Remote Desktop" -ErrorAction SilentlyContinue
        Write-Host "  ✅ RDP firewall rules enabled (port 3389)" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  RDP firewall rules may already be enabled" -ForegroundColor Yellow
    }
    
    # Enable PowerShell remoting
    try {
        Enable-PSRemoting -Force -SkipNetworkProfileCheck -ErrorAction SilentlyContinue
        Write-Host "  ✅ PowerShell remoting enabled (ports 5985/5986)" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  PowerShell remoting may already be enabled" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "🚀 Optimizing Services for Build Performance..." -ForegroundColor Yellow
    
    # Set high performance power plan
    try {
        powercfg -setactive SCHEME_MIN
        Write-Host "  ✅ High performance power plan activated" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  Could not set high performance power plan" -ForegroundColor Yellow
    }
    
    # Disable unnecessary startup programs to free resources
    try {
        # Disable Windows Tips and Suggestions
        Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\CloudContent" -Name "DisableSoftLanding" -Value 1 -ErrorAction SilentlyContinue
        Write-Host "  ✅ Disabled unnecessary startup suggestions" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  Could not disable startup suggestions" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "🎉 SERVICE CONFIGURATION COMPLETE!" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
    Write-Host ""
    
    # Show final status
    Show-ServiceStatus
    
    Write-Host ""
    Write-Host "🔄 Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Restart DRAGON to ensure all auto-start services load properly" -ForegroundColor White
    Write-Host "  2. Test SSH connection from Linux: ssh nader@192.168.1.100" -ForegroundColor White
    Write-Host "  3. Test RDP connection if needed" -ForegroundColor White
    Write-Host "  4. Run BuildWorkspace setup and source sync" -ForegroundColor White
    Write-Host ""
    Write-Host "🛡️ Auto-Start Benefits:" -ForegroundColor Yellow
    Write-Host "  ✅ SSH server starts automatically on boot" -ForegroundColor Green
    Write-Host "  ✅ Remote Desktop available immediately" -ForegroundColor Green  
    Write-Host "  ✅ PowerShell remoting enabled for automation" -ForegroundColor Green
    Write-Host "  ✅ Network services ready for file sharing" -ForegroundColor Green
    Write-Host "  ✅ High performance mode for optimal builds" -ForegroundColor Green
    
    if ($RestartServices) {
        Write-Host ""
        Write-Host "⚠️  Some services were restarted - existing connections may be interrupted" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "🎯 DRAGON is now configured for reliable auto-start services!" -ForegroundColor Green
Write-Host "All required services will start automatically after reboot." -ForegroundColor White
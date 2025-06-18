# DRAGON Auto-Start Services - CORRECTED VERSION
# Run this script on DRAGON as Administrator to fix service auto-start

Write-Host "🐉 DRAGON Auto-Start Services Setup (Fixed)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "❌ This script must be run as Administrator" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Running as Administrator" -ForegroundColor Green
Write-Host ""

# Install OpenSSH if missing
Write-Host "🔧 Installing OpenSSH components..." -ForegroundColor Yellow
try {
    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 -ErrorAction SilentlyContinue
    Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0 -ErrorAction SilentlyContinue
    Write-Host "  ✅ OpenSSH installation attempted" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️  OpenSSH installation issue: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Configure critical services for auto-start
Write-Host ""
Write-Host "🔧 Configuring services for auto-start..." -ForegroundColor Yellow

$services = @(
    @{Name="sshd"; DisplayName="OpenSSH SSH Server"},
    @{Name="ssh-agent"; DisplayName="OpenSSH Authentication Agent"},
    @{Name="WinRM"; DisplayName="Windows Remote Management"},
    @{Name="TermService"; DisplayName="Remote Desktop Services"},
    @{Name="UmRdpService"; DisplayName="RDP User Mode Service"},
    @{Name="LanmanServer"; DisplayName="Server (File Sharing)"},
    @{Name="LanmanWorkstation"; DisplayName="Workstation"},
    @{Name="Dnscache"; DisplayName="DNS Client"},
    @{Name="Dhcp"; DisplayName="DHCP Client"}
)

foreach ($serviceInfo in $services) {
    try {
        $service = Get-Service -Name $serviceInfo.Name -ErrorAction SilentlyContinue
        if ($service) {
            Set-Service -Name $serviceInfo.Name -StartupType Automatic -ErrorAction Stop
            if ($service.Status -ne "Running") {
                Start-Service -Name $serviceInfo.Name -ErrorAction Stop
                Write-Host "  ✅ $($serviceInfo.DisplayName) - Set to auto-start and started" -ForegroundColor Green
            } else {
                Write-Host "  ✅ $($serviceInfo.DisplayName) - Set to auto-start (already running)" -ForegroundColor Green
            }
        } else {
            Write-Host "  ⚠️  $($serviceInfo.DisplayName) - Service not found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ❌ $($serviceInfo.DisplayName) - Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Enable firewall rules with proper syntax
Write-Host ""
Write-Host "🛡️ Configuring firewall rules..." -ForegroundColor Yellow

try {
    # SSH firewall rule
    $existingSSHRule = Get-NetFirewallRule -DisplayName "SSH-Server-In-TCP" -ErrorAction SilentlyContinue
    if (-not $existingSSHRule) {
        New-NetFirewallRule -DisplayName "SSH-Server-In-TCP" -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow
        Write-Host "  ✅ SSH firewall rule created (port 22)" -ForegroundColor Green
    } else {
        Write-Host "  ✅ SSH firewall rule already exists (port 22)" -ForegroundColor Green
    }
} catch {
    Write-Host "  ❌ SSH firewall rule error: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    # RDP firewall rules
    Enable-NetFirewallRule -DisplayGroup "Remote Desktop"
    Write-Host "  ✅ RDP firewall rules enabled (port 3389)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ RDP firewall rule error: $($_.Exception.Message)" -ForegroundColor Red
}

try {
    # PowerShell remoting
    Enable-PSRemoting -Force -SkipNetworkProfileCheck
    Write-Host "  ✅ PowerShell remoting enabled (ports 5985/5986)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ PowerShell remoting error: $($_.Exception.Message)" -ForegroundColor Red
}

# Set high performance power plan
Write-Host ""
Write-Host "⚡ Setting high performance power plan..." -ForegroundColor Yellow
try {
    powercfg -setactive SCHEME_MIN
    Write-Host "  ✅ High performance power plan activated" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️  Could not set high performance power plan" -ForegroundColor Yellow
}

# Configure RDP to allow connections
Write-Host ""
Write-Host "🖥️ Enabling Remote Desktop..." -ForegroundColor Yellow
try {
    Set-ItemProperty -Path "HKLM:\System\CurrentControlSet\Control\Terminal Server" -Name "fDenyTSConnections" -Value 0
    Set-ItemProperty -Path "HKLM:\System\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp" -Name "UserAuthentication" -Value 1
    Write-Host "  ✅ Remote Desktop enabled" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Remote Desktop configuration error: $($_.Exception.Message)" -ForegroundColor Red
}

# Show current service status
Write-Host ""
Write-Host "📊 Current Service Status:" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

foreach ($serviceInfo in $services) {
    $service = Get-Service -Name $serviceInfo.Name -ErrorAction SilentlyContinue
    if ($service) {
        $startType = (Get-CimInstance -ClassName Win32_Service -Filter "Name='$($serviceInfo.Name)'").StartMode
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
        Write-Host "  $($serviceInfo.DisplayName): " -NoNewline -ForegroundColor White
        Write-Host $status -ForegroundColor $color
    } else {
        Write-Host "  $($serviceInfo.DisplayName): " -NoNewline -ForegroundColor White
        Write-Host "❌ Not Found" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "🎉 AUTO-START CONFIGURATION COMPLETE!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "🔄 IMPORTANT: Restart DRAGON to ensure all services auto-start properly" -ForegroundColor Yellow
Write-Host ""
Write-Host "🧪 After restart, test from Linux:" -ForegroundColor Cyan
Write-Host "  ssh nader@192.168.1.100 'echo SSH working'" -ForegroundColor Gray
Write-Host "  scp test.txt nader@192.168.1.100:C:/temp/" -ForegroundColor Gray
Write-Host ""
Write-Host "✅ Services will now start automatically on every DRAGON boot!" -ForegroundColor Green
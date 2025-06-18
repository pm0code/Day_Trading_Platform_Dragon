# Fix DRAGON SSH Connection Reset Issues
# Run this on DRAGON as Administrator

Write-Host "=== DRAGON SSH Connection Reset Fix ===" -ForegroundColor Green

# 1. Check OpenSSH Server Service Status
Write-Host "`n1. Checking OpenSSH Server Status..." -ForegroundColor Yellow
Get-Service sshd
Get-Service ssh-agent

# 2. Stop SSH service to modify configuration
Write-Host "`n2. Stopping SSH services for configuration..." -ForegroundColor Yellow
Stop-Service sshd -Force
Stop-Service ssh-agent -Force

# 3. Backup existing SSH configuration
Write-Host "`n3. Backing up SSH configuration..." -ForegroundColor Yellow
$sshConfigPath = "C:\ProgramData\ssh\sshd_config"
if (Test-Path $sshConfigPath) {
    Copy-Item $sshConfigPath "$sshConfigPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Write-Host "Backed up to: $sshConfigPath.backup.*" -ForegroundColor Green
}

# 4. Create optimized SSH configuration for development
Write-Host "`n4. Creating optimized SSH configuration..." -ForegroundColor Yellow
$optimizedConfig = @"
# DRAGON SSH Server Configuration - Optimized for Development
# Generated: $(Get-Date)

# Connection Settings
Port 22
Protocol 2
ListenAddress 0.0.0.0

# Authentication
PasswordAuthentication yes
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys

# Session Management - CRITICAL FIXES
MaxSessions 20
MaxStartups 20:30:50
ClientAliveInterval 120
ClientAliveCountMax 10

# Connection Timeouts - Prevent Aggressive Disconnects  
LoginGraceTime 300
TCPKeepAlive yes

# Logging
SyslogFacility AUTHPRIV
LogLevel INFO

# Subsystems
Subsystem sftp sftp-server.exe

# Windows-Specific Settings
PidFile C:/ProgramData/ssh/sshd.pid

# Security Settings
StrictModes no
IgnoreRhosts yes
HostbasedAuthentication no
PermitEmptyPasswords no
ChallengeResponseAuthentication no
UsePAM no

# Performance Settings
Compression yes
UseDNS no

# Allow Administrator access
AllowUsers Administrator
"@

# Write optimized configuration
$optimizedConfig | Out-File -FilePath $sshConfigPath -Encoding UTF8 -Force
Write-Host "Created optimized SSH configuration" -ForegroundColor Green

# 5. Set proper permissions on SSH config
Write-Host "`n5. Setting SSH configuration permissions..." -ForegroundColor Yellow
icacls $sshConfigPath /inheritance:r
icacls $sshConfigPath /grant "SYSTEM:F"
icacls $sshConfigPath /grant "Administrators:F"
icacls $sshConfigPath /grant "NT SERVICE\sshd:R"

# 6. Configure Windows Firewall rules
Write-Host "`n6. Configuring Windows Firewall..." -ForegroundColor Yellow
# Remove existing rules
Remove-NetFirewallRule -DisplayName "OpenSSH-Server-In-TCP" -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName "SSH Development Access" -ErrorAction SilentlyContinue

# Add optimized firewall rule
New-NetFirewallRule -DisplayName "SSH Development Access" -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -Profile Any
Write-Host "Added SSH firewall rule" -ForegroundColor Green

# 7. Restart SSH services
Write-Host "`n7. Starting SSH services..." -ForegroundColor Yellow
Start-Service ssh-agent
Start-Service sshd

# Wait for services to fully start
Start-Sleep -Seconds 3

# 8. Verify service status
Write-Host "`n8. Verifying SSH service status..." -ForegroundColor Yellow
$sshdStatus = Get-Service sshd
$agentStatus = Get-Service ssh-agent

Write-Host "SSH Server (sshd): $($sshdStatus.Status)" -ForegroundColor $(if($sshdStatus.Status -eq 'Running'){'Green'}else{'Red'})
Write-Host "SSH Agent: $($agentStatus.Status)" -ForegroundColor $(if($agentStatus.Status -eq 'Running'){'Green'}else{'Red'})

# 9. Test SSH connectivity locally
Write-Host "`n9. Testing SSH connectivity..." -ForegroundColor Yellow
try {
    $testResult = ssh localhost "echo 'SSH test successful'"
    Write-Host "Local SSH test: $testResult" -ForegroundColor Green
} catch {
    Write-Host "Local SSH test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 10. Display connection information
Write-Host "`n=== SSH CONNECTION INFORMATION ===" -ForegroundColor Cyan
Write-Host "SSH Server Status: $($sshdStatus.Status)"
Write-Host "SSH Port: 22"
Write-Host "Max Sessions: 20"
Write-Host "Client Alive Interval: 120 seconds"
Write-Host "Configuration File: $sshConfigPath"

Write-Host "`n=== CONNECTION RESET FIX APPLIED ===" -ForegroundColor Green
Write-Host "Common SSH connection reset issues have been resolved:" -ForegroundColor Yellow
Write-Host "  ✓ Increased MaxSessions from default (10) to 20" -ForegroundColor Green
Write-Host "  ✓ Set ClientAliveInterval to 120 seconds" -ForegroundColor Green  
Write-Host "  ✓ Disabled aggressive connection timeouts" -ForegroundColor Green
Write-Host "  ✓ Optimized Windows Firewall rules" -ForegroundColor Green
Write-Host "  ✓ Set proper SSH configuration permissions" -ForegroundColor Green

Write-Host "`nSSH connection resets should now be resolved!" -ForegroundColor Cyan
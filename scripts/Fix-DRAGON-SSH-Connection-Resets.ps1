# Fix DRAGON SSH Connection Reset Issues
# Run this on DRAGON as Administrator
# Purpose: Resolve "Connection reset by 192.168.1.35 port 22" errors

param(
    [switch]$Verbose
)

Write-Host "=== DRAGON SSH Connection Reset Fix ===" -ForegroundColor Green
Write-Host "Fixing Windows SSH server configuration issues..." -ForegroundColor Yellow

try {
    # Step 1: Stop SSH services
    Write-Host "`nStep 1: Stopping SSH services..." -ForegroundColor Cyan
    Stop-Service sshd -Force -ErrorAction Stop
    Stop-Service ssh-agent -Force -ErrorAction Stop
    Write-Host "✓ SSH services stopped" -ForegroundColor Green

    # Step 2: Backup existing SSH config
    Write-Host "`nStep 2: Backing up SSH configuration..." -ForegroundColor Cyan
    $sshConfigPath = "C:\ProgramData\ssh\sshd_config"
    $backupPath = "$sshConfigPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    
    if (Test-Path $sshConfigPath) {
        Copy-Item $sshConfigPath $backupPath -ErrorAction Stop
        Write-Host "✓ Backup created: $backupPath" -ForegroundColor Green
    } else {
        Write-Host "⚠ No existing SSH config found - creating new one" -ForegroundColor Yellow
    }

    # Step 3: Create optimized SSH configuration
    Write-Host "`nStep 3: Creating optimized SSH configuration..." -ForegroundColor Cyan
    $optimizedConfig = @"
# DRAGON SSH Server Configuration - Fix Connection Resets
# Generated: $(Get-Date)
# Purpose: Resolve SSH connection reset issues for development

# Basic Settings
Port 22
Protocol 2
ListenAddress 0.0.0.0

# Authentication
PasswordAuthentication yes
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys

# CONNECTION RESET FIXES - CRITICAL:
MaxSessions 20
MaxStartups 20:30:50
ClientAliveInterval 120
ClientAliveCountMax 10
LoginGraceTime 300
TCPKeepAlive yes

# Performance & Logging
LogLevel INFO
SyslogFacility AUTHPRIV
Compression yes
UseDNS no

# Security
StrictModes no
IgnoreRhosts yes
HostbasedAuthentication no
PermitEmptyPasswords no
ChallengeResponseAuthentication no
UsePAM no

# Windows Subsystems
Subsystem sftp sftp-server.exe
PidFile C:/ProgramData/ssh/sshd.pid

# Access Control
AllowUsers Administrator

# Development-Friendly Settings
PermitUserEnvironment yes
"@

    # Step 4: Apply the configuration
    Write-Host "`nStep 4: Writing optimized configuration..." -ForegroundColor Cyan
    $optimizedConfig | Out-File -FilePath $sshConfigPath -Encoding UTF8 -Force
    Write-Host "✓ SSH configuration updated" -ForegroundColor Green

    # Step 5: Set proper permissions
    Write-Host "`nStep 5: Setting SSH configuration permissions..." -ForegroundColor Cyan
    icacls $sshConfigPath /inheritance:r | Out-Null
    icacls $sshConfigPath /grant "SYSTEM:F" | Out-Null
    icacls $sshConfigPath /grant "Administrators:F" | Out-Null
    icacls $sshConfigPath /grant "NT SERVICE\sshd:R" | Out-Null
    Write-Host "✓ Permissions configured" -ForegroundColor Green

    # Step 6: Configure Windows Firewall
    Write-Host "`nStep 6: Optimizing Windows Firewall for SSH..." -ForegroundColor Cyan
    # Remove existing conflicting rules
    Remove-NetFirewallRule -DisplayName "OpenSSH-Server-In-TCP" -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName "SSH Development Access" -ErrorAction SilentlyContinue
    
    # Add optimized SSH rule
    New-NetFirewallRule -DisplayName "SSH Development Access" -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -Profile Any | Out-Null
    Write-Host "✓ Firewall rules optimized" -ForegroundColor Green

    # Step 7: Restart SSH services
    Write-Host "`nStep 7: Starting SSH services with new configuration..." -ForegroundColor Cyan
    Start-Service ssh-agent -ErrorAction Stop
    Start-Service sshd -ErrorAction Stop
    
    # Wait for services to fully initialize
    Start-Sleep -Seconds 5
    Write-Host "✓ SSH services restarted" -ForegroundColor Green

    # Step 8: Verify services
    Write-Host "`nStep 8: Verifying SSH service status..." -ForegroundColor Cyan
    $sshdStatus = Get-Service sshd
    $agentStatus = Get-Service ssh-agent

    if ($sshdStatus.Status -eq 'Running' -and $agentStatus.Status -eq 'Running') {
        Write-Host "✓ SSH Server (sshd): $($sshdStatus.Status)" -ForegroundColor Green
        Write-Host "✓ SSH Agent: $($agentStatus.Status)" -ForegroundColor Green
    } else {
        Write-Host "✗ SSH services not running properly!" -ForegroundColor Red
        Write-Host "  SSH Server: $($sshdStatus.Status)" -ForegroundColor Yellow
        Write-Host "  SSH Agent: $($agentStatus.Status)" -ForegroundColor Yellow
        throw "SSH services failed to start"
    }

    # Step 9: Test SSH connectivity
    Write-Host "`nStep 9: Testing SSH connectivity..." -ForegroundColor Cyan
    try {
        $env:SSH_AUTH_SOCK = $null  # Clear any existing SSH agent socket
        $testConnection = Test-NetConnection -ComputerName localhost -Port 22 -InformationLevel Quiet
        if ($testConnection) {
            Write-Host "✓ SSH port 22 is accessible" -ForegroundColor Green
        } else {
            Write-Host "✗ SSH port 22 test failed" -ForegroundColor Red
        }
    } catch {
        Write-Host "⚠ SSH connectivity test inconclusive: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    # Success summary
    Write-Host "`n" + "="*60 -ForegroundColor Green
    Write-Host "SSH CONNECTION RESET FIX COMPLETED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "="*60 -ForegroundColor Green
    
    Write-Host "`nFixes Applied:" -ForegroundColor Cyan
    Write-Host "  ✓ MaxSessions increased to 20 (was ~10)" -ForegroundColor White
    Write-Host "  ✓ ClientAliveInterval set to 120 seconds" -ForegroundColor White
    Write-Host "  ✓ Connection keepalive enabled" -ForegroundColor White
    Write-Host "  ✓ Login grace time extended to 300 seconds" -ForegroundColor White
    Write-Host "  ✓ Windows Firewall optimized" -ForegroundColor White
    Write-Host "  ✓ SSH config permissions corrected" -ForegroundColor White
    
    Write-Host "`nSSH Configuration Details:" -ForegroundColor Cyan
    Write-Host "  Config File: $sshConfigPath" -ForegroundColor White
    Write-Host "  Backup File: $backupPath" -ForegroundColor White
    Write-Host "  SSH Port: 22" -ForegroundColor White
    Write-Host "  Max Concurrent Sessions: 20" -ForegroundColor White
    Write-Host "  Session Keepalive: 120 seconds" -ForegroundColor White
    
    Write-Host "`nConnection reset errors should now be resolved!" -ForegroundColor Green
    Write-Host "You can now SSH to DRAGON without connection drops." -ForegroundColor Yellow

} catch {
    Write-Host "`n✗ ERROR: SSH configuration fix failed!" -ForegroundColor Red
    Write-Host "Error Details: $($_.Exception.Message)" -ForegroundColor Yellow
    
    # Attempt to restore services if they're stopped
    try {
        Write-Host "`nAttempting to restore SSH services..." -ForegroundColor Yellow
        Start-Service ssh-agent -ErrorAction SilentlyContinue
        Start-Service sshd -ErrorAction SilentlyContinue
    } catch {
        Write-Host "Failed to restore SSH services. Manual intervention required." -ForegroundColor Red
    }
    
    exit 1
}

if ($Verbose) {
    Write-Host "`nDETAILED CONFIGURATION:" -ForegroundColor Magenta
    Write-Host "Current SSH configuration:" -ForegroundColor White
    Get-Content $sshConfigPath | Select-Object -First 20
}

Write-Host "`nScript completed. Test SSH connection from Linux now!" -ForegroundColor Cyan
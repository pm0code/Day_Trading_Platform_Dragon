# DRAGON SSH Configuration Fix Script
# Run this on DRAGON Windows 11 as Administrator

Write-Host "🐉 Fixing SSH Configuration on DRAGON..." -ForegroundColor Cyan

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "❌ This script must be run as Administrator"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 1: Enable PubkeyAuthentication in SSH config
Write-Host "📝 Step 1: Enabling PubkeyAuthentication..." -ForegroundColor Yellow
try {
    $sshConfig = "$env:ProgramData\ssh\sshd_config"
    $content = Get-Content $sshConfig
    $newContent = $content -replace '#PubkeyAuthentication yes', 'PubkeyAuthentication yes'
    Set-Content -Path $sshConfig -Value $newContent
    Write-Host "✅ PubkeyAuthentication enabled" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to modify SSH config: $_"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 2: Create administrators_authorized_keys file
Write-Host "📁 Step 2: Creating administrators_authorized_keys file..." -ForegroundColor Yellow
try {
    $adminKeysFile = "$env:ProgramData\ssh\administrators_authorized_keys"
    New-Item -ItemType File -Path $adminKeysFile -Force | Out-Null
    Write-Host "✅ File created: $adminKeysFile" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to create administrators_authorized_keys: $_"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 3: Add the public key to the file
Write-Host "🔑 Step 3: Adding public key..." -ForegroundColor Yellow
try {
    $publicKey = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIH8jGvk9Pks5ZM+5yflYkRr3WHVIIrbLn5tVSqKYQvcf ubuntu-dev-to-dragon"
    Set-Content -Path $adminKeysFile -Value $publicKey -Encoding ASCII
    Write-Host "✅ Public key added to administrators_authorized_keys" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to add public key: $_"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 4: Set proper permissions
Write-Host "🔒 Step 4: Setting file permissions..." -ForegroundColor Yellow
try {
    icacls "$adminKeysFile" /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F" | Out-Null
    Write-Host "✅ Permissions set correctly" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to set permissions: $_"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 5: Restart SSH service
Write-Host "🔄 Step 5: Restarting SSH service..." -ForegroundColor Yellow
try {
    Restart-Service sshd
    Write-Host "✅ SSH service restarted" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to restart SSH service: $_"
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 6: Verification
Write-Host "✅ Step 6: Verification..." -ForegroundColor Yellow
Write-Host ""
Write-Host "=== SSH Configuration ===" -ForegroundColor Cyan
Get-Content $sshConfig | Select-String -Pattern "PubkeyAuthentication"

Write-Host ""
Write-Host "=== Public Key Content ===" -ForegroundColor Cyan
Get-Content $adminKeysFile

Write-Host ""
Write-Host "=== SSH Service Status ===" -ForegroundColor Cyan
Get-Service sshd

Write-Host ""
Write-Host "🎉 SSH configuration completed successfully!" -ForegroundColor Green
Write-Host "Ubuntu machine should now be able to connect via SSH key authentication." -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to exit"
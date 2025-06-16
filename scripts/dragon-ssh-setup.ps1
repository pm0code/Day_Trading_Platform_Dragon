# DRAGON SSH Server Setup Script
# Run this on the DRAGON Windows 11 machine as Administrator

Write-Host "🐉 Setting up SSH Server on DRAGON Windows 11..." -ForegroundColor Cyan

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "❌ This script must be run as Administrator"
    exit 1
}

# Install OpenSSH Server
Write-Host "📦 Installing OpenSSH Server..." -ForegroundColor Yellow
$sshFeature = Get-WindowsCapability -Online | Where-Object Name -like 'OpenSSH.Server*'
if ($sshFeature.State -ne "Installed") {
    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
    Write-Host "✅ OpenSSH Server installed" -ForegroundColor Green
} else {
    Write-Host "✅ OpenSSH Server already installed" -ForegroundColor Green
}

# Start and configure SSH service
Write-Host "🔧 Configuring SSH service..." -ForegroundColor Yellow
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'
Write-Host "✅ SSH service started and set to auto-start" -ForegroundColor Green

# Configure Windows Firewall
Write-Host "🔥 Configuring Windows Firewall..." -ForegroundColor Yellow
$firewallRule = Get-NetFirewallRule -Name "sshd" -ErrorAction SilentlyContinue
if (-not $firewallRule) {
    New-NetFirewallRule -Name sshd -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
    Write-Host "✅ Firewall rule created for SSH" -ForegroundColor Green
} else {
    Write-Host "✅ Firewall rule already exists for SSH" -ForegroundColor Green
}

# Create .ssh directory
Write-Host "📁 Creating .ssh directory..." -ForegroundColor Yellow
$sshDir = "$env:USERPROFILE\.ssh"
if (-not (Test-Path $sshDir)) {
    New-Item -ItemType Directory -Path $sshDir -Force
    Write-Host "✅ Created .ssh directory: $sshDir" -ForegroundColor Green
} else {
    Write-Host "✅ .ssh directory already exists: $sshDir" -ForegroundColor Green
}

# Create authorized_keys file placeholder
$authorizedKeysFile = "$sshDir\authorized_keys"
if (-not (Test-Path $authorizedKeysFile)) {
    New-Item -ItemType File -Path $authorizedKeysFile -Force
    Write-Host "✅ Created authorized_keys file: $authorizedKeysFile" -ForegroundColor Green
}

Write-Host ""
Write-Host "🔑 NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Copy the following public key from Ubuntu:" -ForegroundColor Yellow
Write-Host "   ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIH8jGvk9Pks5ZM+5yflYkRr3WHVIIrbLn5tVSqKYQvcf ubuntu-dev-to-dragon" -ForegroundColor White
Write-Host ""
Write-Host "2. Add it to authorized_keys file:" -ForegroundColor Yellow
Write-Host "   echo 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIH8jGvk9Pks5ZM+5yflYkRr3WHVIIrbLn5tVSqKYQvcf ubuntu-dev-to-dragon' >> $authorizedKeysFile" -ForegroundColor White
Write-Host ""
Write-Host "3. Set proper permissions:" -ForegroundColor Yellow
Write-Host "   icacls `"$authorizedKeysFile`" /inheritance:r /grant:r `"$env:USERNAME`:F`"" -ForegroundColor White
Write-Host ""
Write-Host "4. Test connection from Ubuntu:" -ForegroundColor Yellow
Write-Host "   ./dragon-connect.sh test" -ForegroundColor White
Write-Host ""
Write-Host "✅ SSH Server setup complete! Ready for Ubuntu connection." -ForegroundColor Green
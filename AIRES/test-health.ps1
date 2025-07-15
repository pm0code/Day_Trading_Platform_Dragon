#!/usr/bin/env pwsh
# Test script for health check functionality

Write-Host "Testing AIRES Health Check Functionality" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Change to bin directory
Set-Location -Path "bin/Debug"

Write-Host "`nAttempting to run health check command..." -ForegroundColor Yellow
& ./aires.exe health --detailed

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nHealth check failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    
    Write-Host "`nTrying simple health check..." -ForegroundColor Yellow
    & ./aires.exe health --format simple
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nSimple health check also failed" -ForegroundColor Red
        
        Write-Host "`nTrying to list available commands..." -ForegroundColor Yellow
        & ./aires.exe --help
    }
}
else {
    Write-Host "`nHealth check completed successfully!" -ForegroundColor Green
}
# DRAGON System Performance Analysis Script
# Comprehensive hardware testing for trading platform optimization

Write-Host "DRAGON System Performance Analysis" -ForegroundColor Cyan
Write-Host "Testing: RAM, CPU, Storage, Network, GPU performance" -ForegroundColor Yellow
Write-Host ""

# Create performance results directory
$ResultsDir = "D:\BuildWorkspace\PerformanceResults"
New-Item -ItemType Directory -Path $ResultsDir -Force | Out-Null

$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$ReportFile = "$ResultsDir\DRAGON_Performance_Report_$Timestamp.txt"

# Start performance report
"DRAGON System Performance Analysis" | Out-File -FilePath $ReportFile
"Generated: $(Get-Date)" | Out-File -FilePath $ReportFile -Append
"=" * 50 | Out-File -FilePath $ReportFile -Append

Write-Host "Analyzing System Configuration..." -ForegroundColor Green

# 1. SYSTEM OVERVIEW
Write-Host "1. System Overview" -ForegroundColor White
"`nSYSTEM OVERVIEW" | Out-File -FilePath $ReportFile -Append
Get-ComputerInfo | Select-Object @{
    Name="Manufacturer"; Expression={$_.CsManufacturer}
}, @{
    Name="Model"; Expression={$_.CsModel}
}, @{
    Name="TotalPhysicalMemoryGB"; Expression={[math]::Round($_.TotalPhysicalMemory/1GB, 2)}
}, @{
    Name="ProcessorName"; Expression={$_.CsProcessors.Name}
} | Out-File -FilePath $ReportFile -Append

# 2. CPU ANALYSIS
Write-Host "2. CPU Performance Analysis" -ForegroundColor White
"`nCPU PERFORMANCE" | Out-File -FilePath $ReportFile -Append

# CPU Information
Get-WmiObject -Class Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed | Out-File -FilePath $ReportFile -Append

# CPU Load Test (30 seconds)
Write-Host "   Running CPU stress test (30s)..." -ForegroundColor Yellow
$CPULoadStart = Get-Date
$CPUCounters = @()
for ($i = 0; $i -lt 30; $i++) {
    $CPUUsage = (Get-Counter "\Processor(_Total)\% Processor Time").CounterSamples.CookedValue
    $CPUCounters += $CPUUsage
    Start-Sleep -Seconds 1
}
$AvgCPU = ($CPUCounters | Measure-Object -Average).Average
$MaxCPU = ($CPUCounters | Measure-Object -Maximum).Maximum

"CPU Load Test Results:" | Out-File -FilePath $ReportFile -Append
"Average CPU Usage: $([math]::Round($AvgCPU, 2))%" | Out-File -FilePath $ReportFile -Append
"Maximum CPU Usage: $([math]::Round($MaxCPU, 2))%" | Out-File -FilePath $ReportFile -Append

# 3. MEMORY ANALYSIS
Write-Host "3. Memory Performance Analysis" -ForegroundColor White
"`nMEMORY PERFORMANCE" | Out-File -FilePath $ReportFile -Append

$Memory = Get-WmiObject -Class Win32_ComputerSystem
$MemoryModules = Get-WmiObject -Class Win32_PhysicalMemory

"Total Physical Memory: $([math]::Round($Memory.TotalPhysicalMemory/1GB, 2)) GB" | Out-File -FilePath $ReportFile -Append
"Memory Configuration:" | Out-File -FilePath $ReportFile -Append
$MemoryModules | Select-Object @{
    Name="SlotLocation"; Expression={$_.DeviceLocator}
}, @{
    Name="CapacityGB"; Expression={[math]::Round($_.Capacity/1GB, 2)}
}, @{
    Name="Speed"; Expression={"$($_.Speed) MHz"}
}, @{
    Name="Type"; Expression={$_.MemoryType}
} | Out-File -FilePath $ReportFile -Append

# Memory Usage Test
$MemUsage = Get-Counter "\Memory\Available MBytes"
$TotalMem = (Get-WmiObject -Class Win32_ComputerSystem).TotalPhysicalMemory/1MB
$UsedMem = $TotalMem - $MemUsage.CounterSamples.CookedValue
$MemUsagePercent = ($UsedMem / $TotalMem) * 100

"Current Memory Usage: $([math]::Round($MemUsagePercent, 2))%" | Out-File -FilePath $ReportFile -Append
"Available Memory: $([math]::Round($MemUsage.CounterSamples.CookedValue/1024, 2)) GB" | Out-File -FilePath $ReportFile -Append

# 4. STORAGE ANALYSIS
Write-Host "4. Storage Performance Analysis" -ForegroundColor White
"`nSTORAGE PERFORMANCE" | Out-File -FilePath $ReportFile -Append

# Disk Information
Get-WmiObject -Class Win32_LogicalDisk | Where-Object {$_.DriveType -eq 3} | Select-Object @{
    Name="Drive"; Expression={$_.DeviceID}
}, @{
    Name="SizeGB"; Expression={[math]::Round($_.Size/1GB, 2)}
}, @{
    Name="FreeSpaceGB"; Expression={[math]::Round($_.FreeSpace/1GB, 2)}
}, @{
    Name="UsedPercent"; Expression={[math]::Round((($_.Size - $_.FreeSpace)/$_.Size)*100, 2)}
} | Out-File -FilePath $ReportFile -Append

# Storage Speed Test (D: drive where trading platform is located)
Write-Host "   Running storage speed test on D: drive..." -ForegroundColor Yellow
$TestFile = "D:\BuildWorkspace\storage_test_temp.dat"
$TestSize = 100MB

# Write Test
$WriteStart = Get-Date
$TestData = New-Object byte[] $TestSize
[System.IO.File]::WriteAllBytes($TestFile, $TestData)
$WriteEnd = Get-Date
$WriteTime = ($WriteEnd - $WriteStart).TotalSeconds
$WriteSpeed = [math]::Round(($TestSize / 1MB) / $WriteTime, 2)

# Read Test
$ReadStart = Get-Date
$ReadData = [System.IO.File]::ReadAllBytes($TestFile)
$ReadEnd = Get-Date
$ReadTime = ($ReadEnd - $ReadStart).TotalSeconds
$ReadSpeed = [math]::Round(($TestSize / 1MB) / $ReadTime, 2)

# Cleanup
Remove-Item $TestFile -Force

"Storage Speed Test (D: Drive):" | Out-File -FilePath $ReportFile -Append
"Write Speed: $WriteSpeed MB/s" | Out-File -FilePath $ReportFile -Append
"Read Speed: $ReadSpeed MB/s" | Out-File -FilePath $ReportFile -Append

# 5. NETWORK ANALYSIS
Write-Host "5. Network Performance Analysis" -ForegroundColor White
"`nNETWORK PERFORMANCE" | Out-File -FilePath $ReportFile -Append

# Network Adapter Information
Get-WmiObject -Class Win32_NetworkAdapter | Where-Object {$_.NetConnectionStatus -eq 2 -and $_.Name -like "*Ethernet*"} | Select-Object Name, Speed | Out-File -FilePath $ReportFile -Append

# Network Speed Test (internal)
Write-Host "   Testing network throughput..." -ForegroundColor Yellow
$NetworkCounters = Get-Counter "\Network Interface(*)\Bytes Total/sec" | Where-Object {$_.CounterSamples.InstanceName -notlike "*Loopback*" -and $_.CounterSamples.InstanceName -notlike "*Isatap*"}
"Current Network Utilization:" | Out-File -FilePath $ReportFile -Append
$NetworkCounters.CounterSamples | Select-Object InstanceName, @{
    Name="BytesPerSec"; Expression={[math]::Round($_.CookedValue/1MB, 2)}
} | Out-File -FilePath $ReportFile -Append

# 6. GPU ANALYSIS (for potential GPU acceleration)
Write-Host "6. GPU Analysis" -ForegroundColor White
"`nGPU CONFIGURATION" | Out-File -FilePath $ReportFile -Append

Get-WmiObject -Class Win32_VideoController | Where-Object {$_.Name -notlike "*Basic*"} | Select-Object Name, @{
    Name="MemoryGB"; Expression={[math]::Round($_.AdapterRAM/1GB, 2)}
}, VideoModeDescription | Out-File -FilePath $ReportFile -Append

# 7. TRADING PLATFORM SPECIFIC TESTS
Write-Host "7. Trading Platform Performance Tests" -ForegroundColor White
"`nTRADING PLATFORM PERFORMANCE" | Out-File -FilePath $ReportFile -Append

# .NET Performance Test
Write-Host "   Testing .NET 8.0 performance..." -ForegroundColor Yellow
$DotNetStart = Get-Date
dotnet --info | Out-Null
$DotNetEnd = Get-Date
$DotNetTime = ($DotNetEnd - $DotNetStart).TotalMilliseconds

"\.NET 8.0 Startup Time: $([math]::Round($DotNetTime, 2))ms" | Out-File -FilePath $ReportFile -Append

# Test Build Performance
if (Test-Path "D:\BuildWorkspace\DayTradinPlatform.sln") {
    Write-Host "   Testing solution build performance..." -ForegroundColor Yellow
    $BuildStart = Get-Date
    $BuildResult = dotnet build "D:\BuildWorkspace\DayTradinPlatform.sln" --configuration Release --verbosity quiet 2>&1
    $BuildEnd = Get-Date
    $BuildTime = ($BuildEnd - $BuildStart).TotalSeconds
    
    "Solution Build Time: $([math]::Round($BuildTime, 2)) seconds" | Out-File -FilePath $ReportFile -Append
    if ($LASTEXITCODE -eq 0) {
        "Build Status: SUCCESS" | Out-File -FilePath $ReportFile -Append
    } else {
        "Build Status: FAILED" | Out-File -FilePath $ReportFile -Append
    }
}

# 8. PERFORMANCE RECOMMENDATIONS
Write-Host "8. Generating Performance Recommendations" -ForegroundColor White
"`nPERFORMANCE RECOMMENDATIONS" | Out-File -FilePath $ReportFile -Append

$Recommendations = @()

# RAM Analysis
$MemUsageRounded = [math]::Round($MemUsagePercent, 1)
if ($MemUsagePercent -gt 80) {
    $MemMessage = "WARNING: HIGH MEMORY USAGE " + $MemUsageRounded.ToString() + " PCT - Consider upgrading RAM"
    $Recommendations += $MemMessage
} elseif ($MemUsagePercent -lt 50) {
    $MemMessage = "PASS: Memory usage optimal " + $MemUsageRounded.ToString() + " PCT"
    $Recommendations += $MemMessage
} else {
    $MemMessage = "INFO: Memory usage acceptable " + $MemUsageRounded.ToString() + " PCT"
    $Recommendations += $MemMessage
}

# CPU Analysis
$AvgCPURounded = [math]::Round($AvgCPU, 1)
if ($AvgCPU -gt 70) {
    $CPUMessage = "WARNING: HIGH CPU USAGE " + $AvgCPURounded.ToString() + " PCT - Check background processes"
    $Recommendations += $CPUMessage
} else {
    $CPUMessage = "PASS: CPU performance optimal " + $AvgCPURounded.ToString() + " PCT average"
    $Recommendations += $CPUMessage
}

# Storage Analysis
if ($WriteSpeed -lt 100) {
    $StorageMessage = "WARNING: SLOW STORAGE Write speed " + $WriteSpeed.ToString() + " MB/s - Consider NVMe upgrade"
    $Recommendations += $StorageMessage
} else {
    $StorageMessage = "PASS: Storage performance good Write " + $WriteSpeed.ToString() + " MB/s Read " + $ReadSpeed.ToString() + " MB/s"
    $Recommendations += $StorageMessage
}

# Trading Platform Specific
$BuildTimeRounded = [math]::Round($BuildTime, 1)
if ($BuildTime -gt 30) {
    $BuildMessage = "WARNING: SLOW BUILD TIME " + $BuildTimeRounded.ToString() + " seconds - Consider SSD upgrade or more RAM"
    $Recommendations += $BuildMessage
} else {
    $BuildMessage = "PASS: Build performance acceptable " + $BuildTimeRounded.ToString() + " seconds"
    $Recommendations += $BuildMessage
}

$Recommendations += ""
$Recommendations += "TRADING PLATFORM OPTIMIZATION SUGGESTIONS:"
$Recommendations += "• Ensure Windows 11 is in High Performance mode"
$Recommendations += "• Disable Windows Defender real-time scanning for D:\BuildWorkspace"
$Recommendations += "• Set trading application CPU affinity to P-cores (0-15)"
$Recommendations += "• Configure Windows Timer Resolution to 1ms for low latency"
$Recommendations += "• Monitor memory usage during market hours with 4-screen setup"

$Recommendations | Out-File -FilePath $ReportFile -Append

# Output final results
Write-Host ""
Write-Host "Performance Analysis Complete!" -ForegroundColor Green
Write-Host "Report saved to: $ReportFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Key Metrics:" -ForegroundColor Yellow
Write-Host ("• CPU Usage: " + $AvgCPURounded.ToString() + " PCT") -ForegroundColor White
Write-Host ("• Memory Usage: " + $MemUsageRounded.ToString() + " PCT") -ForegroundColor White  
Write-Host ("• Storage Write: " + $WriteSpeed.ToString() + " MB/s") -ForegroundColor White
Write-Host ("• Storage Read: " + $ReadSpeed.ToString() + " MB/s") -ForegroundColor White
Write-Host ("• Build Time: " + $BuildTimeRounded.ToString() + " seconds") -ForegroundColor White

Write-Host ""
Write-Host "Performance report ready for analysis!" -ForegroundColor Green
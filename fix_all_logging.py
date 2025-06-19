#!/usr/bin/env python3
"""
Comprehensive script to eliminate ALL Microsoft.Extensions.Logging usage 
and replace with TradingPlatform.Core.Interfaces.ILogger
"""

import os
import re
import sys

# Files to process (excluding TradingPlatform.Logging project which legitimately uses Microsoft logging)
files_to_fix = [
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/Program.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.WindowsOptimization/Services/SystemMonitor.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.WindowsOptimization/Services/ProcessManager.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.WindowsOptimization/Services/WindowsOptimizationService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.TradingApp/Views/Settings/MonitorSelectionView.xaml.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/NewsCriteria.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/GapCriteria.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolatilityCriteria.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Criteria/VolumeCriteria.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/ScreeningOrchestrator.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Engines/RealTimeScreeningEngine.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Services/CriteriaConfigurationService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Models/ScreeningRequest.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Screening/Indicators/TechnicalIndicators.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Services/RedisMessageBus.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Messaging/Extensions/ServiceCollectionExtensions.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/Services/DisplaySessionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/Services/MonitorDetectionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/Services/MockGpuDetectionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/Services/GpuDetectionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.DisplayManagement/Services/MockMonitorDetectionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/Services/SubscriptionManager.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/Services/MarketDataService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.MarketData/Services/MarketDataCache.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/Services/HealthMonitor.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/Services/GatewayOrchestrator.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Gateway/Services/ProcessManager.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Testing/Mocks/MockMessageBus.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Testing/Examples/MockMessageBusExamples.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Testing/Utilities/MessageBusTestHelpers.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Services/PerformanceTracker.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Services/StrategyExecutionService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Services/StrategyManager.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Strategies/MomentumStrategy.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Strategies/GoldenRulesStrategy.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.StrategyEngine/Strategies/GapStrategy.cs"
]

def fix_logging_in_file(file_path):
    """Fix Microsoft.Extensions.Logging usage in a single file"""
    try:
        if not os.path.exists(file_path):
            print(f"❌ File not found: {file_path}")
            return False
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        changes_made = []
        
        # 1. Replace using statement
        if 'using Microsoft.Extensions.Logging;' in content:
            content = content.replace('using Microsoft.Extensions.Logging;', 'using TradingPlatform.Core.Interfaces;')
            changes_made.append("using statement")
        
        # 2. Replace ILogger<ClassName> with ILogger in field declarations
        logger_field_pattern = r'private readonly ILogger<(\w+)> _logger;'
        if re.search(logger_field_pattern, content):
            content = re.sub(logger_field_pattern, r'private readonly ILogger _logger;', content)
            changes_made.append("field declaration")
        
        # 3. Replace ILogger<ClassName> with ILogger in constructor parameters
        constructor_param_pattern = r'ILogger<(\w+)> logger'
        if re.search(constructor_param_pattern, content):
            content = re.sub(constructor_param_pattern, r'ILogger logger', content)
            changes_made.append("constructor parameter")
        
        # 4. Replace any remaining ILogger<Something> patterns
        remaining_generic_pattern = r'ILogger<[^>]+>'
        if re.search(remaining_generic_pattern, content):
            content = re.sub(remaining_generic_pattern, 'ILogger', content)
            changes_made.append("remaining generic patterns")
        
        # Write back if changes were made
        if content != original_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"✅ Fixed {file_path} - Changes: {', '.join(changes_made)}")
            return True
        else:
            print(f"ℹ️ No changes needed: {file_path}")
            return False
            
    except Exception as e:
        print(f"❌ Error processing {file_path}: {e}")
        return False

def main():
    print("🚀 Starting comprehensive Microsoft.Extensions.Logging elimination...")
    print(f"📁 Processing {len(files_to_fix)} files")
    
    fixed_count = 0
    error_count = 0
    
    for file_path in files_to_fix:
        success = fix_logging_in_file(file_path)
        if success:
            fixed_count += 1
        elif not os.path.exists(file_path):
            error_count += 1
    
    print(f"\n🎯 SUMMARY:")
    print(f"✅ Files fixed: {fixed_count}")
    print(f"ℹ️ Files unchanged: {len(files_to_fix) - fixed_count - error_count}")
    print(f"❌ Files with errors: {error_count}")
    
    if error_count == 0 and fixed_count > 0:
        print(f"\n🎉 SUCCESS: All Microsoft.Extensions.Logging usage eliminated!")
    elif error_count == 0:
        print(f"\n✅ All files already compliant")
    else:
        print(f"\n⚠️ Some files had errors - manual review needed")

if __name__ == "__main__":
    main()
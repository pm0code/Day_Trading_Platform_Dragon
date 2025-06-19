#!/usr/bin/env python3
"""
Fix remaining generic ILogger<T> usage in PaperTrading and RiskManagement
"""

import os
import re

files_to_fix = [
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/OrderBookSimulator.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/OrderProcessingBackgroundService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/OrderExecutionEngine.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/PortfolioManager.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/SlippageCalculator.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/PaperTradingService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.PaperTrading/Services/ExecutionAnalytics.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/PositionMonitor.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/RiskManagementService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/RiskAlertService.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/RiskCalculator.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/ComplianceMonitor.cs",
    "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services/RiskMonitoringBackgroundService.cs"
]

def fix_generic_logger_in_file(file_path):
    """Fix generic ILogger<T> usage in a single file"""
    try:
        if not os.path.exists(file_path):
            print(f"❌ File not found: {file_path}")
            return False
            
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        changes_made = []
        
        # Check if this file uses our custom ILogger interface already
        if 'using TradingPlatform.Core.Interfaces;' not in content:
            # Add the using statement if not present
            using_pattern = r'(using [^;]+;\s*\n)*'
            match = re.search(using_pattern, content)
            if match:
                insert_position = match.end()
                content = content[:insert_position] + 'using TradingPlatform.Core.Interfaces;\n' + content[insert_position:]
                changes_made.append("added using statement")
        
        # Replace ILogger<ClassName> with ILogger in field declarations
        logger_field_pattern = r'private readonly ILogger<(\w+)> _logger;'
        if re.search(logger_field_pattern, content):
            content = re.sub(logger_field_pattern, r'private readonly ILogger _logger;', content)
            changes_made.append("field declaration")
        
        # Replace ILogger<ClassName> with ILogger in constructor parameters
        constructor_param_pattern = r'ILogger<(\w+)> logger'
        if re.search(constructor_param_pattern, content):
            content = re.sub(constructor_param_pattern, r'ILogger logger', content)
            changes_made.append("constructor parameter")
        
        # Replace any remaining ILogger<Something> patterns
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
    print("🚀 Fixing remaining generic ILogger<T> usage...")
    print(f"📁 Processing {len(files_to_fix)} files")
    
    fixed_count = 0
    error_count = 0
    
    for file_path in files_to_fix:
        success = fix_generic_logger_in_file(file_path)
        if success:
            fixed_count += 1
        elif not os.path.exists(file_path):
            error_count += 1
    
    print(f"\n🎯 SUMMARY:")
    print(f"✅ Files fixed: {fixed_count}")
    print(f"ℹ️ Files unchanged: {len(files_to_fix) - fixed_count - error_count}")
    print(f"❌ Files with errors: {error_count}")
    
    if error_count == 0 and fixed_count > 0:
        print(f"\n🎉 SUCCESS: All generic ILogger<T> usage eliminated!")
    elif error_count == 0:
        print(f"\n✅ All files already compliant")

if __name__ == "__main__":
    main()
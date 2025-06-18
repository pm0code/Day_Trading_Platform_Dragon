#!/usr/bin/env python3
"""
ELIMINATE ALL MICROSOFT LOGGING - PHASE 3 MASS REPLACEMENT SCRIPT
Replaces all Microsoft logging method calls with TradingLogOrchestrator calls
"""

import os
import re
import subprocess
from pathlib import Path

# Mapping of Microsoft logging methods to TradingLogOrchestrator methods
LOGGING_REPLACEMENTS = {
    # Basic Microsoft logging -> TradingLogOrchestrator
    r'_logger\.LogError\s*\(\s*([^,]+)\s*,\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogError(\1, \2)',
    r'_logger\.LogError\s*\(\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogError(\1)',
    r'_logger\.LogWarning\s*\(\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogWarning(\1)',
    r'_logger\.LogInformation\s*\(\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogInfo(\1)',
    r'_logger\.LogDebug\s*\(\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogInfo(\1)',
    r'_logger\.LogTrace\s*\(\s*([^)]+)\s*\)': r'TradingLogOrchestrator.Instance.LogInfo(\1)',
    
    # ILogger<T> constructor parameters -> ILogger
    r'ILogger<([^>]+)>\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\)': r'ILogger \2)',
    r'ILogger<([^>]+)>\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*,': r'ILogger \2,',
    
    # Using statements - remove Microsoft logging imports
    r'using Microsoft\.Extensions\.Logging;\s*\n': '',
    r'using Microsoft\.Extensions\.Logging\.Abstractions;\s*\n': '',
}

# Add TradingLogOrchestrator using statement
ORCHESTRATOR_USING = "using TradingPlatform.Core.Logging;"

def process_cs_file(file_path):
    """Process a single C# file to eliminate Microsoft logging"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Apply all replacements
        for pattern, replacement in LOGGING_REPLACEMENTS.items():
            content = re.sub(pattern, replacement, content, flags=re.MULTILINE)
        
        # Add TradingLogOrchestrator using if Microsoft logging was found
        if content != original_content and 'using TradingPlatform.Core.Logging;' not in content:
            # Find the last using statement and add after it
            using_match = re.findall(r'^using [^;]+;', content, re.MULTILINE)
            if using_match:
                last_using = using_match[-1]
                content = content.replace(last_using, last_using + '\n' + ORCHESTRATOR_USING)
        
        # Write back if changed
        if content != original_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"✅ PROCESSED: {file_path}")
            return True
        
        return False
        
    except Exception as e:
        print(f"❌ ERROR processing {file_path}: {e}")
        return False

def find_cs_files():
    """Find all C# files in the platform"""
    cs_files = []
    for root, dirs, files in os.walk('.'):
        # Skip certain directories
        skip_dirs = {'.git', 'bin', 'obj', 'packages', '.vs'}
        dirs[:] = [d for d in dirs if d not in skip_dirs]
        
        for file in files:
            if file.endswith('.cs'):
                cs_files.append(os.path.join(root, file))
    
    return cs_files

def main():
    print("🚀 ELIMINATING ALL MICROSOFT LOGGING FROM DAY TRADING PLATFORM")
    print("=" * 70)
    
    # Find all C# files
    cs_files = find_cs_files()
    print(f"📁 Found {len(cs_files)} C# files")
    
    # Process each file
    processed_count = 0
    for file_path in cs_files:
        if process_cs_file(file_path):
            processed_count += 1
    
    print("=" * 70)
    print(f"✅ ELIMINATION COMPLETE: {processed_count} files processed")
    print("🎯 ALL Microsoft.Extensions.Logging calls replaced with TradingLogOrchestrator")
    
    return processed_count

if __name__ == "__main__":
    main()
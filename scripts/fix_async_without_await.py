#!/usr/bin/env python3
"""
Fix CS1998 warnings - async methods without await
"""

import re
import sys
from pathlib import Path

def fix_async_without_await(file_path: Path) -> int:
    """Fix async methods that don't use await by removing async and using Task.FromResult"""
    
    try:
        content = file_path.read_text(encoding='utf-8')
        original_content = content
        fixes = 0
        
        # Pattern to find async methods that return Task<bool>
        pattern = r'public async Task<bool> (\w+)\([^)]*\)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}'
        
        def replace_method(match):
            nonlocal fixes
            method_name = match.group(1)
            method_body = match.group(2)
            
            # Check if method body contains 'await'
            if 'await' in method_body:
                return match.group(0)  # Don't change methods that use await
            
            # Replace 'return true;' with 'return Task.FromResult(true);'
            new_body = method_body.replace('return true;', 'return Task.FromResult(true);')
            new_body = new_body.replace('return false;', 'return Task.FromResult(false);')
            
            # Remove async keyword
            result = f'public Task<bool> {method_name}({match.group(0).split("(")[1].split(")")[0]}){{{new_body}}}'
            
            if result != match.group(0):
                fixes += 1
                print(f"  Fixed async method: {method_name}")
            
            return result
        
        # Apply fixes
        content = re.sub(pattern, replace_method, content, flags=re.DOTALL)
        
        # Pattern for async Task (no return value)
        pattern2 = r'public async Task (\w+)\([^)]*\)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}'
        
        def replace_void_method(match):
            nonlocal fixes
            method_name = match.group(1)
            method_body = match.group(2)
            
            # Check if method body contains 'await'
            if 'await' in method_body:
                return match.group(0)
            
            # Add return Task.CompletedTask at the end if not present
            if 'return' not in method_body:
                new_body = method_body.rstrip() + '\n        return Task.CompletedTask;\n    '
            else:
                new_body = method_body.replace('return;', 'return Task.CompletedTask;')
            
            # Remove async keyword
            result = f'public Task {method_name}({match.group(0).split("(")[1].split(")")[0]}){{{new_body}}}'
            
            if result != match.group(0):
                fixes += 1
                print(f"  Fixed async void method: {method_name}")
            
            return result
        
        content = re.sub(pattern2, replace_void_method, content, flags=re.DOTALL)
        
        # Write back if changed
        if content != original_content:
            file_path.write_text(content, encoding='utf-8')
            print(f"Fixed {file_path.name}: {fixes} methods")
            return fixes
        
        return 0
    except Exception as e:
        print(f"Error processing {file_path}: {e}")
        return 0

def main():
    """Main entry point"""
    base_path = Path('/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform')
    
    # Files identified with CS1998 warnings
    target_files = [
        'TradingPlatform.WindowsOptimization/Services/WindowsOptimizationService.cs',
        'TradingPlatform.WindowsOptimization/Services/SystemMonitor.cs',
        'TradingPlatform.DisplayManagement/Services/MonitorDetectionService.cs',
        'TradingPlatform.DataIngestion/RateLimiting/ApiRateLimiter.cs',
        'TradingPlatform.DataIngestion/Services/CacheService.cs'
    ]
    
    total_fixes = 0
    
    for file_rel_path in target_files:
        file_path = base_path / file_rel_path
        if file_path.exists():
            fixes = fix_async_without_await(file_path)
            total_fixes += fixes
        else:
            print(f"File not found: {file_path}")
    
    print(f"\nTotal fixes applied: {total_fixes}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
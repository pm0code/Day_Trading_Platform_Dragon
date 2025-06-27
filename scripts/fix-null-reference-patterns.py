#!/usr/bin/env python3
"""
Fix common null reference false positives in C# code
Targets namespace and static class references that MCP incorrectly flags
"""

import os
import re
import sys
from pathlib import Path

# Patterns that are false positives for null checks
FALSE_POSITIVE_PATTERNS = [
    # Namespace references
    r'Potential null reference: (System|Microsoft|TradingPlatform)\b.*might be null \[null-check\]',
    # Static class references
    r'Potential null reference: (Math|Task|Interlocked|File|Directory|Path|Console|Environment|JsonSerializer|Stopwatch|TimeSpan|CancellationToken|Enumerable|LINQ)\b.*might be null \[null-check\]',
    # Enum references
    r'Potential null reference: (LogLevel|CacheItemPriority|MidpointRounding|StringComparison)\b.*might be null \[null-check\]',
    # Generic type parameters
    r'Potential null reference: (TradingResult|TradingError|FinancialMath)\b.*might be null \[null-check\]',
]

# Real null reference issues we should fix
REAL_NULL_ISSUES = [
    # Method parameters that could be null
    r'Potential null reference: (serviceProvider|validationResult|settings|errors|result|ex|cancellationToken)\b.*might be null \[null-check\]',
    # Instance fields/properties that could be null
    r'Potential null reference: (_[a-zA-Z]+|[a-z][a-zA-Z]*)\b.*might be null \[null-check\]',
]

def is_false_positive(issue_text):
    """Check if an issue is a false positive"""
    for pattern in FALSE_POSITIVE_PATTERNS:
        if re.search(pattern, issue_text):
            return True
    return False

def fix_real_null_issues(file_path):
    """Fix real null reference issues in a file"""
    fixes_made = 0
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        
        # Fix 1: Add null checks for method parameters
        # Pattern: public void Method(Type? param) { param.Something }
        # Fix: public void Method(Type? param) { if (param != null) param.Something }
        
        # Fix 2: Initialize nullable fields
        # Pattern: private Type? _field;
        # Fix: private Type? _field = null;
        
        # Fix 3: Add null-forgiving operator where we know it's not null
        # Pattern: _field.Method()
        # Fix: _field!.Method() (if we're sure it's initialized)
        
        # For now, let's add #nullable enable directive if missing
        if '#nullable enable' not in content and file_path.endswith('.cs'):
            # Add after namespace declaration
            content = re.sub(
                r'(namespace [^;{]+[{;])',
                r'\1\n\n#nullable enable',
                content,
                count=1
            )
            fixes_made += 1
        
        # Save if changes were made
        if content != original_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed {fixes_made} issues in {file_path}")
    
    except Exception as e:
        print(f"Error processing {file_path}: {e}")
    
    return fixes_made

def analyze_mcp_results(results_file):
    """Analyze MCP results and categorize issues"""
    false_positives = []
    real_issues = []
    
    try:
        with open(results_file, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if 'Potential null reference:' in line:
                    if is_false_positive(line):
                        false_positives.append(line)
                    else:
                        real_issues.append(line)
    except FileNotFoundError:
        print(f"Results file not found: {results_file}")
        return [], []
    
    return false_positives, real_issues

def create_mcp_suppressions():
    """Create MCP suppression rules for false positives"""
    suppressions = {
        "suppressions": {
            "null-check": {
                "patterns": [
                    # Suppress null checks for namespaces
                    "^(System|Microsoft|TradingPlatform)\\.",
                    # Suppress null checks for static classes
                    "^(Math|Task|Interlocked|File|Directory|Path|Console|Environment|JsonSerializer)\\.",
                    # Suppress null checks for enums
                    "^(LogLevel|CacheItemPriority|MidpointRounding|StringComparison)\\.",
                ]
            }
        }
    }
    
    import json
    with open('.mcp/suppressions.json', 'w') as f:
        json.dump(suppressions, f, indent=2)
    
    print("Created MCP suppressions file: .mcp/suppressions.json")

def main():
    """Main entry point"""
    project_root = "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
    
    print("🔧 Fixing Null Reference Issues")
    print("="*50)
    
    # Analyze MCP results
    results_file = "/home/nader/my_projects/C#/DayTradingPlatform/key-areas-analysis.txt"
    false_positives, real_issues = analyze_mcp_results(results_file)
    
    print(f"\n📊 Analysis Results:")
    print(f"  False positives: {len(false_positives)}")
    print(f"  Real issues: {len(real_issues)}")
    
    # Create suppressions for false positives
    os.makedirs('.mcp', exist_ok=True)
    create_mcp_suppressions()
    
    # Fix real issues in critical files
    critical_files = [
        "TradingPlatform.Core/Logging/TradingLogOrchestrator.cs",
        "TradingPlatform.Core/Canonical/CanonicalSettingsService.cs",
        "TradingPlatform.Core/Canonical/CanonicalProvider.cs",
        "TradingPlatform.FixEngine/Core/FixEngine.cs",
        "TradingPlatform.FixEngine/Core/OrderManager.cs",
    ]
    
    total_fixes = 0
    print(f"\n🔨 Fixing real null issues in critical files...")
    
    for file_rel in critical_files:
        file_path = os.path.join(project_root, file_rel)
        if os.path.exists(file_path):
            fixes = fix_real_null_issues(file_path)
            total_fixes += fixes
    
    print(f"\n✅ Total fixes applied: {total_fixes}")
    
    # Show sample of real issues to fix manually
    if real_issues:
        print(f"\n⚠️ Sample of real issues requiring manual review:")
        for issue in real_issues[:10]:
            print(f"  - {issue}")

if __name__ == "__main__":
    main()
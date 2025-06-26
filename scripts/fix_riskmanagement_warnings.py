#!/usr/bin/env python3
"""
Fix CA warnings in RiskManagement canonical files
"""

import re
import os

def fix_ca_warnings(file_path):
    """Fix code analysis warnings in a file"""
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_content = content
    
    # Fix CA1860: Replace .Any() with .Count > 0 for lists
    # Pattern 1: if (!list.Any())
    content = re.sub(
        r'if \(!(\w+List|reasons|positionsList|violations)\.Any\(\)\)',
        r'if (\1.Count == 0)',
        content
    )
    
    # Pattern 2: if (list.Any())
    content = re.sub(
        r'if \((reasons|violations|returns)\.Any\(\)\)',
        r'if (\1.Count > 0)',
        content
    )
    
    # Pattern 3: = list.Any()
    content = re.sub(
        r'= (violations)\.Any\(\)',
        r'= \1.Count > 0',
        content
    )
    
    # Fix CA1062: Add null parameter checks
    # For public async methods with IEnumerable parameters
    null_check_methods = [
        ('CalculateVaRAsync', 'returns'),
        ('CalculateExpectedShortfallAsync', 'returns'),
        ('CalculateMaxDrawdownAsync', 'portfolioValues'),
        ('CalculateSharpeRatioAsync', 'returns'),
        ('CalculateBetaAsync', 'assetReturns'),
        ('CalculatePortfolioRisk', 'positions'),
        ('UpdatePositionAsync', 'position'),
        ('ValidateRegulatoryLimitsAsync', 'request'),
        ('LogComplianceEventAsync', 'complianceEvent')
    ]
    
    for method_name, param_name in null_check_methods:
        # Find the method declaration
        method_pattern = rf'(public\s+(?:async\s+)?(?:override\s+)?Task[<\w>]*\s+{method_name}\s*\([^)]*{param_name}[^)]*\)\s*\n\s*{{)'
        
        def add_null_check(match):
            method_decl = match.group(1)
            # Check if null check already exists
            check_pos = match.end()
            next_lines = content[check_pos:check_pos+200]
            if f'if ({param_name} == null)' in next_lines or f'ArgumentNullException(nameof({param_name}))' in next_lines:
                return method_decl
            
            # Add null check
            indent = '            '
            null_check = f'\n{indent}if ({param_name} == null) throw new ArgumentNullException(nameof({param_name}));\n'
            return method_decl + null_check
        
        content = re.sub(method_pattern, add_null_check, content)
    
    # Special case for CalculateBetaAsync which has two parameters
    beta_pattern = r'(public\s+async\s+Task<decimal>\s+CalculateBetaAsync\s*\([^)]*assetReturns[^)]*,\s*[^)]*marketReturns[^)]*\)\s*\n\s*{{)'
    def add_beta_null_checks(match):
        method_decl = match.group(1)
        check_pos = match.end()
        next_lines = content[check_pos:check_pos+400]
        
        checks_needed = []
        if 'if (assetReturns == null)' not in next_lines and 'ArgumentNullException(nameof(assetReturns))' not in next_lines:
            checks_needed.append('assetReturns')
        if 'if (marketReturns == null)' not in next_lines and 'ArgumentNullException(nameof(marketReturns))' not in next_lines:
            checks_needed.append('marketReturns')
        
        if not checks_needed:
            return method_decl
        
        indent = '            '
        null_checks = ''
        for param in checks_needed:
            null_checks += f'\n{indent}if ({param} == null) throw new ArgumentNullException(nameof({param}));'
        
        return method_decl + null_checks + '\n'
    
    content = re.sub(beta_pattern, add_beta_null_checks, content)
    
    # Write back if changed
    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        return True
    return False

def main():
    """Main function"""
    
    base_path = '/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.RiskManagement/Services'
    
    files_to_fix = [
        'RiskCalculatorCanonical.cs',
        'PositionMonitorCanonical.cs',
        'ComplianceMonitorCanonical.cs'
    ]
    
    for file_name in files_to_fix:
        file_path = os.path.join(base_path, file_name)
        if os.path.exists(file_path):
            if fix_ca_warnings(file_path):
                print(f"Fixed warnings in {file_name}")
            else:
                print(f"No changes needed in {file_name}")
        else:
            print(f"File not found: {file_path}")

if __name__ == '__main__':
    main()
#!/usr/bin/env python3
"""
Fix financial precision issues by replacing float/double with decimal in C# code
This is critical for financial accuracy in the Day Trading Platform
"""

import os
import re
import sys
from pathlib import Path

# Financial-related keywords that should use decimal
FINANCIAL_KEYWORDS = [
    'price', 'amount', 'money', 'cost', 'value', 'profit', 'loss',
    'balance', 'equity', 'capital', 'revenue', 'income', 'expense',
    'fee', 'commission', 'premium', 'strike', 'bid', 'ask', 'spread',
    'ratio', 'rate', 'yield', 'return', 'performance', 'score',
    'volatility', 'beta', 'alpha', 'sharpe', 'liquidity', 'volume',
    'eps', 'pe', 'roe', 'roa', 'margin', 'dividend', 'market'
]

def should_use_decimal(context):
    """Determine if a float/double should be replaced with decimal based on context"""
    context_lower = context.lower()
    
    # Check for financial keywords
    for keyword in FINANCIAL_KEYWORDS:
        if keyword in context_lower:
            return True
    
    # Check for specific patterns
    if re.search(r'(financial|trading|stock|portfolio|order|position)', context_lower):
        return True
    
    return False

def fix_float_to_decimal(file_path):
    """Fix float/double to decimal in a C# file"""
    fixes_made = 0
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        lines = content.split('\n')
        
        for i, line in enumerate(lines):
            # Skip comments and strings
            if line.strip().startswith('//') or '"""' in line:
                continue
            
            # Pattern 1: Property/field declarations
            # float Price { get; set; } -> decimal Price { get; set; }
            pattern1 = r'\b(public|private|protected|internal)?\s*(float|double)\s+(\w+)'
            matches = re.finditer(pattern1, line)
            
            for match in matches:
                full_match = match.group(0)
                property_name = match.group(3)
                
                # Get context (current line + surrounding lines)
                context_start = max(0, i - 2)
                context_end = min(len(lines), i + 3)
                context = ' '.join(lines[context_start:context_end])
                
                if should_use_decimal(property_name + ' ' + context):
                    new_declaration = full_match.replace('float', 'decimal').replace('double', 'decimal')
                    lines[i] = lines[i].replace(full_match, new_declaration)
                    fixes_made += 1
            
            # Pattern 2: Method parameters and return types
            # Task<float> CalculatePrice() -> Task<decimal> CalculatePrice()
            pattern2 = r'<(float|double)>'
            if re.search(pattern2, line):
                # Check if it's in a financial context
                if should_use_decimal(line):
                    lines[i] = re.sub(r'<float>', '<decimal>', lines[i])
                    lines[i] = re.sub(r'<double>', '<decimal>', lines[i])
                    fixes_made += 1
            
            # Pattern 3: Dictionary and collection types
            # Dictionary<string, float> -> Dictionary<string, decimal>
            pattern3 = r'Dictionary<([^,]+),\s*(float|double)>'
            if re.search(pattern3, line):
                if should_use_decimal(line):
                    lines[i] = re.sub(r'Dictionary<([^,]+),\s*float>', r'Dictionary<\1, decimal>', lines[i])
                    lines[i] = re.sub(r'Dictionary<([^,]+),\s*double>', r'Dictionary<\1, decimal>', lines[i])
                    fixes_made += 1
            
            # Pattern 4: Array declarations
            # float[] prices -> decimal[] prices
            pattern4 = r'\b(float|double)\[\]'
            if re.search(pattern4, line):
                if should_use_decimal(line):
                    lines[i] = re.sub(r'\bfloat\[\]', 'decimal[]', lines[i])
                    lines[i] = re.sub(r'\bdouble\[\]', 'decimal[]', lines[i])
                    fixes_made += 1
            
            # Pattern 5: Literal values (add 'm' suffix for decimal literals)
            # = 0.1f -> = 0.1m
            pattern5 = r'=\s*(\d+\.?\d*)f\b'
            if re.search(pattern5, line):
                if should_use_decimal(line):
                    lines[i] = re.sub(r'=\s*(\d+\.?\d*)f\b', r'= \1m', lines[i])
                    fixes_made += 1
        
        # Join lines back
        new_content = '\n'.join(lines)
        
        # Save if changes were made
        if new_content != original_content:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"✅ Fixed {fixes_made} precision issues in {file_path}")
            return fixes_made
    
    except Exception as e:
        print(f"❌ Error processing {file_path}: {e}")
        return 0
    
    return fixes_made

def scan_and_fix_project(project_root):
    """Scan entire project and fix financial precision issues"""
    total_fixes = 0
    files_fixed = 0
    
    # Priority files to fix first
    priority_patterns = [
        '**/Models/*.cs',
        '**/Interfaces/*.cs',
        '**/Core/Mathematics/*.cs',
        '**/ML/Algorithms/**/*.cs',
        '**/FixEngine/**/*.cs',
    ]
    
    all_cs_files = []
    
    # Collect all C# files
    for root, dirs, files in os.walk(project_root):
        # Skip bin and obj directories
        if 'bin' in root or 'obj' in root:
            continue
        
        for file in files:
            if file.endswith('.cs'):
                all_cs_files.append(os.path.join(root, file))
    
    print(f"📊 Found {len(all_cs_files)} C# files to analyze")
    
    # Process files
    for file_path in all_cs_files:
        rel_path = os.path.relpath(file_path, project_root)
        fixes = fix_float_to_decimal(file_path)
        
        if fixes > 0:
            total_fixes += fixes
            files_fixed += 1
    
    return total_fixes, files_fixed

def create_decimal_utilities():
    """Create utility methods for decimal operations"""
    utilities_content = '''using System;

namespace TradingPlatform.Core.Mathematics
{
    /// <summary>
    /// Decimal math utilities for financial calculations
    /// Ensures precision for monetary operations
    /// </summary>
    public static class DecimalMath
    {
        private const int DefaultPrecision = 6;
        
        /// <summary>
        /// Calculate square root of a decimal value
        /// </summary>
        public static decimal Sqrt(decimal value, int precision = DefaultPrecision)
        {
            if (value < 0)
                throw new ArgumentException("Cannot calculate square root of negative number");
            
            if (value == 0)
                return 0;
            
            decimal x = value;
            decimal lastX = 0;
            int iterations = 0;
            
            // Newton's method
            while (Math.Abs(x - lastX) > (decimal)Math.Pow(10, -precision) && iterations < 100)
            {
                lastX = x;
                x = (x + value / x) / 2;
                iterations++;
            }
            
            return Math.Round(x, precision);
        }
        
        /// <summary>
        /// Calculate power of a decimal value
        /// </summary>
        public static decimal Pow(decimal baseValue, int exponent)
        {
            if (exponent == 0)
                return 1;
            
            decimal result = 1;
            decimal absBase = Math.Abs(baseValue);
            int absExponent = Math.Abs(exponent);
            
            for (int i = 0; i < absExponent; i++)
            {
                result *= absBase;
            }
            
            if (baseValue < 0 && exponent % 2 != 0)
                result = -result;
            
            if (exponent < 0)
                result = 1 / result;
            
            return result;
        }
        
        /// <summary>
        /// Calculate exponential (e^x) for decimal
        /// </summary>
        public static decimal Exp(decimal value, int precision = DefaultPrecision)
        {
            // Using Taylor series expansion
            decimal sum = 1;
            decimal term = 1;
            
            for (int i = 1; i <= 100; i++)
            {
                term *= value / i;
                sum += term;
                
                if (Math.Abs(term) < (decimal)Math.Pow(10, -precision))
                    break;
            }
            
            return Math.Round(sum, precision);
        }
        
        /// <summary>
        /// Calculate natural logarithm for decimal
        /// </summary>
        public static decimal Log(decimal value, int precision = DefaultPrecision)
        {
            if (value <= 0)
                throw new ArgumentException("Logarithm undefined for non-positive values");
            
            // Convert to double for calculation, then back to decimal
            // This is acceptable for log calculations where extreme precision isn't critical
            double doubleValue = (double)value;
            double logResult = Math.Log(doubleValue);
            
            return Math.Round((decimal)logResult, precision);
        }
        
        /// <summary>
        /// Safe division with null/zero handling
        /// </summary>
        public static decimal SafeDivide(decimal numerator, decimal denominator, decimal defaultValue = 0)
        {
            return denominator == 0 ? defaultValue : numerator / denominator;
        }
        
        /// <summary>
        /// Calculate percentage change
        /// </summary>
        public static decimal PercentageChange(decimal oldValue, decimal newValue)
        {
            if (oldValue == 0)
                return newValue > 0 ? 100m : -100m;
            
            return ((newValue - oldValue) / Math.Abs(oldValue)) * 100m;
        }
        
        /// <summary>
        /// Round to specified decimal places using banker's rounding
        /// </summary>
        public static decimal RoundBankers(decimal value, int decimals)
        {
            return Math.Round(value, decimals, MidpointRounding.ToEven);
        }
    }
}
'''
    
    utilities_path = '/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform/TradingPlatform.Core/Mathematics/DecimalMath.cs'
    
    os.makedirs(os.path.dirname(utilities_path), exist_ok=True)
    
    with open(utilities_path, 'w', encoding='utf-8') as f:
        f.write(utilities_content)
    
    print(f"✅ Created DecimalMath utilities at {utilities_path}")

def main():
    """Main entry point"""
    project_root = "/home/nader/my_projects/C#/DayTradingPlatform/DayTradinPlatform"
    
    print("💰 Fixing Financial Precision Issues")
    print("="*50)
    print("Converting float/double to decimal for monetary values...")
    print("")
    
    # Create decimal utilities first
    create_decimal_utilities()
    
    # Fix precision issues
    total_fixes, files_fixed = scan_and_fix_project(project_root)
    
    print("")
    print("📊 Summary:")
    print(f"  Files fixed: {files_fixed}")
    print(f"  Total fixes: {total_fixes}")
    print("")
    
    if total_fixes > 0:
        print("⚠️  Important:")
        print("  1. Review the changes to ensure correctness")
        print("  2. Update any Math.* calls to use DecimalMath.*")
        print("  3. Add 'm' suffix to decimal literals (e.g., 0.1m)")
        print("  4. Run unit tests to verify calculations")
        print("  5. Update ML.NET code to handle decimal types")

if __name__ == "__main__":
    main()
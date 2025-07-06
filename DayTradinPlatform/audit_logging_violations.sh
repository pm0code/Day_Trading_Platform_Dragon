#!/bin/bash

# Script to audit all service files for LogMethodEntry violations
echo "=== COMPREHENSIVE AUDIT: LogMethodEntry Violations ==="
echo "Date: $(date)"
echo "=========================================="
echo

violations=0
total_files=0
total_missing_methods=0

# Find all service files (excluding tests and interfaces)
find . -type d -name "Services" -exec find {} -name "*.cs" -type f \; | grep -v "Test" | grep -v "Interface" | sort | while read file; do
    total_files=$((total_files + 1))
    
    # Check if file contains LogMethodEntry
    has_log_entry=$(grep -c "LogMethodEntry" "$file" 2>/dev/null || echo "0")
    
    # Count methods more accurately
    method_count=$(grep -E "^\s*(public|private|protected|internal).*\s+\w+\s*\(" "$file" 2>/dev/null | wc -l)
    
    # Count constructors
    constructor_count=$(grep -E "^\s*(public|private|protected|internal).*\s+$(basename "$file" .cs)\s*\(" "$file" 2>/dev/null | wc -l)
    
    # Total methods including constructors
    total_methods=$((method_count + constructor_count))
    
    # File size for priority assessment
    file_size=$(wc -l < "$file" 2>/dev/null || echo "0")
    
    if [ "$has_log_entry" -eq 0 ] && [ "$total_methods" -gt 0 ]; then
        violations=$((violations + 1))
        total_missing_methods=$((total_missing_methods + total_methods))
        
        # Determine priority
        if [ "$total_methods" -gt 20 ] || [ "$file_size" -gt 500 ]; then
            priority="CRITICAL"
        elif [ "$total_methods" -gt 10 ] || [ "$file_size" -gt 200 ]; then
            priority="HIGH"
        elif [ "$total_methods" -gt 5 ]; then
            priority="MEDIUM"
        else
            priority="LOW"
        fi
        
        echo "[$priority] VIOLATION: $file"
        echo "  - Methods needing logging: $total_methods"
        echo "  - File size: $file_size lines"
        echo "  - LogMethodEntry calls: $has_log_entry"
        echo
    fi
done

echo "=========================================="
echo "AUDIT SUMMARY:"
echo "Total service files analyzed: $total_files"
echo "Files with violations: $violations"
echo "Estimated methods needing logging: $total_missing_methods"
echo "=========================================="
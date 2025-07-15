#!/usr/bin/env bash
# Script to fix SA1200 violations - move using directives outside namespace
# AIRES Test Infrastructure StyleCop Fix - Phase 1

set -e

echo "=== AIRES Using Directive Fix Script ==="
echo "Fixing SA1200: Using directives should be outside namespace"
echo ""

# Function to fix a single file
fix_file() {
    local file=$1
    echo "Processing: $(basename $file)"
    
    # Create a temporary file
    local temp_file="${file}.tmp"
    
    # Process the file with awk
    awk '
    BEGIN { 
        in_header = 1
        collected_usings = ""
        namespace_found = 0
        print_blank = 0
    }
    
    # Preserve copyright header
    /^\/\/ <copyright/ { in_header = 1; print; next }
    /^\/\// && in_header { print; next }
    /^$/ && in_header { in_header = 0; print; next }
    
    # Collect using statements before namespace
    /^using / && !namespace_found {
        if (collected_usings != "") collected_usings = collected_usings "\n"
        collected_usings = collected_usings $0
        next
    }
    
    # When we hit namespace, print collected usings first
    /^namespace / {
        if (!namespace_found) {
            namespace_found = 1
            if (collected_usings != "") {
                print collected_usings
                print ""
            }
        }
        print
        next
    }
    
    # Skip using statements inside namespace
    /^using / && namespace_found { next }
    
    # Skip blank lines immediately after namespace
    /^$/ && print_blank { print_blank = 0; next }
    
    # Print everything else
    { print }
    
    ' "$file" > "$temp_file"
    
    # Add blank lines between System and other using groups
    awk '
    BEGIN { last_was_system = 0 }
    /^using System/ { last_was_system = 1; print; next }
    /^using / && last_was_system { 
        last_was_system = 0
        print ""
        print
        next
    }
    /^using / { last_was_system = 0; print; next }
    { last_was_system = 0; print }
    ' "$temp_file" > "${temp_file}.2"
    
    mv "${temp_file}.2" "$file"
    rm -f "$temp_file"
}

# Find all C# files in TestInfrastructure
TEST_INFRA_DIR="/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AIRES/tests/AIRES.TestInfrastructure"

if [ ! -d "$TEST_INFRA_DIR" ]; then
    echo "Error: TestInfrastructure directory not found!"
    exit 1
fi

# Process each .cs file
file_count=0
for file in "$TEST_INFRA_DIR"/*.cs; do
    if [ -f "$file" ]; then
        fix_file "$file"
        ((file_count++))
    fi
done

echo ""
echo "✅ Processed $file_count files"
echo "Next step: Run 'dotnet build' to verify improvements"
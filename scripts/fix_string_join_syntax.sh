#!/bin/bash

# Fix string.Join syntax errors where closing parenthesis is missing

echo "Fixing string.Join syntax errors..."

# Find all files with the pattern
files=$(find . -name "*.cs" -type f -exec grep -l 'string\.Join.*}")' {} \; | grep -v ".bak")

for file in $files; do
    echo "Processing: $file"
    
    # Fix patterns like: string.Join(", ", items}")  ->  string.Join(", ", items)}"
    sed -i 's/string\.Join(\([^)]*\)}/string\.Join(\1)}/g' "$file"
    
    # Fix patterns like: .Count(}"  ->  .Count()}"
    sed -i 's/\.Count(}/\.Count()}/g' "$file"
    
    echo "  ✓ Fixed syntax errors in $file"
done

echo "String.Join syntax fix complete!"
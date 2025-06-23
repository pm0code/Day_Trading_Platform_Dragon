#!/bin/bash

# Fix LogError parameter order issues in C# files
# This script fixes the pattern where LogError is called with placeholders and parameters in wrong order

echo "Starting LogError parameter fix..."

# Function to fix LogError calls in a file
fix_file() {
    local file="$1"
    echo "Processing: $file"
    
    # Create backup
    cp "$file" "$file.bak_logerror"
    
    # Use sed to fix patterns like:
    # LogError("Message {Placeholder}", value, ex) -> LogError($"Message {value}", ex)
    # LogError("Message {Placeholder}", value) -> LogError($"Message {value}")
    
    # First, handle cases with exception as last parameter
    sed -i -E 's/LogError\("([^"]+)\{([^}]+)\}([^"]*)"([[:space:]]*),([[:space:]]*)([^,]+),([[:space:]]*)ex\)/LogError($"\1{\6}\3", ex)/g' "$file"
    
    # Handle cases with multiple placeholders and exception
    sed -i -E 's/LogError\("([^"]+)\{([^}]+)\}([^"]+)\{([^}]+)\}([^"]*)"([[:space:]]*),([[:space:]]*)([^,]+),([[:space:]]*)([^,]+),([[:space:]]*)ex\)/LogError($"\1{\8}\3{\10}\5", ex)/g' "$file"
    
    # Handle cases without exception but with wrong parameter placement
    sed -i -E 's/LogError\("([^"]+)\{([^}]+)\}([^"]*)"([[:space:]]*),([[:space:]]*)([^)]+)\)/LogError($"\1{\6}\3")/g' "$file"
    
    # Handle TradingLogOrchestrator.Instance.LogError patterns
    sed -i -E 's/TradingLogOrchestrator\.Instance\.LogError\("([^"]+)\{([^}]+)\}([^"]*)"([[:space:]]*),([[:space:]]*)([^,]+),([[:space:]]*)ex/TradingLogOrchestrator.Instance.LogError($"\1{\6}\3", ex/g' "$file"
    
    # Check if file was modified
    if ! diff -q "$file" "$file.bak_logerror" > /dev/null; then
        echo "  ✓ Fixed LogError calls in $file"
        rm "$file.bak_logerror"
    else
        echo "  - No changes needed in $file"
        rm "$file.bak_logerror"
    fi
}

# Find all C# files with potential LogError issues
files=$(find . -name "*.cs" -type f -exec grep -l "LogError.*{.*}.*," {} \;)

total=$(echo "$files" | wc -l)
current=0

for file in $files; do
    ((current++))
    echo "[$current/$total] Processing $file..."
    fix_file "$file"
done

echo "LogError parameter fix complete!"
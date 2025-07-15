#!/usr/bin/env bash
# Comprehensive StyleCop fix script for AIRES Test Infrastructure
# Based on Gemini's architectural guidance
# Goal: 0 errors, 0 warnings

set -e

echo "=== AIRES StyleCop Comprehensive Fix Script ==="
echo "Goal: Achieve 0/0 (zero errors, zero warnings)"
echo ""

TEST_DIR="/mnt/d/Projects/CSharp/Day_Trading_Platform_Dragon/AIRES/tests/AIRES.TestInfrastructure"
TEMP_DIR="/tmp/aires-fixes"
mkdir -p "$TEMP_DIR"

# Phase 1: Fix SA1101 - Add "this." prefix to all local calls
echo "Phase 1: Fixing SA1101 - Adding 'this.' prefix..."
for file in "$TEST_DIR"/*.cs; do
    if [ -f "$file" ]; then
        filename=$(basename "$file")
        echo "  Processing $filename for SA1101..."
        
        # Fix method calls without this.
        sed -E '
            # Skip lines that are comments or already have this.
            /^[[:space:]]*\/\//b
            /this\./b
            
            # Add this. to method calls that look like LogMethodEntry, LogInfo, etc.
            s/^([[:space:]]*)Log(MethodEntry|MethodExit|Info|Debug|Warning|Error|Critical|Trace|Fatal)\(/\1this.Log\2(/g
            
            # Add this. to field access
            s/([[:space:]])booklets\[/\1this.booklets[/g
            s/([[:space:]])pathToId\[/\1this.pathToId[/g
            s/([[:space:]])saveCount/\1this.saveCount/g
            s/([[:space:]])logEntries\./\1this.logEntries./g
            s/([[:space:]])correlationId/\1this.correlationId/g
            s/([[:space:]])scopes\./\1this.scopes./g
            s/([[:space:]])responses\[/\1this.responses[/g
            s/([[:space:]])recordedRequests\./\1this.recordedRequests./g
            s/([[:space:]])defaultResponse/\1this.defaultResponse/g
            
            # Fix return statements
            s/return booklets\./return this.booklets./g
            s/return logEntries\./return this.logEntries./g
            s/return recordedRequests\./return this.recordedRequests./g
        ' "$file" > "$TEMP_DIR/$filename"
        
        cp "$TEMP_DIR/$filename" "$file"
    fi
done

# Phase 2: Fix SA1413 - Add trailing commas
echo ""
echo "Phase 2: Fixing SA1413 - Adding trailing commas..."
for file in "$TEST_DIR"/*.cs; do
    if [ -f "$file" ]; then
        filename=$(basename "$file")
        echo "  Processing $filename for SA1413..."
        
        # Add trailing commas to multi-line initializers
        # This is complex - using a more targeted approach
        awk '
        BEGIN { in_init = 0; brace_count = 0 }
        /= new.*{[[:space:]]*$/ { in_init = 1; brace_count = 1; print; next }
        in_init && /{/ { brace_count++; print; next }
        in_init && /}/ { 
            brace_count--
            if (brace_count == 0) {
                in_init = 0
            }
            print
            next
        }
        in_init && /[^,][[:space:]]*$/ && !/^[[:space:]]*\/\// {
            # Add comma if line doesnt end with comma and is not a comment
            print $0 ","
            next
        }
        { print }
        ' "$file" > "$TEMP_DIR/$filename.comma"
        
        cp "$TEMP_DIR/$filename.comma" "$file"
    fi
done

# Phase 3: Fix file endings (SA1518)
echo ""
echo "Phase 3: Fixing SA1518 - Ensuring files end with newline..."
for file in "$TEST_DIR"/*.cs; do
    if [ -f "$file" ]; then
        # Ensure file ends with exactly one newline
        sed -i -e '$a\' "$file" 2>/dev/null || echo >> "$file"
    fi
done

# Phase 4: Remove trailing whitespace (SA1028)
echo ""
echo "Phase 4: Fixing SA1028 - Removing trailing whitespace..."
for file in "$TEST_DIR"/*.cs; do
    if [ -f "$file" ]; then
        sed -i 's/[[:space:]]*$//' "$file"
    fi
done

# Phase 5: Fix missing closing brace blank lines (SA1503)
echo ""
echo "Phase 5: Fixing SA1503 - Adding blank lines after closing braces..."
for file in "$TEST_DIR"/*.cs; do
    if [ -f "$file" ]; then
        filename=$(basename "$file")
        echo "  Processing $filename for SA1503..."
        
        awk '
        /^[[:space:]]*}[[:space:]]*$/ {
            print
            getline
            if ($0 !~ /^[[:space:]]*$/ && $0 !~ /^[[:space:]]*}/) {
                print ""
            }
            print
            next
        }
        { print }
        ' "$file" > "$TEMP_DIR/$filename.braces"
        
        cp "$TEMP_DIR/$filename.braces" "$file"
    fi
done

echo ""
echo "✅ Automated fixes complete!"
echo ""
echo "Remaining manual fixes needed:"
echo "  - SA1611: Add parameter documentation"
echo "  - SA1615: Add return documentation"
echo "  - SA1202: Reorder members (public before private)"
echo "  - SA1200: Some using directives may still need moving"
echo ""
echo "Run 'dotnet build' to check progress..."

# Cleanup
rm -rf "$TEMP_DIR"
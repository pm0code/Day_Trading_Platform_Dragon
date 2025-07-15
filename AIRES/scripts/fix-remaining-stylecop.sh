#!/bin/bash
# Fix remaining StyleCop violations in AIRES TestInfrastructure

echo "=== Fixing remaining StyleCop violations ==="

# Fix SA1518 - Add newline at end of files
echo "Fixing SA1518 - Adding newlines at end of files..."
find tests/AIRES.TestInfrastructure -name "*.cs" -exec sh -c 'tail -c1 {} | read -r _ || echo >> {}' \;

# Fix SA1200 - Move using directives outside namespace
echo "Fixing SA1200 - Moving using directives outside namespace..."
for file in tests/AIRES.TestInfrastructure/TestConfiguration.cs tests/AIRES.TestInfrastructure/TestScope.cs; do
    if [ -f "$file" ]; then
        echo "Processing $file..."
        # Move namespace line after using directives
        sed -i '/^namespace AIRES.TestInfrastructure;$/d' "$file"
        # Add namespace at the end of using directives (before the first non-using, non-comment line)
        awk '
            BEGIN { namespace_added = 0; in_usings = 1 }
            /^using / { print; next }
            /^$/ && in_usings { print; next }
            /^\/\// && in_usings { print; next }
            !namespace_added && !/^using/ && !/^$/ && !/^\/\// {
                print "namespace AIRES.TestInfrastructure;"
                print ""
                namespace_added = 1
                in_usings = 0
            }
            { if (!in_usings) print }
        ' "$file" > "$file.tmp" && mv "$file.tmp" "$file"
    fi
done

# Fix blank line separation between using directives (SA1516)
echo "Fixing SA1516 - Adding blank lines between using directives groups..."
for file in tests/AIRES.TestInfrastructure/*.cs; do
    if [ -f "$file" ]; then
        # Add blank line after System usings before other usings
        sed -i '/^using System/,/^using [^S]/ { /^using [^S]/i\

        }' "$file" 2>/dev/null || true
    fi
done

echo "=== StyleCop fixes applied ==="
echo "Run 'dotnet build' to verify remaining issues"
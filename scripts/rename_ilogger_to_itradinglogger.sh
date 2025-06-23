#!/bin/bash

# Script to rename ILogger to ITradingLogger throughout the project
# This avoids confusion with Microsoft.Extensions.Logging.ILogger

echo "Starting ILogger to ITradingLogger renaming process..."

# First, rename the interface file
if [ -f "DayTradinPlatform/TradingPlatform.Core/Interfaces/ILogger.cs" ]; then
    mv DayTradinPlatform/TradingPlatform.Core/Interfaces/ILogger.cs DayTradinPlatform/TradingPlatform.Core/Interfaces/ITradingLogger.cs
    echo "Renamed ILogger.cs to ITradingLogger.cs"
fi

# Find all CS files and replace ILogger with ITradingLogger
# We need to be careful to only replace our ILogger, not other references
find DayTradinPlatform -name "*.cs" -type f | while read file; do
    # Skip bin and obj directories
    if [[ "$file" == *"/bin/"* ]] || [[ "$file" == *"/obj/"* ]]; then
        continue
    fi
    
    # Check if file contains ILogger
    if grep -q "ILogger" "$file"; then
        # Create a temporary file
        temp_file="${file}.tmp"
        
        # Replace ILogger with ITradingLogger, being careful about word boundaries
        sed -E 's/\bILogger\b/ITradingLogger/g' "$file" > "$temp_file"
        
        # Check if the file was actually modified
        if ! cmp -s "$file" "$temp_file"; then
            mv "$temp_file" "$file"
            echo "Updated: $file"
        else
            rm "$temp_file"
        fi
    fi
done

# Update TradingLogOrchestrator to implement ITradingLogger instead of ILogger
echo "Updating TradingLogOrchestrator class declaration..."
sed -i 's/: ILogger, IDisposable/: ITradingLogger, IDisposable/g' DayTradinPlatform/TradingPlatform.Core/Logging/TradingLogOrchestrator.cs

echo "ILogger to ITradingLogger renaming complete!"
echo "Please rebuild the solution to verify all changes compile correctly."
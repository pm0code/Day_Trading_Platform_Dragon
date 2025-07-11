# PowerShell Complete Reference Guide

## Table of Contents - Quick Index

| Line | Section | Hashtag | Description |
|------|---------|---------|-------------|
| 25   | Windows Commands | #windows-commands | Essential Windows command reference |
| 150  | String Handling | #string-handling | All quote types and string manipulation |
| 280  | Here-Strings | #here-strings | Multi-line string patterns |
| 380  | File Operations | #file-operations | File manipulation patterns |
| 520  | C# File Manipulation | #csharp-manipulation | Working with C# source files |
| 680  | TradingLogger.cs Pattern | #tradinglogger-fix | Specific fix pattern example |
| 780  | Common Errors | #common-errors | Frequent mistakes and solutions |
| 950  | Escape Characters | #escape-characters | Complete escape sequence reference |
| 1080 | Regular Expressions | #regex-csharp | Regex patterns for C# code |
| 1250 | Best Practices | #best-practices | PowerShell coding standards |
| 1380 | Quick Examples | #quick-examples | Copy-paste ready snippets |

## Quick Search Commands
```powershell
# Find section by hashtag
Select-String -Path "PowerShell_Complete_Reference_Guide.md" -Pattern "#windows-commands"

# Find line number
Select-String -Path "PowerShell_Complete_Reference_Guide.md" -Pattern "Line 280"
```

---

## Windows Commands #windows-commands
*Line 25*

### Essential Windows Commands (NOT Linux!)

#### Directory Navigation
```powershell
# CORRECT - Windows style
cd D:\BuildWorkspace
Set-Location "C:\Program Files"
Push-Location D:\Projects
Pop-Location

# WRONG - Linux style
cd /home/user  # This won't work!
```

#### File Listing
```powershell
# CORRECT - Windows
Get-ChildItem
Get-ChildItem -Recurse
Get-ChildItem *.cs -Recurse
dir  # Alias for Get-ChildItem
ls   # Also works but is an alias

# Advanced listing
Get-ChildItem -Path D:\ -Include *.cs -Recurse | Select-Object FullName, Length, LastWriteTime
```

#### Path Handling
```powershell
# CORRECT - Windows paths
$path = "D:\BuildWorkspace\TradingPlatform"
$path = 'C:\Program Files\Application'
$path = "D:\Build Workspace\Trading Platform"  # Spaces are OK

# WRONG - Linux paths
$path = "/usr/local/bin"  # Won't work!
```

#### Environment Variables
```powershell
# Windows style
$env:USERPROFILE
$env:TEMP
$env:PATH
[Environment]::GetEnvironmentVariable("PATH", "Machine")
```

#### Process Management
```powershell
# List processes
Get-Process
Get-Process | Where-Object {$_.CPU -gt 100}

# Stop process
Stop-Process -Name "notepad"
Stop-Process -Id 1234
```

#### Service Management
```powershell
# List services
Get-Service
Get-Service | Where-Object {$_.Status -eq "Running"}

# Start/Stop services
Start-Service -Name "ServiceName"
Stop-Service -Name "ServiceName"
Restart-Service -Name "ServiceName"
```

---

## String Handling #string-handling
*Line 150*

### Quote Types in PowerShell

#### Single Quotes (Literal Strings)
```powershell
# Single quotes = literal, no variable expansion
$name = "John"
$literal = 'Hello $name'  # Output: Hello $name

# CORRECT usage
$path = 'C:\Program Files\App'
$regex = 'namespace\s+(\w+)'

# Escaping single quotes inside single quotes
$text = 'It''s a test'  # Double the single quote
```

#### Double Quotes (Expandable Strings)
```powershell
# Double quotes = variable expansion
$name = "John"
$expanded = "Hello $name"  # Output: Hello John

# Complex expressions need $()
$count = 5
$message = "Count is $($count + 1)"  # Output: Count is 6

# Escaping in double quotes
$path = "C:\Users\$env:USERNAME\Documents"
$escaped = "Line 1`nLine 2"  # Backtick for escape sequences
$quote = "He said, `"Hello`""  # Escaping double quotes
```

#### Backtick Escape Sequences
```powershell
`n  # Newline
`r  # Carriage return
`t  # Tab
`0  # Null
`a  # Alert (beep)
`b  # Backspace
`f  # Form feed
`v  # Vertical tab
``  # Literal backtick
`"  # Literal double quote
`'  # Literal single quote
```

#### String Concatenation
```powershell
# Method 1: Plus operator
$full = $first + " " + $last

# Method 2: String interpolation
$full = "$first $last"

# Method 3: -join operator
$full = $first, $last -join " "

# Method 4: Format operator
$full = "{0} {1}" -f $first, $last

# Method 5: .NET String.Format
$full = [String]::Format("{0} {1}", $first, $last)
```

---

## Here-Strings #here-strings
*Line 280*

### Multi-line String Patterns

#### Basic Here-String (Literal)
```powershell
# Single-quoted here-string (literal, no expansion)
$literal = @'
This is a literal here-string.
Variables like $name are NOT expanded.
Special characters don't need escaping: \ / " '
'@

# IMPORTANT: The closing '@ must be at the start of the line!
```

#### Expandable Here-String
```powershell
# Double-quoted here-string (expandable)
$name = "TradingPlatform"
$expanded = @"
Project: $name
Path: $env:USERPROFILE\$name
Date: $(Get-Date)
"@

# Variables and expressions ARE expanded
```

#### Common Here-String Patterns for C# Code
```powershell
# Pattern 1: Namespace update
$newContent = @"
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace TradingPlatform.Core
{
    public class TradingService
    {
        private readonly ILogger<TradingService> _logger;
        
        public TradingService(ILogger<TradingService> logger)
        {
            _logger = logger;
        }
    }
}
"@

# Pattern 2: Method implementation
$methodCode = @'
        public decimal CalculateProfit(decimal entry, decimal exit, int shares)
        {
            if (shares <= 0)
                throw new ArgumentException("Shares must be positive", nameof(shares));
                
            return (exit - entry) * shares;
        }
'@
```

#### Here-String Rules
```powershell
# CORRECT
$text = @"
Line 1
Line 2
"@  # Closing marker at start of line

# WRONG
$text = @"
Line 1
Line 2
    "@  # Indented closing marker - ERROR!
```

---

## File Operations #file-operations
*Line 380*

### File Reading Patterns

#### Basic File Reading
```powershell
# Read entire file as string
$content = Get-Content -Path "D:\file.txt" -Raw

# Read as array of lines
$lines = Get-Content -Path "D:\file.txt"

# Read with encoding
$content = Get-Content -Path "D:\file.txt" -Encoding UTF8
```

#### File Writing Patterns
```powershell
# Write string to file
"Hello World" | Out-File -FilePath "D:\output.txt"

# Write with specific encoding
"Hello World" | Out-File -FilePath "D:\output.txt" -Encoding UTF8

# Append to file
"New Line" | Out-File -FilePath "D:\output.txt" -Append

# Set-Content (more efficient for large files)
Set-Content -Path "D:\output.txt" -Value $content
```

#### File Operations
```powershell
# Copy files
Copy-Item -Path "D:\source.txt" -Destination "D:\dest.txt"
Copy-Item -Path "D:\*.cs" -Destination "D:\Backup\" -Recurse

# Move files
Move-Item -Path "D:\old.txt" -Destination "D:\new.txt"

# Delete files
Remove-Item -Path "D:\temp.txt"
Remove-Item -Path "D:\Temp\*" -Recurse -Force

# Check if file exists
if (Test-Path -Path "D:\file.txt") {
    Write-Host "File exists"
}

# Create directory
New-Item -ItemType Directory -Path "D:\NewFolder"

# Get file info
$fileInfo = Get-Item -Path "D:\file.txt"
$fileInfo.Length  # Size in bytes
$fileInfo.LastWriteTime
$fileInfo.FullName
```

#### Working with Multiple Files
```powershell
# Process all CS files
Get-ChildItem -Path "D:\Project" -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    # Process content
    Set-Content -Path $_.FullName -Value $content
}

# Batch rename
Get-ChildItem -Path "D:\Files" -Filter "*.txt" | 
    Rename-Item -NewName { $_.Name -replace "old", "new" }
```

---

## C# File Manipulation #csharp-manipulation
*Line 520*

### Working with C# Source Files

#### Reading and Parsing C# Files
```powershell
# Read C# file preserving formatting
$csContent = Get-Content -Path "D:\Project\MyClass.cs" -Raw

# Find using statements
$usingPattern = '^using\s+[\w\.]+;'
$usings = Select-String -Path "D:\Project\MyClass.cs" -Pattern $usingPattern

# Extract namespace
if ($csContent -match 'namespace\s+([\w\.]+)') {
    $namespace = $matches[1]
}

# Extract class name
if ($csContent -match 'public\s+class\s+(\w+)') {
    $className = $matches[1]
}
```

#### Modifying C# Files
```powershell
# Pattern 1: Update namespace
$content = Get-Content -Path "D:\MyClass.cs" -Raw
$updatedContent = $content -replace 'namespace OldNamespace', 'namespace NewNamespace'
Set-Content -Path "D:\MyClass.cs" -Value $updatedContent

# Pattern 2: Add using statement
$lines = Get-Content -Path "D:\MyClass.cs"
$newLines = @()
$usingAdded = $false

foreach ($line in $lines) {
    $newLines += $line
    if ($line -match '^using' -and -not $usingAdded) {
        if ($line -lt 'using System.Threading.Tasks;') {
            $newLines += 'using System.Threading.Tasks;'
            $usingAdded = $true
        }
    }
}

# Pattern 3: Update logger type
$content = Get-Content -Path "D:\Logger.cs" -Raw
$updated = $content -replace 'ILogger<\w+>', 'ILogger<TradingLogger>'
Set-Content -Path "D:\Logger.cs" -Value $updated
```

#### Complex C# Transformations
```powershell
# Remove all logging statements
$content = Get-Content -Path "D:\Service.cs" -Raw
$noLogging = $content -replace '^\s*_logger\.Log\w+\([^;]+\);\s*$', '' -replace '(?m)^\s*$\n', ''

# Update method signatures
$content = $content -replace 'public void (\w+)\((.*?)\)', 'public async Task $1Async($2)'

# Add attributes to classes
$content = $content -replace '(public\s+class\s+\w+)', '[Serializable]`n$1'
```

---

## TradingLogger.cs Pattern #tradinglogger-fix
*Line 680*

### Specific Pattern for Fixing TradingLogger.cs

#### The Problem
```csharp
// WRONG - Old logging infrastructure
using Microsoft.Extensions.Logging;

public class TradingLogger
{
    private readonly ILogger<TradingLogger> _logger;
    
    public void LogTrade(LogLevel level, string message)
    {
        _logger.Log(level, message);  // LogLevel from Microsoft.Extensions.Logging
    }
}
```

#### The Fix Pattern
```powershell
# Step 1: Read the file
$content = Get-Content -Path "D:\TradingLogger.cs" -Raw

# Step 2: Remove Microsoft.Extensions.Logging using
$content = $content -replace 'using Microsoft\.Extensions\.Logging;\r?\n', ''

# Step 3: Update LogLevel references
$content = $content -replace 'Microsoft\.Extensions\.Logging\.LogLevel', 'TradingPlatform.Core.Logging.LogLevel'
$content = $content -replace '(?<!\.)(LogLevel)(?!\.)', 'TradingPlatform.Core.Logging.LogLevel'

# Step 4: Write back
Set-Content -Path "D:\TradingLogger.cs" -Value $content

# Alternative: Using here-string for complete replacement
$fixedContent = @"
using System;
using TradingPlatform.Core.Logging;

namespace TradingPlatform.Core
{
    public class TradingLogger
    {
        private readonly ILogger<TradingLogger> _logger;
        
        public void LogTrade(TradingPlatform.Core.Logging.LogLevel level, string message)
        {
            _logger.Log(level, message);
        }
    }
}
"@

Set-Content -Path "D:\TradingLogger.cs" -Value $fixedContent
```

---

## Common Errors #common-errors
*Line 780*

### Frequent PowerShell Mistakes and Solutions

#### Error: Missing closing quote
```powershell
# WRONG
$text = "This is missing a quote
# Error: The string is missing the terminator: ".

# CORRECT
$text = "This is a complete string"
```

#### Error: Here-string formatting
```powershell
# WRONG
$text = @"
    Some text
    "@  # Indented closing tag

# CORRECT
$text = @"
    Some text
"@  # Closing tag at start of line
```

#### Error: Path with spaces
```powershell
# WRONG
Set-Location C:\Program Files\App  # Spaces break the path

# CORRECT
Set-Location "C:\Program Files\App"
Set-Location 'C:\Program Files\App'
```

#### Error: Variable in single quotes
```powershell
# WRONG (if you want expansion)
$name = "John"
$message = 'Hello $name'  # Output: Hello $name

# CORRECT
$message = "Hello $name"  # Output: Hello John
```

#### Error: Escape sequences in single quotes
```powershell
# WRONG
$text = 'Line 1\nLine 2'  # Output: Line 1\nLine 2

# CORRECT
$text = "Line 1`nLine 2"  # Uses backtick
# Or
$text = @"
Line 1
Line 2
"@
```

#### Error: Pipeline variable confusion
```powershell
# WRONG
Get-ChildItem | ForEach-Object { Write-Host $_ }  # $_ might be null

# CORRECT
Get-ChildItem | ForEach-Object { Write-Host $_.Name }
# Or
Get-ChildItem | ForEach-Object { Write-Host $PSItem.Name }
```

#### Error: Comparison operators
```powershell
# WRONG (using wrong operators)
if ($value == 5)  # Error: == is not valid

# CORRECT
if ($value -eq 5)  # Equals
if ($value -ne 5)  # Not equals
if ($value -gt 5)  # Greater than
if ($value -lt 5)  # Less than
if ($value -ge 5)  # Greater or equal
if ($value -le 5)  # Less or equal
```

#### Error: Array declaration
```powershell
# WRONG
$array = "item1", "item2" "item3"  # Missing comma

# CORRECT
$array = "item1", "item2", "item3"
# Or
$array = @("item1", "item2", "item3")
```

---

## Escape Characters #escape-characters
*Line 950*

### Complete Escape Sequence Reference

#### Backtick Escapes (in double quotes)
```powershell
# Special characters
`n    # Newline (LF)
`r    # Carriage return (CR)
`r`n  # Windows line ending (CRLF)
`t    # Tab
`a    # Alert/Bell
`b    # Backspace
`f    # Form feed
`v    # Vertical tab
`0    # Null character
``    # Literal backtick
`$    # Literal dollar sign
`"    # Literal double quote
`'    # Literal single quote

# Examples
"First line`r`nSecond line"
"Column1`tColumn2`tColumn3"
"Price: `$99.99"
"He said, `"Hello`""
```

#### Regex Escape Characters
```powershell
# Regex special characters that need escaping
\.    # Literal dot
\$    # Literal dollar
\^    # Literal caret
\*    # Literal asterisk
\+    # Literal plus
\?    # Literal question mark
\|    # Literal pipe
\(    # Literal parenthesis
\)    # Literal parenthesis
\[    # Literal bracket
\]    # Literal bracket
\{    # Literal brace
\}    # Literal brace
\\    # Literal backslash

# PowerShell regex examples
$text -match 'C:\\'              # Match C:\
$text -match '\$\d+\.\d{2}'      # Match $99.99
$text -match 'file\.txt'         # Match file.txt
```

#### String Format Escapes
```powershell
# Format string escapes
{{    # Literal left brace
}}    # Literal right brace

# Example
"{0} items cost {{{1}}}" -f 5, 10  # Output: 5 items cost {10}
```

#### Path Escapes
```powershell
# Windows paths don't need escaping
$path = "C:\Users\Name\Documents"  # This is fine

# Unless using regex
$pathPattern = "C:\\Users\\Name\\Documents"

# Or use raw string
$pathPattern = [regex]::Escape("C:\Users\Name\Documents")
```

---

## Regular Expressions #regex-csharp
*Line 1080*

### Regex Patterns for C# Code

#### Common C# Patterns
```powershell
# Using statements
'^\s*using\s+([\w\.]+)\s*;'

# Namespace declaration
'namespace\s+([\w\.]+)\s*\{'

# Class declaration
'(public|private|internal|protected)\s+(abstract\s+|sealed\s+|static\s+)?(class|interface)\s+(\w+)'

# Method declaration
'(public|private|protected|internal)\s+(static\s+)?(async\s+)?(\w+)\s+(\w+)\s*\([^)]*\)'

# Property declaration
'(public|private|protected|internal)\s+(\w+)\s+(\w+)\s*\{\s*get;\s*set;\s*\}'

# Field declaration
'(private|protected)\s+(readonly\s+)?(\w+)\s+(_\w+);'

# Logger pattern
'ILogger<(\w+)>'

# Attribute pattern
'\[(\w+)(\([^)]*\))?\]'

# Generic type pattern
'(\w+)<([^>]+)>'

# Comments
'//.*$'           # Single line comment
'/\*[\s\S]*?\*/'  # Multi-line comment
'///.*$'          # XML documentation comment
```

#### Advanced Patterns
```powershell
# Extract method with body
$methodPattern = @'
(?ms)(public|private|protected|internal)\s+
(static\s+)?(async\s+)?
(\w+)\s+(\w+)\s*
\([^)]*\)\s*
\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}
'@

# Extract complete class
$classPattern = @'
(?ms)(public|internal)\s+class\s+(\w+)
(?:\s*:\s*[\w\s,<>]+)?
\s*\{.*?\n\}
'@

# Find all TODO comments
'//\s*TODO:?\s*(.+)$'

# Find all regions
'#region\s+(.+)$'
'#endregion'
```

#### Using Regex in PowerShell
```powershell
# Simple match
if ($content -match 'namespace\s+(\w+)') {
    $namespace = $matches[1]
}

# Multiple matches
$matches = [regex]::Matches($content, 'public\s+class\s+(\w+)')
foreach ($match in $matches) {
    Write-Host "Found class: $($match.Groups[1].Value)"
}

# Replace with regex
$updated = $content -replace 'ILogger<\w+>', 'ILogger<TradingService>'

# Split with regex
$parts = $content -split '\r?\n\r?\n'  # Split on empty lines
```

---

## Best Practices #best-practices
*Line 1250*

### PowerShell Coding Standards

#### Variable Naming
```powershell
# Good variable names
$userName = "John"
$isEnabled = $true
$itemCount = 42
$filePath = "D:\Documents\file.txt"

# Bad variable names
$un = "John"        # Too short
$flag = $true       # Not descriptive
$temp = 42          # Unclear purpose
$x = "D:\file.txt"  # Meaningless
```

#### Error Handling
```powershell
# Basic try-catch
try {
    $content = Get-Content -Path $filePath -ErrorAction Stop
} catch {
    Write-Error "Failed to read file: $_"
}

# Specific error types
try {
    # Some operation
} catch [System.IO.FileNotFoundException] {
    Write-Error "File not found"
} catch [System.UnauthorizedAccessException] {
    Write-Error "Access denied"
} catch {
    Write-Error "Unexpected error: $_"
}
```

#### Function Design
```powershell
function Update-CSharpFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$FilePath,
        
        [Parameter(Mandatory=$true)]
        [string]$Pattern,
        
        [Parameter(Mandatory=$true)]
        [string]$Replacement
    )
    
    begin {
        # Validate file exists
        if (-not (Test-Path -Path $FilePath)) {
            throw "File not found: $FilePath"
        }
    }
    
    process {
        $content = Get-Content -Path $FilePath -Raw
        $updated = $content -replace $Pattern, $Replacement
        Set-Content -Path $FilePath -Value $updated
    }
}
```

#### Pipeline Usage
```powershell
# Good pipeline usage
Get-ChildItem -Path "D:\Project" -Filter "*.cs" -Recurse |
    Where-Object { $_.Length -gt 1000 } |
    Sort-Object -Property LastWriteTime -Descending |
    Select-Object -First 10 |
    Format-Table Name, Length, LastWriteTime

# Avoid long one-liners
# Break complex operations into steps
$files = Get-ChildItem -Path "D:\Project" -Filter "*.cs" -Recurse
$largeFiles = $files | Where-Object { $_.Length -gt 1000 }
$recent = $largeFiles | Sort-Object -Property LastWriteTime -Descending
$top10 = $recent | Select-Object -First 10
```

---

## Quick Examples #quick-examples
*Line 1380*

### Copy-Paste Ready Snippets

#### File Processing Template
```powershell
# Process all C# files in a directory
$sourceDir = "D:\Project"
$files = Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse

foreach ($file in $files) {
    Write-Host "Processing: $($file.FullName)"
    
    try {
        $content = Get-Content -Path $file.FullName -Raw
        
        # Your processing here
        $updated = $content -replace 'oldPattern', 'newPattern'
        
        Set-Content -Path $file.FullName -Value $updated
        Write-Host "Updated: $($file.Name)" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to process $($file.Name): $_"
    }
}
```

#### Backup Before Modify
```powershell
# Create backup before modifying files
$file = "D:\important.cs"
$backup = "$file.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Copy-Item -Path $file -Destination $backup
Write-Host "Backup created: $backup"

# Now safe to modify
$content = Get-Content -Path $file -Raw
# ... modifications ...
Set-Content -Path $file -Value $content
```

#### Search and Report
```powershell
# Find all occurrences of a pattern
$pattern = 'ILogger<\w+>'
$results = Get-ChildItem -Path "D:\Project" -Filter "*.cs" -Recurse |
    Select-String -Pattern $pattern

# Generate report
$report = @()
foreach ($result in $results) {
    $report += [PSCustomObject]@{
        File = $result.Path
        Line = $result.LineNumber
        Match = $result.Line.Trim()
    }
}

$report | Export-Csv -Path "D:\search_results.csv" -NoTypeInformation
```

#### Bulk Rename
```powershell
# Rename with pattern
Get-ChildItem -Path "D:\Files" -Filter "*.txt" |
    ForEach-Object {
        $newName = $_.Name -replace 'old_prefix', 'new_prefix'
        Rename-Item -Path $_.FullName -NewName $newName -WhatIf
    }
# Remove -WhatIf to actually rename
```

#### C# Namespace Update
```powershell
# Update namespace across multiple files
$oldNamespace = "OldCompany.OldProduct"
$newNamespace = "NewCompany.NewProduct"

Get-ChildItem -Path "D:\Solution" -Filter "*.cs" -Recurse |
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        if ($content -match $oldNamespace) {
            $updated = $content -replace $oldNamespace, $newNamespace
            Set-Content -Path $_.FullName -Value $updated
            Write-Host "Updated: $($_.Name)"
        }
    }
```

---

## Quick Reference Card

### Most Used Commands
```powershell
# Read file
$content = Get-Content -Path "file.txt" -Raw

# Write file
Set-Content -Path "file.txt" -Value $content

# Find files
Get-ChildItem -Filter "*.cs" -Recurse

# Search in files
Select-String -Path "*.cs" -Pattern "TODO"

# Replace text
$new = $old -replace 'pattern', 'replacement'

# Test path
if (Test-Path -Path "D:\file.txt") { }

# Create directory
New-Item -ItemType Directory -Path "D:\NewDir"

# Copy files
Copy-Item -Path "source.txt" -Destination "dest.txt"

# Remove files
Remove-Item -Path "temp.txt" -Force
```

### String Literals Cheat Sheet
```powershell
'Single quotes = literal'
"Double quotes = $expandable"
@'
Here-string
literal
'@
@"
Here-string
$expandable
"@
```

### Escape Sequences
```powershell
`n = newline
`t = tab
`" = quote
`` = backtick
`$ = dollar
```

---

*End of PowerShell Complete Reference Guide*
*Total Lines: ~1450*
*Generated: 2025-06-23*
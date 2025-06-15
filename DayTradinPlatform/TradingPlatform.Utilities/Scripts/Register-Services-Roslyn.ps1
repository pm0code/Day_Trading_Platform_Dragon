# File: TradingPlatform.Utilities\Scripts\Register-Services-Roslyn.ps1

# Requires -Modules Microsoft.CodeAnalysis.CSharp.Scripting, Microsoft.CodeAnalysis.CSharp.Workspaces
# Install the packages if needed:
# Install-Package Microsoft.CodeAnalysis.CSharp.Scripting
# Install-Package Microsoft.CodeAnalysis.CSharp.Workspaces

# Project root directory (now points to the Utilities project)
$projectRoot = "D:\Projects\C#.Net\DayTradingPlatform-P2N\DayTradinPlatform\TradingPlatform.Utilities"

# Solution file path
$solutionFile = "D:\Projects\C#.Net\DayTradingPlatform-P2N\DayTradinPlatform\DayTradinPlatform.sln"

# Log file path
$logFile = "d:\Projects\C#.Net\MyDocs-DayTradingPlatform-P2N\Docs\Logs\Register-Services-$(Get-Date -Format 'yyyyMMddHHmmss').md"

# Function to find service classes using Roslyn and Workspaces
function Find-ServiceClasses-Roslyn {
    param(
        [string]$solutionPath
    )
    try {
        $workspace = [Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace]::Create()
        $solution = $workspace.OpenSolutionAsync($solutionPath).GetAwaiter().GetResult()

        $services = foreach ($project in $solution.Projects) {
            foreach ($document in $project.Documents) {
                $model = $compilation.GetSemanticModel($document.SyntaxTree)
                foreach ($node in $document.GetSyntaxRootAsync().Result.DescendantNodes().OfType([Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax])) {
                    $symbol = $model.GetDeclaredSymbol($node)

                    if ($symbol.Interfaces.Count -gt 0 -and $symbol.DeclaredAccessibility -eq "Public") {
                        [PSCustomObject]@{
                            Interface = $symbol.Interfaces[0].ToDisplayString()
                            Class = $symbol.ToDisplayString()
                            Lifetime = Get-ServiceLifetime $node # Call helper function for lifetime
                        }
                    }
                }
            }
        }
        return $services
    }
    catch {
        Write-Warning "Failed to process solution: $($solutionPath). Error: $_"
        return $null # Return null on error
    }
}

# Helper function to determine service lifetime (expand as needed)
function Get-ServiceLifetime {
    param(
        [Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax]$classNode
    )
    if ($classNode.AttributeLists.Any())
    {
        # Check for ServiceLifetime attribute (add more robust parsing if needed)
        if ($classNode.AttributeLists | Where-Object {$_.ToString() -match "ServiceLifetime"})
        {
            return "Scoped" # Or extract the actual lifetime from the attribute
        }
    }

    # Fallback to naming conventions (can be expanded)
    if ($classNode.Identifier.Text -match "Transient") { return "Transient" }
    if ($classNode.Identifier.Text -match "Singleton") { return "Singleton" }
    return "Scoped" # Default lifetime
}

# Function to update Program.cs (idempotency and logging)
function Update-Program {
    # ... (This function remains the same as in Reply #150)
}

# --- Main Script Logic ---

$allServices = Find-ServiceClasses-Roslyn -solutionPath $solutionFile

if ($allServices) { # Check if any services were found
    # Update Program.cs
    Update-Program -services $allServices

    # Log output
    "$(Get-Date) - Service registration script completed." | Out-File -FilePath $logFile -Append
    "Discovered $($allServices.Count) services." | Out-File -FilePath $logFile -Append
}
else {
    "$(Get-Date) - No services found or error during processing." | Out-File -FilePath $logFile -Append
}

# --- Usage Instructions ---

# 1. Save the script as Register-Services-Roslyn.ps1 in the TradingPlatform.Utilities\Scripts folder.
# 2. Open PowerShell as administrator.
# 3. Navigate to the TradingPlatform.Utilities\Scripts directory.
# 4. Run the script: .\Register-Services-Roslyn.ps1

# --- Assumptions ---

# - Project and solution paths are correctly set in $projectRoot and $solutionFile.
# - Log file directory exists.
# - Service classes are public and implement at least one interface.
# - Default lifetime is "Scoped".  Improve Get-ServiceLifetime for attribute-based or other lifetime detection.

# Total Lines: 130

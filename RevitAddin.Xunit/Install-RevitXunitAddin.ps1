# RevitAddin.Xunit Installation Script
# This script generates the Revit addin manifest for RevitAddin.Xunit
# File installation is handled by RevitTestFramework.Common.exe

param(
    [Parameter(Position=0)]
    [string]$AssemblyPath
)

# Function to normalize pre-release versions for assembly compatibility
function Normalize-VersionForAssembly {
    param([string]$Version)
    
    if ([string]::IsNullOrEmpty($Version)) {
        return "2025.0.0.0"
    }
    
    # Extract base version (before any pre-release suffix)
    $baseVersion = $Version.Split('-')[0]
    
    # If it's already a standard version, ensure it has 4 parts
    if (-not $Version.Contains('-')) {
        $parts = $baseVersion.Split('.')
        switch ($parts.Length) {
            1 { return "$($parts[0]).0.0.0" }
            2 { return "$($parts[0]).$($parts[1]).0.0" }
            3 { return "$($parts[0]).$($parts[1]).$($parts[2]).0" }
            default { return $baseVersion }
        }
    }
    
    # Handle pre-release versions
    $preReleaseSection = $Version.Substring($baseVersion.Length + 1)
    
    # Extract numeric values from pre-release section
    $numbers = [regex]::Matches($preReleaseSection, '\d+') | ForEach-Object { $_.Value }
    
    # Combine numbers into revision number
    $revisionNumber = "0"
    if ($numbers.Count -gt 0) {
        $combined = -join $numbers
        
        # Parse and validate it fits in a 16-bit integer (max 65535)
        if (-not [string]::IsNullOrEmpty($combined)) {
            try {
                $revisionValue = [int]$combined
                if ($revisionValue -le 65535) {
                    $revisionNumber = $revisionValue.ToString()
                } else {
                    # If too large, take a hash to ensure uniqueness while staying within limits
                    $revisionNumber = [Math]::Abs($combined.GetHashCode() % 65535).ToString()
                }
            } catch {
                # If parsing fails, use hash of the pre-release section
                $revisionNumber = [Math]::Abs($preReleaseSection.GetHashCode() % 65535).ToString()
            }
        } else {
            # If empty, use hash of the pre-release section
            $revisionNumber = [Math]::Abs($preReleaseSection.GetHashCode() % 65535).ToString()
        }
    }
    
    # Ensure it's not zero
    if ($revisionNumber -eq "0") {
        $revisionNumber = "1"
    }
    
    # Ensure base version has 3 parts
    $baseParts = $baseVersion.Split('.')
    $normalizedBase = switch ($baseParts.Length) {
        1 { "$($baseParts[0]).0.0" }
        2 { "$($baseParts[0]).$($baseParts[1]).0" }
        default { "$($baseParts[0]).$($baseParts[1]).$($baseParts[2])" }
    }
    
    return "$normalizedBase.$revisionNumber"
}

# If no assembly path is provided, use the script directory to locate the assembly
if ([string]::IsNullOrEmpty($AssemblyPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $assemblyFiles = Get-ChildItem -Path $scriptDir -Filter "RevitAddin.Xunit*.dll"
    
    if ($assemblyFiles.Count -eq 0) {
        Write-Error "Could not find RevitAddin.Xunit assembly in $scriptDir"
        exit 1
    }
    
    $AssemblyPath = $assemblyFiles[0].FullName
    Write-Host "Using assembly: $AssemblyPath"
}

# Extract assembly version from the assembly filename
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($AssemblyPath)
Write-Host "Assembly name: $assemblyName"

# Parse version from filename - handle both standard and pre-release formats
# Expected formats: 
# - RevitAddin.Xunit.2025.0.0
# - RevitAddin.Xunit.2025.1.0-pullrequest0018.103
$versionMatches = [regex]::Match($assemblyName, 'RevitAddin\.Xunit\.(?<version>\d+\.\d+\.\d+(?:-[^.]+(?:\.\d+)*)?)')
if ($versionMatches.Success) {
    $originalVersion = $versionMatches.Groups['version'].Value
    Write-Host "Extracted original version from filename: $originalVersion"
    
    # Normalize version for assembly compatibility
    $assemblyVersion = Normalize-VersionForAssembly -Version $originalVersion
    Write-Host "Normalized assembly version: $assemblyVersion"
    
    # Extract Revit version from original version (first part before any pre-release suffix)
    $RevitVersion = $originalVersion.Split('-')[0].Split('.')[0]
    Write-Host "Extracted Revit version: $RevitVersion"
} else {
    Write-Warning "Could not parse version from assembly name '$assemblyName'"
    Write-Warning "Expected formats:"
    Write-Warning "  - RevitAddin.Xunit.YYYY.M.P (e.g., RevitAddin.Xunit.2025.0.0)"
    Write-Warning "  - RevitAddin.Xunit.YYYY.M.P-prerelease (e.g., RevitAddin.Xunit.2025.1.0-pullrequest0018.103)"
    
    # Ultimate fallback
    $assemblyVersion = "2025.0.0.0"
    $RevitVersion = "2025"
    Write-Host "Using default assembly version: $assemblyVersion"
    Write-Host "Using default Revit version: $RevitVersion"
}

# Look for RevitTestFramework.Common.exe in the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$commonExeFiles = Get-ChildItem -Path $scriptDir -Filter "RevitTestFramework.Common*.exe"

if ($commonExeFiles.Count -gt 0) {
    $commonExePath = $commonExeFiles[0].FullName
    
    # Generate addin manifest (RevitTestFramework.Common.exe handles all file installation)
    $addinDir = Join-Path $env:AppData "Autodesk\Revit\Addins\$RevitVersion"
    if (-not (Test-Path $addinDir)) {
        New-Item -Path $addinDir -ItemType Directory -Force | Out-Null
    }
    
    Write-Host "Generating addin manifest and installing files using RevitTestFramework.Common..."
    
    # Pass the original version if it was extracted from filename, otherwise use normalized version
    $versionToPass = if ($versionMatches.Success) { $originalVersion } else { $assemblyVersion }
    $manifestToolCommand = "& '$commonExePath' generate-manifest --output '$addinDir' --assembly '$AssemblyPath' --assembly-version '$versionToPass'"
    
    Write-Host "Running: $manifestToolCommand"
    Invoke-Expression $manifestToolCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully generated addin manifest and installed files" -ForegroundColor Green
        Write-Host "Addin version: $assemblyVersion" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate addin manifest. Exit code: $LASTEXITCODE"
    }
} else {
    Write-Warning "RevitTestFramework.Common.exe not found in $scriptDir. Addin manifest will not be generated."
    Write-Warning "To generate the addin manifest, run RevitTestFramework.Common.exe manually:"
    Write-Warning "RevitTestFramework.Common.exe generate-manifest --assembly '$AssemblyPath' --assembly-version '$versionToPass'"
}

Write-Host "Installation completed successfully." -ForegroundColor Green
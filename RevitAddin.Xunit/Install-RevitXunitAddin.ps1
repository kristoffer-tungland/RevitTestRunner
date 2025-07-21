# RevitAddin.Xunit Installation Script
# This script generates the Revit addin manifest for RevitAddin.Xunit
# File installation is handled by RevitTestFramework.Common.exe

param(
    [Parameter(Position=0)]
    [string]$AssemblyPath
)

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

# Parse version from filename (expected format: RevitAddin.Xunit.2025.0.0)
$versionMatches = [regex]::Match($assemblyName, 'RevitAddin\.Xunit\.(?<version>\d+\.\d+\.\d+)')
if ($versionMatches.Success) {
    $assemblyVersion = $versionMatches.Groups['version'].Value
    Write-Host "Extracted assembly version from filename: $assemblyVersion"
    
    # Extract Revit version from assembly version (first part)
    $RevitVersion = $assemblyVersion.Split('.')[0]
    Write-Host "Extracted Revit version from assembly version: $RevitVersion"
} else {
    Write-Warning "Could not parse version from assembly name '$assemblyName'"
    Write-Warning "Expected format: RevitAddin.Xunit.YYYY.M.P (e.g., RevitAddin.Xunit.2025.0.0)"
    
    # Ultimate fallback
    $assemblyVersion = "2025.0.0"
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
    $manifestToolCommand = "& '$commonExePath' generate-manifest --output '$addinDir' --assembly '$AssemblyPath' --assembly-version '$assemblyVersion'"
    
    Write-Host "Running: $manifestToolCommand"
    Invoke-Expression $manifestToolCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully generated addin manifest and installed files" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate addin manifest. Exit code: $LASTEXITCODE"
    }
} else {
    Write-Warning "RevitTestFramework.Common.exe not found in $scriptDir. Addin manifest will not be generated."
    Write-Warning "To generate the addin manifest, run RevitTestFramework.Common.exe manually:"
    Write-Warning "RevitTestFramework.Common.exe generate-manifest --assembly '$AssemblyPath' --assembly-version '$assemblyVersion'"
}

Write-Host "Installation completed successfully." -ForegroundColor Green
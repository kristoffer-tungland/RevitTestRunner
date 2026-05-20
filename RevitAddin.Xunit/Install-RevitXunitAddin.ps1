# RevitAddin.Xunit Installation Script
# This script generates the Revit addin manifest for RevitAddin.Xunit
# All version extraction and processing logic is handled by RevitTestFramework.Common.exe

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

# Extract assembly name for display
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($AssemblyPath)
Write-Host "Assembly name: $assemblyName"

# Look for RevitTestFramework.Common.exe in the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$commonExeFiles = Get-ChildItem -Path $scriptDir -Filter "RevitTestFramework.Common*.exe"

if ($commonExeFiles.Count -eq 0) {
    Write-Error "RevitTestFramework.Common.exe not found in $scriptDir. Cannot proceed with addin installation."
    Write-Error "Expected files: RevitTestFramework.Common.exe or RevitTestFramework.Common.*.exe"
    exit 1
}

$commonExePath = $commonExeFiles[0].FullName
Write-Host "Using common tool: $commonExePath"

# Generate addin manifest using the C# tool
# The tool will automatically:
# - Extract version from assembly filename  
# - Determine appropriate Revit version
# - Calculate output directory based on Revit version
Write-Host "Generating addin manifest and installing files..."

$manifestArgs = @(
    "generate-manifest"
    "--assembly" 
    $AssemblyPath
)

Write-Host "Running: $commonExePath $($manifestArgs -join ' ')"
& $commonExePath @manifestArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "Successfully generated addin manifest and installed files" -ForegroundColor Green
} else {
    Write-Error "Failed to generate addin manifest. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Installation completed successfully." -ForegroundColor Green
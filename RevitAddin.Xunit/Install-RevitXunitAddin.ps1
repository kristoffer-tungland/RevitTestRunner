# RevitAddin.Xunit Installation Script
# This script installs the RevitAddin.Xunit component to ProgramData for use with Revit
# It extracts assembly version from the assembly filename (format: RevitAddin.Xunit.2025.0.0.dll)

param(
    [Parameter(Position=0)]
    [string]$AssemblyPath,

    [Parameter()]
    [string]$RevitVersion,
    
    [Parameter()]
    [string]$OutputDir
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
    if ([string]::IsNullOrEmpty($RevitVersion)) {
        $RevitVersion = $assemblyVersion.Split('.')[0]
        Write-Host "Extracted Revit version from assembly version: $RevitVersion"
    }
} else {
    Write-Warning "Could not parse version from assembly name '$assemblyName'"
    Write-Warning "Expected format: RevitAddin.Xunit.YYYY.M.P (e.g., RevitAddin.Xunit.2025.0.0)"
    
    # Ultimate fallback
    $assemblyVersion = "2025.0.0"
    if ([string]::IsNullOrEmpty($RevitVersion)) {
        $RevitVersion = "2025"
    }
    Write-Host "Using default assembly version: $assemblyVersion"
    Write-Host "Using default Revit version: $RevitVersion"
}

# Set output directory
if ([string]::IsNullOrEmpty($OutputDir)) {
    $OutputDir = Join-Path $env:ProgramData "Autodesk\RVT $RevitVersion\RevitTestRunner\$assemblyVersion"
}

Write-Host "Installing RevitAddin.Xunit to $OutputDir"

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
    Write-Host "Created directory: $OutputDir"
}

# Copy the assembly
Copy-Item -Path $AssemblyPath -Destination $OutputDir -Force
Write-Host "Copied: $([System.IO.Path]::GetFileName($AssemblyPath)) -> $OutputDir"

# Copy dependencies from the same directory
$assemblyDir = [System.IO.Path]::GetDirectoryName($AssemblyPath)
$dependencies = Get-ChildItem -Path $assemblyDir -Filter "*.dll" | 
                Where-Object { $_.FullName -ne $AssemblyPath }

foreach ($dep in $dependencies) {
    $destPath = Join-Path $OutputDir $dep.Name
    Copy-Item -Path $dep.FullName -Destination $destPath -Force
    Write-Host "Copied dependency: $($dep.Name) -> $destPath"
}

# Look for RevitTestFramework.Common.exe in the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$commonExeFiles = Get-ChildItem -Path $scriptDir -Filter "RevitTestFramework.Common*.exe"

if ($commonExeFiles.Count -gt 0) {
    $commonExePath = $commonExeFiles[0].FullName
    
    # Generate addin manifest
    $addinDir = Join-Path $env:AppData "Autodesk\Revit\Addins\$RevitVersion"
    if (-not (Test-Path $addinDir)) {
        New-Item -Path $addinDir -ItemType Directory -Force | Out-Null
    }

    $installedAssemblyPath = Join-Path $OutputDir ([System.IO.Path]::GetFileName($AssemblyPath))
    
    Write-Host "Generating addin manifest using RevitTestFramework.Common..."
    $manifestToolCommand = "& '$commonExePath' generate-xunit-manifest --output '$addinDir' --assembly '$installedAssemblyPath' --assembly-version '$assemblyVersion'"
    
    Write-Host "Running: $manifestToolCommand"
    Invoke-Expression $manifestToolCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully generated addin manifest in $addinDir" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate addin manifest. Exit code: $LASTEXITCODE"
    }
} else {
    Write-Warning "RevitTestFramework.Common.exe not found in $scriptDir. Addin manifest will not be generated."
    Write-Warning "To generate the addin manifest, run RevitTestFramework.Common.exe manually:"
    Write-Warning "RevitTestFramework.Common.exe generate-xunit-manifest --assembly '$AssemblyPath' --assembly-version '$assemblyVersion'"
}

Write-Host "Installation completed successfully." -ForegroundColor Green
Write-Host "RevitAddin.Xunit installed to: $OutputDir"
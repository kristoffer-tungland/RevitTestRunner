# RevitAddin.Xunit Installation Script
# This script installs the RevitAddin.Xunit component to ProgramData for use with Revit
# It extracts Revit version and assembly version from the assembly name

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

# Extract Revit version and assembly version from the assembly name
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($AssemblyPath)

# First extract the Revit version
$revitMatches = [regex]::Match($assemblyName, 'RevitAddin\.Xunit\.(?<revitVersion>\d{4})')
if ($revitMatches.Success) {
    if ([string]::IsNullOrEmpty($RevitVersion)) {
        $RevitVersion = $revitMatches.Groups['revitVersion'].Value
        Write-Host "Extracted Revit version: $RevitVersion"
    }

    # Now extract the assembly version that comes after the Revit version
    $assemblyVersionMatches = [regex]::Match($assemblyName, 'RevitAddin\.Xunit\.\d{4}\.(?<assemblyVersion>\d+\.\d+\.\d+)')
    if ($assemblyVersionMatches.Success) {
        $assemblyVersion = $assemblyVersionMatches.Groups['assemblyVersion'].Value
        Write-Host "Extracted assembly version: $assemblyVersion"
    } else {
        $assemblyVersion = "1.0.0"  # Default version if not found
        Write-Host "Using default assembly version: $assemblyVersion"
    }
} else {
    # If no Revit version found in filename, use default assembly version pattern
    $assemblyVersionMatches = [regex]::Match($assemblyName, 'RevitAddin\.Xunit\.(?<assemblyVersion>\d+\.\d+\.\d+)')
    if ($assemblyVersionMatches.Success) {
        $assemblyVersion = $assemblyVersionMatches.Groups['assemblyVersion'].Value
        Write-Host "Extracted assembly version: $assemblyVersion"
    } else {
        $assemblyVersion = "1.0.0"  # Default version
        Write-Host "Using default assembly version: $assemblyVersion"
    }
    
    # Handle Revit version if it wasn't in the filename
    if ([string]::IsNullOrEmpty($RevitVersion)) {
        $RevitVersion = "2025"  # Default Revit version
        Write-Host "Using default Revit version: $RevitVersion"
    }
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
    $manifestToolCommand = "& '$commonExePath' generate-xunit-manifest --output '$addinDir' --revit-version '$RevitVersion' --assembly '$installedAssemblyPath' --package-version '$assemblyVersion'"
    
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
    Write-Warning "RevitTestFramework.Common.exe generate-xunit-manifest --revit-version $RevitVersion --assembly '$AssemblyPath' --package-version '$assemblyVersion'"
}

Write-Host "Installation completed successfully." -ForegroundColor Green
Write-Host "RevitAddin.Xunit installed to: $OutputDir"
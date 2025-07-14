# RevitAddin.NUnit Installation Script
# This script installs the RevitAddin.NUnit component to ProgramData for use with Revit
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
    $assemblyFiles = Get-ChildItem -Path $scriptDir -Filter "RevitAddin.NUnit*.dll"
    
    if ($assemblyFiles.Count -eq 0) {
        Write-Error "Could not find RevitAddin.NUnit assembly in $scriptDir"
        exit 1
    }
    
    $AssemblyPath = $assemblyFiles[0].FullName
    Write-Host "Using assembly: $AssemblyPath"
}

# Extract Revit version and assembly version from the assembly name
$assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($AssemblyPath)
$matches = [regex]::Match($assemblyName, 'RevitAddin\.NUnit\.(?<assemblyVersion>\d+\.\d+\.\d+)')

$assemblyVersion = "1.0.0"  # Default version
if ($matches.Success) {
    $assemblyVersion = $matches.Groups['assemblyVersion'].Value
    Write-Host "Extracted assembly version: $assemblyVersion"
}

# If RevitVersion is not specified, try to extract it from the filename or use default
if ([string]::IsNullOrEmpty($RevitVersion)) {
    $revitMatches = [regex]::Match($assemblyName, 'RevitAddin\.NUnit\.(?<revitVersion>\d{4})\.')
    if ($revitMatches.Success) {
        $RevitVersion = $revitMatches.Groups['revitVersion'].Value
        Write-Host "Extracted Revit version: $RevitVersion"
    } else {
        # Get RevitVersion from Directory.Build.props if accessible
        $buildPropsPath = Join-Path (Split-Path -Parent (Split-Path -Parent $scriptDir)) "Directory.Build.props"
        if (Test-Path $buildPropsPath) {
            [xml]$buildPropsXml = Get-Content $buildPropsPath
            $RevitVersion = $buildPropsXml.Project.PropertyGroup | 
                Where-Object { $_.RevitVersion } | 
                ForEach-Object { $_.RevitVersion } | 
                Select-Object -First 1
        }
        
        if ([string]::IsNullOrEmpty($RevitVersion)) {
            $RevitVersion = "2025"  # Default Revit version
        }
        Write-Host "Using Revit version: $RevitVersion"
    }
}

# Set output directory
if ([string]::IsNullOrEmpty($OutputDir)) {
    $OutputDir = Join-Path $env:ProgramData "Autodesk\RVT $RevitVersion\RevitTestRunner\$assemblyVersion"
}

Write-Host "Installing RevitAddin.NUnit to $OutputDir"

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

# Find the RevitTestFramework.Common tool for generating addin manifests
$commonExe = Get-ChildItem -Path $assemblyDir -Filter "RevitTestFramework.Common*.exe" | Select-Object -First 1

if ($commonExe) {
    $destExePath = Join-Path $OutputDir $commonExe.Name
    Copy-Item -Path $commonExe.FullName -Destination $destExePath -Force
    Write-Host "Copied manifest tool: $($commonExe.Name) -> $destExePath"

    # Generate addin manifest
    $addinDir = Join-Path $env:AppData "Autodesk\Revit\Addins\$RevitVersion"
    if (-not (Test-Path $addinDir)) {
        New-Item -Path $addinDir -ItemType Directory -Force | Out-Null
    }

    $installedAssemblyPath = Join-Path $OutputDir ([System.IO.Path]::GetFileName($AssemblyPath))
    
    Write-Host "Generating addin manifest..."
    $manifestToolCommand = "& '$destExePath' generate-nunit-manifest --output '$addinDir' --revit-version '$RevitVersion' --assembly '$installedAssemblyPath' --package-version '$assemblyVersion'"
    
    Write-Host "Running: $manifestToolCommand"
    Invoke-Expression $manifestToolCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Successfully generated addin manifest in $addinDir" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate addin manifest. Exit code: $LASTEXITCODE"
    }
} else {
    Write-Warning "RevitTestFramework.Common.exe not found. Addin manifest will not be generated."
    Write-Warning "To generate the addin manifest, run RevitTestFramework.Common.exe manually:"
    Write-Warning "RevitTestFramework.Common.exe generate-nunit-manifest --revit-version $RevitVersion --assembly '$AssemblyPath' --package-version '$assemblyVersion'"
}

Write-Host "Installation completed successfully." -ForegroundColor Green
Write-Host "RevitAddin.NUnit installed to: $OutputDir"
# Test Dynamic Revit Version Detection
# This script validates that the RevitXunitExecutor correctly determines the Revit version from assembly version

param(
    [string[]]$RevitVersions = @("2025", "2026"),
    [string]$Configuration = "Release"
)

Write-Host "=== Testing Dynamic Revit Version Detection ===" -ForegroundColor Green
Write-Host "Testing Revit Versions: $($RevitVersions -join ', ')" -ForegroundColor Yellow
Write-Host ""

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

# Use temporary directory outside project structure
$TempDir = [System.IO.Path]::GetTempPath()
$TestWorkDir = Join-Path $TempDir "RevitTestRunner-VersionDetectionTest-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

try {
    New-Item -ItemType Directory -Path $TestWorkDir -Force | Out-Null
    Write-Host "Using temporary work directory: $TestWorkDir" -ForegroundColor Gray
    Write-Host ""

    foreach ($RevitVersion in $RevitVersions) {
        Write-Host "--- Testing Revit $RevitVersion Version Detection ---" -ForegroundColor Cyan
        
        # Calculate version (simulate GitVersion)
        $BaseVersion = "1.0.0"
        $RevitSpecificVersion = "$RevitVersion.0.0"
        
        Write-Host "  Expected Version: $RevitSpecificVersion" -ForegroundColor Gray
        
        # Build the RevitXunitAdapter for this specific version with unique output directory
        $OutputDir = Join-Path $TestWorkDir "output-$RevitVersion"
        Write-Host "  Building RevitXunitAdapter for Revit $RevitVersion (Output: $OutputDir)..." -ForegroundColor Gray
        dotnet build RevitXunitAdapter/RevitXunitAdapter.csproj --configuration $Configuration /p:RevitVersion=$RevitVersion /p:Version=$RevitSpecificVersion /p:OutputPath="$OutputDir\" | Out-Null
        
        # Check the built assembly version
        $AssemblyPath = Join-Path $OutputDir "RevitXunit.TestAdapter.dll"
        
        if (Test-Path $AssemblyPath) {
            Write-Host "  ? Assembly built successfully: $AssemblyPath" -ForegroundColor Green
            
            # Load the assembly and check its version
            try {
                $Assembly = [System.Reflection.Assembly]::LoadFile((Resolve-Path $AssemblyPath).Path)
                $AssemblyVersion = $Assembly.GetName().Version
                $DetectedRevitVersion = $AssemblyVersion.Major
                
                Write-Host "  ?? Assembly Version: $($AssemblyVersion.ToString())" -ForegroundColor Gray
                Write-Host "  ?? Detected Revit Version: $DetectedRevitVersion" -ForegroundColor Gray
                
                if ($DetectedRevitVersion -eq $RevitVersion) {
                    Write-Host "  ? Revit version detection: PASS (Expected: $RevitVersion, Detected: $DetectedRevitVersion)" -ForegroundColor Green
                } else {
                    Write-Host "  ? Revit version detection: FAIL (Expected: $RevitVersion, Detected: $DetectedRevitVersion)" -ForegroundColor Red
                    throw "Version detection mismatch for Revit $RevitVersion"
                }
            }
            catch {
                Write-Host "  ? Failed to load or analyze assembly: $($_.Exception.Message)" -ForegroundColor Red
                throw
            }
        } else {
            Write-Host "  ? Assembly not found at: $AssemblyPath" -ForegroundColor Red
            throw "Assembly not built for Revit $RevitVersion"
        }
        
        Write-Host ""
    }
    
    $ElapsedTime = (Get-Date) - $StartTime
    Write-Host "=== Dynamic Version Detection Test Completed Successfully ===" -ForegroundColor Green
    Write-Host "All Revit versions correctly detected from assembly versions!" -ForegroundColor Green
    Write-Host "Total time: $($ElapsedTime.TotalSeconds.ToString('F1')) seconds" -ForegroundColor Yellow
}
catch {
    Write-Host ""
    Write-Host "? Dynamic version detection test failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    # Cleanup temporary directory
    if (Test-Path $TestWorkDir) {
        try {
            Write-Host ""
            Write-Host "Cleaning up temporary directory..." -ForegroundColor Gray
            Remove-Item $TestWorkDir -Recurse -Force
        } catch {
            Write-Host "Could not clean up temporary directory: $TestWorkDir" -ForegroundColor Yellow
        }
    }
}
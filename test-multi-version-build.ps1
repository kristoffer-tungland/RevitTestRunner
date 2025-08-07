# Test Multi-Version Build Script
# This script tests building and packaging for multiple Revit versions

param(
    [string[]]$RevitVersions = @("2025", "2026"),
    [string]$Configuration = "Release"
)

Write-Host "=== Testing Multi-Version Build ===" -ForegroundColor Green
Write-Host "Revit Versions: $($RevitVersions -join ', ')" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host ""

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

# Use temporary directory outside project structure
$TempDir = [System.IO.Path]::GetTempPath()
$TestWorkDir = Join-Path $TempDir "RevitTestRunner-MultiVersionTest-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

try {
    New-Item -ItemType Directory -Path $TestWorkDir -Force | Out-Null
    Write-Host "Using temporary work directory: $TestWorkDir" -ForegroundColor Gray
    Write-Host ""

    foreach ($RevitVersion in $RevitVersions) {
        Write-Host "--- Building for Revit $RevitVersion ---" -ForegroundColor Cyan
        
        # Clean previous artifacts
        $OutputDir = Join-Path $TestWorkDir "packages-$RevitVersion"
        if (Test-Path $OutputDir) {
            Remove-Item $OutputDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
        
        # Calculate version (simulate GitVersion)
        $BaseVersion = "1.0.0"
        $RevitSpecificVersion = "$RevitVersion.0.0"
        
        Write-Host "  Version: $RevitSpecificVersion" -ForegroundColor Gray
        
        # Restore packages
        Write-Host "  Restoring packages..." -ForegroundColor Gray
        dotnet restore RevitTestRunner.sln /p:RevitVersion=$RevitVersion | Out-Null
        
        # Build solution
        Write-Host "  Building solution..." -ForegroundColor Gray
        dotnet build RevitTestRunner.sln --configuration $Configuration --no-restore /p:RevitVersion=$RevitVersion /p:Version=$RevitSpecificVersion | Out-Null
        
        # Pack NuGet package
        Write-Host "  Creating NuGet package..." -ForegroundColor Gray
        dotnet pack RevitXunitAdapter/RevitXunitAdapter.csproj --configuration $Configuration --no-build --output $OutputDir /p:RevitVersion=$RevitVersion /p:Version=$RevitSpecificVersion | Out-Null
        
        # Verify package
        $Package = Get-ChildItem -Path $OutputDir -Filter "*.nupkg" | Select-Object -First 1
        if ($Package) {
            Write-Host "  ? Package created: $($Package.Name)" -ForegroundColor Green
            Write-Host "  ?? Size: $([math]::Round($Package.Length / 1MB, 2)) MB" -ForegroundColor Green
        } else {
            throw "No package found for Revit $RevitVersion"
        }
        
        Write-Host ""
    }
    
    $ElapsedTime = (Get-Date) - $StartTime
    Write-Host "=== Build Test Completed Successfully ===" -ForegroundColor Green
    Write-Host "Total time: $($ElapsedTime.TotalSeconds.ToString('F1')) seconds" -ForegroundColor Yellow
    
    # Show all created packages
    Write-Host ""
    Write-Host "Created packages:" -ForegroundColor Yellow
    foreach ($RevitVersion in $RevitVersions) {
        $OutputDir = Join-Path $TestWorkDir "packages-$RevitVersion"
        $Packages = Get-ChildItem -Path $OutputDir -Filter "*.nupkg" -ErrorAction SilentlyContinue
        foreach ($Package in $Packages) {
            Write-Host "  - $($Package.Name)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host ""
    Write-Host "? Build test failed: $($_.Exception.Message)" -ForegroundColor Red
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
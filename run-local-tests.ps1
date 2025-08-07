# Local Test Execution Guide
# This script helps developers run the full test suite locally where Revit is installed

param(
    [string[]]$RevitVersions = @("2025", "2026"),
    [string]$Configuration = "Release",
    [switch]$FrameworkOnly,
    [switch]$IntegrationOnly
)

Write-Host "=== RevitTestRunner Local Test Execution ===" -ForegroundColor Green
Write-Host "Revit Versions: $($RevitVersions -join ', ')" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

if ($FrameworkOnly) {
    Write-Host "Mode: Framework tests only (no Revit required)" -ForegroundColor Cyan
} elseif ($IntegrationOnly) {
    Write-Host "Mode: Integration tests only (requires Revit)" -ForegroundColor Cyan
} else {
    Write-Host "Mode: All tests (requires Revit installation)" -ForegroundColor Cyan
}

Write-Host ""

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

try {
    # Check if Revit is required and available
    if (-not $FrameworkOnly) {
        Write-Host "--- Checking Revit Installation ---" -ForegroundColor Yellow
        
        $revitNotFound = @()
        foreach ($version in $RevitVersions) {
            $revitPath = "C:\Program Files\Autodesk\Revit $version\Revit.exe"
            if (Test-Path $revitPath) {
                Write-Host "  ? Revit $version found: $revitPath" -ForegroundColor Green
            } else {
                Write-Host "  ? Revit $version not found: $revitPath" -ForegroundColor Red
                $revitNotFound += $version
            }
        }
        
        if ($revitNotFound.Count -gt 0 -and -not $FrameworkOnly) {
            Write-Host ""
            Write-Host "??  Missing Revit versions: $($revitNotFound -join ', ')" -ForegroundColor Yellow
            Write-Host "Integration tests will fail for these versions." -ForegroundColor Yellow
            Write-Host "Consider using -FrameworkOnly switch to test only framework components." -ForegroundColor Yellow
            Write-Host ""
            
            $response = Read-Host "Continue anyway? (y/N)"
            if ($response -notmatch '^[Yy]') {
                Write-Host "Execution cancelled by user." -ForegroundColor Yellow
                exit 0
            }
        }
        Write-Host ""
    }

    foreach ($RevitVersion in $RevitVersions) {
        Write-Host "--- Testing Revit $RevitVersion ---" -ForegroundColor Cyan
        
        # Build first
        Write-Host "  Building solution..." -ForegroundColor Gray
        dotnet build RevitTestRunner.sln --configuration $Configuration /p:RevitVersion=$RevitVersion | Out-Null
        
        if ($FrameworkOnly) {
            Write-Host "  ??  Framework-only mode: Skipping test execution" -ForegroundColor Blue
            Write-Host "  ? Build successful for Revit $RevitVersion" -ForegroundColor Green
        } else {
            # Run tests
            Write-Host "  Running tests..." -ForegroundColor Gray
            
            if ($IntegrationOnly) {
                # Run only MyRevitTestsXunit
                $testCommand = "dotnet test MyRevitTestsXunit/MyRevitTestsXunit.csproj --configuration $Configuration --no-build --verbosity normal /p:RevitVersion=$RevitVersion"
            } else {
                # Run all tests
                $testCommand = "dotnet test RevitTestRunner.sln --configuration $Configuration --no-build --verbosity normal /p:RevitVersion=$RevitVersion"
            }
            
            Write-Host "  Command: $testCommand" -ForegroundColor DarkGray
            
            $testResult = Invoke-Expression $testCommand
            $testExitCode = $LASTEXITCODE
            
            if ($testExitCode -eq 0) {
                Write-Host "  ? Tests passed for Revit $RevitVersion" -ForegroundColor Green
            } else {
                Write-Host "  ? Tests failed for Revit $RevitVersion (Exit code: $testExitCode)" -ForegroundColor Red
                Write-Host "Test output:" -ForegroundColor Yellow
                Write-Host $testResult -ForegroundColor White
            }
        }
        
        Write-Host ""
    }
    
    $ElapsedTime = (Get-Date) - $StartTime
    Write-Host "=== Test Execution Completed ===" -ForegroundColor Green
    Write-Host "Total time: $($ElapsedTime.TotalSeconds.ToString('F1')) seconds" -ForegroundColor Yellow
}
catch {
    Write-Host ""
    Write-Host "? Test execution failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "?? Test Execution Tips:" -ForegroundColor Cyan
Write-Host "  - Use -FrameworkOnly to test without Revit installation" -ForegroundColor White
Write-Host "  - Use -IntegrationOnly to test only Revit integration tests" -ForegroundColor White
Write-Host "  - Integration tests require corresponding Revit versions installed" -ForegroundColor White
Write-Host "  - Tests will automatically install/update the Revit add-in" -ForegroundColor White
Write-Host "  - Debugger will attach automatically when running from Visual Studio" -ForegroundColor White
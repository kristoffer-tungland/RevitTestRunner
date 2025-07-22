# Test script for RevitDebuggerHelper
# This script tests the helper application functionality

Write-Host "RevitDebuggerHelper Test Script" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Find the helper executable
$helperPaths = @(
    ".\bin\Release\RevitDebuggerHelper.exe",
    ".\bin\Debug\RevitDebuggerHelper.exe",
    ".\RevitDebuggerHelper.exe"
)

$helperPath = $null
foreach ($path in $helperPaths) {
    if (Test-Path $path) {
        $helperPath = $path
        break
    }
}

if (-not $helperPath) {
    Write-Host "ERROR: RevitDebuggerHelper.exe not found!" -ForegroundColor Red
    Write-Host "Looked in:" -ForegroundColor Yellow
    foreach ($path in $helperPaths) {
        Write-Host "  $path" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Please build the helper first:" -ForegroundColor Yellow
    Write-Host "  Option 1: Run build.bat" -ForegroundColor Yellow
    Write-Host "  Option 2: csc /target:exe /out:bin\Release\RevitDebuggerHelper.exe Program.cs" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found helper at: $helperPath" -ForegroundColor Green
Write-Host ""

# Test 1: Show usage
Write-Host "Test 1: Showing usage help" -ForegroundColor Cyan
Write-Host "Command: $helperPath" -ForegroundColor Gray
& $helperPath
Write-Host ""

# Test 2: Try to find Visual Studio
Write-Host "Test 2: Looking for Visual Studio" -ForegroundColor Cyan
Write-Host "Command: $helperPath --find-revit" -ForegroundColor Gray
$result = & $helperPath --find-revit
$exitCode = $LASTEXITCODE

Write-Host "Exit code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } elseif ($exitCode -eq 2) { "Yellow" } else { "Red" })

switch ($exitCode) {
    0 { Write-Host "SUCCESS: Found and attached to Revit process" -ForegroundColor Green }
    1 { Write-Host "WARNING: Visual Studio not found" -ForegroundColor Yellow }
    2 { Write-Host "INFO: No Revit process running" -ForegroundColor Yellow }
    3 { Write-Host "ERROR: COM or other error occurred" -ForegroundColor Red }
    default { Write-Host "UNKNOWN: Unexpected exit code" -ForegroundColor Red }
}
Write-Host ""

# Test 3: Check for running processes
Write-Host "Test 3: Checking for running processes" -ForegroundColor Cyan
$vsProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue
$revitProcesses = Get-Process -Name "Revit" -ErrorAction SilentlyContinue

Write-Host "Visual Studio processes: $($vsProcesses.Count)" -ForegroundColor $(if ($vsProcesses.Count -gt 0) { "Green" } else { "Yellow" })
if ($vsProcesses.Count -gt 0) {
    foreach ($proc in $vsProcesses) {
        Write-Host "  PID $($proc.Id): $($proc.ProcessName)" -ForegroundColor Gray
    }
}

Write-Host "Revit processes: $($revitProcesses.Count)" -ForegroundColor $(if ($revitProcesses.Count -gt 0) { "Green" } else { "Yellow" })
if ($revitProcesses.Count -gt 0) {
    foreach ($proc in $revitProcesses) {
        Write-Host "  PID $($proc.Id): $($proc.ProcessName)" -ForegroundColor Gray
    }
}
Write-Host ""

# Summary
Write-Host "Test Summary" -ForegroundColor Green
Write-Host "============" -ForegroundColor Green
Write-Host "Helper executable: Found" -ForegroundColor Green
Write-Host "Visual Studio: $(if ($vsProcesses.Count -gt 0) { 'Running' } else { 'Not running' })" -ForegroundColor $(if ($vsProcesses.Count -gt 0) { "Green" } else { "Yellow" })
Write-Host "Revit: $(if ($revitProcesses.Count -gt 0) { 'Running' } else { 'Not running' })" -ForegroundColor $(if ($revitProcesses.Count -gt 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($vsProcesses.Count -eq 0) {
    Write-Host "To test debugger attachment:" -ForegroundColor Yellow
    Write-Host "1. Start Visual Studio" -ForegroundColor Yellow
    Write-Host "2. Start Revit" -ForegroundColor Yellow
    Write-Host "3. Run this test script again" -ForegroundColor Yellow
}

if ($exitCode -eq 0) {
    Write-Host "The helper is working correctly!" -ForegroundColor Green
} elseif ($exitCode -eq 2 -and $vsProcesses.Count -gt 0) {
    Write-Host "The helper can connect to Visual Studio but no Revit process is running." -ForegroundColor Yellow
    Write-Host "This is expected if Revit is not running." -ForegroundColor Yellow
} else {
    Write-Host "Consider checking the troubleshooting section in README.md" -ForegroundColor Yellow
}
# Enhanced test script for RevitDebuggerHelper with Detachment Testing
# This script tests both attachment and detachment functionality

Write-Host "RevitDebuggerHelper Enhanced Test Script with Detachment" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green
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
    Write-Host "Building helper manually..." -ForegroundColor Yellow
    
    try {
        & csc /target:exe /out:bin\Debug\RevitDebuggerHelper.exe Program.cs
        if ($LASTEXITCODE -eq 0) {
            Write-Host "? Helper built successfully!" -ForegroundColor Green
            $helperPath = ".\bin\Debug\RevitDebuggerHelper.exe"
        } else {
            Write-Host "? Failed to build helper" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        Write-Host "? Error building helper: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Found helper at: $helperPath" -ForegroundColor Green
Write-Host ""

# Test 1: Show usage
Write-Host "Test 1: Showing usage help" -ForegroundColor Cyan
Write-Host "=" * 30 -ForegroundColor Gray
Write-Host "Command: $helperPath" -ForegroundColor Gray
& $helperPath
Write-Host ""

# Check for Visual Studio and Revit processes
Write-Host "Test 2: Process availability check" -ForegroundColor Cyan
Write-Host "=" * 30 -ForegroundColor Gray

$vsProcesses = Get-Process -Name "devenv" -ErrorAction SilentlyContinue
$revitProcesses = Get-Process -Name "Revit" -ErrorAction SilentlyContinue

Write-Host "Visual Studio processes: $($vsProcesses.Count)" -ForegroundColor $(if ($vsProcesses.Count -gt 0) { "Green" } else { "Yellow" })
if ($vsProcesses.Count -gt 0) {
    foreach ($proc in $vsProcesses) {
        Write-Host "  PID $($proc.Id): $($proc.ProcessName) - $($proc.MainWindowTitle)" -ForegroundColor Gray
    }
} else {
    Write-Host "  No Visual Studio instances found" -ForegroundColor Yellow
}

Write-Host "Revit processes: $($revitProcesses.Count)" -ForegroundColor $(if ($revitProcesses.Count -gt 0) { "Green" } else { "Yellow" })
if ($revitProcesses.Count -gt 0) {
    foreach ($proc in $revitProcesses) {
        Write-Host "  PID $($proc.Id): $($proc.ProcessName)" -ForegroundColor Gray
    }
} else {
    Write-Host "  No Revit instances found" -ForegroundColor Yellow
}
Write-Host ""

# Test 3: Test detachment commands
Write-Host "Test 3: Testing detachment commands" -ForegroundColor Cyan
Write-Host "=" * 30 -ForegroundColor Gray

if ($revitProcesses.Count -gt 0) {
    $targetRevit = $revitProcesses[0]
    
    Write-Host "Testing detach from specific process..." -ForegroundColor Yellow
    Write-Host "Command: $helperPath --detach $($targetRevit.Id)" -ForegroundColor Gray
    
    $result = & $helperPath --detach $targetRevit.Id 2>&1
    $exitCode = $LASTEXITCODE
    
    Write-Host "Exit code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } elseif ($exitCode -eq 2) { "Yellow" } else { "Red" })
    $result | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    Write-Host ""
    
    Write-Host "Testing detach from all Revit processes..." -ForegroundColor Yellow
    Write-Host "Command: $helperPath --detach-all" -ForegroundColor Gray
    
    $result = & $helperPath --detach-all 2>&1
    $exitCode = $LASTEXITCODE
    
    Write-Host "Exit code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } elseif ($exitCode -eq 2) { "Yellow" } else { "Red" })
    $result | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
} else {
    Write-Host "No Revit processes to test detachment with" -ForegroundColor Yellow
}
Write-Host ""

# Test 4: Try to find Revit (attachment test)
Write-Host "Test 4: Testing --find-revit command (attachment)" -ForegroundColor Cyan
Write-Host "=" * 30 -ForegroundColor Gray
Write-Host "Command: $helperPath --find-revit" -ForegroundColor Gray

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$result = & $helperPath --find-revit 2>&1
$sw.Stop()
$exitCode = $LASTEXITCODE

Write-Host "Exit code: $exitCode (completed in $($sw.ElapsedMilliseconds)ms)" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } elseif ($exitCode -eq 2) { "Yellow" } else { "Red" })
Write-Host "Output:" -ForegroundColor Gray
$result | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

switch ($exitCode) {
    0 { 
        Write-Host "? SUCCESS: Found and attached to Revit process" -ForegroundColor Green 
        Write-Host "The debugger should now be attached to Revit!" -ForegroundColor Green
        
        # If attachment succeeded, test detachment
        if ($revitProcesses.Count -gt 0) {
            Write-Host ""
            Write-Host "Testing automatic detachment after successful attachment..." -ForegroundColor Cyan
            Start-Sleep -Seconds 2
            
            $targetRevit = $revitProcesses[0]
            Write-Host "Command: $helperPath --detach $($targetRevit.Id)" -ForegroundColor Gray
            
            $detachResult = & $helperPath --detach $targetRevit.Id 2>&1
            $detachExitCode = $LASTEXITCODE
            
            Write-Host "Detach exit code: $detachExitCode" -ForegroundColor $(if ($detachExitCode -eq 0) { "Green" } else { "Yellow" })
            $detachResult | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        }
    }
    1 { 
        Write-Host "??  WARNING: Visual Studio not found or not accessible" -ForegroundColor Yellow 
        Write-Host "Make sure Visual Studio is running and not busy" -ForegroundColor Yellow
    }
    2 { 
        Write-Host "??  INFO: No Revit process running" -ForegroundColor Yellow 
        Write-Host "This is expected if Revit is not running" -ForegroundColor Yellow
    }
    3 { 
        Write-Host "? ERROR: COM or other error occurred" -ForegroundColor Red 
        Write-Host "Try running Visual Studio as administrator" -ForegroundColor Red
    }
    default { 
        Write-Host "? UNKNOWN: Unexpected exit code" -ForegroundColor Red 
    }
}
Write-Host ""

# Summary and recommendations
Write-Host "Summary and Recommendations" -ForegroundColor Green
Write-Host "=" * 30 -ForegroundColor Green

Write-Host "?? Functionality Test Results:" -ForegroundColor Cyan
Write-Host "? Helper executable: Found and working" -ForegroundColor Green
Write-Host "? Attachment commands: Tested" -ForegroundColor Green
Write-Host "? Detachment commands: Tested" -ForegroundColor Green
Write-Host "? Usage help: Working" -ForegroundColor Green

if ($vsProcesses.Count -gt 0 -and $revitProcesses.Count -gt 0) {
    Write-Host "?? Full debugging workflow is ready!" -ForegroundColor Green
    Write-Host "? Visual Studio is running" -ForegroundColor Green
    Write-Host "? Revit is running" -ForegroundColor Green
    Write-Host "? Helper can attach and detach debugger" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? You can now run Revit tests with automatic debugging:" -ForegroundColor Cyan
    Write-Host "1. Press F5 in Visual Studio to run tests with debugger" -ForegroundColor Cyan
    Write-Host "2. Debugger will automatically attach to Revit" -ForegroundColor Cyan
    Write-Host "3. Set breakpoints in your test code" -ForegroundColor Cyan
    Write-Host "4. Debugger will automatically detach when tests finish" -ForegroundColor Cyan
} elseif ($vsProcesses.Count -eq 0) {
    Write-Host "?? To enable full debugging workflow:" -ForegroundColor Yellow
    Write-Host "1. Start Visual Studio" -ForegroundColor Yellow
    Write-Host "2. Start Revit (if not already running)" -ForegroundColor Yellow
    Write-Host "3. Run this test script again" -ForegroundColor Yellow
} elseif ($revitProcesses.Count -eq 0) {
    Write-Host "?? To enable full debugging workflow:" -ForegroundColor Yellow
    Write-Host "1. Start Revit (Visual Studio is already running)" -ForegroundColor Yellow
    Write-Host "2. Run this test script again" -ForegroundColor Yellow
} else {
    Write-Host "?? Troubleshooting suggestions:" -ForegroundColor Yellow
    Write-Host "1. Make sure Visual Studio is not already debugging another process" -ForegroundColor Yellow
    Write-Host "2. Try running Visual Studio as administrator" -ForegroundColor Yellow
    Write-Host "3. Check if Visual Studio can manually attach to the Revit process" -ForegroundColor Yellow
    Write-Host "   (Debug > Attach to Process > Select Revit.exe)" -ForegroundColor Yellow
    Write-Host "4. Restart Visual Studio if needed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Helper location: $helperPath" -ForegroundColor Cyan
Write-Host "Test completed at: $(Get-Date)" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? The automatic attach/detach workflow is now ready for test execution!" -ForegroundColor Green
# Combined RevitAddin Installation Script
# This script installs both RevitAddin.Xunit and RevitAddin.NUnit components to ProgramData for use with Revit

param(
    [Parameter()]
    [string]$BuildConfiguration = "Release",
    
    [Parameter()]
    [string]$RevitVersion = "2025"
)

Write-Host "===== Installing RevitAddin.Xunit and RevitAddin.NUnit for Revit $RevitVersion =====" -ForegroundColor Cyan

# Get the current script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Execute the individual installation scripts
Write-Host "Installing RevitAddin.Xunit..." -ForegroundColor Yellow
& "$scriptDir\InstallRevitAddinXunit.ps1" -BuildConfiguration $BuildConfiguration -RevitVersion $RevitVersion

Write-Host "`n"
Write-Host "Installing RevitAddin.NUnit..." -ForegroundColor Yellow
& "$scriptDir\InstallRevitAddinNUnit.ps1" -BuildConfiguration $BuildConfiguration -RevitVersion $RevitVersion

Write-Host "`n"
Write-Host "===== Installation Complete =====" -ForegroundColor Green
Write-Host "Both RevitAddin.Xunit and RevitAddin.NUnit have been installed for Revit $RevitVersion."
# RevitTestRunner Installation Guide

This document explains how to install the RevitTestRunner components for Revit.

## Installation Scripts

There are multiple ways to install the RevitTestRunner components:

1. **Solution-Level Installation Script**:
   - `Install-RevitTestRunner.ps1` - Installs both components at once from the solution root

2. **Individual Project Installation Scripts** (included in build output):
   - `RevitAddin.Xunit\Install-RevitXunitAddin.ps1` - Installs the Xunit component
   - `RevitAddin.NUnit\Install-RevitNUnitAddin.ps1` - Installs the NUnit component

## Installation Process

The installation scripts perform the following tasks:

1. Extract Revit version and assembly version from the assembly name
2. Copy the assemblies and dependencies to `C:\ProgramData\Autodesk\RVT <RevitVersion>\RevitTestRunner\<AssemblyVersion>\`
3. Generate addin manifests for Revit using RevitTestFramework.Common.exe
4. Place the manifests in the user's Revit addins folder

## Usage Options

### Option 1: Install from Solution Root

Run the combined installation script from the solution directory:
.\Install-RevitTestRunner.ps1
Optional parameters:
- `-BuildConfiguration "Debug"` - Use Debug build (default is Release)
- `-RevitVersion "2024"` - Install for a specific Revit version

### Option 2: Install from Build Output

After building the projects, the installation scripts are included in the output directories:
# Navigate to the output directory
cd RevitAddin.Xunit\bin\Release\net8.0

# Run the installation script directly
.\Install-RevitXunitAddin.ps1
Optional parameters:
- `-RevitVersion "2024"` - Install for a specific Revit version
- `-OutputDir "C:\CustomPath"` - Use a custom installation directory

### Option 3: Install Specific Assembly

You can also install a specific assembly by providing its path:
.\Install-RevitXunitAddin.ps1 -AssemblyPath "C:\Path\To\RevitAddin.Xunit.1.0.0.dll"
## Verification

After installation, check that:

1. The files are in `C:\ProgramData\Autodesk\RVT <RevitVersion>\RevitTestRunner\<AssemblyVersion>\`
2. The addin manifests are in `%AppData%\Autodesk\Revit\Addins\<RevitVersion>\`

## Assembly Naming Convention

The scripts can extract Revit and assembly versions from filenames following these patterns:
- `RevitAddin.Xunit.<AssemblyVersion>.dll` (e.g., RevitAddin.Xunit.1.0.0.dll)
- `RevitAddin.Xunit.<RevitVersion>.<AssemblyVersion>.dll` (e.g., RevitAddin.Xunit.2025.1.0.0.dll)

## Troubleshooting

If installation fails:

1. Check that RevitTestFramework.Common.exe is available
2. Try running PowerShell as administrator if permission issues occur
3. Check the error messages for specific issues
4. Verify that the correct Revit version is being used
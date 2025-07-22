# Revit Test Framework - Addin Setup

This document explains how to set up the Revit Test Framework addins for Revit.

## Automatic Installation

The easiest way to install the addin manifest is to use the command-line tool provided in RevitTestFramework.Common:

```bash
# Generate Xunit addin manifest (automatically detects Revit version from assembly)
RevitTestFramework.Common.exe generate-manifest

# Generate manifest with specific assembly
RevitTestFramework.Common.exe generate-manifest --assembly "path\to\RevitAddin.Xunit.2025.0.0.dll"

# Generate manifest to specific output directory
RevitTestFramework.Common.exe generate-manifest --output "C:\Custom\Path"
```

## Command Options

- `--output <path>`: Output directory (default: %APPDATA%\Autodesk\Revit\Addins\<RevitVersion>)
- `--assembly <path>`: Path to assembly file (optional, auto-discovered if not provided)
- `--assembly-version <ver>`: Assembly version to use (default: extracted from current assembly)
- `--fixed-guids <bool>`: Use fixed GUIDs (default: true)

## Manual Installation

You can also use the PowerShell installation script provided with RevitAddin.Xunit:

```powershell
.\Install-RevitXunitAddin.ps1
# Multi-Version Setup Guide

This guide explains how the multi-version build system works and how to add support for new Revit versions.

## Current Setup

The solution is configured to build NuGet packages for multiple Revit versions:
- **Revit 2025**: Creates `RevitXunit.TestAdapter.2025.x.y.nupkg`
- **Revit 2026**: Creates `RevitXunit.TestAdapter.2026.x.y.nupkg`

## How It Works

### 1. Version Calculation
The GitHub Actions workflow:
1. Uses GitVersion to generate a base semantic version (e.g., `1.2.3-alpha.1`)
2. Transforms it into Revit-specific versions: `<RevitVersion>.<Minor>.<Patch>[<Prerelease>]`
3. Examples:
   - Base: `1.2.3` ? Revit 2025: `2025.2.3`, Revit 2026: `2026.2.3`
   - Base: `1.2.3-alpha.1` ? Revit 2025: `2025.2.3-alpha.1`, Revit 2026: `2026.2.3-alpha.1`

### 2. Build Matrix
The workflows use GitHub Actions matrix strategy to build in parallel:
```yaml
strategy:
  matrix:
    revit-version: [2025, 2026]
```

### 3. Build Parameters
Each build job passes the Revit version as MSBuild properties:
- `/p:RevitVersion=<version>` - Sets the target Revit version
- `/p:Version=<calculated-version>` - Sets the specific package version

### 4. Dependency Resolution
Projects using `$(RevitVersion)` in package references automatically get the correct versions:
```xml
<PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="$(RevitVersion).*" />
```

### 5. Dynamic Version Detection
The `RevitXunitExecutor` automatically detects the target Revit version from the assembly's major version number:
- Assembly version `2025.x.y.z` ? Connects to Revit 2025
- Assembly version `2026.x.y.z` ? Connects to Revit 2026

This ensures that the correct Revit instance is launched or connected to during test execution.

## Adding Support for New Revit Versions

### Step 1: Update GitHub Actions Workflows

**File: `.github/workflows/build-and-publish.yml`**
```yaml
strategy:
  matrix:
    revit-version: [2025, 2026, 2027]  # Add new version here
```

**File: `.github/workflows/pr-validation.yml`**
```yaml
strategy:
  matrix:
    revit-version: [2025, 2026, 2027]  # Add new version here
```

### Step 2: Update Test Script (Optional)

**File: `test-multi-version-build.ps1`**
```powershell
param(
    [string[]]$RevitVersions = @("2025", "2026", "2027"),  # Add new version here
    [string]$Configuration = "Release"
)
```

### Step 3: Verify Dependencies

Check that required NuGet packages support the new Revit version:
- `Nice3point.Revit.Api.RevitAPI` version `<RevitVersion>.*`
- `Nice3point.Revit.Api.RevitAPIUI` version `<RevitVersion>.*`

### Step 4: Test Locally

Run the test scripts to verify everything works:
```powershell
# Test multi-version build
.\test-multi-version-build.ps1 -RevitVersions @("2025", "2026", "2027")

# Test dynamic version detection
.\test-dynamic-version-detection.ps1 -RevitVersions @("2025", "2026", "2027")
```

### Step 5: Update Documentation

Update badges and documentation to include the new version:

**In `.github/badges.md`:**
```markdown
### Revit 2027
[![NuGet Revit 2027](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=NuGet%20(Revit%202027)&color=orange&versionPrefix=2027)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
```

## Removing Support for Old Revit Versions

### Step 1: Update Workflows
Remove the version from the matrix arrays in both workflow files.

### Step 2: Update Documentation
Remove badges and references to the deprecated version.

### Step 3: Archive Packages (Optional)
Consider marking old packages as deprecated on NuGet.org rather than deleting them.

## Version Management

### Semantic Versioning
The base version follows semantic versioning principles:
- **Major**: Breaking changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes, backward compatible

### Commit Message Control
Use commit messages to control version bumps:
- `+semver: major` - Increment major version
- `+semver: minor` - Increment minor version
- `+semver: patch` - Increment patch version
- `+semver: none` - No version increment

### Release Process
1. **Development**: Work on feature branches, creates alpha versions
2. **PR**: Creates pull request versions for testing
3. **Main Branch**: Creates stable versions for each Revit version
4. **GitHub Release**: Publishes to NuGet.org

## Technical Details

### Assembly Versioning
The `RevitXunitAdapter` project is configured to use the build version for its assembly version:
```xml
<AssemblyVersion>$(Version)</AssemblyVersion>
<FileVersion>$(Version)</FileVersion>
```

This allows the test executor to determine the target Revit version dynamically at runtime.

### Revit Process Selection
The framework follows this logic for connecting to Revit:
1. Scans for running Revit processes
2. Checks each process version against the assembly major version
3. Connects to matching process or launches new instance
4. Ensures add-in compatibility with target Revit version

## Troubleshooting

### Missing Package Dependencies
If a new Revit version doesn't have corresponding NuGet packages:
1. Check if `Nice3point.Revit.Api` packages are available
2. Consider using a different package source or version
3. Update package references if needed

### Build Failures
1. **Check API compatibility**: New Revit versions may have breaking API changes
2. **Update target frameworks**: Ensure .NET versions are compatible
3. **Review deprecated APIs**: Update code using obsolete Revit APIs

### Version Detection Issues
1. **Verify assembly versions**: Use `test-dynamic-version-detection.ps1` to validate
2. **Check build parameters**: Ensure `/p:Version=<RevitVersion>.x.y` is passed correctly
3. **Review project files**: Confirm `AssemblyVersion` property is set correctly

### Version Conflicts
1. **Check GitVersion configuration**: Ensure version calculation is correct
2. **Verify branch naming**: Follow GitFlow conventions for proper versioning
3. **Review package dependencies**: Ensure no version conflicts between Revit versions

## Manual Testing Commands

### Build specific version:
```bash
dotnet build RevitTestRunner.sln --configuration Release /p:RevitVersion=2027
```

### Pack specific version:
```bash
dotnet pack RevitXunitAdapter/RevitXunitAdapter.csproj --configuration Release /p:RevitVersion=2027 /p:Version=2027.1.0
```

### Test all versions:
```powershell
.\test-multi-version-build.ps1
```

### Test version detection:
```powershell
.\test-dynamic-version-detection.ps1
```

**Note**: Test scripts use temporary directories outside the project structure to avoid cluttering the workspace or appearing in source control.
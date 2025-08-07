# GitHub Actions Status Badges

Add these badges to your main README.md file to show the build status:

## Build Status
```markdown
[![Build and Publish](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/build-and-publish.yml)
```

## PR Validation
```markdown
[![PR Validation](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/pr-validation.yml)
```

## NuGet Packages

### Revit 2025
```markdown
[![NuGet Revit 2025](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=NuGet%20(Revit%202025)&color=blue&versionPrefix=2025)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
[![Downloads Revit 2025](https://img.shields.io/nuget/dt/RevitXunit.TestAdapter?logo=nuget&label=Downloads%20(2025)&color=blue&versionPrefix=2025)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
```

### Revit 2026
```markdown
[![NuGet Revit 2026](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=NuGet%20(Revit%202026)&color=green&versionPrefix=2026)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
[![Downloads Revit 2026](https://img.shields.io/nuget/dt/RevitXunit.TestAdapter?logo=nuget&label=Downloads%20(2026)&color=green&versionPrefix=2026)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
```

### All Versions Combined
```markdown
[![NuGet](https://img.shields.io/nuget/v/RevitXunit.TestAdapter.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/RevitXunit.TestAdapter.svg?logo=nuget&label=Total%20Downloads)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
```

## Usage Examples

### For Project README
```markdown
# RevitTestRunner

[![Build and Publish](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/YOUR_USERNAME/RevitTestRunner/actions/workflows/build-and-publish.yml)
[![NuGet](https://img.shields.io/nuget/v/RevitXunit.TestAdapter.svg?logo=nuget)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)

## Supported Revit Versions

| Revit Version | NuGet Package | Status |
|---------------|---------------|--------|
| 2025 | [![NuGet 2025](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=2025&color=blue&versionPrefix=2025)](https://www.nuget.org/packages/RevitXunit.TestAdapter/) | ? Supported |
| 2026 | [![NuGet 2026](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=2026&color=green&versionPrefix=2026)](https://www.nuget.org/packages/RevitXunit.TestAdapter/) | ? Supported |
```

**Note**: Replace `YOUR_USERNAME` with your actual GitHub username or organization name.
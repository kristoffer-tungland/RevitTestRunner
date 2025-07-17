# Centralized Version Management

This solution uses centralized version management through the `Directory.Build.props` file in the root directory.

## How It Works

The `Directory.Build.props` file defines common properties that apply to all projects in the solution:

- Version information (Version, AssemblyVersion, FileVersion)
- Common project properties (TargetFramework, ImplicitUsings, Nullable)
- Revit-specific properties (RevitVersion)
- Project-type specific settings

## Updating Versions

To update the version for all projects at once:

1. Open the `Directory.Build.props` file in the root directory
2. Update the version properties:
   ```xml
   <Version>1.0.0</Version>
   <AssemblyVersion>1.0.0</AssemblyVersion>
   <FileVersion>1.0.0</FileVersion>
   ```
3. Save the file and rebuild the solution

## Overriding Settings

Individual projects can override these settings by explicitly defining the properties in their project files:

```xml
<PropertyGroup>
  <Version>2.0.0</Version>
  <!-- This project will use version 2.0.0 instead of the common version -->
</PropertyGroup>
```

## Revit Version

The Revit version is also centralized in `Directory.Build.props`:

```xml
<RevitVersion>2025</RevitVersion>
```

This property is used in package references like:

```xml
<PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="$(RevitVersion).*"/>
```

To target a different Revit version, just update the `RevitVersion` property.

## Framework Targeting

The solution contains projects targeting different frameworks:
- Most projects target `net8.0`
- Adapter projects target `net9.0`

These settings are managed in `Directory.Build.props` using conditions.
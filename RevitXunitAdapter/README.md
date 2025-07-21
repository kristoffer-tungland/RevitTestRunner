# RevitXunit.TestAdapter

**RevitXunit.TestAdapter** is a powerful xUnit test adapter that enables seamless integration testing of Autodesk Revit add-ins directly within the Revit environment.

## Overview

This test adapter allows you to write standard xUnit tests that automatically load Revit models, access the full Revit API, and execute inside a running Revit instance while reporting results back to Visual Studio Test Explorer or your CI/CD pipeline. Perfect for BIM developers who need reliable, automated testing of Revit functionality without the complexity of manual testing workflows.

## Features

- ? **Standard xUnit Tests** - Write familiar xUnit tests with `[RevitFact]` attribute
- ? **Automatic Model Loading** - Load local files, cloud models, or use active documents
- ? **Full Revit API Access** - Tests run inside Revit with complete API access
- ? **Visual Studio Integration** - Results appear in Test Explorer
- ? **CI/CD Ready** - Works with dotnet test and build pipelines
- ? **Version Placeholders** - Dynamic Revit version path resolution
- ? **Multiple Parameter Types** - Inject Document, UIApplication, or both

## Quick Start

### 1. Install the Package

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <RevitVersion>2025</RevitVersion>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="RevitXunit.TestAdapter" Version="$(RevitVersion).*" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="$(RevitVersion).*" />
    <PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="$(RevitVersion).*" />
  </ItemGroup>
</Project>
```

### 2. Write Your First Test

```csharp
using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;

public class MyRevitTests
{
    [RevitFact(@"C:\Models\sample.rvt")]
    public void Should_CountWalls_InSampleModel(Document doc)
    {
        var walls = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .ToElements();
            
        Assert.True(walls.Count > 0, "Expected at least one wall");
    }
}
```

## RevitFact Attribute Options

### Load Specific Local File
```csharp
[RevitFact(@"C:\Models\sample.rvt")]
public void TestLocalFile(Document doc) { }
```

### Load Cloud Model (BIM 360/ACC)
```csharp
[RevitFact("project-guid", "model-guid")]
public void TestCloudModel(Document doc) { }
```

### Use Active Document
```csharp
[RevitFact]
public void TestActiveDocument(Document? doc) { }
```

### Version Placeholder Support
```csharp
[RevitFact(@"C:\Program Files\Autodesk\Revit [RevitVersion]\Samples\sample.rvt")]
public void TestWithVersionPlaceholder(Document doc) { }
```

## Supported Test Parameters

Your test methods can accept these parameters (dependency injection):

- **`Document doc`** - The Revit document
- **`Document? doc`** - Optional document (nullable)
- **`UIApplication uiapp`** - The Revit UI application
- **Both** - `TestMethod(UIApplication uiapp, Document doc)`

## Requirements

- Autodesk Revit 2025+ (or matching package version)
- .NET 8.0
- Visual Studio with Test Explorer or dotnet CLI

## How It Works

1. **Discovery** - Finds tests marked with `[RevitFact]`
2. **Communication** - Connects to running Revit via named pipes
3. **Model Loading** - Opens specified models automatically
4. **Execution** - Runs tests inside Revit process
5. **Reporting** - Reports results to Test Explorer/CI

## Support

- [Issues & Bug Reports](https://github.com/your-repo/issues)
- [Documentation](https://github.com/your-repo/wiki)

## License

[Specify your license here]
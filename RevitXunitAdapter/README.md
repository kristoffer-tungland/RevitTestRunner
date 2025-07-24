# RevitXunit.TestAdapter

**RevitXunit.TestAdapter** is a powerful xUnit test adapter that enables seamless integration testing of Autodesk Revit add-ins directly within the Revit environment.

## Overview

This test adapter allows you to write standard xUnit tests that automatically load Revit models, access the full Revit API, and execute inside a running Revit instance while reporting results back to Visual Studio Test Explorer or your CI/CD pipeline. Perfect for BIM developers who need reliable, automated testing of Revit functionality without the complexity of manual testing workflows.

## Features

- ✅ **Standard xUnit Tests** - Write familiar xUnit tests with `[RevitFact]` attribute
- ✅ **Automatic Model Loading** - Load local files, cloud models, or use active documents
- ✅ **Full Revit API Access** - Tests run inside Revit with complete API access
- ✅ **Visual Studio Integration** - Results appear in Test Explorer with smart debugger support
- ✅ **CI/CD Ready** - Works with dotnet test and build pipelines
- ✅ **Version Placeholders** - Dynamic Revit version path resolution
- ✅ **Multiple Parameter Types** - Inject Document, UIApplication, or both
- ✅ **Advanced Debugging** - Intelligent Visual Studio instance detection and management
- ✅ **Worksharing Support** - Advanced workset and central model management

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

## Worksharing and Workset Management

### DetachOption Parameter

Control how workshared models are handled when opening:

```csharp
// Detach from central and preserve worksets (recommended for testing)
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndPreserveWorksets)]
public void TestDetachedModel(Document doc)
{
    Assert.False(doc.IsWorkshared, "Document should be detached from central");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.True(worksets.Count > 0, "Worksets should be preserved");
}

// Detach and remove worksharing completely
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndDiscardWorksets)]
public void TestNonWorksharedModel(Document doc)
{
    Assert.False(doc.IsWorkshared);
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.Empty(worksets, "Worksets should be discarded");
}
```

### WorksetsToOpen Parameter

Specify which worksets should be opened:

```csharp
// Open specific worksets by ID
[RevitFact(@"C:\Models\WorksharedModel.rvt", WorksetsToOpen = [1, 2, 5])]
public void TestSpecificWorksets(Document doc)
{
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen).ToList();
    
    // Verify only specified worksets are open
    var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToArray();
    Assert.Contains(1, openIds);
    Assert.Contains(2, openIds);
    Assert.Contains(5, openIds);
}

// Combined with DetachOption
[RevitFact(@"C:\Models\CentralModel.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1, 3])]
public void TestDetachedWithSpecificWorksets(Document doc)
{
    Assert.False(doc.IsWorkshared, "Should be detached from central");
    
    var openUserWorksets = new FilteredWorksetCollector(doc)
        .ToWorksets()
        .Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset)
        .ToList();
    
    Assert.Equal(2, openUserWorksets.Count);
    var openIds = openUserWorksets.Select(w => w.Id.IntegerValue).ToArray();
    Assert.Contains(1, openIds);
    Assert.Contains(3, openIds);
}
```

### Available DetachOption Values

| Option | Description | Use Case |
|--------|-------------|----------|
| `DoNotDetach` (default) | Keep connection to central | Use carefully - may affect central model |
| `DetachAndPreserveWorksets` | Detach but keep worksets | **Recommended** for most tests |
| `DetachAndDiscardWorksets` | Detach and remove worksets | Testing non-workshared functionality |
| `ClearTransmittedSaveAsNewCentral` | Create new central | Testing transmitted model workflows |

### Default Behavior

When no worksharing options are specified:
- **DetachOption**: `DoNotDetach` (remains connected to central)
- **WorksetsToOpen**: `null` (all worksets open for better accessibility)

### Comprehensive Logging

The framework provides detailed logging of all worksharing operations:

```
2024-01-15 14:30:25.123 [INFO] === RevitTestModelHelper.OpenModel - START ===
2024-01-15 14:30:25.125 [INFO] DetachFromCentral: DetachAndPreserveWorksets
2024-01-15 14:30:25.127 [INFO] WorksetsToOpen: [1, 2, 5]
2024-01-15 14:30:27.255 [INFO] Total Worksets: 8
2024-01-15 14:30:27.261 [INFO]   ID: 1, Name: 'Architecture', Status: OPEN, EDITABLE
2024-01-15 14:30:27.263 [INFO]   ID: 2, Name: 'Structure', Status: OPEN, EDITABLE  
2024-01-15 14:30:27.265 [INFO]   ID: 5, Name: 'Interiors', Status: OPEN, EDITABLE
2024-01-15 14:30:27.267 [INFO]   ID: 3, Name: 'MEP', Status: CLOSED, READ-ONLY
```

## Supported Test Parameters

Your test methods can accept these parameters (dependency injection):

- **`Document doc`** - The Revit document
- **`Document? doc`** - Optional document (nullable)
- **`UIApplication uiapp`** - The Revit UI application
- **Both** - `TestMethod(UIApplication uiapp, Document doc)`

## Advanced Debugging Features

### Automatic Debugger Management
The test adapter includes sophisticated debugging support that works seamlessly with Visual Studio:

#### Multiple Visual Studio Instance Support
- **Smart Detection** - Automatically identifies the correct Visual Studio instance when multiple are running
- **Process Tree Analysis** - Walks up the process hierarchy to find the Visual Studio that initiated the test run
- **ROT Enumeration** - Uses Windows Running Object Table to discover all Visual Studio DTE instances
- **Fallback Mechanisms** - Multiple strategies ensure reliable debugger connection

#### Debugger Lifecycle Management
- **Automatic Attachment** - When debugging is enabled, attaches to the Revit process automatically
- **Reliable Detachment** - Multiple detachment strategies ensure clean test cycles:
  - Independent process detachment (survives test host exit)
  - Shutdown event handling for backup detachment
  - Configurable synchronous detachment when needed

#### Environment Variable Configuration
Control debugging behavior with environment variables:

```bash
# Disable automatic debugger detachment (manual detachment only)
set REVIT_DEBUG_DISABLE_AUTO_DETACH=true

# Force synchronous detachment (blocks test completion until debugger detaches)
set REVIT_DEBUG_SYNC_DETACH=true
```

### Debugging Workflow
1. **Test Start** - Framework detects if debugger is attached to test process
2. **VS Instance Detection** - Automatically finds the correct Visual Studio instance
3. **Revit Attachment** - Uses RevitDebuggerHelper to attach debugger to Revit process
4. **Test Execution** - Tests run with full debugging capabilities
5. **Automatic Cleanup** - Debugger is properly detached after test completion

### RevitDebuggerHelper Integration
The adapter includes a .NET Framework 4.8 helper application that provides:
- **COM Interop** - Proper Visual Studio DTE access from .NET 8 test adapter
- **Instance Enumeration** - ROT-based discovery of all Visual Studio instances
- **Robust Operations** - Reliable attach/detach operations with comprehensive error handling
- **Process Management** - Independent operation that survives test host lifecycle

## Requirements

- Autodesk Revit 2025+ (or matching package version)
- .NET 8.0
- Visual Studio with Test Explorer or dotnet CLI
- .NET Framework 4.8 (included for RevitDebuggerHelper)

## How It Works

1. **Discovery** - Finds tests marked with `[RevitFact]`
2. **Communication** - Connects to running Revit via named pipes
3. **Model Loading** - Opens specified models automatically with worksharing configuration
4. **Debugger Setup** - Attaches debugger to correct Visual Studio instance if debugging
5. **Execution** - Runs tests inside Revit process
6. **Reporting** - Reports results to Test Explorer/CI with detailed workset logging
7. **Cleanup** - Proper debugger detachment and resource cleanup

## Support

- [Issues & Bug Reports](https://github.com/your-repo/issues)
- [Documentation](https://github.com/your-repo/wiki)

## License

[Specify your license here]
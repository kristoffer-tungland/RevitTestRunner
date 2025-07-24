# Revit Test Runner

**RevitXunit.TestAdapter** is a powerful xUnit test adapter that enables seamless integration testing of Autodesk Revit add-ins directly within the Revit environment. Write standard xUnit tests that automatically load Revit models, access the full Revit API, and execute inside a running Revit instance while reporting results back to Visual Studio Test Explorer or your CI/CD pipeline. Perfect for BIM developers who need reliable, automated testing of Revit functionality without the complexity of manual testing workflows.

## Projects

- **RevitAddin.Xunit** – Add-in loaded into Revit. Implements xUnit runner and a named pipe server.
- **RevitAdapterCommon** – Shared helper library for connecting the adapters to the Revit pipe.
- **RevitXunitAdapter** – Test adapter for xUnit. Discovers tests and sends execution commands to the Revit process.
- **RevitTestFramework.Xunit** – xUnit-specific framework components and runners.
- **RevitTestFramework.Xunit.Contracts** – Attributes and helpers shared by the add-in and test projects.
- **RevitDebuggerHelper** – .NET Framework 4.8 helper for Visual Studio debugger operations with advanced multi-instance support.
- **MyRevitTestsXunit** – Example xUnit test library that uses the `RevitFact` attribute.

## Key Features

- **🔧 Standard xUnit Tests** - Write familiar xUnit tests with `[RevitFact]` attribute
- **📁 Automatic Model Loading** - Load local files, cloud models, or use active documents
- **🔌 Full Revit API Access** - Tests run inside Revit with complete API access
- **🔍 Visual Studio Integration** - Results appear in Test Explorer with intelligent debugger support
- **🚀 CI/CD Ready** - Works with dotnet test and build pipelines
- **📝 Version Placeholders** - Dynamic Revit version path resolution
- **🎯 Multiple Parameter Types** - Inject Document, UIApplication, or both
- **🐛 Advanced Debugging** - Smart Visual Studio instance detection for seamless debugging experience
- **🏗️ **Worksharing Support** - Advanced workset and central model management with detailed logging

## Getting Started

### 1. Install the Test Adapter

Add the RevitXunit.TestAdapter NuGet package to your test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
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

### 2. Write Your Tests

Use the `[RevitFact]` attribute to mark tests that should run inside Revit:

```csharp
using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public class MyRevitTests
{
    [RevitFact]
    public void Should_RunSuccessfully_WhenNoRevitDocumentIsRequired()
    {
        // Test Revit functionality that doesn't require a specific model
        Assert.True(true, "This test runs inside Revit");
    }

    [RevitFact(@"C:\Models\sample.rvt")]
    public void Should_LoadLocalFile_AndVerifyModelContent(Document doc)
    {
        Assert.NotNull(doc);
        Assert.Equal("Expected Model Name", doc.Title);
        
        var walls = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .ToElements();
            
        Assert.True(walls.Count > 0, "Expected at least one wall in the model");
    }

    [RevitFact("project-guid", "model-guid")]
    public void Should_LoadCloudModel_WhenProvidedWithGuids(Document doc)
    {
        Assert.NotNull(doc);
        // Test with BIM 360/ACC cloud model
    }

    [RevitFact]
    public void Should_ProvideUIApplication_WhenTestRequiresRevitUI(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        // Test Revit UI functionality
    }

    [RevitFact(@"C:\Models\sample.rvt")]
    public void Should_ProvideBothUIApplicationAndDocument(UIApplication uiapp, Document doc)
    {
        Assert.NotNull(uiapp);
        Assert.NotNull(doc);
        // Test with both UI application and document context
    }
}
```

## RevitFact Attribute Options

The `[RevitFact]` attribute supports several ways to specify which model to use:

### 1. No Parameters - Use Active Document

```csharp
[RevitFact]
public void TestWithActiveDocument(Document? doc)
{
    // Uses currently active document in Revit (can be null)
}
```

### 2. Local File Path

```csharp
[RevitFact(@"C:\Models\sample.rvt")]
public void TestWithLocalFile(Document doc)
{
    // Opens specified local Revit file
}
```

### 3. Cloud Model (BIM 360/ACC)

```csharp
[RevitFact("project-guid", "model-guid")]
public void TestWithCloudModel(Document doc)
{
    // Opens specified cloud model using project and model GUIDs
}
```

### 4. Version Placeholder

```csharp
[RevitFact(@"C:\Program Files\Autodesk\Revit [RevitVersion]\Samples\sample.rvt")]
public void TestWithVersionPlaceholder(Document doc)
{
    // [RevitVersion] is replaced with actual Revit version at runtime
}
```

## Worksharing and Workset Management

The RevitTestRunner includes comprehensive support for testing workshared models with advanced workset management capabilities.

### DetachOption Parameter

Control how models are detached from central using the `DetachOption` parameter:

```csharp
// Detach and preserve worksets (recommended for most tests)
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndPreserveWorksets)]
public void TestDetachedModel(Document doc)
{
    Assert.False(doc.IsWorkshared, "Document should be detached from central");
    
    // Worksets are preserved and can be inspected
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.True(worksets.Count > 0, "Worksets should be preserved");
}

// Detach and discard worksets
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndDiscardWorksets)]
public void TestDetachedModelWithoutWorksets(Document doc)
{
    Assert.False(doc.IsWorkshared, "Document should be detached from central");
    
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    Assert.True(worksets.Count == 0, "Worksets should be discarded");
}

// Keep connection to central (default behavior)
[RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DoNotDetach)]
public void TestConnectedModel(Document doc)
{
    // Document remains connected to central model
    // Use with caution - may affect central model
}

// Create new central from transmitted model
[RevitFact(@"C:\Models\TransmittedModel.rvt", DetachOption = DetachOption.ClearTransmittedSaveAsNewCentral)]
public void TestNewCentralFromTransmitted(Document doc)
{
    // Creates a new central model from transmitted file
}
```

### Available DetachOption Values

| Option | Description | Use Case |
|--------|-------------|----------|
| `DoNotDetach` | Keep connection to central | Testing that requires central model connection (use carefully) |
| `DetachAndPreserveWorksets` | Detach but keep worksets | Most common for testing workset-aware functionality |
| `DetachAndDiscardWorksets` | Detach and remove worksets | Testing non-workshared functionality on workshared models |
| `ClearTransmittedSaveAsNewCentral` | Create new central from transmitted | Testing transmitted model workflows |

### WorksetsToOpen Parameter

Control which specific worksets are opened when loading a model:

```csharp
// Open specific worksets by ID
[RevitFact(@"C:\Models\WorksharedModel.rvt", WorksetsToOpen = [1, 2, 5])]
public void TestSpecificWorksets(Document doc)
{
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen).ToList();
    
    // Verify only specified worksets are open
    var openIds = openWorksets.Select(w => w.Id.IntegerValue).ToList();
    Assert.Contains(1, openIds);
    Assert.Contains(2, openIds);
    Assert.Contains(5, openIds);
    
    // Verify other worksets are closed
    var closedWorksets = worksets.Where(w => !w.IsOpen).ToList();
    Assert.True(closedWorksets.Count > 0, "Some worksets should be closed");
}

// Test with single workset
[RevitFact(@"C:\Models\WorksharedModel.rvt", WorksetsToOpen = [1])]
public void TestSingleWorkset(Document doc)
{
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    
    Assert.Single(openWorksets, "Only one user workset should be open");
    Assert.Equal(1, openWorksets.First().Id.IntegerValue);
}
```

### Combined Worksharing Options

You can combine `DetachOption` and `WorksetsToOpen` for precise control:

```csharp
// Detach from central AND open specific worksets
[RevitFact(@"C:\Models\CentralModel.rvt", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [1, 3, 7])]
public void TestDetachedWithSpecificWorksets(Document doc)
{
    // Document is detached from central
    Assert.False(doc.IsWorkshared, "Document should be detached");
    
    // Only specified worksets are open
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openUserWorksets = worksets
        .Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset)
        .ToList();
    
    var expectedIds = new[] { 1, 3, 7 };
    var actualIds = openUserWorksets.Select(w => w.Id.IntegerValue).ToArray();
    
    Assert.Equal(expectedIds.Length, actualIds.Length);
    foreach (var expectedId in expectedIds)
    {
        Assert.Contains(expectedId, actualIds);
    }
}

// Cloud model with workset control
[RevitFact("project-guid", "model-guid", 
           DetachOption = DetachOption.DetachAndPreserveWorksets,
           WorksetsToOpen = [311, 312])]
public void TestCloudModelWithWorksets(Document doc)
{
    Assert.NotNull(doc);
    Assert.False(doc.IsWorkshared, "Cloud model should be detached");
    
    // Verify specific worksets are open
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openWorksetIds = worksets
        .Where(w => w.IsOpen)
        .Select(w => w.Id.IntegerValue)
        .ToList();
        
    Assert.Contains(311, openWorksetIds);
    Assert.Contains(312, openWorksetIds);
}
```

### Default Worksharing Behavior

When no worksharing options are specified:

- **DetachOption**: Defaults to `DoNotDetach`
- **WorksetsToOpen**: Defaults to opening all worksets for better accessibility and usability

```csharp
// Default behavior - no worksharing options specified
[RevitFact(@"C:\Models\WorksharedModel.rvt")]
public void TestDefaultWorksharing(Document doc)
{
    // Model remains connected to central (if it was central)
    // All worksets are open by default for better accessibility
    var worksets = new FilteredWorksetCollector(doc).ToWorksets();
    var openUserWorksets = worksets.Where(w => w.IsOpen && w.Kind == WorksetKind.UserWorkset).ToList();
    
    Assert.True(openUserWorksets.Count > 0, "User worksets should be open by default");
}
```

### Workset Information Logging

The framework provides comprehensive logging of workset operations:

```
2024-01-15 14:30:25.123 [INFO] === RevitTestModelHelper.OpenModel - START ===
2024-01-15 14:30:25.125 [INFO] Configuration: LocalPath=C:\Models\Project.rvt, DetachFromCentral=DetachAndPreserveWorksets
2024-01-15 14:30:25.127 [INFO] WorksetsToOpen: [1, 2, 5]
2024-01-15 14:30:25.129 [INFO] Opening local model...
2024-01-15 14:30:25.131 [INFO] Opening with DetachFromCentralOption: DetachAndPreserveWorksets
2024-01-15 14:30:25.133 [INFO] Opening local model with 3 specified worksets: [1, 2, 5]
2024-01-15 14:30:27.245 [INFO] Successfully opened local model: Project
2024-01-15 14:30:27.247 [INFO] --- Model Information ---
2024-01-15 14:30:27.249 [INFO] Document Title: Project
2024-01-15 14:30:27.251 [INFO] Is Workshared: False
2024-01-15 14:30:27.253 [INFO] --- Workset Information ---
2024-01-15 14:30:27.255 [INFO] Central Model Path: \\server\central\Project_Central.rvt
2024-01-15 14:30:27.257 [INFO] Total Worksets: 8
2024-01-15 14:30:27.259 [INFO] Workset Details:
2024-01-15 14:30:27.261 [INFO]   ID: 1, Name: 'Architecture', Status: OPEN, EDITABLE, Owner: user1
2024-01-15 14:30:27.263 [INFO]   ID: 2, Name: 'Structure', Status: OPEN, EDITABLE, Owner: user2
2024-01-15 14:30:27.265 [INFO]   ID: 5, Name: 'Interiors', Status: OPEN, EDITABLE, Owner: user3
2024-01-15 14:30:27.267 [INFO]   ID: 3, Name: 'MEP', Status: CLOSED, READ-ONLY, Owner: No Owner
2024-01-15 14:30:27.269 [INFO] Open Worksets: 3
2024-01-15 14:30:27.271 [INFO]   Open IDs: [1, 2, 5]
2024-01-15 14:30:27.273 [INFO]   Open Names: ['Architecture', 'Structure', 'Interiors']
2024-01-15 14:30:27.275 [INFO] Closed Worksets: 5
2024-01-15 14:30:27.277 [INFO]   Closed IDs: [3, 4, 6, 7, 8]
2024-01-15 14:30:27.279 [INFO] === RevitTestModelHelper.OpenModel - END (Success) ===
```

Log files are stored at: `%LOCALAPPDATA%\RevitTestRunner\Logs\RevitTestFramework.Common-yyyyMMdd.log`

### Best Practices for Worksharing Tests

1. **Use DetachAndPreserveWorksets** for most tests to avoid affecting central models
2. **Specify WorksetsToOpen** explicitly to ensure consistent test conditions
3. **Test workset-specific functionality** by opening only relevant worksets
4. **Use comprehensive assertions** to verify workset states
5. **Check log files** for detailed workset information during debugging

## Supported Test Method Parameters

Your test methods can accept the following parameters, and the framework will inject them automatically:

- **`Document doc`** - The Revit document (required when using file path or cloud model)
- **`Document? doc`** - Optional document (when using no parameters, can be null if no active document)
- **`UIApplication uiapp`** - The Revit UI application instance

## Advanced Debugging Support

The test framework includes sophisticated debugging capabilities:

### Automatic Debugger Attachment
- **Smart Instance Detection** - Automatically finds the correct Visual Studio instance when multiple are running
- **Process Tree Analysis** - Walks up the process hierarchy to identify the Visual Studio that initiated the test run
- **Running Object Table (ROT) Enumeration** - Uses Windows COM ROT to find all Visual Studio instances
- **Reliable Detachment** - Multiple strategies ensure debugger is properly detached after test completion

### Debugging Environment Variables
- `REVIT_DEBUG_DISABLE_AUTO_DETACH=true` - Disable automatic debugger detachment
- `REVIT_DEBUG_SYNC_DETACH=true` - Use synchronous detachment (blocks test completion until debugger is detached)

### Multiple Visual Studio Instance Support
When you have multiple Visual Studio instances open, the framework:
1. Prioritizes the Visual Studio instance that started the test run
2. Falls back to any available Visual Studio instance
3. Uses the RevitDebuggerHelper (.NET Framework 4.8) for reliable COM interop
4. Provides detailed logging for troubleshooting debugger operations

## Building

The projects rely on NuGet packages (`xunit`, `Microsoft.NET.Test.Sdk`, etc.). Building requires restoring those packages. In environments without internet access the restore step will fail.

```bash
dotnet build RevitTestRunner.sln
```

## How It Works

1. **Test Discovery**: The RevitXunitAdapter discovers tests marked with `[RevitFact]` in your test assemblies
2. **Revit Communication**: When tests run, the adapter communicates with a running Revit instance via named pipes
3. **Model Loading**: If specified, the appropriate Revit model is opened automatically with worksharing options
4. **Debugger Attachment**: If debugging is enabled, automatically attaches to the correct Visual Studio instance
5. **Test Execution**: Tests run inside the Revit process with full access to the Revit API
6. **Result Reporting**: Test results are reported back to Visual Studio Test Explorer
7. **Cleanup**: Automatic debugger detachment and process cleanup ensures clean test cycles

## Requirements

- Autodesk Revit (2025 or compatible version)
- .NET 8.0
- Visual Studio with Test Explorer or dotnet test CLI
- .NET Framework 4.8 (for RevitDebuggerHelper)

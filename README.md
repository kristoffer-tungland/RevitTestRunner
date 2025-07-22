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
3. **Model Loading**: If specified, the appropriate Revit model is opened automatically
4. **Debugger Attachment**: If debugging is enabled, automatically attaches to the correct Visual Studio instance
5. **Test Execution**: Tests run inside the Revit process with full access to the Revit API
6. **Result Reporting**: Test results are reported back to Visual Studio Test Explorer
7. **Cleanup**: Automatic debugger detachment and process cleanup ensures clean test cycles

## Requirements

- Autodesk Revit (2025 or compatible version)
- .NET 8.0
- Visual Studio with Test Explorer or dotnet test CLI
- .NET Framework 4.8 (for RevitDebuggerHelper)

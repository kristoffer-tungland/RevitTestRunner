# Revit Test Runner

[![Build and Publish](https://github.com/kristoffer-tungland/RevitTestRunner/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/kristoffer-tungland/RevitTestRunner/actions/workflows/build-and-publish.yml)
[![PR Validation](https://github.com/kristoffer-tungland/RevitTestRunner/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/kristoffer-tungland/RevitTestRunner/actions/workflows/pr-validation.yml)
[![NuGet](https://img.shields.io/nuget/v/RevitXunit.TestAdapter.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/RevitXunit.TestAdapter.svg?logo=nuget&label=Total%20Downloads)](https://www.nuget.org/packages/RevitXunit.TestAdapter/)

## Supported Revit Versions

| Revit Version | NuGet Package | Status |
|---------------|---------------|--------|
| 2025 | [![NuGet 2025](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=2025&color=blue&versionPrefix=2025.1.0)](https://www.nuget.org/packages/RevitXunit.TestAdapter/2025.1.0#readme-body-tab) | ✓ Supported |
| 2026 | [![NuGet 2026](https://img.shields.io/nuget/v/RevitXunit.TestAdapter?logo=nuget&label=2026&color=green&versionPrefix=2026.1.0)](https://www.nuget.org/packages/RevitXunit.TestAdapter/) | ✓ Supported |


**RevitXunit.TestAdapter** is a powerful xUnit test adapter that enables seamless integration testing of Autodesk Revit add-ins directly within the Revit environment. Write standard xUnit tests that automatically load Revit models, access the full Revit API, and execute inside a running Revit instance while reporting results back to Visual Studio Test Explorer or your CI/CD pipeline. Perfect for BIM developers who need reliable, automated testing of Revit functionality without the complexity of manual testing workflows.

---

## Table of Contents
- [Projects](#projects)
- [Key Features](#key-features)
- [Getting Started](#getting-started)
- [Quick Example](#quick-example)
- [What Can You Do?](#what-can-you-do)
- [Full Attribute Reference](#full-attribute-reference)
- [Local Path Support and Special Folders](#local-path-support-and-special-folders)
- [Test Output Logs](#test-output-logs)
- [Example: Worksharing Test](#example-worksharing-test)
- [Advanced](#advanced)
- [Requirements](#requirements)
- [How It Works](#how-it-works)
- [Example tests](#example-tests)

---

## Projects

- **RevitAddin.Xunit** – Add-in loaded into Revit. Implements xUnit runner and a named pipe server.
- **RevitAdapterCommon** – Shared helper library for connecting the adapters to the Revit pipe.
- **RevitXunitAdapter** – Test adapter for xUnit. Discovers tests and sends execution commands to the Revit process.
- **RevitTestFramework.Xunit** – xUnit-specific framework components and runners.
- **RevitTestFramework.Xunit.Contracts** – Attributes and helpers shared by the add-in and test projects.
- **RevitDebuggerHelper** – .NET Framework 4.8 helper for Visual Studio debugger operations with advanced multi-instance support.
- **MyRevitTestsXunit** – Example xUnit test library that uses the `RevitFact` attribute.

---

## Key Features

- **🔧 Standard xUnit Tests** - Write familiar xUnit tests with `[RevitFact]` attribute
- **📁 Automatic Model Loading** - Load local files, cloud models, or use active documents
- **🔌 Full Revit API Access** - Tests run inside Revit with complete API access
- **🔍 Visual Studio Integration** - Results appear in Test Explorer with intelligent debugger support
- **🚀 CI/CD Ready** - Works with dotnet test and build pipelines
- **📝 Version Placeholders** - Dynamic Revit version path resolution
- **🎯 Multiple Parameter Types** - Inject Document, UIApplication, or both
- **🐛 Advanced Debugging** - Smart Visual Studio instance detection for seamless debugging experience
- **🏗️ Worksharing Support** - Advanced workset and central model management with detailed logging

---

## Getting Started

### 1. Install the Test Adapter

To use the test framework, add the following NuGet package reference to your test project:

```
<PackageReference Include="RevitXunit.TestAdapter" Version="$(RevitVersion).*" />
```

Or use this example test project file:

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

---

## Quick Example

```csharp
using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;

public class MyRevitTests
{
    [RevitFact(@"C:\Models\sample.rvt")]
    public void Should_CountWalls(Document doc)
    {
        var walls = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .ToElements();
        Assert.True(walls.Count > 0);
    }
}
```

---

## What Can You Do?

### 1. **Run xUnit Tests in Revit**
- Use `[RevitFact]` to run tests inside Revit.
- Inject `Document`, `UIApplication`, or both as parameters.

### 2. **Model Loading Options**
- **Active Document:**
  ```csharp
  [RevitFact]
  public void TestActiveDoc(Document? doc) { }
  ```
- **Local File:**
  ```csharp
  [RevitFact(@"C:\Models\sample.rvt")]
  public void TestLocalFile(Document doc) { }
  ```
- **Cloud Model:**
  ```csharp
  [RevitFact("project-guid", "model-guid")]
  public void TestCloudModel(Document doc) { }
  ```
- **Version Placeholder:**
  ```csharp
  [RevitFact(@"C:\Program Files\Autodesk\Revit {RevitVersion}\Samples\sample.rvt")]
  public void TestWithVersionPlaceholder(Document doc) { }
  ```

### 3. **Worksharing & Workset Management**
- **DetachOption:**
  - `DoNotDetach` (default)
  - `DetachAndPreserveWorksets`
  - `DetachAndDiscardWorksets`
  - `ClearTransmittedSaveAsNewCentral`
- **WorksetsToOpen:**
  - Open specific worksets by ID: `[RevitFact(@".\Project1.rvt", WorksetsToOpen = [1,2,5])]`
- **CloseModel:**
  - Close the model after test: `[RevitFact(CloseModel = true)]`

### 4. **Parameter Injection**
- `Document doc` or `Document? doc`
- `UIApplication uiapp`
- Both: `TestMethod(UIApplication uiapp, Document doc)`
- `CancellationToken cancellationToken`

### 5. **Cloud Region Support**
- Specify region: `[RevitFact(CloudRegion.EMEA, projectGuid: "...", modelGuid: "...")]`

### 6. **Advanced Debugging**
- Automatic debugger attach/detach to correct Visual Studio instance
- Environment variables:
  - `REVIT_DEBUG_DISABLE_AUTO_DETACH=true`
  - `REVIT_DEBUG_SYNC_DETACH=true`

### 7. **Comprehensive Logging**
- Log level set via `%LOCALAPPDATA%\RevitTestRunner\Logs\loglevel.txt`
- Levels: `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`

---

## Full Attribute Reference

| Attribute/Parameter         | Description                                                      |
|----------------------------|------------------------------------------------------------------|
| `localPath`                | Path to local Revit file                                         |
| `projectGuid`, `modelGuid` | Cloud model GUIDs                                                |
| `CloudRegion`              | Cloud region (e.g., EMEA, US)                                    |
| `WorksetsToOpen`           | Array of workset IDs to open                                     |
| `DetachOption`             | How to detach from central (see above)                           |
| `CloseModel`               | Close model after test                                           |

---

## Local Path Support and Special Folders

You can specify the Revit file path in `[RevitFact]` using:

- **Absolute paths** (e.g. `C:\Models\sample.rvt`)
- **Relative paths** (e.g. `@".\Project1.rvt"`)
  - Relative paths are resolved from the test output directory (where your test assembly DLL is built)
- **Special folders**:
  - `%PROGRAMFILES%` (e.g. `%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\sample.rvt`)
  - `%USERPROFILE%` (e.g. `%USERPROFILE%\Documents\Revit\MyModel.rvt`)
- **Version placeholders**:
  - `{RevitVersion}` is replaced with the actual Revit version at runtime

**Examples:**
```csharp
[RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
public void TestSampleModel(Document doc) { }

[RevitFact(localPath: @".\Project1.rvt")]
public void TestRelativePath(Document doc) { }
```

---

## Test Output Logs

The test output logs provide detailed information about the test run and Revit environment. Key information includes:

- **Test Discovery and Execution:** Which tests were found, which models or cloud GUIDs are used, and which tests are being executed.
- **Add-in and Revit Instance Management:** Whether the Revit add-in was installed, which Revit process is used, and how the connection is established.
- **Model Loading:** The path, title, and type of the model being opened, DetachOption, WorksetsToOpen, and whether the model is workshared.
- **Workset Information:** IDs, names, open/closed status, and ownership of all worksets in the model. This is especially useful for configuring `WorksetsToOpen` in your tests.
- **Test Results:** Pass/fail status, execution time, and any errors or exceptions.
- **Cleanup:** Whether models or Revit processes are closed after the test run.

You can use the log output to debug test setup, extract workset IDs, and verify the test environment and results.

---

## Example: Worksharing Test

```csharp
[RevitFact(@".\Project1.rvt", WorksetsToOpen = [0], DetachOption = DetachOption.DetachAndPreserveWorksets, CloseModel = true)]
public void DetachAndPreserveWorksets_ShouldDetachModelAndPreserveWorksets(Document document)
{
    Assert.NotNull(document);
    Assert.Equal("Project1_detached", document.Title);
    var userWorksets = new FilteredWorksetCollector(document)
        .OfKind(WorksetKind.UserWorkset)
        .ToWorksets();
    Assert.NotEmpty(userWorksets);
    var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();
    Assert.Single(openWorksets);
}
```

When working with workshared models, you may need to specify workset IDs to open. You can extract the available workset IDs from the test output logs. Look for log lines like these in the test output:

```
Revit.RevitTestModelHelper [INFO] 08:46:24.405: --- Workset Information ---
Revit.RevitTestModelHelper [INFO] 08:46:24.413: Central Model Path: Autodesk.Revit.DB.ModelPath
Revit.RevitTestModelHelper [INFO] 08:46:24.427: Total Worksets: 2
Revit.RevitTestModelHelper [INFO] 08:46:24.431: Workset Details:
Revit.RevitTestModelHelper [INFO] 08:46:24.441:   ID: 0, Name: 'Workset1', Status: CLOSED, READ-ONLY, Owner: No Owner
Revit.RevitTestModelHelper [INFO] 08:46:24.447:   ID: 183, Name: 'Shared Views, Levels, Grids', Status: CLOSED, READ-ONLY, Owner: No Owner
```

Use these IDs in your test attribute:

```csharp
[RevitFact(@".\Project1.rvt", WorksetsToOpen = [0, 183], DetachOption = DetachOption.DetachAndPreserveWorksets, CloseModel = true)]
public void OpenSpecificWorksets(Document document)
{
    // ...test code...
}
```

---

## Advanced
- **Debugger**: Smart attach/detach, multi-VS support
- **Logging**: Detailed workset/model logs, configurable level
- **CI/CD**: Works with `dotnet test` and build pipelines

---

## Requirements
- Autodesk Revit 2025+
- .NET 8.0
- Visual Studio or `dotnet test`
- .NET Framework 4.8 (for debugger helper)

---

## How It Works
1. **Discovery**: Finds `[RevitFact]` tests
2. **Communication**: Connects to Revit via named pipes
3. **Model Loading**: Opens models with worksharing config
4. **Debugger Setup**: Attaches debugger if needed
5. **Execution**: Runs tests in Revit
6. **Reporting**: Results to Test Explorer/CI
7. **Cleanup**: Detach debugger, clean resources

---

## Example tests


See the following files in the [MyRevitTestsXunit](./MyRevitTestsXunit/) project for real-world test examples:
- [RollbackTests.cs](./MyRevitTestsXunit/RollbackTests.cs)
- [OpenLocalModelTests.cs](./MyRevitTestsXunit/OpenLocalModelTests.cs)
- [ApplicationTests.cs](./MyRevitTestsXunit/ApplicationTests.cs)
- [CancellationTokenTests.cs](./MyRevitTestsXunit/CancellationTokenTests.cs)
- [CloudModelTests.cs](./MyRevitTestsXunit/CloudModelTests.cs)

---

## CI/CD Pipeline Documentation

For details on how to manage and configure CI/CD for this repository, see [CI/CD Pipeline Guide](.github/pipeline-readme.md).

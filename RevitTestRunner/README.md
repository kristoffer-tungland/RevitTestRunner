# Revit Test Runner

**RevitXunit.TestAdapter** enables seamless, automated integration testing of Autodesk Revit add-ins using xUnit. Write tests that run inside Revit, load models, access the full API, and report results to Visual Studio or CI/CD—all with simple attributes.

---

## Getting Started

To use the test framework, add the following NuGet package reference to your test project:

```
<PackageReference Include="RevitXunit.TestAdapter" Version="$(RevitVersion).*" />
```

Or use this example test project file:

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

## What Can You Test?

### 1. **Run in Revit with Full API Access**
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

## Example: Worksharing Test

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

## Projects in This Solution
- **RevitAddin.Xunit** – Add-in loaded into Revit, runs tests
- **RevitAdapterCommon** – Shared pipe helpers
- **RevitXunitAdapter** – xUnit test adapter
- **RevitTestFramework.Xunit** – xUnit-specific runners
- **RevitTestFramework.Xunit.Contracts** – Shared attributes/helpers
- **RevitDebuggerHelper** – Debugger attach/detach helper
- **MyRevitTestsXunit** – Example test library

---

## More Info
- [Issues & Bug Reports](https://github.com/your-repo/issues)
- [Documentation](https://github.com/your-repo/wiki)

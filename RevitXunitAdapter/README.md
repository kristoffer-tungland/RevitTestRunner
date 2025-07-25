# RevitXunit.TestAdapter

**RevitXunit.TestAdapter** lets you write xUnit tests that run inside Autodesk Revit, load models, access the full Revit API, and report results to Visual Studio or CI/CD. It is ideal for BIM developers who want reliable, automated Revit add-in testing.

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

## How Test Discovery and Execution Works

After installing the NuGet package:
- Test discovery will automatically pick up your tests decorated with `[RevitFact]` (from the `RevitTestFramework.Xunit` namespace).
- On the first test run, the framework checks if the Revit add-in is installed. If not, it installs it automatically to `%APPDATA%\Autodesk\Revit\Addins\<RevitMainVersion>`.
- The framework scans for open Revit instances matching your NuGet version (e.g., 2025.x.x connects/starts Revit 2025). If no instance is open, it launches a new Revit instance and connects to it.
- If Revit was started automatically, it will be closed after the test run. If you already had a matching Revit instance open, it will remain open after the test run.
- When debugging, Visual Studio is automatically attached to the Revit process, so you can hit breakpoints in your test code without manual attachment.

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
```csharp
using Xunit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for verifying rollback functionality and project parameter changes in Revit documents.
/// </summary>
public class RollbackTests
{
    /// <summary>
    /// Changes the project name parameter in the document's project information and verifies the change.
    /// </summary>
    /// <remarks>
    /// This test starts a transaction, sets the project name, commits the transaction, and asserts the new value.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void ChangeParameterValue_ShouldBeRolledBack(Document document)
    {
        using var transaction = new Transaction(document, "Change Project Name");
        transaction.Start();

        var projecInfo = document.ProjectInformation;
        using var parameter = projecInfo.get_Parameter(BuiltInParameter.PROJECT_NAME);
        Assert.NotNull(parameter);
        parameter.Set("New Project Name");
        transaction.Commit();
        Assert.Equal("New Project Name", parameter.AsString());
    }

    /// <summary>
    /// Verifies that the project name parameter in the document's project information has not been modified by a previous test.
    /// </summary>
    /// <remarks>
    /// This test checks that the project name parameter remains unchanged after a transaction is rolled back or after a new document load.
    /// </remarks>
    /// <param name="document">The Revit document to test. Must not be null.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void TestRollbackFunctionality(Document document)
    {
        var projecInfo = document.ProjectInformation;
        using var parameter = projecInfo.get_Parameter(BuiltInParameter.PROJECT_NAME);
        Assert.NotNull(parameter);
        Assert.NotEqual("New Project Name", parameter.AsString());
    }
}

/// <summary>
/// Contains tests for opening local and workshared Revit models and verifying workset states.
/// </summary>
public class OpenLocalModelTests
{
    /// <summary>
    /// Verifies that the specified Revit document is successfully loaded and contains the expected data.
    /// </summary>
    /// <remarks>
    /// Ensures the document is loaded, has the expected title, and contains at least one wall element.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void OpenLocalModel_ShouldLoadDocument(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Snowdon Towers Sample Architectural", document.Title);

        var wallsCount = new FilteredElementCollector(document)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    /// <summary>
    /// Opens a workshared model with a relative path and verifies that only specific worksets are open.
    /// </summary>
    /// <remarks>
    /// Opens the model and checks that only the specified workset is open.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @".\Project1.rvt", WorksetsToOpen = [0], CloseModel = true)]
    public void OpenWorksharedModel_ShouldLoadDocumentAndOpenWorksets(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Project1", document.Title);

        var userWorksets = new FilteredWorksetCollector(document)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets();

        Assert.NotEmpty(userWorksets);

        var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();
        Assert.Single(openWorksets);
    }

    /// <summary>
    /// Opens a workshared model and verifies that all user worksets are open.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @".\Project1.rvt", CloseModel = true)]
    public void OpenWorksharedModel_ShouldLoadDocumentAndOpenAllWorksets(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Project1", document.Title);

        var userWorksets = new FilteredWorksetCollector(document)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets();

        Assert.NotEmpty(userWorksets);

        var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();

        Assert.Equal(userWorksets.Count, openWorksets.Count);
    }

    /// <summary>
    /// Detaches a workshared model and verifies that worksets are preserved and only specific worksets are open.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(
        localPath: @".\Project1.rvt",
        WorksetsToOpen = [0],
        DetachOption = DetachOption.DetachAndPreserveWorksets,
        CloseModel = true)]
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
}

/// <summary>
/// Contains tests for verifying the Revit application context and loaded add-ins.
/// </summary>
public class ApplicationTests
{
    /// <summary>
    /// Verifies that the Revit application is initialized and has a valid version.
    /// </summary>
    /// <param name="uiapp">The Revit application to be tested.</param>
    [RevitFact]
    public void Application_ShouldBeInitialized(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotNull(uiapp.Application);
        Assert.StartsWith("20", uiapp.Application.VersionNumber);
    }

    /// <summary>
    /// Verifies that the Revit application has the test framework loaded.
    /// </summary>
    /// <param name="uiapp">The Revit application to be tested.</param>
    [RevitFact]
    public void Application_ShouldHaveTestFrameworkLoaded(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotEmpty(uiapp.LoadedApplications);

        var loadedApplicationsNames = uiapp.LoadedApplications.OfType<IExternalApplication>().Select(app => app.GetType().Name).ToList();

        Assert.Contains("RevitXunitTestFrameworkApplication", loadedApplicationsNames, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Contains tests for verifying cancellation token support in Revit test execution.
/// </summary>
public class CancellationTokenTests
{
    /// <summary>
    /// Verifies that the cancellation token can be passed and used within a Revit context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to be tested.</param>
    [RevitFact]
    public void CancellationToken_ShouldBePassedAndUsed(CancellationToken cancellationToken)
    {
        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled at this point.");

        // Simulate a long-running operation
        for (int i = 0; i < 100; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            Thread.Sleep(100); // Simulate work
        }

        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled after the operation.");
    }
}

/// <summary>
/// Contains tests for opening and verifying cloud models in Revit.
/// </summary>
public class CloudModelTests
{
    /// <summary>
    /// Opens a cloud model in the EMEA region with specific project and model GUIDs and verifies it is a cloud model.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(
        CloudRegion.EMEA, // Specify the EMEA region for the cloud model
        projectGuid: "AAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", // Project GUID for the cloud model
        modelGuid: "BBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB",  // Model GUID for the cloud model
        WorksetsToOpen = [0], // Open the first workset (Workset 1), or leave empty to open all worksets
        DetachOption = DetachOption.DetachAndPreserveWorksets, // Detach the model and preserve worksets
        CloseModel = true)] // Close the model after the test
    public void OpenCloud_EMEA_Region_ShouldLoadDocument(Document document)
    {
        Assert.NotNull(document);
        Assert.True(document.IsModelInCloud, "Expected the document to be a cloud model.");
    }

    /// <summary>
    /// Retrieves the cloud model path from the active document and outputs the project and model GUIDs for reference.
    /// </summary>
    /// <remarks>
    /// Open the cloud model manually in Revit and run this test to see the GUIDs in the test output.
    /// </remarks>
    /// <param name="document">The Revit document to be tested. Can be null if no active document.</param>
    [RevitFact]
    public void GetCloudModelPath_LetYouGetProjectAndModelGUIDs(Document? document)
    {
        if (document is null)
        {
            Assert.Null(document);
            return; // No active document, test passes gracefully
        }
        var cloudModelPath = document.GetCloudModelPath();
        Assert.NotNull(cloudModelPath);
        var projectGuid = cloudModelPath.GetProjectGUID();
        var modelGuid = cloudModelPath.GetModelGUID();
        // Make the test fail to get the GUIDs
        Assert.Equal("AAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", projectGuid.ToString());
        Assert.Equal("BBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB", modelGuid.ToString());
    }
}
```
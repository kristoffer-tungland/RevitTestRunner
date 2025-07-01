# Revit Test Runner

This repository contains a minimal proof of concept for running NUnit and xUnit based Revit tests inside Revit and reporting the results back to Visual Studio Test Explorer.

## Projects

- **RevitAddin** – Add-in loaded into Revit. Implements simple NUnit and xUnit runners and a named pipe server.
- **RevitAdapterCommon** – Shared helper library for connecting the adapters to the Revit pipe.
- **RevitNUnitAdapter** – Test adapter for NUnit. Discovers tests and sends execution commands to the Revit process.
- **RevitTestFramework** – Attributes and helpers shared by the add-in and test projects.
- **RevitXunitAdapter** – Test adapter for xUnit.
- **MyRevitTestsNUnit** – Example NUnit test library that uses the `RevitNUnitTestModel` attribute.
- **MyRevitTestsXunit** – Example xUnit test library that uses the `RevitXunitTestModel` attribute.

## Building

The projects rely on NuGet packages (`NUnit`, `Microsoft.NET.Test.Sdk`, etc.). Building requires restoring those packages. In environments without internet access the restore step will fail.

```bash
dotnet build RevitTestRunner.sln
```

## Sample Test

```csharp
[Test]
[RevitNUnitTestModel("proj-guid", "model-guid")]
public void TestWalls()
{
    Assert.IsNotNull(RevitModelService.CurrentDocument);
}
```

The `RevitNUnitTestModel` attribute ensures the specified model is open and wraps the
test in a transaction group. It accepts either a pair of BIM 360 GUIDs or a
local file path:

```csharp
[RevitNUnitTestModel(@"C:\\Models\\sample.rvt")]
```

# Revit Test Runner

This repository contains a minimal proof of concept for running NUnit based Revit tests inside Revit and reporting the results back to Visual Studio Test Explorer.

## Projects

- **RevitAddin** – Add-in loaded into Revit. Implements a simple NUnit runner and a named pipe server.
- **RevitTestAdapter** – Custom test adapter for Visual Studio. Discovers tests and sends execution commands to the Revit process via a named pipe whose name includes the Revit process id.
- **MyRevitTests** – Example test library that uses the `RevitTestModel` attribute.

## Building

The projects rely on NuGet packages (`NUnit`, `Microsoft.NET.Test.Sdk`, etc.). Building requires restoring those packages. In environments without internet access the restore step will fail.

```bash
dotnet build RevitTestRunner.sln
```

## Sample Test

```csharp
[assembly: RevitAddin.RevitTransaction]
[Test]
[RevitTestModel("proj-guid", "model-guid")]
public void TestWalls()
{
    Assert.IsNotNull(RevitAddin.RevitNUnitExecutor.CurrentDocument);
}
```

The `RevitTestModel` attribute accepts either a pair of BIM 360 GUIDs or a local
file path:

```csharp
[RevitTestModel(@"C:\\Models\\sample.rvt")]
```

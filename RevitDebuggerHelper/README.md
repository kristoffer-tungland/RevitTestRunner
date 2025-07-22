# Revit Debugger Helper

This is a .NET Framework 4.8 console application that enables Visual Studio debugger attachment and detachment for Revit processes from .NET Core/5+ applications.

## Why This Helper is Needed

The main test framework runs on .NET 8, but Visual Studio's DTE (Development Tools Environment) COM API requires .NET Framework for proper interop. This helper bridges that gap for both attachment and detachment operations.

## ?? **IMPORTANT: Project Setup**

The project file contains COM references that may cause build issues. If you encounter build errors related to EnvDTE, follow these steps:

### Option 1: Remove COM References (Recommended)
1. **Close Visual Studio** if the project is open
2. **Edit `RevitDebuggerHelper.csproj`** and remove the entire `<ItemGroup>` containing the `<COMReference>` entries:
   ```xml
   <!-- Remove this entire section -->
   <ItemGroup>
     <COMReference Include="EnvDTE">
       <!-- ... -->
     </COMReference>
     <COMReference Include="EnvDTE80">
       <!-- ... -->
     </COMReference>
   </ItemGroup>
   ```
3. **Save the file** and reopen in Visual Studio
4. **Build the project**

### Option 2: Manual Build with CSC
If you continue to have issues, you can build manually:
```cmd
csc /target:exe /out:bin\Release\RevitDebuggerHelper.exe Program.cs
```

## ? **Why This Works**

The updated `Program.cs` uses **dynamic COM interop** instead of compile-time EnvDTE references. This approach:
- ? Avoids COM reference resolution issues
- ? Works with any Visual Studio version
- ? Doesn't require specific EnvDTE assemblies
- ? Uses dynamic property access for all COM interactions

## Usage

### Command Line Options

#### Attach to Specific Process
```bash
RevitDebuggerHelper.exe <ProcessId>
```

#### Find and Attach to First Revit Process
```bash
RevitDebuggerHelper.exe --find-revit
```

#### Detach from Specific Process
```bash
RevitDebuggerHelper.exe --detach <ProcessId>
```

#### Detach from All Revit Processes
```bash
RevitDebuggerHelper.exe --detach-all
```

### From Code
The `PipeClientHelper` automatically uses this helper when debugger attachment/detachment is needed.

## Requirements

- .NET Framework 4.8
- Visual Studio 2017 or later (running)
- No additional dependencies required

## Exit Codes

| Code | Description |
|------|-------------|
| 0    | Success - operation completed |
| 1    | Invalid arguments or Visual Studio not found |
| 2    | Target process not found or not being debugged |
| 3    | COM error or other exception |

## Building

### Option 1: Visual Studio
1. Remove COM references from project file (see setup above)
2. Build normally in Visual Studio

### Option 2: MSBuild
```cmd
msbuild RevitDebuggerHelper.csproj /p:Configuration=Release
```

### Option 3: Batch Script
Run `build.bat` which will automatically find MSBuild and build the project.

### Option 4: Manual CSC
```cmd
csc /target:exe /out:bin\Release\RevitDebuggerHelper.exe Program.cs
```

## Integration

The helper is automatically used by `PipeClientHelper` for:

### Attachment (when tests start)
- Debug mode is enabled in the test command
- A debugger is attached to the test process
- Visual Studio DTE COM API is not directly accessible

### Detachment (when tests finish)
- Automatically detaches debugger after test execution completes
- Prevents debugger from staying attached to Revit unnecessarily
- Ensures clean test execution cycles

The helper is searched for in these locations:
1. Same directory as the calling assembly
2. `../RevitDebuggerHelper/bin/Release/`
3. `../RevitDebuggerHelper/bin/Debug/`
4. System PATH

## Troubleshooting

### Build Issues
1. **COM reference errors**: Remove COM references from project file (see setup above)
2. **Missing assemblies**: Use dynamic COM approach (already implemented)
3. **Platform mismatch**: Ensure AnyCPU target platform

### Runtime Issues
1. **Visual Studio not found**: Ensure Visual Studio is running
2. **Process not found**: Verify the target process exists
3. **Permission denied**: Run as administrator if needed
4. **COM errors**: Check that Visual Studio can see the process (Debug > Attach to Process)

### Testing the Helper
```cmd
# Test attachment to a Revit process
RevitDebuggerHelper.exe --find-revit

# Test detachment from a specific process
RevitDebuggerHelper.exe --detach 1234

# Test detachment from all Revit processes
RevitDebuggerHelper.exe --detach-all
```

## Technical Details

The helper uses **dynamic COM interop** with these key features:

### Attachment Features
- Uses `Marshal.GetActiveObject()` to find Visual Studio DTE
- Tries multiple Visual Studio versions (2022, 2019, 2017)
- Uses dynamic property access to navigate DTE object model
- Handles all COM interactions dynamically at runtime

### Detachment Features
- Accesses `debugger.DebuggedProcesses` to find attached processes
- Calls `process.Detach()` method on target processes
- Can detach from specific process ID or all Revit processes
- Gracefully handles cases where process is not being debugged

### Error Handling
- Comprehensive COM exception handling
- Specific error codes for different failure scenarios
- Detailed logging for troubleshooting
- No compile-time dependencies on Visual Studio assemblies

## Automatic Usage in Test Framework

When running Revit tests with debugging enabled:

1. **Test Start**: Helper attaches debugger to Revit process
2. **Test Execution**: Tests run with debugger attached
3. **Test Completion**: Helper automatically detaches debugger
4. **Clean State**: Revit process is left in clean state for next test run

This ensures that debugging doesn't interfere with subsequent test executions and provides a clean debugging experience.
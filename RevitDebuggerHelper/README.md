# Revit Debugger Helper

This is a .NET Framework 4.8 console application that enables Visual Studio debugger attachment and detachment for Revit processes from .NET Core/5+ applications.

## Why This Helper is Needed

The main test framework runs on .NET 8, but Visual Studio's DTE (Development Tools Environment) COM API requires .NET Framework for proper interop. This helper bridges that gap for both attachment and detachment operations.

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
- Microsoft.CSharp package for dynamic COM interop

## Exit Codes

| Code | Description |
|------|-------------|
| 0    | Success - operation completed |
| 1    | Invalid arguments or Visual Studio not found |
| 2    | Target process not found or not being debugged |
| 3    | COM error or other exception |

## Building

The project builds normally using standard .NET tooling:

### Visual Studio
Build the project normally in Visual Studio (Build ? Build Solution)

### .NET CLI
```cmd
dotnet build RevitDebuggerHelper.csproj
```

### MSBuild
```cmd
msbuild RevitDebuggerHelper.csproj /p:Configuration=Release
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
The project should build without issues using standard .NET tooling. If you encounter problems:
1. **Missing Microsoft.CSharp**: The project includes the necessary package reference
2. **Platform mismatch**: Ensure the project targets .NET Framework 4.8
3. **Visual Studio version**: Works with Visual Studio 2017 or later

### Runtime Issues
1. **Visual Studio not found**: Ensure Visual Studio is running
2. **Process not found**: Verify the target process exists
3. **Permission denied**: Run as administrator if needed
4. **COM errors**: Check that Visual Studio can see the process (Debug > Attach to Process)

## Technical Details

The helper uses **dynamic COM interop** with these key features:

### Dynamic COM Interop Approach
- Uses `Marshal.GetActiveObject()` to find Visual Studio DTE
- Tries multiple Visual Studio versions (2022, 2019, 2017)
- Uses dynamic property access to navigate DTE object model
- Handles all COM interactions dynamically at runtime
- No compile-time dependencies on Visual Studio assemblies

### Attachment Features
- Searches through `debugger.LocalProcesses` to find target process
- Calls `process.Attach()` method on target process
- Validates process exists before attempting attachment
- Provides detailed error messages for troubleshooting

### Detachment Features
- Accesses `debugger.DebuggedProcesses` to find attached processes
- Calls `process.Detach()` method on target processes
- Can detach from specific process ID or all Revit processes
- Gracefully handles cases where process is not being debugged

### Error Handling
- Comprehensive COM exception handling with specific HRESULT guidance
- Specific error codes for different failure scenarios
- Detailed logging for troubleshooting
- Fallback mechanisms for finding Visual Studio instances

## Automatic Usage in Test Framework

When running Revit tests with debugging enabled:

1. **Test Start**: Helper attaches debugger to Revit process
2. **Test Execution**: Tests run with debugger attached
3. **Test Completion**: Helper automatically detaches debugger
4. **Clean State**: Revit process is left in clean state for next test run

This ensures that debugging doesn't interfere with subsequent test executions and provides a clean debugging experience.
# Advanced Debugging Guide for Revit Tests

This guide explains how to debug Revit tests using the enhanced debugging features of the Revit Test Framework.

## ?? Quick Start

1. **Setup the helper** (one-time): See [Helper Setup](#-helper-setup) below
2. **Run tests with debugger attached** (F5 in Visual Studio)
3. **Debug mode is automatically enabled** when `Debugger.IsAttached` is true
4. **Debugger automatically attaches to Revit** when tests start
5. **Debugger automatically detaches from Revit** when tests finish (asynchronously)

## ??? **Helper Setup**

Before debugging, you need to set up the RevitDebuggerHelper:

### Step 1: Fix Project File (Important!)
The helper project may have COM reference issues. To fix:

1. **Close Visual Studio** if RevitDebuggerHelper project is open
2. **Edit** `RevitDebuggerHelper\RevitDebuggerHelper.csproj`
3. **Remove** the entire `<ItemGroup>` containing `<COMReference>` entries:
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
4. **Save and reopen** in Visual Studio

### Step 2: Build the Helper
Choose one option:

**Option A: Visual Studio**
1. Right-click RevitDebuggerHelper project ? Build

**Option B: Batch Script**
1. Run `RevitDebuggerHelper\build.bat`

**Option C: Manual (if all else fails)**
```cmd
cd RevitDebuggerHelper
csc /target:exe /out:bin\Release\RevitDebuggerHelper.exe Program.cs
```

### Step 3: Verify Setup
Test the helper:
```cmd
RevitDebuggerHelper\bin\Release\RevitDebuggerHelper.exe --find-revit
```

## ?? Components

### 1. Automatic Debug Detection
- **Location**: `RevitXunitExecutor.SendRunCommandStreaming`
- **Trigger**: When `Debugger.IsAttached` is true
- **Action**: Sets `PipeCommand.Debug = true`

### 2. Revit Process Debugger Attachment
- **Location**: `PipeClientHelper.TryAttachDebuggerToRevit`
- **Method**: Uses the specific Revit process ID that's running tests
- **Timing**: When test execution starts
- **Fallback**: Manual attachment instructions when automatic fails

### 3. Revit Process Debugger Detachment
- **Location**: `PipeClientHelper.TryDetachDebuggerUsingHelper`
- **Method**: Uses the specific Revit process ID that was running tests
- **Timing**: When test execution completes (END signal received)
- **Execution**: **Asynchronous** - doesn't block test host exit
- **Purpose**: Ensures clean state for subsequent test runs

### 4. .NET Framework Helper Application
- **Location**: `RevitDebuggerHelper/`
- **Purpose**: Handles Visual Studio DTE COM interop for .NET Core/5+ applications
- **Operations**: Both attachment (`RevitDebuggerHelper.exe <ProcessId>`) and detachment (`RevitDebuggerHelper.exe --detach <ProcessId>`)

### 5. Test Execution Breakpoints
- **Location**: `RevitXunitTestCaseRunner.RunTestAsync`
- **Behavior**: Configurable breakpoint insertion
- **Control**: Environment variable `REVIT_TEST_BREAK_ON_ALL`

## ?? Debugging Flow

### **Complete Debugging Lifecycle**

1. **Test Discovery**: Tests are discovered normally
2. **Debug Detection**: `Debugger.IsAttached` triggers debug mode
3. **Revit Connection**: Framework connects to specific Revit process
4. **Debugger Attachment**: Helper attaches VS debugger to Revit
5. **Test Execution**: Tests run with breakpoint opportunities
6. **Test Completion**: END signal indicates tests are finished
7. **Debugger Detachment**: Helper detaches VS debugger from Revit (asynchronously)
8. **Test Host Exit**: Test host exits cleanly without hanging

## ?? Debugging Modes

### Automatic Mode (Default)
```csharp
// No special setup needed - just run with debugger attached (F5)
[RevitFact]
public void MyTest()
{
    // Test will run in debug mode automatically
    // Debugger will attach at start and detach at end
}
```

### Selective Breakpoints
```csharp
// Automatically breaks when debugger attached
[RevitFact]
public void MyDebugTest() // Contains "Debug" in name
{
    // Breakpoint will be triggered
    // Debugger will detach when test completes
}
```

### Force All Breakpoints
Set environment variable before running tests:
```bash
set REVIT_TEST_BREAK_ON_ALL=true
```

## ??? Helper Application Usage

### Manual Usage
```bash
# Attach to specific Revit process
RevitDebuggerHelper.exe 12345

# Find and attach to first Revit process
RevitDebuggerHelper.exe --find-revit

# Detach from specific process
RevitDebuggerHelper.exe --detach 12345

# Detach from all Revit processes
RevitDebuggerHelper.exe --detach-all
```

### Automatic Usage
The helper is automatically used for:

**Attachment** (when tests start):
- Debug mode is enabled
- A debugger is attached to the test process
- Visual Studio DTE COM API is accessible

**Detachment** (when tests finish):
- Test execution completes (END signal received)
- Debugger was previously attached by the framework
- **Runs asynchronously** to avoid blocking test host exit
- Ensures clean state for next test run

## ?? Helper Application Locations

The framework searches for `RevitDebuggerHelper.exe` in:
1. Same directory as test assembly
2. `../RevitDebuggerHelper/bin/Release/`
3. `../RevitDebuggerHelper/bin/Debug/`
4. System PATH

## ?? Configuration Options

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `REVIT_TEST_BREAK_ON_ALL` | `false` | Break on all tests when debugger attached |
| `REVIT_DEBUG_DISABLE_AUTO_DETACH` | `false` | Disable automatic debugger detachment |

### Disable Auto-Detachment
If the automatic detachment causes issues, you can disable it:
```cmd
set REVIT_DEBUG_DISABLE_AUTO_DETACH=true
```

Then run your tests. You'll need to manually detach the debugger or use:
```cmd
RevitDebuggerHelper.exe --detach-all
```

## ?? Debug Logging

All debug operations are logged via `ILogger` interface:
- Connection attempts
- Process discovery
- Debugger attachment status
- Test execution progress
- Debugger detachment status (asynchronous)
- Helper application output
- Fallback instructions

### Example Debug Log Output
```
PipeClientHelper: Found 1 running Revit process(es)
PipeClientHelper: Successfully connected to Revit process 12345
PipeClientHelper: Attempting to attach debugger to specific Revit process 12345
PipeClientHelper: Successfully attached debugger to process 12345
PipeClientHelper: Test execution completed - END signal received
PipeClientHelper: Test execution finished - initiating debugger detachment
PipeClientHelper: Debugger detachment initiated asynchronously for process 12345
```

## ?? Troubleshooting

### Test Host Crashing Issues
If you experience test host crashes with "Test host process crashed":

1. **Try disabling auto-detachment**:
   ```cmd
   set REVIT_DEBUG_DISABLE_AUTO_DETACH=true
   ```

2. **Check helper availability**: Ensure RevitDebuggerHelper.exe is built and accessible

3. **Manual detachment**: After tests complete, manually run:
   ```cmd
   RevitDebuggerHelper.exe --detach-all
   ```

### Helper Build Issues
1. **COM reference errors**: Remove COM references from project file (see [Helper Setup](#-helper-setup))
2. **Missing assemblies**: Our dynamic COM approach handles this automatically
3. **Platform mismatch**: Ensure AnyCPU target

### Helper Runtime Issues
1. **Visual Studio not found**: Ensure VS is running
2. **Process not found**: Confirm Revit process exists
3. **Permission denied**: Run as administrator if needed
4. **Helper not found**: Check the search locations above

### Attachment Issues
1. **Can't attach**: Ensure Visual Studio can manually attach (Debug > Attach to Process)
2. **Access denied**: Try running Visual Studio as administrator
3. **Process not visible**: Check user permissions and process visibility

### Detachment Issues
1. **Can't detach**: Usually not critical - debugger will detach when VS closes
2. **Process not found**: Process may have already exited - this is normal
3. **Already detached**: Helper will report this as success (exit code 0)
4. **Timeout**: Detachment times out after 5 seconds to avoid hanging

### Breakpoint Issues
1. **Symbol loading**: Ensure PDB files are available
2. **Source mapping**: Verify source file paths
3. **Environment variable**: Set `REVIT_TEST_BREAK_ON_ALL=true`

### Connection Issues
1. **Process ID**: Check log for specific Revit process ID
2. **Named pipes**: Verify pipe connection succeeds
3. **Permissions**: Run as administrator if needed

## ?? Best Practices

1. **Setup helper first**: Complete the helper setup before debugging
2. **Name debug tests appropriately**: Include "Debug" in test or class names
3. **Use environment variables**: Set `REVIT_TEST_BREAK_ON_ALL` for comprehensive debugging
4. **Check logs**: Review debug output for attachment/detachment status
5. **Manual fallback**: Use manual attachment if automatic fails
6. **Test helper standalone**: Verify helper works before running tests
7. **Clean test cycles**: Automatic detachment ensures clean state between runs
8. **Disable auto-detach if needed**: Use environment variable if experiencing crashes

## ?? Exit Codes (Helper Application)

| Code | Description |
|------|-------------|
| 0    | Success - operation completed |
| 1    | Invalid arguments or Visual Studio not found |
| 2    | Target process not found or not being debugged |
| 3    | COM error or exception |

## ?? Integration Points

- **Test Executor**: Automatic debug mode detection
- **Pipe Client**: Process-specific debugger attachment and detachment
- **Test Runner**: Configurable breakpoint behavior
- **Helper App**: .NET Framework COM interop bridge for attach/detach operations

## ?? Quick Setup Checklist

- [ ] Remove COM references from `RevitDebuggerHelper.csproj`
- [ ] Build the helper application successfully
- [ ] Test helper with `--find-revit` command
- [ ] Test helper with `--detach-all` command
- [ ] Verify helper location is accessible to test framework
- [ ] Run tests with F5 (debugger attached)
- [ ] Verify automatic detachment in logs
- [ ] If test host crashes, set `REVIT_DEBUG_DISABLE_AUTO_DETACH=true`

## ?? Benefits of Asynchronous Detachment

1. **?? Clean Test Cycles**: Each test run starts with a clean debugging state
2. **?? Performance**: Avoids debugger overhead when not needed
3. **??? Reliability**: Prevents debugging artifacts from affecting subsequent tests
4. **? Non-blocking**: Asynchronous execution prevents test host hangs
5. **?? Transparency**: Full logging of attachment/detachment operations
6. **?? Configurable**: Can be disabled if it causes issues

This comprehensive debugging solution provides both automatic attachment and detachment capabilities, making it easy to debug Revit tests while ensuring clean execution cycles and preventing test host crashes.
# Real-time Log Forwarding Example

When running Revit tests, you will now see **important** log messages from the Revit application appear in real-time in your test framework output (e.g., Visual Studio Test Output window). DEBUG level messages are kept in file logs only to avoid cluttering the test output.

## Example Test Output (Clean - No DEBUG Messages)

```
[14:30:20.123] RevitXunitExecutor: Starting test execution with specific test cases
[14:30:20.145] RevitXunitExecutor: Running 3 tests from C:\MyProject\bin\Debug\MyRevitTests.dll
[14:30:20.167] RevitXunitExecutor: Preparing command for assembly C:\MyProject\bin\Debug\MyRevitTests.dll
[14:30:20.189] RevitXunitExecutor: Sending command to Revit via named pipe
[14:30:21.234] Revit.PipeCommandHandler [INFO] 14:30:21.234: Starting execution of pipe command: RunTests for assembly: C:\MyProject\bin\Debug\MyRevitTests.dll
[14:30:21.278] Revit.XunitTestAssemblyLoadContext [INFO] 14:30:21.278: Setting up Revit test infrastructure
[14:30:21.345] Revit.RevitXunitExecutor [INFO] 14:30:21.345: Starting test execution for assembly: C:\Temp\RunTests\abc123\MyRevitTests.dll
[14:30:21.523] Revit.RevitXunitExecutor [INFO] 14:30:21.523: Discovered 15 test cases
[14:30:21.545] Revit.RevitXunitExecutor [INFO] 14:30:21.545: Filtered to 3 test cases (from 15) based on method filter
[14:30:21.567] Revit.RevitXunitExecutor [INFO] 14:30:21.567: Starting execution of 3 test cases
[14:30:22.123] Revit.StreamingXmlTestExecutionVisitor [INFO] 14:30:22.123: Test passed: MyRevitTests.WallTests.Should_Create_Wall in 456ms
[14:30:22.145] RevitXunitExecutor: Recorded result for MyRevitTests.WallTests.Should_Create_Wall: Passed
[14:30:22.678] Revit.StreamingXmlTestExecutionVisitor [INFO] 14:30:22.678: Test passed: MyRevitTests.WallTests.Should_Count_Walls in 234ms
[14:30:22.689] RevitXunitExecutor: Recorded result for MyRevitTests.WallTests.Should_Count_Walls: Passed
[14:30:23.234] Revit.StreamingXmlTestExecutionVisitor [INFO] 14:30:23.234: Test passed: MyRevitTests.DoorTests.Should_Insert_Door in 345ms
[14:30:23.245] RevitXunitExecutor: Recorded result for MyRevitTests.DoorTests.Should_Insert_Door: Passed
[14:30:23.267] Revit.RevitXunitExecutor [INFO] 14:30:23.267: Test execution completed. Results saved to: C:\Temp\RevitXunitResults_def456.xml
[14:30:23.289] Revit.RevitXunitExecutor [INFO] 14:30:23.289: Test execution async operation completed
[14:30:23.312] Revit.XunitTestAssemblyLoadContext [INFO] 14:30:23.312: Tearing down Revit test infrastructure
[14:30:23.334] Revit.PipeCommandHandler [INFO] 14:30:23.334: Test execution completed successfully
[14:30:23.356] RevitXunitExecutor: Received END signal from Revit
```

## What Changed - Cleaner Test Output

**Before (Too Verbose):**
```
[14:30:21.256] Revit.XunitTestAssemblyLoadContext [DEBUG] 14:30:21.256: Creating test assembly load context for directory: C:\Temp\RunTests\abc123
[14:30:21.367] Revit.RevitXunitExecutor [DEBUG] 14:30:21.367: Test execution command - Debug: False, Methods: ...
[14:30:21.389] Revit.RevitXunitExecutor [DEBUG] 14:30:21.389: Starting xUnit test execution in background thread
[14:30:21.456] Revit.RevitXunitExecutor [DEBUG] 14:30:21.456: Discovering test cases in assembly
[14:30:21.864] Revit.XunitTestAssemblyLoadContext [DEBUG] 14:30:21.864: Assembly resolution delegated to default context: System.Runtime
[14:30:21.865] Revit.XunitTestAssemblyLoadContext [DEBUG] 14:30:21.865: Resolving assembly: MyRevitTests from test directory: C:\Temp\...
[14:30:21.866] Revit.XunitTestAssemblyLoadContext [DEBUG] 14:30:21.866: Assembly resolution delegated to default context: System.Collections
```

**After (Clean and Focused):**
```
// DEBUG messages are now in file logs only - test output shows only important events
[14:30:21.278] Revit.XunitTestAssemblyLoadContext [INFO] 14:30:21.278: Setting up Revit test infrastructure
[14:30:21.523] Revit.RevitXunitExecutor [INFO] 14:30:21.523: Discovered 15 test cases
[14:30:21.567] Revit.RevitXunitExecutor [INFO] 14:30:21.567: Starting execution of 3 test cases
```

## Log Level Distribution

### Test Output Window (Clean)
- **INFO**: Important progress and status updates
- **WARN**: Issues that need attention but don't stop execution
- **ERROR**: Problems that may affect test results
- **FATAL**: Critical failures

### File Logs (Comprehensive)
- **DEBUG**: Detailed diagnostic information (assembly resolution, method invocations, etc.)
- **INFO**: Same as test output
- **WARN**: Same as test output  
- **ERROR**: Same as test output
- **FATAL**: Same as test output

## File Log Contains All DEBUG Details

While the test output is clean, the file log still contains all the detailed DEBUG information:

```
2024-01-15 14:30:21.256 +01:00 [DEBUG] [PID:1234] [TID:19] Creating test assembly load context for directory: C:\Temp\RunTests\abc123
2024-01-15 14:30:21.367 +01:00 [DEBUG] [PID:1234] [TID:24] Test execution command - Debug: False, Methods: MyTest
2024-01-15 14:30:21.389 +01:00 [DEBUG] [PID:1234] [TID:25] Starting xUnit test execution in background thread
2024-01-15 14:30:21.456 +01:00 [DEBUG] [PID:1234] [TID:25] Discovering test cases in assembly
2024-01-15 14:30:21.878 +01:00 [DEBUG] [PID:1234] [TID:1] Assembly resolution delegated to default context: System.Runtime
2024-01-15 14:30:21.907 +01:00 [DEBUG] [PID:1234] [TID:24] Resolving assembly: RevitTestFramework.Contracts from test directory: C:\Temp\...
```

## Key Benefits

1. **Cleaner Test Output**: No more spam from detailed assembly resolution and internal operations
2. **Focus on Important Events**: Test output shows progress, results, and issues that matter
3. **Better Signal-to-Noise Ratio**: Easier to follow test execution without diagnostic noise
4. **Maintained Debugging**: All detailed information is still available in file logs when needed
5. **Real-time Important Updates**: Still see critical events as they happen

## When to Check Each Source

### Check Test Output for:
- Test execution progress and results
- Important warnings and errors during execution
- Infrastructure setup/teardown status
- Real-time monitoring of test runs

### Check File Logs for:
- Detailed assembly resolution diagnostics
- Internal method call tracing
- Step-by-step debugging information
- Performance analysis and timing details
- Troubleshooting complex issues

This provides the best of both worlds: clean, actionable information in real-time, with comprehensive diagnostic details available when you need to dig deeper
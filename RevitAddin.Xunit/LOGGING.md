# Custom Logging Implementation

This implementation adds comprehensive file logging to the RevitAddin.Xunit project using a lightweight custom logger instead of external dependencies like Serilog.

## Features

- **File-based logging** to `%LOCALAPPDATA%\RevitTestRunner\Logs\`
- **Daily log files** with format `RevitAddin.Xunit-yyyyMMdd.log`
- **Thread-safe logging** with lock-based synchronization
- **No external dependencies** - uses only built-in .NET functionality
- **Fallback to Debug output** if file writing fails
- **Automatic directory creation** for log files
- **?? Pipe-aware logging** - logs are forwarded to the test framework in real-time
- **?? Smart assembly resolution logging** - reduces noise from expected system assembly failures
- **?? File-only DEBUG logging** - DEBUG messages are kept in files only, not sent to test output

## Custom Logger Implementation

The custom logger is implemented in `RevitAddin.Common/Logger.cs` with:

- **ILogger interface**: Simple interface with LogDebug, LogInformation, LogWarning, LogError, and LogFatal methods
- **FileLogger class**: Singleton implementation that writes to daily log files
- **?? PipeAwareLogger class**: Extends file logging with selective real-time forwarding to test framework
- **Thread safety**: Uses lock-based synchronization for safe multi-threaded access
- **Automatic fallback**: Falls back to Debug.WriteLine if file writing fails

## Log File Location

Log files are stored in:
```
C:\Users\{username}\AppData\Local\RevitTestRunner\Logs\
```

## ?? Real-time Log Forwarding

In addition to file logging, the system now forwards **important** log messages from Revit back to the test framework in real-time through the named pipe connection:

- **Visual Studio Test Output**: INFO, WARN, ERROR, and FATAL messages appear in the Test Output window
- **File-only DEBUG**: DEBUG messages are kept in file logs only for detailed diagnostics
- **Real-time feedback**: See important events happening inside Revit during test execution
- **Better debugging**: Immediate visibility into test infrastructure operations without noise
- **Structured messages**: Logs are formatted with timestamp, source, and level information

### Log Message Format in Test Output
```
Revit.PipeCommandHandler [INFO] 14:30:25.123: Starting execution of pipe command: RunTests
Revit.RevitXunitExecutor [INFO] 14:30:25.167: Test passed: MyTest in 125ms
Revit.XunitTestAssemblyLoadContext [WARN] 14:30:25.189: Failed to resolve assembly: MyCustomAssembly
```

### Log Message Format in File Only (DEBUG)
```
2024-01-15 14:30:25.145 +01:00 [DEBUG] [PID:1234] [TID:5] Assembly resolution delegated to default loader: System.Runtime
2024-01-15 14:30:25.146 +01:00 [DEBUG] [PID:1234] [TID:5] Loading RevitAddin.Xunit assembly from: C:\Path\To\Assembly.dll
```

## ?? Smart Assembly Resolution Logging

The logging system now intelligently filters assembly resolution messages to reduce noise:

- **System assemblies** (System.Runtime, System.Collections, System.Diagnostics.Process, System.Xml.XDocument, etc.) are logged at DEBUG level
- **Revit API assemblies** (RevitAPI, RevitAPIUI, etc.) are logged at DEBUG level
- **Testing framework assemblies** (Microsoft.TestPlatform, etc.) are logged at DEBUG level
- **Unexpected assembly failures** are still logged as WARNING for investigation

This significantly reduces the verbosity of logs while still capturing important assembly resolution issues.

## Log Levels

- **DEBUG**: Detailed diagnostic information (assembly resolution details, test discovery details) - **FILE ONLY**
- **INFO**: General application flow and important events (startup, shutdown, test execution) - **FILE + TEST OUTPUT**
- **WARN**: Potential issues that don't prevent operation (unexpected assembly resolution failures, failed dependency copies) - **FILE + TEST OUTPUT**
- **ERROR**: Error conditions that may affect functionality - **FILE + TEST OUTPUT**
- **FATAL**: Critical errors that may cause application termination - **FILE + TEST OUTPUT**

## Logged Events

### Startup & Shutdown
- Application startup/shutdown events (INFO - visible in test output)
- Logger initialization (DEBUG - file only)
- Assembly resolution activities (DEBUG - file only, filtered for relevance)
- PipeServer start/stop (INFO - visible in test output)

### Test Execution
- Test assembly loading and copying (DEBUG - file only)
- Test discovery and filtering (DEBUG/INFO - mixed visibility)
- Individual test results (INFO - visible in test output)
- Test execution timing (INFO - visible in test output)
- Infrastructure setup/teardown (INFO - visible in test output)

### Communication
- Named pipe connections and disconnections (INFO - visible in test output)
- Command processing (INFO - visible in test output)
- Client communication events (DEBUG - file only)

### Error Handling
- Exceptions with full stack traces (ERROR/FATAL - visible in test output)
- Unexpected assembly resolution failures (WARN - visible in test output)
- Test execution failures (ERROR - visible in test output)
- Cleanup errors (WARN/ERROR - visible in test output)

## Log Format

### File Log Format (All Levels)
```
2024-01-15 14:30:25.123 +01:00 [INFO] [PID:1234] [TID:5] RevitAddin.Xunit startup beginning
2024-01-15 14:30:25.145 +01:00 [DEBUG] [PID:1234] [TID:5] Assembly resolution delegated to default loader: System.Runtime
2024-01-15 14:30:25.167 +01:00 [INFO] [PID:1234] [TID:5] RevitAddin.Xunit detected Revit version: 2025
2024-01-15 14:30:25.189 +01:00 [WARN] [PID:1234] [TID:5] Failed to resolve assembly: MyCustomAssembly
2024-01-15 14:30:25.211 +01:00 [ERROR] [PID:1234] [TID:5] Test execution failed
```

### Test Framework Output Format (INFO, WARN, ERROR, FATAL Only)
```
Revit [INFO] 14:30:25.123: RevitAddin.Xunit startup beginning
Revit.PipeCommandHandler [INFO] 14:30:25.145: Starting execution of pipe command: RunTests
Revit.XunitTestAssemblyLoadContext [WARN] 14:30:25.189: Failed to resolve assembly: MyCustomAssembly
Revit.RevitXunitExecutor [ERROR] 14:30:25.211: Test execution failed
```

## Logger Usage

### Basic File-only Logging
```csharp
private static readonly ILogger Logger = FileLogger.Instance;

Logger.LogInformation("Application started");  // Appears in both file and test output
Logger.LogDebug("Detailed info");              // Appears in file only
Logger.LogWarning("Non-critical issue occurred"); // Appears in both file and test output
Logger.LogError(exception, "Critical error occurred"); // Appears in both file and test output
```

### Pipe-aware Logging (automatically used during test execution)
```csharp
// This is automatically created when a pipe writer is available
var pipeAwareLogger = PipeAwareLogger.ForContext<MyClass>(pipeWriter);

pipeAwareLogger.LogInformation("This will appear in both file and test output");
pipeAwareLogger.LogDebug("This will appear in file only");
```

## Components with Logging

1. **RevitApplication**: Startup, shutdown, smart assembly resolution
2. **XunitTestAssemblyLoadContext**: Test assembly loading and infrastructure setup (?? pipe-aware, smart assembly resolution)
3. **RevitXunitExecutor**: Test execution and results (?? pipe-aware)
4. **PipeServer**: Named pipe communication and command processing
5. **PipeCommandHandler**: Command execution coordination (?? pipe-aware)

## Benefits over External Dependencies

- **No package dependencies**: Reduces assembly size and potential conflicts
- **Simplified deployment**: No need to distribute additional libraries
- **Better performance**: Direct file I/O without framework overhead
- **Full control**: Can customize logging behavior as needed
- **Reliability**: Fewer moving parts means fewer potential failure points
- **?? Real-time visibility**: Immediate feedback in test framework for important events
- **?? Clean logs**: Intelligent filtering reduces noise while preserving important information
- **?? Selective forwarding**: DEBUG details stay in files, important events go to test output

## Usage Examples

### Check the log files when:
- Tests fail to discover or execute
- Assemblies fail to load
- Communication issues between test adapter and Revit
- Performance analysis of test execution
- Debugging test infrastructure issues
- **Detailed assembly resolution diagnostics**

### Check the Test Output window for:
- **?? Real-time feedback during test execution**
- Test execution progress and results
- Important warnings and errors
- Infrastructure setup/teardown events

The structured logging format makes it easy to search and filter log entries for specific operations or error conditions. The selective forwarding ensures you see the important messages in real-time without being overwhelmed by detailed diagnostic information that belongs in log files.
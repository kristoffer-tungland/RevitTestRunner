# Custom Logging Implementation

This implementation adds comprehensive file logging to the RevitAddin.Xunit project using a lightweight custom logger instead of external dependencies like Serilog.

## Features

- **File-based logging** to `%LOCALAPPDATA%\RevitTestRunner\Logs\`
- **Daily log files** with format `RevitAddin.Xunit-yyyyMMdd.log`
- **Thread-safe logging** with lock-based synchronization
- **No external dependencies** - uses only built-in .NET functionality
- **Fallback to Debug output** if file writing fails
- **Automatic directory creation** for log files
- **Configurable log level via `loglevel.txt`** in the log directory
- **Pipe-aware logging** - logs are forwarded to the test framework in real-time
- **Smart assembly resolution logging** - reduces noise from expected system assembly failures
- **File-only TRACE/DEBUG logging** - TRACE and DEBUG messages are kept in files only, not sent to test output

## Configuring Log Level

The minimum log level for logging is configured by editing the `loglevel.txt` file in the log directory:

```
C:\Users\{username}\AppData\Local\RevitTestRunner\Logs\loglevel.txt
```

Set the content of this file to one of the following values (case-insensitive):

- `Trace`
- `Debug`
- `Info`
- `Warn`
- `Error`
- `Fatal`

For example, to enable verbose logging, set the file content to:
```
Trace
```
To reduce log verbosity, set it to:
```
Warn
```
If the file does not exist, it will be created automatically with the default value `Debug`.

## Log File Location

Log files are stored in:
```
C:\Users\{username}\AppData\Local\RevitTestRunner\Logs\
```

## Real-time Log Forwarding

In addition to file logging, the system forwards **important** log messages from Revit back to the test framework in real-time through the named pipe connection:

- **Visual Studio Test Output**: INFO, WARN, ERROR, and FATAL messages appear in the Test Output window
- **File-only TRACE/DEBUG**: TRACE and DEBUG messages are kept in file logs only for detailed diagnostics
- **Real-time feedback**: See important events happening inside Revit during test execution
- **Better debugging**: Immediate visibility into test infrastructure operations without noise
- **Structured messages**: Logs are formatted with timestamp, source, and level information

### Log Message Format in Test Output
```
Revit.PipeCommandHandler [INFO] 14:30:25.123: Starting execution of pipe command: RunTests
Revit.RevitXunitExecutor [INFO] 14:30:25.167: Test passed: MyTest in 125ms
Revit.XunitTestAssemblyLoadContext [WARN] 14:30:25.189: Failed to resolve assembly: MyCustomAssembly
Revit.RevitXunitExecutor [ERROR] 14:30:25.211: Test execution failed
```

### Log Message Format in File Only (TRACE/DEBUG)
```
2024-01-15 14:30:25.120 +01:00 [TRACE] [PID:1234] [TID:5] Logger initialized. Log file: C:\Path\To\Log.log
2024-01-15 14:30:25.145 +01:00 [DEBUG] [PID:1234] [TID:5] Assembly resolution delegated to default loader: System.Runtime
2024-01-15 14:30:25.146 +01:00 [DEBUG] [PID:1234] [TID:5] Loading RevitAddin.Xunit assembly from: C:\Path\To\Assembly.dll
```

### Log File Format (All Levels)
```
2024-01-15 14:30:25.120 +01:00 [TRACE] [PID:1234] [TID:5] Logger initialized. Log file: C:\Path\To\Log.log
2024-01-15 14:30:25.123 +01:00 [INFO] [PID:1234] [TID:5] RevitAddin.Xunit startup beginning
2024-01-15 14:30:25.145 +01:00 [DEBUG] [PID:1234] [TID:5] Assembly resolution delegated to default loader: System.Runtime
2024-01-15 14:30:25.167 +01:00 [INFO] [PID:1234] [TID:5] RevitAddin.Xunit detected Revit version: 2025
2024-01-15 14:30:25.189 +01:00 [WARN] [PID:1234] [TID:5] Failed to resolve assembly: MyCustomAssembly
2024-01-15 14:30:25.211 +01:00 [ERROR] [PID:1234] [TID:5] Test execution failed
```

## Log Levels

- **TRACE**: Most detailed diagnostic information (file only)
- **DEBUG**: Detailed diagnostic information (file only)
- **INFO**: General application flow and important events (file + test output)
- **WARN**: Potential issues that don't prevent operation (file + test output)
- **ERROR**: Error conditions that may affect functionality (file + test output)
- **FATAL**: Critical errors that may cause application termination (file + test output)

## Usage Example

To change the log level, edit `loglevel.txt` in the log directory and set the desired level.

## Other Features

- **Smart Assembly Resolution Logging**: System, Revit API, and test framework assemblies are logged at DEBUG level; unexpected failures at WARN.
- **Pipe-aware Logging**: INFO and above are forwarded to test output; TRACE/DEBUG are file only.
- **Automatic loglevel.txt creation**: If missing, created with default value `Debug`.

---

For more details, see the main README or contact the project maintainers.
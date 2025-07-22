using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading;
using RevitTestFramework.Contracts;

namespace RevitAdapterCommon;

/// <summary>
/// Simple logging interface to avoid direct dependency on test platform types
/// </summary>
public interface ILogger
{
    void LogInformation(string message);
    void LogError(string message);
}

/// <summary>
/// Wrapper for IFrameworkHandle to implement ILogger
/// </summary>
public class FrameworkHandleLogger : ILogger
{
    private readonly object _frameworkHandle;
    private readonly object _informationalLevel;
    private readonly object _errorLevel;

    public FrameworkHandleLogger(object frameworkHandle)
    {
        _frameworkHandle = frameworkHandle ?? throw new ArgumentNullException(nameof(frameworkHandle));

        try
        {
            // Look for TestMessageLevel in multiple possible locations
            Type? testMessageLevelType = null;

            // First try the framework handle's assembly
            var handleAssembly = frameworkHandle.GetType().Assembly;
            testMessageLevelType = handleAssembly.GetType("Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel");

            // If not found, try loaded assemblies
            if (testMessageLevelType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    testMessageLevelType = assembly.GetType("Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel");
                    if (testMessageLevelType != null) break;
                }
            }

            // If still not found, try a more generic approach
            if (testMessageLevelType == null)
            {
                // Look for any type named TestMessageLevel in any namespace
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var types = assembly.GetTypes();
                        testMessageLevelType = types.FirstOrDefault(t => t.Name == "TestMessageLevel");
                        if (testMessageLevelType != null) break;
                    }
                    catch
                    {
                        // Ignore exceptions when accessing assembly types
                        continue;
                    }
                }
            }

            if (testMessageLevelType == null)
            {
                throw new InvalidOperationException("Could not find TestMessageLevel type in any loaded assembly");
            }

            _informationalLevel = Enum.Parse(testMessageLevelType, "Informational");
            _errorLevel = Enum.Parse(testMessageLevelType, "Error");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize FrameworkHandleLogger: {ex.Message}", ex);
        }
    }

    public void LogInformation(string message)
    {
        SendMessage(_informationalLevel, message);
    }

    public void LogError(string message)
    {
        SendMessage(_errorLevel, message);
    }

    private void SendMessage(object level, string message)
    {
        try
        {
            var method = _frameworkHandle.GetType().GetMethod("SendMessage");
            method?.Invoke(_frameworkHandle, new[] { level, message });
        }
        catch (Exception ex)
        {
            // Fallback: just output to debug if SendMessage fails
            Debug.WriteLine($"Failed to send message '{message}': {ex.Message}");
        }
    }
}

/// <summary>
/// Extension methods for creating loggers from framework handles
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Creates a logger from a framework handle object
    /// </summary>
    /// <param name="frameworkHandle">The framework handle object</param>
    /// <returns>An ILogger instance</returns>
    public static ILogger ToLogger(this object frameworkHandle)
    {
        return new FrameworkHandleLogger(frameworkHandle);
    }
}

public static class PipeClientHelper
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MINIMIZE = 6;
    private const int SW_HIDE = 0;

    /// <summary>
    /// Tracks launched Revit processes to ensure they are cleaned up after test execution
    /// </summary>
    private static readonly HashSet<int> _launchedRevitProcessIds = new();
    private static readonly object _processTrackingLock = new();

    static PipeClientHelper()
    {
        // Register for process exit to clean up any remaining Revit processes
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
    }

    /// <summary>
    /// Cleanup handler for when the current process is exiting
    /// </summary>
    private static void OnProcessExit(object? sender, EventArgs e)
    {
        CleanupLaunchedRevitProcesses(null);
    }

    /// <summary>
    /// Cleanup handler for when the current AppDomain is unloading
    /// </summary>
    private static void OnDomainUnload(object? sender, EventArgs e)
    {
        CleanupLaunchedRevitProcesses(null);
    }

    /// <summary>
    /// Tracks a launched Revit process for cleanup
    /// </summary>
    /// <param name="processId">The process ID to track</param>
    /// <param name="logger">Optional logger for informational messages</param>
    private static void TrackLaunchedRevitProcess(int processId, ILogger? logger)
    {
        lock (_processTrackingLock)
        {
            _launchedRevitProcessIds.Add(processId);
            logger?.LogInformation($"PipeClientHelper: Tracking launched Revit process {processId} for cleanup");
        }
    }

    /// <summary>
    /// Stops tracking a Revit process (called when the process is manually killed or exits)
    /// </summary>
    /// <param name="processId">The process ID to stop tracking</param>
    private static void UntrackRevitProcess(int processId)
    {
        lock (_processTrackingLock)
        {
            _launchedRevitProcessIds.Remove(processId);
        }
    }

    /// <summary>
    /// Cleans up all launched Revit processes
    /// </summary>
    /// <param name="logger">Optional logger for informational messages</param>
    public static void CleanupLaunchedRevitProcesses(ILogger? logger)
    {
        List<int> processesToKill;
        lock (_processTrackingLock)
        {
            processesToKill = new List<int>(_launchedRevitProcessIds);
            _launchedRevitProcessIds.Clear();
        }

        if (processesToKill.Count > 0)
        {
            logger?.LogInformation($"PipeClientHelper: Cleaning up {processesToKill.Count} launched Revit process(es)");
            
            foreach (var processId in processesToKill)
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        logger?.LogInformation($"PipeClientHelper: Killing launched Revit process {processId}");
                        process.Kill();
                        process.WaitForExit(5000); // Wait up to 5 seconds for graceful exit
                    }
                    else
                    {
                        logger?.LogInformation($"PipeClientHelper: Launched Revit process {processId} already exited");
                    }
                }
                catch (ArgumentException)
                {
                    // Process not found, already cleaned up
                    logger?.LogInformation($"PipeClientHelper: Launched Revit process {processId} not found (already cleaned up)");
                }
                catch (Exception ex)
                {
                    logger?.LogError($"PipeClientHelper: Failed to kill launched Revit process {processId}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Result type for connection that includes both the client stream and the connected process ID
    /// </summary>
    public record RevitConnectionResult(NamedPipeClientStream Client, int ProcessId);

    /// <summary>
    /// Gets the default Revit executable path for a given version
    /// </summary>
    /// <param name="revitVersion">The Revit version (e.g., "2025")</param>
    /// <returns>The path to Revit.exe</returns>
    private static string GetRevitExecutablePath(string revitVersion)
    {
        return $@"C:\Program Files\Autodesk\Revit {revitVersion}\Revit.exe";
    }

    /// <summary>
    /// Launches a hidden Revit instance and returns the process
    /// </summary>
    /// <param name="revitVersion">The Revit version to launch</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
    /// <returns>The launched Revit process</returns>
    private static Process LaunchHiddenRevit(string revitVersion, ILogger? logger)
    {
        return LaunchHiddenRevit(revitVersion, logger, null);
    }

    /// <summary>
    /// Launches a hidden Revit instance and returns the process
    /// </summary>
    /// <param name="revitVersion">The Revit version to launch</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
    /// <param name="customExecutablePath">Optional custom path to Revit.exe. If null, uses default path.</param>
    /// <returns>The launched Revit process</returns>
    private static Process LaunchHiddenRevit(string revitVersion, ILogger? logger, string? customExecutablePath)
    {
        var revitExePath = customExecutablePath ?? GetRevitExecutablePath(revitVersion);

        if (!File.Exists(revitExePath))
        {
            throw new FileNotFoundException($"Revit executable not found at: {revitExePath}");
        }

        logger?.LogInformation($"PipeClientHelper: Launching hidden Revit {revitVersion} from: {revitExePath}");

        var psi = new ProcessStartInfo
        {
            FileName = revitExePath,
            Arguments = "/nosplash",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var revitProc = Process.Start(psi);
        if (revitProc == null)
        {
            throw new InvalidOperationException($"Failed to start Revit process from: {revitExePath}");
        }

        logger?.LogInformation($"PipeClientHelper: Revit process started with ID: {revitProc.Id}");

        // Track this process for cleanup
        TrackLaunchedRevitProcess(revitProc.Id, logger);

        // Wait for main window to initialize
        logger?.LogInformation("PipeClientHelper: Waiting for Revit main window to initialize...");
        int waitTimeMs = 0;
        const int maxWaitTimeMs = 30000; // 30 seconds
        const int pollIntervalMs = 500;

        while (revitProc.MainWindowHandle == IntPtr.Zero && waitTimeMs < maxWaitTimeMs)
        {
            if (revitProc.HasExited)
            {
                UntrackRevitProcess(revitProc.Id);
                throw new InvalidOperationException("Revit process exited unexpectedly during startup");
            }

            Thread.Sleep(pollIntervalMs);
            waitTimeMs += pollIntervalMs;
            revitProc.Refresh();
        }

        if (revitProc.MainWindowHandle != IntPtr.Zero)
        {
            logger?.LogInformation($"PipeClientHelper: Revit window handle found: {revitProc.MainWindowHandle}");
            logger?.LogInformation("PipeClientHelper: Minimizing Revit window to reduce visual disruption");
            ShowWindow(revitProc.MainWindowHandle, SW_MINIMIZE);
        }
        else
        {
            logger?.LogError($"PipeClientHelper: Revit window handle not found after {maxWaitTimeMs}ms. Process may still be initializing.");
        }

        return revitProc;
    }

    /// <summary>
    /// Waits for the Revit process to initialize its test infrastructure and become available for pipe connections
    /// </summary>
    /// <param name="revitProcess">The Revit process to wait for</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
    /// <param name="maxWaitTimeMs">Maximum time to wait in milliseconds</param>
    /// <returns>True if the pipe becomes available, false if timeout occurs</returns>
    private static bool WaitForRevitPipeAvailability(Process revitProcess, ILogger? logger, int maxWaitTimeMs = 60000)
    {
        logger?.LogInformation("PipeClientHelper: Waiting for Revit test infrastructure to initialize...");

        int waitTimeMs = 0;
        const int pollIntervalMs = 1000;

        while (waitTimeMs < maxWaitTimeMs)
        {
            if (revitProcess.HasExited)
            {
                logger?.LogError("PipeClientHelper: Revit process exited while waiting for pipe availability");
                return false;
            }

            try
            {
                // Try to construct the pipe name for this process
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var assemblyVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2025.0.0";
                var pipeName = PipeNaming.GetPipeName(assemblyVersion, revitProcess.Id);

                logger?.LogInformation($"PipeClientHelper: Checking for pipe availability: '{pipeName}'");

                using var testClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                testClient.Connect(100); // Quick connection test

                logger?.LogInformation("PipeClientHelper: Revit test infrastructure is ready!");
                return true;
            }
            catch
            {
                // Pipe not ready yet, continue waiting
            }

            Thread.Sleep(pollIntervalMs);
            waitTimeMs += pollIntervalMs;

            if (waitTimeMs % 5000 == 0) // Log progress every 5 seconds
            {
                logger?.LogInformation($"PipeClientHelper: Still waiting for Revit test infrastructure... ({waitTimeMs / 1000}s elapsed)");
            }
        }

        logger?.LogError($"PipeClientHelper: Timeout waiting for Revit test infrastructure after {maxWaitTimeMs / 1000}s");
        return false;
    }

    /// <summary>
    /// Connects to a Revit process using the new pipe naming format (assembly version + process ID based)
    /// </summary>
    /// <param name="revitVersion">The Revit version to connect to (used for process selection)</param>
    /// <returns>Connected NamedPipeClientStream</returns>
    public static NamedPipeClientStream ConnectToRevit(string revitVersion)
    {
        return ConnectToRevit(revitVersion, null);
    }

    /// <summary>
    /// Connects to a Revit process using the new pipe naming format (assembly version + process ID based)
    /// If no running Revit process is found, launches a new hidden instance.
    /// </summary>
    /// <param name="revitVersion">The Revit version to connect to (used for process selection)</param>
    /// <param name="logger">Optional logger for sending informational messages to test console</param>
    /// <returns>Connected NamedPipeClientStream</returns>
    public static NamedPipeClientStream ConnectToRevit(string revitVersion, ILogger? logger)
    {
        var result = ConnectToRevitWithProcessId(revitVersion, logger);
        return result.Client;
    }

    /// <summary>
    /// Connects to a Revit process and returns both the connection and the process ID
    /// If no running Revit process is found, launches a new hidden instance.
    /// </summary>
    /// <param name="revitVersion">The Revit version to connect to (used for process selection)</param>
    /// <param name="logger">Optional logger for sending informational messages to test console</param>
    /// <returns>Connection result with client stream and process ID</returns>
    public static RevitConnectionResult ConnectToRevitWithProcessId(string revitVersion, ILogger? logger)
    {
        var exceptions = new List<Exception>();
        var revitProcesses = Process.GetProcessesByName("Revit");

        logger?.LogInformation($"PipeClientHelper: Found {revitProcesses.Length} running Revit process(es)");

        // Try to connect to each existing Revit process using the new naming format
        foreach (var proc in revitProcesses)
        {
            logger?.LogInformation($"PipeClientHelper: Attempting to connect to Revit process ID {proc.Id}");

            try
            {
                // Try with the current assembly version and the specific process ID
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var assemblyVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2025.0.0";
                var pipeName = PipeNaming.GetPipeName(assemblyVersion, proc.Id);

                logger?.LogInformation($"PipeClientHelper: Trying to connect to pipe: '{pipeName}'");

                var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                try
                {
                    client.Connect(100);
                    logger?.LogInformation($"PipeClientHelper: Successfully connected to Revit process {proc.Id} via pipe '{pipeName}'");
                    return new RevitConnectionResult(client, proc.Id);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to connect to pipe '{pipeName}' for process {proc.Id}";
                    logger?.LogInformation($"PipeClientHelper: {errorMsg}: {ex.Message}");
                    exceptions.Add(new Exception(errorMsg, ex));
                    client.Dispose();
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error creating pipe name for process {proc.Id}";
                logger?.LogInformation($"PipeClientHelper: {errorMsg}: {ex.Message}");
                exceptions.Add(new Exception(errorMsg, ex));
            }
        }

        // No existing process found or connection failed - launch a new hidden Revit instance
        logger?.LogInformation($"PipeClientHelper: No suitable Revit process found. Launching new hidden Revit {revitVersion} instance...");

        try
        {
            var newRevitProcess = LaunchHiddenRevit(revitVersion, logger);

            // Wait for the test infrastructure to be ready
            if (!WaitForRevitPipeAvailability(newRevitProcess, logger))
            {
                newRevitProcess.Kill();
                UntrackRevitProcess(newRevitProcess.Id);
                throw new InvalidOperationException("Revit test infrastructure failed to initialize within the timeout period");
            }

            // Now try to connect to the new process
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var assemblyVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2025.0.0";
            var pipeName = PipeNaming.GetPipeName(assemblyVersion, newRevitProcess.Id);

            logger?.LogInformation($"PipeClientHelper: Connecting to newly launched Revit via pipe: '{pipeName}'");

            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            client.Connect(5000); // Give it more time for the new process

            logger?.LogInformation($"PipeClientHelper: Successfully connected to newly launched Revit process {newRevitProcess.Id}");
            return new RevitConnectionResult(client, newRevitProcess.Id);
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Failed to launch and connect to new Revit instance: {ex.Message}");
            exceptions.Add(ex);

            // If we reach here, everything failed
            var aggregateException = new AggregateException("Failed to connect to any Revit process and failed to launch new instance", exceptions);
            throw new InvalidOperationException($"No Revit process with test pipe found for version {revitVersion} and failed to launch new instance. Tried {exceptions.Count} connections.", aggregateException);
        }
    }

    /// <summary>
    /// Sends a command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <returns>The response from the server</returns>
    public static string SendCommand(object command, string revitVersion)
    {
        return SendCommand(command, revitVersion, null);
    }

    /// <summary>
    /// Sends a command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="logger">Optional logger for sending informational messages to test console</param>
    /// <returns>The response from the server</returns>
    public static string SendCommand(object command, string revitVersion, ILogger? logger)
    {
        var connectionResult = ConnectToRevitWithProcessId(revitVersion, logger);
        using var client = connectionResult.Client;
        var json = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(client, leaveOpen: true);
        sw.WriteLine(json);
        sw.Flush();
        using var sr = new StreamReader(client);
        var result = sr.ReadLine() ?? string.Empty;
        return result;
    }

    /// <summary>
    /// Sends a command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="frameworkHandle">Framework handle for sending informational messages to test console</param>
    /// <returns>The response from the server</returns>
    public static string SendCommand(object command, string revitVersion, object frameworkHandle)
    {
        try
        {
            return SendCommand(command, revitVersion, frameworkHandle.ToLogger());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PipeClientHelper: Failed to create logger from framework handle: {ex.Message}");
            return SendCommand(command, revitVersion, null);
        }
    }

    /// <summary>
    /// Sends a streaming command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="logger">Optional logger for sending informational messages to test console</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, CancellationToken cancellationToken, string revitVersion, ILogger? logger)
    {
        using var cancelServer = new NamedPipeServerStream(command.CancelPipe, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connectionResult = ConnectToRevitWithProcessId(revitVersion, logger);
        using var client = connectionResult.Client;

        // Track if we attached the debugger so we can detach it later
        bool debuggerAttached = false;

        // If debug mode is enabled and debugger is attached, attempt to attach debugger to the specific Revit process
        if (command.Debug && Debugger.IsAttached)
        {
            TryAttachDebuggerToRevit(connectionResult.ProcessId, logger);
            debuggerAttached = true;
        }

        var json = JsonSerializer.Serialize(command);

        // Create StreamWriter in a try-catch to handle disposal issues
        StreamWriter? sw = null;
        try
        {
            sw = new StreamWriter(client, leaveOpen: true);
            sw.WriteLine(json);
            sw.Flush();
        }
        catch (ObjectDisposedException)
        {
            // Pipe was already closed, ignore disposal issues
        }
        finally
        {
            try
            {
                sw?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Ignore disposal exceptions when pipe is already closed
            }
        }

        _ = Task.Run(async () =>
        {
            await cancelServer.WaitForConnectionAsync().ConfigureAwait(false);
            await Task.Run(() =>
            {
                cancellationToken.WaitHandle.WaitOne();
                if (cancelServer.IsConnected)
                {
                    using var cw = new StreamWriter(cancelServer);
                    cw.WriteLine("CANCEL");
                    cw.Flush();
                }
            });
        });

        try
        {
            using var sr = new StreamReader(client);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                handleLine(line);
                if (line == "END")
                {
                    logger?.LogInformation("PipeClientHelper: Test execution completed - END signal received");
                    break;
                }
            }
        }
        finally
        {
            // Clean up launched Revit processes after test execution is complete
            logger?.LogInformation("PipeClientHelper: Test execution finished - cleaning up launched Revit processes");
            CleanupLaunchedRevitProcesses(logger);

            // Detach debugger after test execution is completed
            // This is now asynchronous and won't block the test host from exiting
            // Can be disabled via environment variable if it causes issues
            if (debuggerAttached)
            {
                var disableAutoDetach = Environment.GetEnvironmentVariable("REVIT_DEBUG_DISABLE_AUTO_DETACH");
                if (string.IsNullOrEmpty(disableAutoDetach) || disableAutoDetach.ToLower() != "true")
                {
                    logger?.LogInformation("PipeClientHelper: Test execution finished - initiating debugger detachment");
                    TryDetachDebuggerUsingHelper(connectionResult.ProcessId, logger);
                }
                else
                {
                    logger?.LogInformation("PipeClientHelper: Automatic debugger detachment disabled via REVIT_DEBUG_DISABLE_AUTO_DETACH environment variable");
                }
            }
        }
    }

    /// <summary>
    /// Attempts to attach the current debugger to a specific Revit process
    /// </summary>
    /// <param name="processId">The specific Revit process ID to attach to</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
    private static void TryAttachDebuggerToRevit(int processId, ILogger? logger)
    {
        try
        {
            logger?.LogInformation($"PipeClientHelper: Attempting to attach debugger to specific Revit process {processId}...");

            var revitProcess = Process.GetProcessById(processId);
            if (revitProcess == null || revitProcess.HasExited)
            {
                logger?.LogInformation($"PipeClientHelper: Revit process {processId} not found or has exited");
                return;
            }

            logger?.LogInformation($"PipeClientHelper: Found Revit process {processId} ({revitProcess.ProcessName})");

            logger?.LogInformation("PipeClientHelper: Attempting to attach debugger to Revit");
            // Try using the .NET Framework helper application
            TryAttachDebuggerUsingHelper(processId, logger);
        }
        catch (ArgumentException)
        {
            logger?.LogError($"PipeClientHelper: Revit process {processId} not found");
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Failed to attach debugger to Revit process {processId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper result for debugger helper operations
    /// </summary>
    private record DebuggerHelperResult(bool Success, string? Output, string? Error, int ExitCode);

    /// <summary>
    /// Finds the RevitDebuggerHelper.exe in common locations
    /// </summary>
    /// <param name="logger">Optional logger</param>
    /// <returns>Path to helper executable or null if not found</returns>
    private static string? FindDebuggerHelper(ILogger? logger)
    {
        var configuration = AppDomain.CurrentDomain.BaseDirectory.Contains("Debug", StringComparison.OrdinalIgnoreCase)
            ? "Debug"
            : "Release";

        // Look for the helper executable in common locations
        var helperPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RevitDebuggerHelper.exe"),
            // Navigate from test assembly location to workspace root, then to helper
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "RevitDebuggerHelper", "bin", configuration, "net48", "RevitDebuggerHelper.exe"),
            "RevitDebuggerHelper.exe", // Try PATH
        };

        foreach (var path in helperPaths)
        {
            if (File.Exists(path))
            {
                logger?.LogInformation($"PipeClientHelper: Found helper at: {path}");
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes the debugger helper with the specified arguments
    /// </summary>
    /// <param name="helperPath">Path to the helper executable</param>
    /// <param name="arguments">Arguments to pass to the helper</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Result of the helper execution</returns>
    private static DebuggerHelperResult ExecuteDebuggerHelper(string helperPath, string arguments, int timeoutMs, ILogger? logger)
    {
        var psi = new ProcessStartInfo
        {
            FileName = helperPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return new DebuggerHelperResult(false, null, "Failed to start debugger helper process", -1);
        }

        bool completed = process.WaitForExit(timeoutMs);

        if (!completed)
        {
            try
            {
                process.Kill();
            }
            catch (Exception killEx)
            {
                logger?.LogInformation($"PipeClientHelper: Failed to kill timed-out helper: {killEx.Message}");
            }
            return new DebuggerHelperResult(false, null, $"Helper timed out after {timeoutMs}ms", -1);
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        return new DebuggerHelperResult(process.ExitCode == 0, output, error, process.ExitCode);
    }

    /// <summary>
    /// Attempts to attach debugger using the .NET Framework helper application
    /// </summary>
    /// <param name="processId">The process ID to attach to</param>
    /// <param name="logger">Optional logger</param>
    private static void TryAttachDebuggerUsingHelper(int processId, ILogger? logger)
    {
        try
        {
            logger?.LogInformation($"PipeClientHelper: Attempting to attach debugger using helper application to process {processId}");

            var helperPath = FindDebuggerHelper(logger);
            if (string.IsNullOrEmpty(helperPath))
            {
                logger?.LogInformation("PipeClientHelper: RevitDebuggerHelper.exe not found in expected locations");
                logger?.LogInformation($"PipeClientHelper: To debug Revit tests, manually attach debugger to Revit.exe process ID {processId}");
                return;
            }

            // Try to find the Visual Studio process that initiated the test run
            var vsProcessId = FindVisualStudioProcessForTestRun(logger);
            var arguments = vsProcessId.HasValue 
                ? $"{processId} --vs-process {vsProcessId.Value}"
                : processId.ToString();
            
            var result = ExecuteDebuggerHelper(helperPath, arguments, 10000, logger);

            if (result.Success)
            {
                logger?.LogInformation($"PipeClientHelper: Successfully attached debugger to process {processId}");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    logger?.LogInformation($"PipeClientHelper: Helper output: {result.Output.Trim()}");
                }
            }
            else
            {
                logger?.LogError($"PipeClientHelper: Helper failed with exit code {result.ExitCode}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    logger?.LogError($"PipeClientHelper: Helper error: {result.Error.Trim()}");
                }
                if (!string.IsNullOrEmpty(result.Output))
                {
                    logger?.LogInformation($"PipeClientHelper: Helper output: {result.Output.Trim()}");
                }

                // Fallback to manual attachment message
                logger?.LogInformation($"PipeClientHelper: To debug Revit tests, manually attach debugger to Revit.exe process ID {processId}");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Error using debugger helper: {ex.Message}");
            logger?.LogInformation($"PipeClientHelper: To debug Revit tests, manually attach debugger to Revit.exe process ID {processId}");
        }
    }

    /// <summary>
    /// Attempts to detach debugger using the .NET Framework helper application
    /// </summary>
    /// <param name="processId">The process ID to detach from</param>
    /// <param name="logger">Optional logger</param>
    private static void TryDetachDebuggerUsingHelper(int processId, ILogger? logger)
    {
        try
        {
            logger?.LogInformation($"PipeClientHelper: Attempting to detach debugger from process {processId} using helper application");

            var helperPath = FindDebuggerHelper(logger);
            if (string.IsNullOrEmpty(helperPath))
            {
                logger?.LogInformation("PipeClientHelper: RevitDebuggerHelper.exe not found - skipping debugger detachment");
                return;
            }

            // Start the detachment process asynchronously to avoid blocking test host exit
            Task.Run(() =>
            {
                try
                {
                    // Try to find the Visual Studio process that initiated the test run
                    var vsProcessId = FindVisualStudioProcessForTestRun(logger);
                    var arguments = vsProcessId.HasValue 
                        ? $"--detach {processId} --vs-process {vsProcessId.Value}"
                        : $"--detach {processId}";
                    
                    var result = ExecuteDebuggerHelper(helperPath, arguments, 5000, logger);

                    if (result.Success)
                    {
                        logger?.LogInformation($"PipeClientHelper: Successfully detached debugger from process {processId}");
                        if (!string.IsNullOrEmpty(result.Output))
                        {
                            logger?.LogInformation($"PipeClientHelper: Detach helper output: {result.Output.Trim()}");
                        }
                    }
                    else
                    {
                        if (result.ExitCode == -1)
                        {
                            logger?.LogInformation($"PipeClientHelper: Detach helper timed out - continuing");
                        }
                        else
                        {
                            logger?.LogInformation($"PipeClientHelper: Detach helper completed with exit code {result.ExitCode}");
                            if (!string.IsNullOrEmpty(result.Error))
                            {
                                logger?.LogInformation($"PipeClientHelper: Detach helper error: {result.Error.Trim()}");
                            }
                            if (!string.IsNullOrEmpty(result.Output))
                            {
                                logger?.LogInformation($"PipeClientHelper: Detach helper output: {result.Output.Trim()}");
                            }

                            // Exit codes for detachment (not necessarily errors)
                            switch (result.ExitCode)
                            {
                                case 1:
                                    logger?.LogInformation("PipeClientHelper: Visual Studio not found - debugger detachment not needed");
                                    break;
                                case 2:
                                    logger?.LogInformation("PipeClientHelper: Process not being debugged - detachment not needed");
                                    break;
                            }
                        }
                    }
                }
                catch (Exception asyncEx)
                {
                    logger?.LogInformation($"PipeClientHelper: Error in async debugger detachment: {asyncEx.Message}");
                }
            });

            // Don't wait for the async task to complete - let test host exit normally
            logger?.LogInformation($"PipeClientHelper: Debugger detachment initiated asynchronously for process {processId}");
        }
        catch (Exception ex)
        {
            logger?.LogInformation($"PipeClientHelper: Error starting debugger helper for detachment: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to find the Visual Studio process that initiated the current test run
    /// by walking up the process tree and looking for devenv.exe
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information</param>
    /// <returns>Visual Studio process ID if found, null otherwise</returns>
    private static int? FindVisualStudioProcessForTestRun(ILogger? logger)
    {
        try
        {
            logger?.LogInformation("PipeClientHelper: Attempting to find Visual Studio process that initiated test run");
            
            var currentProcess = Process.GetCurrentProcess();
            var currentProcessId = currentProcess.Id;
            
            logger?.LogInformation($"PipeClientHelper: Starting from current process: {currentProcess.ProcessName} (ID: {currentProcessId})");
            
            // Walk up the process tree to find devenv.exe
            var checkedProcesses = new HashSet<int>();
            var processToCheck = currentProcess;
            
            while (processToCheck != null && !checkedProcesses.Contains(processToCheck.Id))
            {
                checkedProcesses.Add(processToCheck.Id);
                
                try
                {
                    var processName = processToCheck.ProcessName;
                    logger?.LogInformation($"PipeClientHelper: Checking process: {processName} (ID: {processToCheck.Id})");
                    
                    // Check if this is a Visual Studio process
                    if (processName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.LogInformation($"PipeClientHelper: Found Visual Studio process: {processName} (ID: {processToCheck.Id})");
                        return processToCheck.Id;
                    }
                    
                    // Get parent process
                    var parentPid = GetParentProcessId(processToCheck.Id);
                    if (parentPid == 0 || parentPid == processToCheck.Id)
                    {
                        logger?.LogInformation($"PipeClientHelper: Reached top of process tree or circular reference");
                        break;
                    }
                    
                    processToCheck = Process.GetProcessById(parentPid);
                }
                catch (ArgumentException)
                {
                    logger?.LogInformation($"PipeClientHelper: Parent process not found or exited");
                    break;
                }
                catch (Exception ex)
                {
                    logger?.LogInformation($"PipeClientHelper: Error accessing process: {ex.Message}");
                    break;
                }
            }
            
            // If we didn't find devenv.exe in the process tree, look for any running devenv.exe processes
            logger?.LogInformation("PipeClientHelper: Visual Studio not found in process tree, checking for any running devenv.exe processes");
            
            var devenvProcesses = Process.GetProcessesByName("devenv");
            if (devenvProcesses.Length > 0)
            {
                var devenvProcess = devenvProcesses[0];
                logger?.LogInformation($"PipeClientHelper: Found running Visual Studio process: devenv.exe (ID: {devenvProcess.Id})");
                return devenvProcess.Id;
            }
            
            logger?.LogInformation("PipeClientHelper: No Visual Studio process found");
            return null;
        }
        catch (Exception ex)
        {
            logger?.LogInformation($"PipeClientHelper: Error finding Visual Studio process: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the parent process ID for a given process ID using WMI
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>Parent process ID, or 0 if not found</returns>
    private static int GetParentProcessId(int processId)
    {
        try
        {
            // Use WMI to get parent process ID
            using (var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {processId}"))
            {
                using (var results = searcher.Get())
                {
                    foreach (System.Management.ManagementObject result in results)
                    {
                        var parentPid = result["ParentProcessId"];
                        if (parentPid != null && uint.TryParse(parentPid.ToString(), out uint pid))
                        {
                            return (int)pid;
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // WMI not available or other error
        }
        
        return 0;
    }
}

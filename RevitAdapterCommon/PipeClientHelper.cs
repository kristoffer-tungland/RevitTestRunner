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

        // Wait for main window to initialize
        logger?.LogInformation("PipeClientHelper: Waiting for Revit main window to initialize...");
        int waitTimeMs = 0;
        const int maxWaitTimeMs = 30000; // 30 seconds
        const int pollIntervalMs = 500;

        while (revitProc.MainWindowHandle == IntPtr.Zero && waitTimeMs < maxWaitTimeMs)
        {
            if (revitProc.HasExited)
            {
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
                    return client;
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
            return client;
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
        using var client = ConnectToRevit(revitVersion, logger);
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
        using var client = ConnectToRevit(revitVersion, logger);
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

        using var sr = new StreamReader(client);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            handleLine(line);
            if (line == "END")
                break;
        }
    }

    /// <summary>
    /// Sends a streaming command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="frameworkHandle">Framework handle for sending informational messages to test console</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, CancellationToken cancellationToken, string revitVersion, object frameworkHandle)
    {
        try
        {
            SendCommandStreaming(command, handleLine, cancellationToken, revitVersion, frameworkHandle.ToLogger());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PipeClientHelper: Failed to create logger from framework handle: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a streaming command using the new connection method without cancellation
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, string revitVersion)
        => SendCommandStreaming(command, handleLine, CancellationToken.None, revitVersion, null);

    /// <summary>
    /// Sends a streaming command using the new connection method without cancellation
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="logger">Optional logger for sending informational messages to test console</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, string revitVersion, ILogger? logger)
        => SendCommandStreaming(command, handleLine, CancellationToken.None, revitVersion, logger);

    /// <summary>
    /// Sends a streaming command using the new connection method without cancellation
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="frameworkHandle">Framework handle for sending informational messages to test console</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, string revitVersion, object frameworkHandle)
    {
        try
        {
            SendCommandStreaming(command, handleLine, CancellationToken.None, revitVersion, frameworkHandle.ToLogger());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PipeClientHelper: Failed to create logger from framework handle: {ex.Message}");
            SendCommandStreaming(command, handleLine, CancellationToken.None, revitVersion, null);
        }
    }
}

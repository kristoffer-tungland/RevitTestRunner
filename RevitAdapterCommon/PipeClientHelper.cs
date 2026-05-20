using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
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

    /// <summary>
    /// Tracks processes that need debugger detachment during shutdown
    /// </summary>
    private static readonly HashSet<int> _processesNeedingDetachment = new();
    private static readonly object _detachmentTrackingLock = new();

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
        // First, detach debugger from any processes that need it
        DetachDebuggersOnShutdown();

        // Then clean up launched Revit processes
        CleanupLaunchedRevitProcesses(null);
    }

    /// <summary>
    /// Cleanup handler for when the current AppDomain is unloading
    /// </summary>
    private static void OnDomainUnload(object? sender, EventArgs e)
    {
        // First, detach debugger from any processes that need it
        DetachDebuggersOnShutdown();

        // Then clean up launched Revit processes
        CleanupLaunchedRevitProcesses(null);
    }

    /// <summary>
    /// Tracks a process that needs debugger detachment during shutdown
    /// </summary>
    /// <param name="processId">The process ID that needs detachment</param>
    private static void TrackProcessForDetachment(int processId)
    {
        lock (_detachmentTrackingLock)
        {
            _processesNeedingDetachment.Add(processId);
        }
    }

    /// <summary>
    /// Stops tracking a process for detachment (called when detachment is manually performed)
    /// </summary>
    /// <param name="processId">The process ID to stop tracking</param>
    private static void UntrackProcessForDetachment(int processId)
    {
        lock (_detachmentTrackingLock)
        {
            _processesNeedingDetachment.Remove(processId);
        }
    }

    /// <summary>
    /// Detaches debugger from all tracked processes during shutdown
    /// </summary>
    private static void DetachDebuggersOnShutdown()
    {
        List<int> processesToDetach;
        lock (_detachmentTrackingLock)
        {
            processesToDetach = new List<int>(_processesNeedingDetachment);
            _processesNeedingDetachment.Clear();
        }

        if (processesToDetach.Count > 0)
        {
            // Use synchronous detachment during shutdown for better reliability
            foreach (var processId in processesToDetach)
            {
                try
                {
                    // Perform synchronous detachment with shorter timeout during shutdown
                    PerformSynchronousDetachment(processId, 2000); // 2 second timeout during shutdown
                }
                catch (Exception ex)
                {
                    // Log to debug during shutdown (can't use logger here)
                    Debug.WriteLine($"PipeClientHelper: Error detaching from process {processId} during shutdown: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Performs synchronous debugger detachment (used during shutdown)
    /// </summary>
    /// <param name="processId">The process ID to detach from</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    private static void PerformSynchronousDetachment(int processId, int timeoutMs)
    {
        var helperPath = FindDebuggerHelper(null);
        if (string.IsNullOrEmpty(helperPath))
        {
            return;
        }

        try
        {
            // Don't try to find VS process during shutdown - too risky
            var arguments = $"--detach {processId}";

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
            if (process != null)
            {
                // Wait for completion with timeout
                bool completed = process.WaitForExit(timeoutMs);
                if (!completed)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Ignore kill errors during shutdown
                    }
                }
            }
        }
        catch
        {
            // Ignore all errors during shutdown
        }
    }

    /// <summary>
    /// Tracks a launched Revit process for cleanup
    /// </summary>
    /// <param name="processId">The process ID to track</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
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
    /// Gets the Revit executable path for a given version, first consulting the Windows registry
    /// and falling back to the conventional default install location.
    /// Registry layout: HKLM\SOFTWARE\Autodesk\Revit\{year}\REVIT-XX:LLLL → InstallationLocation
    /// </summary>
    /// <param name="revitVersion">The Revit version (e.g., "2025")</param>
    /// <returns>The path to Revit.exe</returns>
    private static string GetRevitExecutablePath(string revitVersion)
    {
        // Try registry: HKLM\SOFTWARE\Autodesk\Revit\{version}
        // Subkeys named REVIT-XX:LLLL (product + language code) carry InstallationLocation.
        string[] registryRoots =
        [
            $@"SOFTWARE\Autodesk\Revit\{revitVersion}",
            $@"SOFTWARE\WOW6432Node\Autodesk\Revit\{revitVersion}"
        ];

        foreach (var registryPath in registryRoots)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    // Match subkeys like "REVIT-05:0409" (product code : language code)
                    if (!System.Text.RegularExpressions.Regex.IsMatch(subKeyName, @"^REVIT-\w+:\w+$"))
                        continue;

                    using var subKey = key.OpenSubKey(subKeyName);
                    var installLocation = subKey?.GetValue("InstallationLocation") as string;
                    if (string.IsNullOrEmpty(installLocation)) continue;

                    var candidate = Path.Combine(installLocation, "Revit.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception)
            {
                // Registry access failure — try next root or fall back to default
            }
        }

        // Fall back to the conventional default location
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
        const int maxWaitTimeMs = 300000; // 5 minutes
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
    /// <param name="normalizedAddinVersion">Normalized version string derived from the addin DLL filename</param>
    /// <param name="logger">Optional logger for sending informational messages</param>
    /// <param name="maxWaitTimeMs">Maximum time to wait in milliseconds</param>
    /// <returns>True if the pipe becomes available, false if timeout occurs</returns>
    private static bool WaitForRevitPipeAvailability(Process revitProcess, string normalizedAddinVersion, ILogger? logger, int maxWaitTimeMs = 60000)
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
                // Use the addin's normalized version (from its filename) for the pipe name
                var pipeName = PipeNaming.GetPipeName(normalizedAddinVersion, revitProcess.Id);

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
    /// Ensures the Revit addin is installed before attempting to connect
    /// </summary>
    /// <param name="revitVersion">The Revit version to install for</param>
    /// <param name="normalizedAddinVersion">Normalized version derived from the addin DLL filename</param>
    /// <param name="addinAssemblyPath">Path to the RevitAddin.Xunit DLL (may be null)</param>
    /// <param name="commonTool">Path to RevitTestFramework.Common.exe (may be null)</param>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>True if addin is installed or was successfully installed, false otherwise</returns>
    private static bool EnsureRevitAddinInstalled(string revitVersion, string normalizedAddinVersion, string? addinAssemblyPath, string? commonTool, ILogger? logger)
    {
        try
        {
            logger?.LogInformation($"PipeClientHelper: Checking if Revit addin is installed for version {revitVersion}");

            // Construct the manifest file path
            var addinDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                      "Autodesk", "Revit", "Addins", revitVersion);
            var manifestFile = Path.Combine(addinDir, $"RevitAddin.Xunit.{normalizedAddinVersion}.addin");

            if (File.Exists(manifestFile))
            {
                logger?.LogInformation($"PipeClientHelper: Revit addin manifest found: {manifestFile}");
                return true;
            }

            logger?.LogInformation($"PipeClientHelper: Revit addin manifest not found at: {manifestFile}");
            logger?.LogInformation("PipeClientHelper: Attempting to install addin automatically...");

            if (string.IsNullOrEmpty(commonTool))
            {
                logger?.LogError("PipeClientHelper: Could not find RevitTestFramework.Common.exe tool for automatic addin installation");
                return false;
            }

            // Run the tool to generate the addin manifest
            var arguments = string.IsNullOrEmpty(addinAssemblyPath)
                ? $"generate-manifest --output \"{addinDir}\""
                : $"generate-manifest --output \"{addinDir}\" --assembly \"{addinAssemblyPath}\"";
            logger?.LogInformation($"PipeClientHelper: Running addin installation: {commonTool} {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = commonTool,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    logger?.LogInformation("PipeClientHelper: Addin installation completed successfully");
                    if (!string.IsNullOrEmpty(output))
                    {
                        logger?.LogInformation($"PipeClientHelper: Installation output: {output}");
                    }
                    return true;
                }
                else
                {
                    logger?.LogError($"PipeClientHelper: Addin installation failed with exit code {process.ExitCode}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        logger?.LogError($"PipeClientHelper: Installation error: {error}");
                    }
                    return false;
                }
            }
            else
            {
                logger?.LogError("PipeClientHelper: Failed to start addin installation process");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Error during addin installation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds the RevitTestFramework.Common.exe tool for automatic addin installation
    /// </summary>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>Path to the tool, or null if not found</returns>
    private static string? FindRevitTestFrameworkCommonTool(ILogger? logger)
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? throw new InvalidOperationException("Could not determine assembly location");
        // Use the raw informational version (lowercased, build-metadata stripped) for the NuGet cache
        // path. The NuGet cache folder uses the original package version string without the +<sha>
        // build metadata suffix (e.g. "2027.1.1-pullrequest0020.12").
        var rawInformationalVersion = PipeNaming.GetAssemblyInformationalVersion(assembly)
            .ToLowerInvariant()
            .Split('+')[0];
        var nugetCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                          ".nuget", "packages", "revitxunit.testadapter", rawInformationalVersion, "content", "RevitAddin");

        // Try to find RevitTestFramework.Common.exe in various locations
        var searchLocations = new[]
        {
            // Same directory as current assembly — works when RevitTestFramework.Common.exe is
            // included in build/{tfm}/ of the NuGet package and copied to output via props.
            assemblyDirectory,
#if DEBUG
            // Relative path for project-reference (non-NuGet) builds in debug mode
            Path.Combine(assemblyDirectory, "..", "..", "..", "..", "RevitAddin.Xunit", "bin", "Debug", "net8.0"),
#endif
            // NuGet package cache fallback
            nugetCachePath
        };

        foreach (var location in searchLocations.Where(l => !string.IsNullOrEmpty(l)))
        {
            try
            {
                if (Directory.Exists(location))
                {
                    var toolFiles = Directory.GetFiles(location, "RevitTestFramework.Common*.exe");
                    if (toolFiles.Length > 0)
                    {
                        logger?.LogInformation($"PipeClientHelper: Found RevitTestFramework.Common tool at: {toolFiles[0]}");
                        return toolFiles[0];
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"PipeClientHelper: Error searching for tool in {location}: {ex.Message}");
            }
        }

        logger?.LogError("PipeClientHelper: RevitTestFramework.Common.exe not found in any search location");
        return null;
    }

    /// <summary>
    /// Finds the RevitAddin.Xunit assembly for use with generate-manifest --assembly
    /// </summary>
    private static string? FindRevitAddinXunitAssembly(string revitVersion, string? commonToolPath, ILogger? logger)
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? "";

        var searchLocations = new List<string> { assemblyDirectory };

        // If we know where the common tool is AND it's in a NuGet cache build/{tfm} folder,
        // derive content/RevitAddin from the package version directory.
        // Tool path: .nuget/packages/revitxunit.testadapter/{version}/build/{tfm}/RevitTestFramework.Common.exe
        // Content path: .nuget/packages/revitxunit.testadapter/{version}/content/RevitAddin/
        if (!string.IsNullOrEmpty(commonToolPath))
        {
            var toolDir = Path.GetDirectoryName(commonToolPath); // .../build/{tfm} or output dir
            if (toolDir != null)
            {
                // Check two levels up (build/{tfm}) to see if it's a NuGet package structure
                var buildDir = Path.GetDirectoryName(toolDir);
                if (buildDir != null && Path.GetFileName(buildDir).Equals("build", StringComparison.OrdinalIgnoreCase))
                {
                    var packageVersionDir = Path.GetDirectoryName(buildDir);
                    if (packageVersionDir != null)
                    {
                        searchLocations.Add(Path.Combine(packageVersionDir, "content", "RevitAddin"));
                    }
                }
            }
        }

        // Also search NuGet package cache using the raw informational version
        var rawInformationalVersion = PipeNaming.GetAssemblyInformationalVersion(assembly)
            .ToLowerInvariant()
            .Split('+')[0];
        if (rawInformationalVersion.StartsWith(revitVersion + ".", StringComparison.OrdinalIgnoreCase))
        {
            var nugetContentPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "revitxunit.testadapter", rawInformationalVersion, "content", "RevitAddin");
            searchLocations.Add(nugetContentPath);
        }

        // Also try scanning all versions of the package in NuGet cache
        // Prefer versions matching the requested Revit year.
        var revitYearPrefix = revitVersion + ".";
        var nugetPackagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages", "revitxunit.testadapter");
        if (Directory.Exists(nugetPackagePath))
        {
            // Put matching Revit-year versions first
            var versionDirs = Directory.GetDirectories(nugetPackagePath)
                .OrderByDescending(d => Path.GetFileName(d).StartsWith(revitYearPrefix, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(d => d);
            foreach (var versionDir in versionDirs)
            {
                searchLocations.Add(Path.Combine(versionDir, "content", "RevitAddin"));
            }
        }

        foreach (var location in searchLocations.Where(l => !string.IsNullOrEmpty(l)))
        {
            try
            {
                if (!Directory.Exists(location)) continue;
                var matches = Directory.GetFiles(location, "RevitAddin.Xunit*.dll");
                if (matches.Length > 0)
                {
                    logger?.LogInformation($"PipeClientHelper: Found RevitAddin.Xunit assembly at: {matches[0]}");
                    return matches[0];
                }
            }
            catch { }
        }
        logger?.LogInformation("PipeClientHelper: RevitAddin.Xunit assembly not found; tool will auto-detect");
        return null;
    }

    /// <summary>
    /// Finds the RevitDebuggerHelper.exe for debugger operations
    /// </summary>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>Path to the debugger helper, or null if not found</returns>
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
    /// Connects to an existing Revit instance or launches a new one
    /// </summary>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>Connection result with client stream and process ID</returns>
    public static RevitConnectionResult ConnectOrLaunchRevit(string revitVersion, ILogger? logger)
    {
        logger?.LogInformation($"PipeClientHelper: Attempting to connect to Revit {revitVersion}");

        // Discover the addin DLL and derive the normalized version from its filename.
        // Both the client (here) and the server (IsolatedPipeServerHandler) use the filename
        // to get a Revit-year-prefixed version (e.g. 2027.1.1.2012), which ensures that
        // versions are globally unique across Revit years and supports PR builds.
        var commonTool = FindRevitTestFrameworkCommonTool(logger);
        var addinDllPath = FindRevitAddinXunitAssembly(revitVersion, commonTool, logger);
        var normalizedAddinVersion = GetNormalizedVersionFromAddinPath(addinDllPath, logger);
        logger?.LogInformation($"PipeClientHelper: Using addin version: {normalizedAddinVersion}");

        // Ensure the addin is installed
        if (!EnsureRevitAddinInstalled(revitVersion, normalizedAddinVersion, addinDllPath, commonTool, logger))
        {
            throw new InvalidOperationException($"Failed to ensure Revit addin is installed for version {revitVersion}");
        }

        // Try to connect to existing Revit instances first
        var existingConnection = ConnectToExistingRevitInstance(normalizedAddinVersion, logger);
        if (existingConnection != null)
        {
            return existingConnection;
        }

        // No existing instance found, launch a new one
        logger?.LogInformation("PipeClientHelper: No existing Revit instance found, launching new instance");
        var revitProcess = LaunchHiddenRevit(revitVersion, logger);

        // Wait for the test infrastructure to be available
        if (!WaitForRevitPipeAvailability(revitProcess, normalizedAddinVersion, logger))
        {
            // Clean up the launched process if pipe isn't available
            try
            {
                revitProcess.Kill();
                UntrackRevitProcess(revitProcess.Id);
            }
            catch (Exception ex)
            {
                logger?.LogError($"PipeClientHelper: Error cleaning up failed Revit process: {ex.Message}");
            }

            throw new InvalidOperationException("Revit test infrastructure failed to initialize");
        }

        // Connect to the newly launched instance
        var newConnection = ConnectToSpecificRevitProcess(revitProcess.Id, normalizedAddinVersion, logger);
        if (newConnection == null)
        {
            throw new InvalidOperationException($"Failed to connect to newly launched Revit process {revitProcess.Id}");
        }

        return newConnection;
    }

    /// <summary>
    /// Returns the normalized version string derived from the addin DLL's filename.
    /// Falls back to the Contracts assembly version when no addin DLL is available.
    /// </summary>
    private static string GetNormalizedVersionFromAddinPath(string? addinDllPath, ILogger? logger)
    {
        if (!string.IsNullOrEmpty(addinDllPath) && File.Exists(addinDllPath))
        {
            var rawVersion = PipeNaming.GetVersionFromAssemblyFile(addinDllPath);
            return VersionNormalizationUtils.NormalizeVersion(rawVersion);
        }
        logger?.LogInformation("PipeClientHelper: Addin DLL not found; falling back to Contracts assembly version");
        return GetCurrentAssemblyVersion();
    }

    /// <summary>
    /// Attempts to connect to an existing Revit instance using the given addin version.
    /// </summary>
    /// <param name="normalizedAddinVersion">Normalized version derived from the addin DLL filename</param>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>Connection result if successful, null if no suitable instance found</returns>
    private static RevitConnectionResult? ConnectToExistingRevitInstance(string normalizedAddinVersion, ILogger? logger)
    {
        try
        {
            var revitProcesses = Process.GetProcessesByName("Revit");
            logger?.LogInformation($"PipeClientHelper: Found {revitProcesses.Length} running Revit processes");

            foreach (var process in revitProcesses)
            {
                try
                {
                    var connection = ConnectToSpecificRevitProcess(process.Id, normalizedAddinVersion, logger);
                    if (connection != null)
                    {
                        logger?.LogInformation($"PipeClientHelper: Successfully connected to existing Revit process {process.Id}");
                        return connection;
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError($"PipeClientHelper: Failed to connect to Revit process {process.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Error finding existing Revit instances: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Attempts to connect to a specific Revit process using the given addin version.
    /// </summary>
    /// <param name="processId">The process ID to connect to</param>
    /// <param name="normalizedAddinVersion">Normalized version derived from the addin DLL filename</param>
    /// <param name="logger">Optional logger for informational messages</param>
    /// <returns>Connection result if successful, null if connection failed</returns>
    private static RevitConnectionResult? ConnectToSpecificRevitProcess(int processId, string normalizedAddinVersion, ILogger? logger)
    {
        try
        {
            var pipeName = PipeNaming.GetPipeName(normalizedAddinVersion, processId);

            logger?.LogInformation($"PipeClientHelper: Attempting to connect to pipe: {pipeName}");

            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);

            // Try to connect with a reasonable timeout
            client.Connect(5000); // 5 second timeout

            logger?.LogInformation($"PipeClientHelper: Successfully connected to Revit process {processId}");
            return new RevitConnectionResult(client, processId);
        }
        catch (Exception ex)
        {
            logger?.LogError($"PipeClientHelper: Failed to connect to Revit process {processId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends a command to Revit and streams the results back
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="onLineReceived">Callback for each line received</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="revitVersion">The Revit version to target</param>
    /// <param name="logger">Logger for informational messages</param>
    public static void SendCommandStreaming(
        object command,
        Action<string> onLineReceived,
        CancellationToken cancellationToken,
        string revitVersion,
        ILogger logger)
    {
        try
        {
            var connection = ConnectOrLaunchRevit(revitVersion, logger);

            using (connection.Client)
            {
                var writer = new StreamWriter(connection.Client) { AutoFlush = true };
                var reader = new StreamReader(connection.Client);

                // Send the command
                var commandJson = JsonSerializer.Serialize(command);
                logger.LogInformation($"PipeClientHelper: Sending command: {commandJson}");
                writer.WriteLine(commandJson);

                // Read responses until END or cancellation
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;

                    onLineReceived(line);

                    if (line == "END") break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"PipeClientHelper: Error in SendCommandStreaming: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the normalized assembly version for the current executing assembly
    /// Uses the informational version (includes pre-release info) with shared normalization utility for consistency
    /// </summary>
    /// <returns>Normalized assembly version string suitable for pipe names and manifest files</returns>
    private static string GetCurrentAssemblyVersion()
    {
        // Get the full informational version (includes pre-release info like "2025.1.1-pullrequest0020.10")
        var assemblyVersion = PipeNaming.GetCurrentAssemblyInformationalVersion();

        // Use the shared normalization utility to ensure consistency (always 4-part versions)
        return VersionNormalizationUtils.NormalizeVersion(assemblyVersion);
    }
}

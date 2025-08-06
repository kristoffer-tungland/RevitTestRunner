using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;
using System.Diagnostics;
using System.Reflection;

namespace RevitAddin.Xunit;

public class RevitXunitTestFrameworkApplication : IExternalApplication
{
    private PipeServer? _server;
    private RevitTask? _revitTask;
    private static readonly ILogger _logger = FileLogger.ForContext<RevitXunitTestFrameworkApplication>();

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            _logger.LogInformation("Startup beginning");
            
            // Register assembly resolution handler
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Log startup info
            string addinLocation = Assembly.GetExecutingAssembly().Location;
            _logger.LogInformation($"Starting from: {addinLocation}");

            // Extract Revit version from the application
            var revitVersion = application.ControlledApplication.VersionNumber;
            _logger.LogInformation($"Detected Revit version: {revitVersion}");

            // Use RevitTask to manage UI thread execution
            _revitTask = new RevitTask();
            var pipeName = PipeNaming.GetCurrentProcessPipeName();
            _logger.LogInformation($"Using pipe name: {pipeName}");
            
            _server = new PipeServer(pipeName, _revitTask, path => new XunitTestAssemblyLoadContext(path));
            _server.Start();
            
            _logger.LogInformation("Startup completed successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup failed");
            
            // Also log to Trace as fallback
            Trace.WriteLine($"Startup failed: {ex.Message}");
            Trace.WriteLine($"Stack trace: {ex}");
            
            // Clean up any partially initialized resources
            try
            {
                _server?.Dispose();
                _revitTask?.Dispose();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Error during startup cleanup");
                Trace.WriteLine($"Error during startup cleanup: {cleanupEx.Message}");
            }
            
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            _logger.LogInformation("Shutdown starting");
            
            // Unregister assembly resolution handler
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

            _server?.Dispose();
            _revitTask?.Dispose();
            
            _logger.LogInformation("Shutdown completed successfully");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shutdown failed");
            
            // Also log to Trace as fallback
            Trace.WriteLine($"Shutdown failed: {ex.Message}");
            Trace.WriteLine($"Stack trace: {ex}");
            
            // Even if shutdown fails, we should return Succeeded to avoid preventing Revit from closing
            return Result.Succeeded;
        }
    }

    private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        try
        {
            // Parse assembly name from args
            AssemblyName assemblyName = new AssemblyName(args.Name);

            // Don't try to resolve resources
            if (assemblyName.Name.EndsWith(".resources"))
                return null;

            // Get the assembly directory where this RevitApplication is located
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

            // Check for exact filename match first
            string potentialPath = Path.Combine(assemblyDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(potentialPath))
            {
                _logger.LogTrace($"Resolved assembly: {assemblyName.Name} from: {potentialPath}");
                return Assembly.LoadFrom(potentialPath);
            }

            // Also look for version-specific file if the exact name wasn't found
            string[] candidateFiles = Directory.GetFiles(assemblyDirectory, $"{assemblyName.Name}*.dll");
            if (candidateFiles.Length > 0)
            {
                _logger.LogTrace($"Resolved assembly: {assemblyName.Name} from: {candidateFiles[0]}");
                return Assembly.LoadFrom(candidateFiles[0]);
            }

            // Only log warnings for assemblies that we expect to find but failed to resolve
            // Skip common system assemblies that should be resolved by the default context
            if (ShouldLogAssemblyResolutionFailure(assemblyName.Name))
            {
                _logger.LogWarning($"Failed to resolve assembly: {assemblyName.Name}");
            }
            else
            {
                _logger.LogTrace($"Assembly resolution delegated to default loader: {assemblyName.Name}");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error resolving assembly: {args.Name}");
            return null;
        }
    }

    /// <summary>
    /// Determines whether an assembly resolution failure should be logged as a warning
    /// </summary>
    /// <param name="assemblyName">The name of the assembly that failed to resolve</param>
    /// <returns>True if the failure should be logged as a warning, false if it should be logged as debug</returns>
    private static bool ShouldLogAssemblyResolutionFailure(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // System assemblies - these are expected to be resolved by the default context
        var systemAssemblies = new[]
        {
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Text.Json",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Memory",
            "System.Diagnostics.Debug",
            "System.IO",
            "System.Reflection",
            "System.ComponentModel",
            "System.Diagnostics.Process",
            "System.Xml.XDocument", 
            "System.Globalization",
            "System.Diagnostics.TraceSource",
            "System.IO.FileSystem",
            "System.Runtime.Extensions",
            "System.Reflection.Extensions",
            "System.Collections.Concurrent",
            "System.Threading.Thread",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging"
        };

        // Revit API assemblies - these should be resolved by the main AppDomain
        var revitAssemblies = new[]
        {
            "RevitAPI",
            "RevitAPIUI",
            "AdWindows",
            "UIFramework"
        };

        // Visual Studio and testing framework assemblies
        var vsTestingAssemblies = new[]
        {
            "Microsoft.VisualStudio.TestPlatform",
            "Microsoft.TestPlatform",
            "testhost"
        };

        return !systemAssemblies.Any(sys => assemblyName.StartsWith(sys, StringComparison.OrdinalIgnoreCase)) &&
               !revitAssemblies.Any(revit => assemblyName.StartsWith(revit, StringComparison.OrdinalIgnoreCase)) &&
               !vsTestingAssemblies.Any(vs => assemblyName.StartsWith(vs, StringComparison.OrdinalIgnoreCase));
    }
}
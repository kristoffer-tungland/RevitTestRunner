using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;
using System.Diagnostics;
using System.Reflection;

namespace RevitAddin.Xunit;

/// <summary>
/// Isolated handler for PipeServer operations - runs in its own AssemblyLoadContext
/// This class contains the actual logic that was previously in RevitXunitTestFrameworkApplication
/// </summary>
public class IsolatedPipeServerHandler
{
    private static PipeServer? _server;
    private static RevitTask? _revitTask;
    private static readonly ILogger _logger = FileLogger.ForContext<IsolatedPipeServerHandler>();

    /// <summary>
    /// Startup method called via reflection from the isolated context
    /// </summary>
    public static Result OnStartup(UIControlledApplication application)
    {
        try
        {
            _logger.LogInformation("Isolated pipe server startup beginning");

            // Use RevitTask to manage UI thread execution
            _revitTask = new RevitTask();
            var pipeName = PipeNaming.GetCurrentProcessPipeName();
            _logger.LogInformation($"Using pipe name: {pipeName}");
            
            _server = new PipeServer(pipeName, _revitTask, path => new XunitTestAssemblyLoadContext(path));
            _server.Start();
            
            _logger.LogInformation("Isolated pipe server startup completed successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Isolated pipe server startup failed");
            
            // Also log to Trace as fallback
            Trace.WriteLine($"Isolated pipe server startup failed: {ex.Message}");
            Trace.WriteLine($"Stack trace: {ex}");
            
            // Clean up any partially initialized resources
            try
            {
                _server?.Dispose();
                _revitTask?.Dispose();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Error during isolated pipe server startup cleanup");
                Trace.WriteLine($"Error during isolated pipe server startup cleanup: {cleanupEx.Message}");
            }
            
            return Result.Failed;
        }
    }

    /// <summary>
    /// Shutdown method called via reflection from the isolated context
    /// </summary>
    public static Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            _logger.LogInformation("Isolated pipe server shutdown starting");

            _server?.Dispose();
            _revitTask?.Dispose();
            
            _logger.LogInformation("Isolated pipe server shutdown completed successfully");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Isolated pipe server shutdown failed");
            
            // Also log to Trace as fallback
            Trace.WriteLine($"Isolated pipe server shutdown failed: {ex.Message}");
            Trace.WriteLine($"Stack trace: {ex}");
            
            // Even if shutdown fails, we should return Succeeded to avoid preventing Revit from closing
            return Result.Succeeded;
        }
    }
}
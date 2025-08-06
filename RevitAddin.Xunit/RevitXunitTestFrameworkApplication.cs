using Autodesk.Revit.UI;
using System.Diagnostics;
using System.Reflection;

namespace RevitAddin.Xunit;

public class RevitXunitTestFrameworkApplication : IExternalApplication
{
    private IsolatedPipeServerContext? _isolatedContext;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            Trace.WriteLine("RevitXunitTestFrameworkApplication: Startup beginning");

            // Log startup info
            string addinLocation = Assembly.GetExecutingAssembly().Location;
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Starting from: {addinLocation}");

            // Extract Revit version from the application
            var revitVersion = application.ControlledApplication.VersionNumber;
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Detected Revit version: {revitVersion}");

            // Get addin directory for isolated context
            string addinDirectory = Path.GetDirectoryName(addinLocation) 
                ?? throw new DirectoryNotFoundException("Could not determine addin directory");

            // Create isolated context and start pipe server within it
            // Pass the executing assembly location directly instead of constructing the path
            _isolatedContext = new IsolatedPipeServerContext(addinDirectory, addinLocation);
            _isolatedContext.StartPipeServer(application);
            
            Trace.WriteLine("RevitXunitTestFrameworkApplication: Startup completed successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            // Use only Trace for logging since we don't want dependencies on RevitTestFramework.Common
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Startup failed: {ex.Message}");
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Stack trace: {ex}");
            
            // Clean up any partially initialized resources
            try
            {
                _isolatedContext?.StopPipeServer(application);
            }
            catch (Exception cleanupEx)
            {
                Trace.WriteLine($"RevitXunitTestFrameworkApplication: Error during startup cleanup: {cleanupEx.Message}");
            }
            
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            Trace.WriteLine("RevitXunitTestFrameworkApplication: Shutdown starting");

            // Stop isolated pipe server
            _isolatedContext?.StopPipeServer(application);
            
            Trace.WriteLine("RevitXunitTestFrameworkApplication: Shutdown completed successfully");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Shutdown failed: {ex.Message}");
            Trace.WriteLine($"RevitXunitTestFrameworkApplication: Stack trace: {ex}");
            
            // Even if shutdown fails, we should return Succeeded to avoid preventing Revit from closing
            return Result.Succeeded;
        }
    }
}
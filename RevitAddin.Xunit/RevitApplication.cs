using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;
using System.Diagnostics;
using System.Reflection;

namespace RevitAddin.Xunit;

public class RevitApplication : IExternalApplication
{
    private PipeServer? _server;
    private RevitTask? _revitTask;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // Register assembly resolution handler
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Log startup info
            string addinLocation = Assembly.GetExecutingAssembly().Location;
            Trace.WriteLine($"RevitAddin.Xunit starting from: {addinLocation}");

            // Extract Revit version from the application
            var revitVersion = application.ControlledApplication.VersionNumber;
            Trace.WriteLine($"RevitAddin.Xunit detected Revit version: {revitVersion}");

            // Use RevitTask to manage UI thread execution
            _revitTask = new RevitTask();
            var pipeName = PipeNaming.GetCurrentProcessPipeName();
            Trace.WriteLine($"RevitAddin.Xunit using pipe name: {pipeName}");
            
            _server = new PipeServer(pipeName, _revitTask, path => new XunitTestAssemblyLoadContext(path));
            _server.Start();
            
            Trace.WriteLine("RevitAddin.Xunit startup completed successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"RevitAddin.Xunit startup failed: {ex.Message}");
            Trace.WriteLine($"Stack trace: {ex}");
            
            // Clean up any partially initialized resources
            try
            {
                _server?.Dispose();
                _revitTask?.Dispose();
            }
            catch (Exception cleanupEx)
            {
                Trace.WriteLine($"Error during startup cleanup: {cleanupEx.Message}");
            }
            
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            Trace.WriteLine("RevitAddin.Xunit shutdown starting");
            
            // Unregister assembly resolution handler
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

            _server?.Dispose();
            _revitTask?.Dispose();
            
            Trace.WriteLine("RevitAddin.Xunit shutdown completed successfully");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"RevitAddin.Xunit shutdown failed: {ex.Message}");
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
                Trace.WriteLine($"Resolved assembly: {assemblyName.Name} from: {potentialPath}");
                return Assembly.LoadFrom(potentialPath);
            }

            // Also look for version-specific file if the exact name wasn't found
            string[] candidateFiles = Directory.GetFiles(assemblyDirectory, $"{assemblyName.Name}*.dll");
            if (candidateFiles.Length > 0)
            {
                Trace.WriteLine($"Resolved assembly: {assemblyName.Name} from: {candidateFiles[0]}");
                return Assembly.LoadFrom(candidateFiles[0]);
            }

            Trace.WriteLine($"Failed to resolve assembly: {assemblyName.Name}");
            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error resolving assembly: {ex.Message}");
            return null;
        }
    }
}
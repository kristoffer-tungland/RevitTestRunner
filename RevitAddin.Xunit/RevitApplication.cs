using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RevitAddin.Xunit;

public class RevitApplication : IExternalApplication
{
    private PipeServer? _server;
    private RevitTask? _revitTask;

    public Result OnStartup(UIControlledApplication application)
    {
#if DEBUG
        Debugger.Launch();
#endif
        // Register assembly resolution handler
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        // Log startup info
        string addinLocation = Assembly.GetExecutingAssembly().Location;
        Trace.WriteLine($"RevitAddin.Xunit starting from: {addinLocation}");

        // Use RevitTask to manage UI thread execution
        _revitTask = new RevitTask();
        var pipeName = PipeConstants.PipeNamePrefix + Process.GetCurrentProcess().Id;
        _server = new PipeServer(pipeName, _revitTask, path => new XunitTestAssemblyLoadContext(path));
        _server.Start();
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        // Unregister assembly resolution handler
        AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

        _server?.Dispose();
        _revitTask?.Dispose();
        return Result.Succeeded;
    }

    private Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
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
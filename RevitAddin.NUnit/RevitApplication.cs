using Autodesk.Revit.UI;
using RevitAddin.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RevitAddin.NUnit;

public class RevitApplication : IExternalApplication
{
    private PipeServer? _server;

    public Result OnStartup(UIControlledApplication application)
    {
        // Register assembly resolution handler
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        
        // Log startup info
        string addinLocation = Assembly.GetExecutingAssembly().Location;
        Trace.WriteLine($"RevitAddin.NUnit starting from: {addinLocation}");
        
        var handler = new TestCommandHandler();
        var extEvent = ExternalEvent.Create(handler);
        var pipeName = PipeConstants.PipeNamePrefix + Process.GetCurrentProcess().Id;
        _server = new PipeServer(pipeName, extEvent, handler);
        _server.Start();
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        // Unregister assembly resolution handler
        AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        
        _server?.Dispose();
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
using System.Runtime.Loader;
using System.Reflection;
using Autodesk.Revit.UI;
using System.Diagnostics;

namespace RevitAddin.Xunit;

/// <summary>
/// Isolated AssemblyLoadContext for running the PipeServer and all its dependencies in isolation from the main application
/// </summary>
internal class IsolatedPipeServerContext : AssemblyLoadContext
{
    private readonly string _addinDirectory;
    private readonly string _assemblyPath;
    private Assembly? _isolatedAssembly;

    public IsolatedPipeServerContext(string addinDirectory, string assemblyPath) : base("IsolatedPipeServerContext", isCollectible: false)
    {
        _addinDirectory = addinDirectory ?? throw new ArgumentNullException(nameof(addinDirectory));
        _assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
        
        Trace.WriteLine($"IsolatedPipeServerContext: Created for directory: {_addinDirectory}");
        Trace.WriteLine($"IsolatedPipeServerContext: Assembly path: {_assemblyPath}");
        
        // Set up assembly resolution
        Resolving += OnResolving;
    }

    /// <summary>
    /// Starts the isolated pipe server by loading the handler in this context and calling its startup method
    /// </summary>
    public void StartPipeServer(UIControlledApplication application)
    {
        try
        {
            Trace.WriteLine("IsolatedPipeServerContext: Starting isolated pipe server");
            
            var assembly = LoadIsolatedAssembly();
            var handlerType = assembly.GetType("RevitAddin.Xunit.IsolatedPipeServerHandler")
                ?? throw new InvalidOperationException("Could not find IsolatedPipeServerHandler type");

            var startupMethod = handlerType.GetMethod("OnStartup", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find OnStartup method");

            var result = startupMethod.Invoke(null, [application]);
            Trace.WriteLine($"IsolatedPipeServerContext: Isolated pipe server startup completed with result: {result}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"IsolatedPipeServerContext: Failed to start isolated pipe server: {ex.Message}");
            Trace.WriteLine($"IsolatedPipeServerContext: Stack trace: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Stops the isolated pipe server by calling its shutdown method
    /// </summary>
    public void StopPipeServer(UIControlledApplication application)
    {
        try
        {
            Trace.WriteLine("IsolatedPipeServerContext: Stopping isolated pipe server");
            
            if (_isolatedAssembly == null)
            {
                Trace.WriteLine("IsolatedPipeServerContext: Isolated assembly not loaded, nothing to stop");
                return;
            }

            var handlerType = _isolatedAssembly.GetType("RevitAddin.Xunit.IsolatedPipeServerHandler")
                ?? throw new InvalidOperationException("Could not find IsolatedPipeServerHandler type");

            var shutdownMethod = handlerType.GetMethod("OnShutdown", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find OnShutdown method");

            var result = shutdownMethod.Invoke(null, [application]);
            Trace.WriteLine($"IsolatedPipeServerContext: Isolated pipe server shutdown completed with result: {result}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"IsolatedPipeServerContext: Failed to stop isolated pipe server: {ex.Message}");
            Trace.WriteLine($"IsolatedPipeServerContext: Stack trace: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Loads the RevitAddin.Xunit assembly into this isolated context
    /// </summary>
    private Assembly LoadIsolatedAssembly()
    {
        if (_isolatedAssembly == null)
        {
            if (!File.Exists(_assemblyPath))
            {
                throw new FileNotFoundException($"Could not find RevitAddin.Xunit.dll at: {_assemblyPath}");
            }

            Trace.WriteLine($"IsolatedPipeServerContext: Loading isolated assembly from: {_assemblyPath}");
            _isolatedAssembly = LoadFromAssemblyPath(_assemblyPath);
            Trace.WriteLine("IsolatedPipeServerContext: Isolated assembly loaded successfully");
        }
        return _isolatedAssembly;
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        // Don't resolve resource assemblies
        if (assemblyName.Name?.EndsWith(".resources") == true)
            return null;

        Trace.WriteLine($"IsolatedPipeServerContext: Attempting to resolve assembly: {assemblyName.Name}");

        try
        {
            // Try to load from the addin directory (for RevitAddin.Common, RevitTestFramework.Common, PipeServer dependencies)
            var addinPath = Path.Combine(_addinDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(addinPath))
            {
                Trace.WriteLine($"IsolatedPipeServerContext: Resolving assembly: {assemblyName.Name} from addin directory: {addinPath}");
                return LoadFromAssemblyPath(addinPath);
            }

            // Check for version-specific files in addin directory
            var addinPattern = $"{assemblyName.Name}*.dll";
            var addinCandidates = Directory.GetFiles(_addinDirectory, addinPattern);
            if (addinCandidates.Length > 0)
            {
                Trace.WriteLine($"IsolatedPipeServerContext: Resolving assembly: {assemblyName.Name} from addin directory (pattern match): {addinCandidates[0]}");
                return LoadFromAssemblyPath(addinCandidates[0]);
            }

            // Only log warnings for assemblies that we expect to find but failed to resolve
            if (ShouldLogAssemblyResolutionFailure(assemblyName.Name))
            {
                Trace.WriteLine($"IsolatedPipeServerContext: Failed to resolve assembly: {assemblyName.Name}");
                // List all files in the addin directory for debugging
                Trace.WriteLine($"IsolatedPipeServerContext: Files in addin directory {_addinDirectory}:");
                try
                {
                    var files = Directory.GetFiles(_addinDirectory, "*.dll");
                    foreach (var file in files)
                    {
                        Trace.WriteLine($"IsolatedPipeServerContext:   - {Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"IsolatedPipeServerContext: Failed to list files in addin directory: {ex.Message}");
                }
            }
            else
            {
                Trace.WriteLine($"IsolatedPipeServerContext: Assembly resolution delegated to default context: {assemblyName.Name}");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"IsolatedPipeServerContext: Error while resolving assembly: {assemblyName.Name} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Determines whether an assembly resolution failure should be logged as a warning
    /// </summary>
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

        // RevitTestFramework assemblies that we expect to resolve in the isolated context
        var expectedRevitTestFrameworkAssemblies = new[]
        {
            "RevitTestFramework.Common",
            "RevitTestFramework.Contracts",
            "RevitAddin.Common"
        };

        // Only log as warning if it's an assembly we expect to find in our addin directory
        return expectedRevitTestFrameworkAssemblies.Any(expected => assemblyName.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) &&
               !systemAssemblies.Any(sys => assemblyName.StartsWith(sys, StringComparison.OrdinalIgnoreCase)) &&
               !revitAssemblies.Any(revit => assemblyName.StartsWith(revit, StringComparison.OrdinalIgnoreCase)) &&
               !vsTestingAssemblies.Any(vs => assemblyName.StartsWith(vs, StringComparison.OrdinalIgnoreCase));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        return OnResolving(this, assemblyName);
    }
}
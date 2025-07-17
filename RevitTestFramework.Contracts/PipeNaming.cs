using System.Reflection;

namespace RevitTestFramework.Contracts;

/// <summary>
/// Provides constants and utility methods for pipe naming in the Revit Test Framework
/// </summary>
public static class PipeNaming
{
    /// <summary>
    /// The prefix used for all Revit test pipe names
    /// </summary>
    public const string PipeNamePrefix = "RevitTestPipe_";

    /// <summary>
    /// Calculates the pipe name using Revit version, assembly version, and process ID
    /// </summary>
    /// <param name="revitVersion">The Revit version (e.g., "2025")</param>
    /// <param name="assemblyVersion">The assembly version (e.g., "1.0.0")</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name: RevitTestPipe_&lt;RevitVersion&gt;_&lt;AssemblyVersion&gt;_&lt;ProcessId&gt;</returns>
    public static string GetPipeName(string revitVersion, string assemblyVersion, int processId)
    {
        return $"{PipeNamePrefix}{revitVersion}_{assemblyVersion}_{processId}";
    }

    /// <summary>
    /// Calculates the pipe name for the current process using Revit version and the executing assembly version
    /// </summary>
    /// <param name="revitVersion">The Revit version (e.g., "2025")</param>
    /// <returns>The formatted pipe name using the current assembly version and process ID</returns>
    public static string GetCurrentProcessPipeName(string revitVersion)
    {
        var assemblyVersion = GetFormattedAssemblyVersion(Assembly.GetExecutingAssembly());
        return GetPipeName(revitVersion, assemblyVersion, Environment.ProcessId);
    }

    /// <summary>
    /// Calculates the pipe name for a specific assembly using Revit version and the assembly version
    /// </summary>
    /// <param name="revitVersion">The Revit version (e.g., "2025")</param>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name using the specified assembly version and process ID</returns>
    public static string GetPipeNameForAssembly(string revitVersion, Assembly assembly, int processId)
    {
        var assemblyVersion = GetFormattedAssemblyVersion(assembly);
        return GetPipeName(revitVersion, assemblyVersion, processId);
    }

    /// <summary>
    /// Formats an assembly version to use only the first 3 parts (Major.Minor.Build)
    /// </summary>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <returns>The formatted version string (e.g., "1.0.0")</returns>
    private static string GetFormattedAssemblyVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
}
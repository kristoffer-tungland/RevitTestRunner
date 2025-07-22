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
    /// Calculates the pipe name using assembly version and process ID
    /// The assembly version now contains the Revit version in format: RevitVersion.Minor.Patch (e.g., "2025.0.0")
    /// </summary>
    /// <param name="assemblyVersion">The assembly version (e.g., "2025.0.0")</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name: RevitTestPipe_&lt;AssemblyVersion&gt;_&lt;ProcessId&gt;</returns>
    public static string GetPipeName(string assemblyVersion, int processId)
    {
        return $"{PipeNamePrefix}{assemblyVersion}_{processId}";
    }

    /// <summary>
    /// Calculates the pipe name for the current process using the executing assembly version
    /// </summary>
    /// <returns>The formatted pipe name using the current assembly version and process ID</returns>
    public static string GetCurrentProcessPipeName()
    {
        var assemblyVersion = GetFormattedAssemblyVersion(Assembly.GetExecutingAssembly());
        return GetPipeName(assemblyVersion, Environment.ProcessId);
    }

    /// <summary>
    /// Calculates the pipe name for a specific assembly using the assembly version
    /// </summary>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name using the specified assembly version and process ID</returns>
    public static string GetPipeNameForAssembly(Assembly assembly, int processId)
    {
        var assemblyVersion = GetFormattedAssemblyVersion(assembly);
        return GetPipeName(assemblyVersion, processId);
    }

    /// <summary>
    /// Formats an assembly version to use only the first 3 parts (Major.Minor.Build)
    /// With the new format, Major contains the Revit version (e.g., 2025.0.0)
    /// </summary>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <returns>The formatted version string (e.g., "2025.0.0")</returns>
    private static string GetFormattedAssemblyVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "2025.0.0";
    }
}
using System.Reflection;
using System.Text.RegularExpressions;

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
    /// For pre-release versions, normalizes them to ensure pipe name compatibility
    /// </summary>
    /// <param name="assemblyVersion">The assembly version (e.g., "2025.0.0" or "2025.1.0-pullrequest0018.103")</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name: RevitTestPipe_&lt;NormalizedAssemblyVersion&gt;_&lt;ProcessId&gt;</returns>
    public static string GetPipeName(string assemblyVersion, int processId)
    {
        var normalizedVersion = NormalizeVersionForPipe(assemblyVersion);
        return $"{PipeNamePrefix}{normalizedVersion}_{processId}";
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
    /// Normalizes a version string for use in pipe names
    /// For standard versions (e.g., "2025.1.0"), keeps the version as is
    /// For pre-release versions (e.g., "2025.1.0-pullrequest0018.109"), converts to normalized format (e.g., "2025.1.0.18109")
    /// </summary>
    /// <param name="version">Original version string</param>
    /// <returns>Normalized version suitable for pipe names</returns>
    public static string NormalizeVersionForPipe(string version)
    {
        if (string.IsNullOrEmpty(version))
            return "2025.0.0";

        // Extract the base version (before any pre-release suffix)
        string baseVersion = version.Split('-')[0];
        
        // If it's already a standard version, keep it as is (with 3 parts)
        if (!version.Contains('-'))
        {
            var parts = baseVersion.Split('.');
            return parts.Length switch
            {
                1 => $"{parts[0]}.0.0",
                2 => $"{parts[0]}.{parts[1]}.0",
                3 => baseVersion, // Keep as is for standard 3-part versions
                _ => $"{parts[0]}.{parts[1]}.{parts[2]}" // Take first 3 parts
            };
        }

        // Handle pre-release versions - convert to 4-part version for uniqueness
        string preReleaseSection = version.Substring(baseVersion.Length + 1); // Skip the '-'
        
        // Extract numeric values from the pre-release section
        var numbers = Regex.Matches(preReleaseSection, @"\d+")
                          .Cast<Match>()
                          .Select(m => m.Value)
                          .ToArray();
        
        // Combine numbers into a single revision number
        // Use the same logic as assembly version normalization for consistency
        string revisionNumber = "1"; // Default to 1 for pre-release to distinguish from standard
        if (numbers.Length > 0)
        {
            // Combine all numbers into a single string
            string combined = string.Join("", numbers);
            
            // Parse and validate it fits in a reasonable range for pipe names
            if (!string.IsNullOrEmpty(combined) && int.TryParse(combined, out int revisionValue))
            {
                if (revisionValue <= 65535)
                {
                    revisionNumber = revisionValue.ToString();
                }
                else
                {
                    // If too large, take a hash to ensure uniqueness while staying within limits
                    revisionNumber = Math.Abs(combined.GetHashCode() % 65535).ToString();
                }
            }
            else
            {
                // If parsing fails or empty, use hash of the pre-release section
                revisionNumber = Math.Abs(preReleaseSection.GetHashCode() % 65535).ToString();
            }
        }
        
        // Ensure it's not zero
        if (revisionNumber == "0")
            revisionNumber = "1";
        
        // Ensure base version has 3 parts
        var baseParts = baseVersion.Split('.');
        string normalizedBase = baseParts.Length switch
        {
            1 => $"{baseParts[0]}.0.0",
            2 => $"{baseParts[0]}.{baseParts[1]}.0",
            _ => $"{baseParts[0]}.{baseParts[1]}.{baseParts[2]}"
        };
        
        return $"{normalizedBase}.{revisionNumber}";
    }

    /// <summary>
    /// Formats an assembly version to use only the first 3 parts (Major.Minor.Build)
    /// With the new format, Major contains the Revit version (e.g., 2025.0.0)
    /// For 4-part versions that might be pre-release normalized versions, includes all 4 parts
    /// </summary>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <returns>The formatted version string (e.g., "2025.0.0" or "2025.1.0.18103")</returns>
    private static string GetFormattedAssemblyVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        if (version == null) return "2025.0.0";
        
        // If revision is 0, use 3-part version (standard case)
        // If revision is non-zero, use 4-part version (pre-release case)
        return version.Revision == 0 
            ? $"{version.Major}.{version.Minor}.{version.Build}" 
            : $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
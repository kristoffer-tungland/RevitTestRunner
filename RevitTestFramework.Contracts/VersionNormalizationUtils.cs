using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitTestFramework.Contracts;

/// <summary>
/// Utilities for normalizing version strings in the RevitTestFramework
/// </summary>
public static class VersionNormalizationUtils
{
    /// <summary>
    /// Normalizes a version string to always produce a 4-part version format
    /// </summary>
    /// <param name="version">Original version string</param>
    /// <param name="defaultRevisionForPrerelease">Default revision number for pre-release versions when no numbers found</param>
    /// <returns>Normalized version string (always 4-part format)</returns>
    public static string NormalizeVersion(string version, string defaultRevisionForPrerelease = "1")
    {
        if (string.IsNullOrEmpty(version))
            return "2025.0.0.0";

        // Extract the base version (before any pre-release suffix)
        string baseVersion = version.Split('-')[0];
        
        // If it's already a standard version
        if (!version.Contains('-'))
        {
            var parts = baseVersion.Split('.');
            string standardNormalizedBase = parts.Length switch
            {
                1 => $"{parts[0]}.0.0",
                2 => $"{parts[0]}.{parts[1]}.0",
                3 => baseVersion,
                _ => $"{parts[0]}.{parts[1]}.{parts[2]}"
            };
            
            // Always include revision for consistent 4-part format
            return $"{standardNormalizedBase}.0";
        }

        // Handle pre-release versions
        string preReleaseSection = version.Substring(baseVersion.Length + 1); // Skip the '-'
        
        // Extract numeric values from the pre-release section
        var numbers = Regex.Matches(preReleaseSection, @"\d+")
                          .Cast<Match>()
                          .Select(m => m.Value)
                          .ToArray();
        
        // Combine numbers into a single revision number
        string revisionNumber = "0"; // Start with "0" for algorithm
        if (numbers.Length > 0)
        {
            // Combine all numbers into a single string
            string combined = string.Join("", numbers);
            
            // Parse and validate it fits in a 16-bit integer (max 65535)
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
        else
        {
            // No numbers found in pre-release section, use hash of the pre-release section
            revisionNumber = Math.Abs(preReleaseSection.GetHashCode() % 65535).ToString();
        }
        
        // Ensure it's not zero for pre-release versions
        if (revisionNumber == "0")
            revisionNumber = defaultRevisionForPrerelease;
        
        // Ensure base version has 3 parts
        var baseParts = baseVersion.Split('.');
        string preReleaseNormalizedBase = baseParts.Length switch
        {
            1 => $"{baseParts[0]}.0.0",
            2 => $"{baseParts[0]}.{baseParts[1]}.0",
            _ => $"{baseParts[0]}.{baseParts[1]}.{baseParts[2]}"
        };
        
        return $"{preReleaseNormalizedBase}.{revisionNumber}";
    }
}

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
    /// The assembly version contains the Revit version in format: RevitVersion.Minor.Patch.Revision (e.g., "2025.0.0.0")
    /// For pre-release versions, normalizes them to ensure pipe name compatibility (e.g., "2025.1.0.18109")
    /// </summary>
    /// <param name="assemblyVersion">The assembly version (e.g., "2025.0.0" or "2025.1.0-pullrequest0018.103")</param>
    /// <param name="processId">The Revit process ID</param>
    /// <returns>The formatted pipe name: RevitTestPipe_&lt;NormalizedAssemblyVersion&gt;_&lt;ProcessId&gt; (always 4-part version)</returns>
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
    /// Always uses 4-part version format for consistency (e.g., "2025.1.0.0" or "2025.1.0.18109")
    /// </summary>
    /// <param name="version">Original version string</param>
    /// <returns>Normalized version suitable for pipe names (always 4-part)</returns>
    public static string NormalizeVersionForPipe(string version)
    {
        // Always use 4-part versions for consistency
        return VersionNormalizationUtils.NormalizeVersion(version, defaultRevisionForPrerelease: "1");
    }

    /// <summary>
    /// Formats an assembly version to always use 4 parts (Major.Minor.Build.Revision)
    /// With the new format, Major contains the Revit version (e.g., 2025.0.0.0)
    /// For pre-release versions, includes the revision number (e.g., 2025.1.0.18103)
    /// </summary>
    /// <param name="assembly">The assembly to get the version from</param>
    /// <returns>The formatted version string (always 4-part, e.g., "2025.0.0.0" or "2025.1.0.18103")</returns>
    private static string GetFormattedAssemblyVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;
        if (version == null) return "2025.0.0.0";
        
        // Always return 4-part version for consistency
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }
}
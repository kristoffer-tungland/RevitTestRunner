using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitTestFramework.Common;

/// <summary>
/// Tool for generating addin manifests for RevitTestFramework
/// </summary>
public static class AddinManifestTool
{
    // Base GUID that will be modified based on version to ensure uniqueness across versions
    private static readonly Guid XunitBaseGuid = new Guid("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");

    /// <summary>
    /// Generates the addin manifest for the RevitAddin.Xunit project
    /// </summary>
    /// <param name="outputDirectory">Directory where to save the addin manifest file</param>
    /// <param name="assemblyPath">Path to the RevitAddin.Xunit assembly</param>
    /// <param name="useFixedGuids">Whether to use fixed GUIDs for consistent identification</param>
    /// <param name="assemblyVersion">Assembly version to use (if null, extracts from assembly). Format: RevitVersion.Minor.Patch (e.g., "2025.0.0") or pre-release (e.g., "2025.1.0-pullrequest0018.103")</param>
    public static void GenerateXunitAddinManifest(
        string outputDirectory, 
        string? assemblyPath = null,
        bool useFixedGuids = true,
        string? assemblyVersion = null)
    {
        assemblyPath = FindAssemblyPath(assemblyPath, "RevitAddin.Xunit.dll");
        
        // Get assembly version from the assembly if not provided
        if (string.IsNullOrEmpty(assemblyVersion))
        {
            assemblyVersion = GetAssemblyVersion(assemblyPath);
        }
        
        // Normalize the version for use in paths and manifests
        string normalizedVersion = NormalizeVersionForAssembly(assemblyVersion);
        
        // Create version-specific directory and copy assemblies
        string versionedOutputDir = Path.Combine(outputDirectory, $"RevitTestFramework.v{normalizedVersion}");
        Directory.CreateDirectory(versionedOutputDir);
        
        // Copy the assembly and its dependencies to the versioned output directory
        string versionedAssemblyPath = CopyAssemblyWithDependencies(assemblyPath, versionedOutputDir);
        
        string addinName = $"RevitTestFramework Xunit v{normalizedVersion}";
        
        // Generate version-specific GUID using original version for uniqueness
        Guid versionSpecificGuid = useFixedGuids ? 
            GenerateVersionSpecificGuid(XunitBaseGuid, assemblyVersion) : 
            Guid.NewGuid();

        RevitAddInManifestGenerator.GenerateAddinManifest(
            outputDirectory,
            versionedAssemblyPath,
            "RevitXunitTestFrameworkApplication",
            "RevitTestFramework",
            addinName,
            versionSpecificGuid,
            normalizedVersion
        );
    }

    /// <summary>
    /// Normalizes a version string to be compatible with .NET assembly versions
    /// Converts pre-release versions like "2025.1.0-pullrequest0018.109" to "2025.1.0.0018109"
    /// </summary>
    /// <param name="version">Original version string</param>
    /// <returns>Normalized version suitable for assembly versions</returns>
    public static string NormalizeVersionForAssembly(string version)
    {
        if (string.IsNullOrEmpty(version))
            return "2025.0.0.0";

        // Extract the base version (before any pre-release suffix)
        string baseVersion = version.Split('-')[0];
        
        // If it's already a standard version, ensure it has 4 parts
        if (!version.Contains('-'))
        {
            var parts = baseVersion.Split('.');
            return parts.Length switch
            {
                1 => $"{parts[0]}.0.0.0",
                2 => $"{parts[0]}.{parts[1]}.0.0",
                3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
                _ => baseVersion
            };
        }

        // Handle pre-release versions
        string preReleaseSection = version.Substring(baseVersion.Length + 1); // Skip the '-'
        
        // Extract numeric values from the pre-release section
        var numbers = Regex.Matches(preReleaseSection, @"\d+")
                          .Cast<Match>()
                          .Select(m => m.Value)
                          .ToArray();
        
        // Combine numbers into a single revision number
        // .NET Version revision field is a 16-bit integer (max 65535)
        // We'll allow up to 5 digits but validate the result doesn't exceed 65535
        string revisionNumber = "0";
        if (numbers.Length > 0)
        {
            // Combine all numbers into a single string
            string combined = string.Join("", numbers);
            
            // Parse and validate it fits in a 16-bit integer
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
    /// Gets the assembly version from an assembly file
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly</param>
    /// <returns>The formatted assembly version (e.g., "2025.0.0.0")</returns>
    private static string GetAssemblyVersion(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "2025.0.0.0";
        }
        catch
        {
            // Fallback to default version if assembly cannot be loaded
            return "2025.0.0.0";
        }
    }

    /// <summary>
    /// Copy an assembly and all its dependencies to the output directory
    /// </summary>
    /// <param name="assemblyPath">Path to the main assembly</param>
    /// <param name="outputDir">Directory to copy files to</param>
    /// <returns>Path to the copied main assembly</returns>
    private static string CopyAssemblyWithDependencies(string assemblyPath, string outputDir)
    {
        // Get the directory containing the assembly
        string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
        string outputAssemblyPath = Path.Combine(outputDir, Path.GetFileName(assemblyPath));
        
        // Copy the main assembly
        File.Copy(assemblyPath, outputAssemblyPath, true);
        
        // Get all DLL files from the assembly directory
        string[] allDlls = Directory.GetFiles(assemblyDir, "*.dll");
        foreach (string dll in allDlls)
        {
            if (dll != assemblyPath) // Skip the main assembly as it's already copied
            {
                string outputDllPath = Path.Combine(outputDir, Path.GetFileName(dll));
                File.Copy(dll, outputDllPath, true);
            }
        }
        
        return outputAssemblyPath;
    }

    /// <summary>
    /// Generate a version-specific GUID by combining the base GUID with the version
    /// Uses the full original version string (including pre-release info) for maximum uniqueness
    /// </summary>
    private static Guid GenerateVersionSpecificGuid(Guid baseGuid, string version)
    {
        // Use hash of the full version string for consistent GUID generation
        int versionHash = version.GetHashCode();
        
        // Get the bytes of the base GUID
        byte[] bytes = baseGuid.ToByteArray();
        
        // Modify last 4 bytes with the version hash to ensure uniqueness
        byte[] versionBytes = BitConverter.GetBytes(versionHash);
        for (int i = 0; i < 4; i++)
        {
            bytes[bytes.Length - 1 - i] = versionBytes[i];
        }
        
        return new Guid(bytes);
    }

    /// <summary>
    /// Find the assembly path by checking various locations
    /// </summary>
    private static string FindAssemblyPath(string? providedPath, string assemblyName)
    {
        if (!string.IsNullOrEmpty(providedPath) && File.Exists(providedPath))
        {
            return providedPath;
        }

        // Try to find the assembly in the same directory as this assembly
        string? currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (currentDir != null)
        {
            // First try exact match
            string potentialPath = Path.Combine(currentDir, assemblyName);
            if (File.Exists(potentialPath))
            {
                return potentialPath;
            }

            // Try to find versioned assembly (e.g., RevitAddin.Xunit.2025.0.0.dll when looking for RevitAddin.Xunit.dll)
            string baseAssemblyName = Path.GetFileNameWithoutExtension(assemblyName);
            string[] versionedAssemblies = Directory.GetFiles(currentDir, $"{baseAssemblyName}.*.dll");
            if (versionedAssemblies.Length > 0)
            {
                // Return the first match (could be improved to select latest version)
                return versionedAssemblies[0];
            }
            
            // Look up one directory and check bin folders
            string? parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null)
            {
                string projectName = Path.GetFileNameWithoutExtension(assemblyName);
                
                var possibleLocations = new[]
                {
                    Path.Combine(parentDir, projectName, "bin", "Debug", "net8.0", assemblyName),
                    Path.Combine(parentDir, projectName, "bin", "Release", "net8.0", assemblyName),
                    Path.Combine(parentDir, projectName, "bin", "Debug", "net9.0", assemblyName),
                    Path.Combine(parentDir, projectName, "bin", "Release", "net9.0", assemblyName)
                };

                foreach (var location in possibleLocations)
                {
                    if (File.Exists(location))
                    {
                        return location;
                    }
                }

                // Also try to find versioned assemblies in bin folders
                foreach (var binPath in new[] { "bin/Debug/net8.0", "bin/Release/net8.0", "bin/Debug/net9.0", "bin/Release/net9.0" })
                {
                    string binDir = Path.Combine(parentDir, projectName, binPath);
                    if (Directory.Exists(binDir))
                    {
                        string[] versionedBinAssemblies = Directory.GetFiles(binDir, $"{baseAssemblyName}.*.dll");
                        if (versionedBinAssemblies.Length > 0)
                        {
                            return versionedBinAssemblies[0];
                        }
                    }
                }
            }
        }

        throw new FileNotFoundException($"Could not find {assemblyName}. Please provide the path explicitly.");
    }
}
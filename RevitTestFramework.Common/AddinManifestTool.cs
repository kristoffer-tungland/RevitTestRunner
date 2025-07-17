using System.Reflection;

namespace RevitTestFramework.Common;

/// <summary>
/// Tool for generating addin manifests for RevitTestFramework
/// </summary>
public static class AddinManifestTool
{
    // Base GUIDs that will be modified based on version to ensure uniqueness across versions
    private static readonly Guid XunitBaseGuid = new Guid("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");
    private static readonly Guid NUnitBaseGuid = new Guid("11111111-2222-3333-4444-555555555555");

    /// <summary>
    /// Generates the addin manifest for the RevitAddin.Xunit project
    /// </summary>
    /// <param name="outputDirectory">Directory where to save the addin manifest file</param>
    /// <param name="assemblyPath">Path to the RevitAddin.Xunit assembly</param>
    /// <param name="useFixedGuids">Whether to use fixed GUIDs for consistent identification</param>
    /// <param name="packageVersion">Version to use for side-loading (determines GUID and output path)</param>
    public static void GenerateXunitAddinManifest(
        string outputDirectory, 
        string? assemblyPath = null,
        bool useFixedGuids = true,
        string packageVersion = "1.0.0")
    {
        assemblyPath = FindAssemblyPath(assemblyPath, "RevitAddin.Xunit.dll");
        
        // Create version-specific directory and copy assemblies
        string versionedOutputDir = Path.Combine(outputDirectory, $"RevitTestFramework.v{packageVersion}");
        Directory.CreateDirectory(versionedOutputDir);
        
        // Copy the assembly and its dependencies to the versioned output directory
        string versionedAssemblyPath = CopyAssemblyWithDependencies(assemblyPath, versionedOutputDir);
        
        string addinName = $"RevitTestFramework Xunit v{packageVersion}";
        
        // Generate version-specific GUID
        Guid versionSpecificGuid = useFixedGuids ? 
            GenerateVersionSpecificGuid(XunitBaseGuid, packageVersion) : 
            Guid.NewGuid();

        RevitAddInManifestGenerator.GenerateAddinManifest(
            outputDirectory,
            versionedAssemblyPath,
            "RevitApplication",
            "RevitTestFramework",
            addinName,
            versionSpecificGuid,
            packageVersion
        );
    }

    /// <summary>
    /// Generates the addin manifest for the RevitAddin.NUnit project
    /// </summary>
    /// <param name="outputDirectory">Directory where to save the addin manifest file</param>
    /// <param name="assemblyPath">Path to the RevitAddin.NUnit assembly</param>
    /// <param name="useFixedGuids">Whether to use fixed GUIDs for consistent identification</param>
    /// <param name="packageVersion">Version to use for side-loading (determines GUID and output path)</param>
    public static void GenerateNUnitAddinManifest(
        string outputDirectory, 
        string? assemblyPath = null,
        bool useFixedGuids = true,
        string packageVersion = "1.0.0")
    {
        assemblyPath = FindAssemblyPath(assemblyPath, "RevitAddin.NUnit.dll");
        
        // Create version-specific directory and copy assemblies
        string versionedOutputDir = Path.Combine(outputDirectory, $"RevitTestFramework.v{packageVersion}");
        Directory.CreateDirectory(versionedOutputDir);
        
        // Copy the assembly and its dependencies to the versioned output directory
        string versionedAssemblyPath = CopyAssemblyWithDependencies(assemblyPath, versionedOutputDir);
        
        string addinName = $"RevitTestFramework NUnit v{packageVersion}";
        
        // Generate version-specific GUID
        Guid versionSpecificGuid = useFixedGuids ? 
            GenerateVersionSpecificGuid(NUnitBaseGuid, packageVersion) : 
            Guid.NewGuid();
        
        RevitAddInManifestGenerator.GenerateAddinManifest(
            outputDirectory,
            versionedAssemblyPath,
            "RevitApplication",
            "RevitTestFramework",
            addinName,
            versionSpecificGuid,
            packageVersion
        );
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
    /// </summary>
    private static Guid GenerateVersionSpecificGuid(Guid baseGuid, string version)
    {
        // Convert version to a numeric value by removing dots and converting to an integer
        string numericVersion = version.Replace(".", "");
        int versionValue;
        if (!int.TryParse(numericVersion, out versionValue))
        {
            // Handle non-numeric parts in version (e.g., 1.0.0-beta)
            versionValue = version.GetHashCode();
        }
        
        // Get the bytes of the base GUID
        byte[] bytes = baseGuid.ToByteArray();
        
        // Modify last 4 bytes with the version value to ensure uniqueness
        byte[] versionBytes = BitConverter.GetBytes(versionValue);
        for (int i = 0; i < 4 && i < versionBytes.Length; i++)
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
            string potentialPath = Path.Combine(currentDir, assemblyName);
            if (File.Exists(potentialPath))
            {
                return potentialPath;
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
            }
        }

        throw new FileNotFoundException($"Could not find {assemblyName}. Please provide the path explicitly.");
    }
}
using System.Reflection;
using System.Text.RegularExpressions;

namespace RevitTestFramework.Common
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: RevitTestFramework.Common.exe generate-manifest [options]");
                    Console.WriteLine("Options:");
                    Console.WriteLine("  --output <path>           Output directory (default: auto-detected from Revit version)");
                    Console.WriteLine("  --assembly <path>         Path to assembly file (optional)");
                    Console.WriteLine("  --fixed-guids <bool>      Use fixed GUIDs (default: true)");
                    return 0;
                }

                string command = args[0].ToLowerInvariant();
                var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Parse options
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i].StartsWith("--"))
                    {
                        string key = args[i].Substring(2);
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        {
                            options[key] = args[i + 1];
                            i++;
                        }
                        else
                        {
                            options[key] = "true";
                        }
                    }
                }

                // Only support generate-manifest command
                if (command == "generate-manifest")
                {
                    return HandleGenerateManifest(options);
                }
                else
                {
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Available command: generate-manifest");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static int HandleGenerateManifest(Dictionary<string, string> options)
        {
            string? assemblyPath = GetOptionOrDefault(options, "assembly", (string?)null);
            
            // Always auto-extract version from assembly filename if assembly is provided
            string? assemblyVersion = null;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                string? extractedVersion = ExtractVersionFromAssemblyName(assemblyName);
                
                if (!string.IsNullOrEmpty(extractedVersion))
                {
                    assemblyVersion = extractedVersion;
                    Console.WriteLine($"Extracted version from assembly filename: {assemblyVersion}");
                }
            }
            
            // If still no version, fall back to current assembly version
            if (string.IsNullOrEmpty(assemblyVersion))
            {
                assemblyVersion = GetAssemblyVersion();
                Console.WriteLine($"Using current assembly version: {assemblyVersion}");
            }
            
            // Extract Revit version and determine output directory
            string revitVersion = ExtractRevitVersionFromAssemblyVersion(assemblyVersion);
            string? outputDir = GetOptionOrDefault(options, "output", (string?)null);
            
            // If no output directory specified, use default based on Revit version
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = GetDefaultOutputDir(revitVersion);
                Console.WriteLine($"Using default output directory: {outputDir}");
            }
            
            bool useFixedGuids = GetOptionOrDefault(options, "fixed-guids", "true") != "false";

            AddinManifestTool.GenerateXunitAddinManifest(outputDir, assemblyPath, useFixedGuids, assemblyVersion);
            Console.WriteLine("Xunit manifest generated successfully.");
            return 0;
        }

        /// <summary>
        /// Extracts version from assembly filename using regex
        /// Handles both standard and pre-release version formats
        /// </summary>
        /// <param name="assemblyName">The assembly filename without extension</param>
        /// <returns>The extracted version string, or null if not found</returns>
        private static string? ExtractVersionFromAssemblyName(string assemblyName)
        {
            // Updated regex to handle both standard and pre-release versions
            // Examples:
            // - RevitAddin.Xunit.2025.0.0 -> 2025.0.0
            // - RevitAddin.Xunit.2025.1.0-pullrequest0018.109 -> 2025.1.0-pullrequest0018.109
            var regex = new Regex(@"RevitAddin\.Xunit\.(?<version>\d+\.\d+\.\d+(?:-[^.]+(?:\.\d+)*)?)", RegexOptions.IgnoreCase);
            var match = regex.Match(assemblyName);

            return match.Success ? match.Groups["version"].Value : null;
        }

        private static string? GetOptionOrDefault(Dictionary<string, string> options, string key, string? defaultValue)
        {
            return options.TryGetValue(key, out string? value) ? value : defaultValue;
        }

        private static string GetDefaultOutputDir(string revitVersion)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", revitVersion);
        }

        private static string GetAssemblyVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "2025.0.0.0";
        }

        /// <summary>
        /// Extracts the Revit version from the assembly version (first part of version string)
        /// Handles both standard versions (e.g., "2025.0.0") and pre-release versions (e.g., "2025.1.0-pullrequest0018.103")
        /// </summary>
        /// <param name="assemblyVersion">Assembly version in format RevitVersion.Minor.Patch (e.g., "2025.0.0") or pre-release format</param>
        /// <returns>The Revit version (e.g., "2025")</returns>
        private static string ExtractRevitVersionFromAssemblyVersion(string assemblyVersion)
        {
            // Handle pre-release versions by extracting base version first
            string baseVersion = assemblyVersion.Split('-')[0];
            var parts = baseVersion.Split('.');
            return parts.Length > 0 ? parts[0] : "2025";
        }
    }
}
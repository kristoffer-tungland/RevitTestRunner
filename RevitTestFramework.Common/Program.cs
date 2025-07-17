using System.Reflection;

namespace RevitTestFramework.Common
{
    public class Program
    {
        private const string DefaultRevitVersion = "2025";

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage: RevitTestFramework.Common.exe <command> [options]");
                    Console.WriteLine("Commands: generate-xunit-manifest, generate-nunit-manifest, generate-all-manifests");
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

                // Get common parameters
                string revitVersion = GetOptionOrDefault(options, "revit-version", DefaultRevitVersion);
                string packageVersion = GetOptionOrDefault(options, "package-version", GetAssemblyVersion());
                string outputDir = GetOptionOrDefault(options, "output", GetDefaultOutputDir(revitVersion));
                bool useFixedGuids = GetOptionOrDefault(options, "fixed-guids", "true") != "false";

                // Process command
                if (command == "generate-xunit-manifest")
                {
                    string assemblyPath = GetOptionOrDefault(options, "assembly", null);
                    AddinManifestTool.GenerateXunitAddinManifest(outputDir, assemblyPath, useFixedGuids, packageVersion);
                    Console.WriteLine("Xunit manifest generated successfully.");
                    return 0;
                }
                else if (command == "generate-nunit-manifest")
                {
                    string assemblyPath = GetOptionOrDefault(options, "assembly", null);
                    AddinManifestTool.GenerateNUnitAddinManifest(outputDir, assemblyPath, useFixedGuids, packageVersion);
                    Console.WriteLine("NUnit manifest generated successfully.");
                    return 0;
                }
                else if (command == "generate-all-manifests")
                {
                    AddinManifestTool.GenerateXunitAddinManifest(outputDir, null, useFixedGuids, packageVersion);
                    AddinManifestTool.GenerateNUnitAddinManifest(outputDir, null, useFixedGuids, packageVersion);
                    Console.WriteLine("All manifests generated successfully.");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Unknown command: {command}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private static string GetOptionOrDefault(Dictionary<string, string> options, string key, string defaultValue)
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
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }
    }
}
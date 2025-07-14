using System.CommandLine;
using System.Reflection;

namespace RevitTestFramework.Common;

/// <summary>
/// Main program for RevitTestFramework.Common tools
/// </summary>
public class Program
{
    /// <summary>
    /// Default Revit version to use if not specified
    /// </summary>
    private const string DefaultRevitVersion = "2025";

    /// <summary>
    /// Default package version to use if not specified
    /// </summary>
    private static string DefaultPackageVersion => GetCurrentAssemblyVersion();

    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Revit Test Framework utilities");
        
        // Command for generating the Xunit addin manifest
        var generateXunitManifestCommand = new Command("generate-xunit-manifest", "Generate the addin manifest for RevitAddin.Xunit");
        
        var xunitOutputDirOption = new Option<string?>("--output", "Output directory where to save the addin manifest (if not provided, uses Revit addins folder)");
        xunitOutputDirOption.SetDefaultValue(null);
        
        var xunitRevitVersionOption = new Option<string>("--revit-version", $"Revit version to target (default: {DefaultRevitVersion})");
        xunitRevitVersionOption.SetDefaultValue(DefaultRevitVersion);

        var xunitPackageVersionOption = new Option<string>("--package-version", $"Package version to use in manifest names and GUIDs (default: current assembly version)");
        xunitPackageVersionOption.SetDefaultValue(DefaultPackageVersion);
        
        var xunitAssemblyOption = new Option<string?>("--assembly", "Path to the RevitAddin.Xunit assembly");
        xunitAssemblyOption.SetDefaultValue(null);
        
        var xunitUseFixedGuidsOption = new Option<bool>("--fixed-guids", "Whether to use fixed GUIDs for consistent identification");
        xunitUseFixedGuidsOption.SetDefaultValue(true);
        
        generateXunitManifestCommand.AddOption(xunitOutputDirOption);
        generateXunitManifestCommand.AddOption(xunitRevitVersionOption);
        generateXunitManifestCommand.AddOption(xunitPackageVersionOption);
        generateXunitManifestCommand.AddOption(xunitAssemblyOption);
        generateXunitManifestCommand.AddOption(xunitUseFixedGuidsOption);
        
        generateXunitManifestCommand.SetHandler((outputDir, revitVersion, packageVersion, assemblyPath, useFixedGuids) =>
        {
            try
            {
                string effectiveOutputDir = GetEffectiveOutputDirectory(outputDir, revitVersion);
                AddinManifestTool.GenerateXunitAddinManifest(effectiveOutputDir, assemblyPath, useFixedGuids, packageVersion);
                Console.WriteLine($"Xunit addin manifest v{packageVersion} generated successfully at {effectiveOutputDir}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error generating Xunit addin manifest: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }, xunitOutputDirOption, xunitRevitVersionOption, xunitPackageVersionOption, xunitAssemblyOption, xunitUseFixedGuidsOption);
        
        // Command for generating the NUnit addin manifest
        var generateNUnitManifestCommand = new Command("generate-nunit-manifest", "Generate the addin manifest for RevitAddin.NUnit");
        
        var nunitOutputDirOption = new Option<string?>("--output", "Output directory where to save the addin manifest (if not provided, uses Revit addins folder)");
        nunitOutputDirOption.SetDefaultValue(null);
        
        var nunitRevitVersionOption = new Option<string>("--revit-version", $"Revit version to target (default: {DefaultRevitVersion})");
        nunitRevitVersionOption.SetDefaultValue(DefaultRevitVersion);

        var nunitPackageVersionOption = new Option<string>("--package-version", $"Package version to use in manifest names and GUIDs (default: current assembly version)");
        nunitPackageVersionOption.SetDefaultValue(DefaultPackageVersion);
        
        var nunitAssemblyOption = new Option<string?>("--assembly", "Path to the RevitAddin.NUnit assembly");
        nunitAssemblyOption.SetDefaultValue(null);
        
        var nunitUseFixedGuidsOption = new Option<bool>("--fixed-guids", "Whether to use fixed GUIDs for consistent identification");
        nunitUseFixedGuidsOption.SetDefaultValue(true);
        
        generateNUnitManifestCommand.AddOption(nunitOutputDirOption);
        generateNUnitManifestCommand.AddOption(nunitRevitVersionOption);
        generateNUnitManifestCommand.AddOption(nunitPackageVersionOption);
        generateNUnitManifestCommand.AddOption(nunitAssemblyOption);
        generateNUnitManifestCommand.AddOption(nunitUseFixedGuidsOption);
        
        generateNUnitManifestCommand.SetHandler((outputDir, revitVersion, packageVersion, assemblyPath, useFixedGuids) =>
        {
            try
            {
                string effectiveOutputDir = GetEffectiveOutputDirectory(outputDir, revitVersion);
                AddinManifestTool.GenerateNUnitAddinManifest(effectiveOutputDir, assemblyPath, useFixedGuids, packageVersion);
                Console.WriteLine($"NUnit addin manifest v{packageVersion} generated successfully at {effectiveOutputDir}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error generating NUnit addin manifest: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }, nunitOutputDirOption, nunitRevitVersionOption, nunitPackageVersionOption, nunitAssemblyOption, nunitUseFixedGuidsOption);
        
        // Command to generate both manifests
        var generateAllManifestsCommand = new Command("generate-all-manifests", "Generate both Xunit and NUnit addin manifests");
        
        var allOutputDirOption = new Option<string?>("--output", "Output directory where to save the addin manifests (if not provided, uses Revit addins folder)");
        allOutputDirOption.SetDefaultValue(null);
        
        var allRevitVersionOption = new Option<string>("--revit-version", $"Revit version to target (default: {DefaultRevitVersion})");
        allRevitVersionOption.SetDefaultValue(DefaultRevitVersion);

        var allPackageVersionOption = new Option<string>("--package-version", $"Package version to use in manifest names and GUIDs (default: current assembly version)");
        allPackageVersionOption.SetDefaultValue(DefaultPackageVersion);
        
        var allUseFixedGuidsOption = new Option<bool>("--fixed-guids", "Whether to use fixed GUIDs for consistent identification");
        allUseFixedGuidsOption.SetDefaultValue(true);
        
        generateAllManifestsCommand.AddOption(allOutputDirOption);
        generateAllManifestsCommand.AddOption(allRevitVersionOption);
        generateAllManifestsCommand.AddOption(allPackageVersionOption);
        generateAllManifestsCommand.AddOption(allUseFixedGuidsOption);
        
        generateAllManifestsCommand.SetHandler((outputDir, revitVersion, packageVersion, useFixedGuids) =>
        {
            try
            {
                string effectiveOutputDir = GetEffectiveOutputDirectory(outputDir, revitVersion);
                AddinManifestTool.GenerateXunitAddinManifest(effectiveOutputDir, null, useFixedGuids, packageVersion);
                AddinManifestTool.GenerateNUnitAddinManifest(effectiveOutputDir, null, useFixedGuids, packageVersion);
                Console.WriteLine($"All addin manifests v{packageVersion} generated successfully at {effectiveOutputDir}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error generating addin manifests: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }, allOutputDirOption, allRevitVersionOption, allPackageVersionOption, allUseFixedGuidsOption);

        rootCommand.AddCommand(generateXunitManifestCommand);
        rootCommand.AddCommand(generateNUnitManifestCommand);
        rootCommand.AddCommand(generateAllManifestsCommand);

        return rootCommand.Invoke(args);
    }
    
    /// <summary>
    /// Gets the effective output directory based on user inputs.
    /// If no output directory is specified, uses the standard Revit addins folder.
    /// </summary>
    /// <param name="outputDir">User-specified output directory or null</param>
    /// <param name="revitVersion">Revit version to target</param>
    /// <returns>The full path to the directory where the addin manifests should be saved</returns>
    private static string GetEffectiveOutputDirectory(string? outputDir, string revitVersion)
    {
        if (!string.IsNullOrEmpty(outputDir))
        {
            return outputDir;
        }
        
        // Use the standard Revit addins folder in user's AppData
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "Revit",
            "Addins",
            revitVersion);
    }

    /// <summary>
    /// Gets the current assembly version from the executing assembly
    /// </summary>
    private static string GetCurrentAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
}
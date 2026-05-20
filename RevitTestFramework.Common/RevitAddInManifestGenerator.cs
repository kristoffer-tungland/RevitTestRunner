using System.Xml;
using System.Text.RegularExpressions;

namespace RevitTestFramework.Common;

/// <summary>
/// Generator for Revit addin manifest files
/// </summary>
public static partial class RevitAddInManifestGenerator
{
    /// <summary>
    /// Generates a Revit addin manifest file using direct XML generation
    /// </summary>
    /// <param name="outputPath">Directory where the addin file will be saved</param>
    /// <param name="assemblyPath">Path to the assembly containing the external application</param>
    /// <param name="applicationClassName">Name of the class implementing IExternalApplication</param>
    /// <param name="vendorName">Name of the vendor/developer</param>
    /// <param name="addinName">Name of the addin</param>
    /// <param name="appGuid">Optional fixed GUID for the application (will generate random one if not provided)</param>
    /// <param name="version">Version string to include in the manifest filename</param>
    public static void GenerateAddinManifest(
        string outputPath, 
        string assemblyPath,
        string applicationClassName,
        string vendorName,
        string addinName,
        Guid? appGuid = null,
        string version = "1.0.0")
    {
        string assemblyFullPath = Path.GetFullPath(assemblyPath);
        string assemblyFileName = Path.GetFileName(assemblyPath);
        
        // Extract base name without extension (e.g., "RevitAddin.Xunit.2025.1.0-pullrequest0018.103" from "RevitAddin.Xunit.2025.1.0-pullrequest0018.103.dll")
        string baseClassName = Path.GetFileNameWithoutExtension(assemblyFileName);
        
        // Remove version suffix from the base class name for cleaner namespace (e.g., "RevitAddin.Xunit")
        // This handles both standard versions (2025.0.0) and pre-release versions (2025.1.0-pullrequest0018.103)
        string cleanBaseClassName = AllVersionSuffixRegex().Replace(baseClassName, "");
        
        // Use the normalized version for the manifest filename instead of the original assembly name
        string manifestPath = Path.Combine(outputPath, $"RevitAddin.Xunit.{version}.addin");

        // Create or use provided GUID for the add-in
        var addinAppId = appGuid ?? Guid.NewGuid();
        
        // Create the XML document directly
        var doc = new XmlDocument();
        var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "utf-8", null);
        doc.AppendChild(xmlDeclaration);

        var revitAddIns = doc.CreateElement("RevitAddIns");
        doc.AppendChild(revitAddIns);

        // Application entry
        var addInApplication = doc.CreateElement("AddIn");
        addInApplication.SetAttribute("Type", "Application");
        revitAddIns.AppendChild(addInApplication);

        var appId = doc.CreateElement("AddInId");
        appId.InnerText = addinAppId.ToString().ToUpper(); // Format as uppercase with braces
        addInApplication.AppendChild(appId);

        var appName = doc.CreateElement("Name");
        appName.InnerText = addinName; // Already contains version in the name
        addInApplication.AppendChild(appName);

        var appAssembly = doc.CreateElement("Assembly");
        appAssembly.InnerText = assemblyFullPath;
        addInApplication.AppendChild(appAssembly);  

        // Use the clean base class name as the namespace (e.g., "RevitAddin.Xunit")
        var appFullClassName = doc.CreateElement("FullClassName");
        appFullClassName.InnerText = $"{cleanBaseClassName}.{applicationClassName}";
        addInApplication.AppendChild(appFullClassName);

        var appVendorId = doc.CreateElement("VendorId");
        appVendorId.InnerText = vendorName;
        addInApplication.AppendChild(appVendorId);

        var appVendorDesc = doc.CreateElement("VendorDescription");
        appVendorDesc.InnerText = $"{vendorName} (v{version})";
        addInApplication.AppendChild(appVendorDesc);
        
        // Create the output directory if it doesn't exist
        Directory.CreateDirectory(outputPath);
        
        // Save the XML document
        doc.Save(manifestPath);
        
        Console.WriteLine($"Created addin manifest at {manifestPath}");
    }

    /// <summary>
    /// Regex to match standard version suffixes like .2025.0.0
    /// </summary>
    [GeneratedRegex(@"\.\d{4}\.\d+\.\d+")]
    private static partial Regex AssemblyVersionSuffixRegex();

    /// <summary>
    /// Regex to match all version suffixes including pre-release versions
    /// Matches patterns like .2025.0.0, .2025.1.0-pullrequest0018.103, etc.
    /// </summary>
    [GeneratedRegex(@"\.\d{4}\.\d+\.\d+(?:-[a-zA-Z0-9\-\.]+)?")]
    private static partial Regex AllVersionSuffixRegex();
}
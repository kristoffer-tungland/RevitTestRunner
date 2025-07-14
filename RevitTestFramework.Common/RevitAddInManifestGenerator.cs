using System;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

namespace RevitTestFramework.Common;

/// <summary>
/// Generator for Revit addin manifest files
/// </summary>
public static class RevitAddInManifestGenerator
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
        
        // Extract base name without version and extension
        string baseClassName = Path.GetFileNameWithoutExtension(assemblyFileName);
        baseClassName = Regex.Replace(baseClassName, @"\.v\d+\.\d+\.\d+", ""); // Remove version suffix if present
        
        // Include version in manifest filename
        string manifestPath = Path.Combine(outputPath, $"{baseClassName}.v{version}.addin");

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
        appId.InnerText = addinAppId.ToString("B").ToUpper(); // Format as uppercase with braces
        addInApplication.AppendChild(appId);

        var appName = doc.CreateElement("Name");
        appName.InnerText = addinName; // Already contains version in the name
        addInApplication.AppendChild(appName);

        var appAssembly = doc.CreateElement("Assembly");
        appAssembly.InnerText = assemblyFullPath;
        addInApplication.AppendChild(appAssembly);

        // Extract the namespace from the assembly name (without version)
        string namespaceName = baseClassName;
        var appFullClassName = doc.CreateElement("FullClassName");
        appFullClassName.InnerText = $"{namespaceName}.{applicationClassName}";
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
}
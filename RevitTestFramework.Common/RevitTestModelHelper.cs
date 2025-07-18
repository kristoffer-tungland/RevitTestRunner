using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitTestFramework.Common;

public static class RevitTestModelHelper
{

    public static Document OpenModel(UIApplication uiApp, string? localPath, string? projectGuid, string? modelGuid)
    {
        if (string.IsNullOrEmpty(localPath) && (string.IsNullOrEmpty(projectGuid) || string.IsNullOrEmpty(modelGuid)))
        {
            throw new ArgumentException("Either localPath or both projectGuid and modelGuid must be provided.");
        }
        
        try
        {
            if (!string.IsNullOrEmpty(localPath))
            {
                return OpenLocalModel(uiApp, localPath);
            }
            else
            {
                return OpenCloudModel(uiApp, projectGuid!, modelGuid!);
            }
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            // Wrap with more specific context about which operation failed
            var operation = !string.IsNullOrEmpty(localPath) ? "local model" : "cloud model";
            var identifier = !string.IsNullOrEmpty(localPath) ? localPath : $"{projectGuid}:{modelGuid}";
            throw new InvalidOperationException($"Failed to open {operation} '{identifier}': {ex.Message}", ex);
        }
    }

    private static Document OpenLocalModel(UIApplication uiApp, string localPath)
    {
        try
        {
            if (!File.Exists(localPath))
            {
                throw new FileNotFoundException($"Revit model file not found at path: {localPath}");
            }

            var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(localPath);
            var app = uiApp.Application;

            var opts = new OpenOptions();
            opts.DetachFromCentralOption = DetachFromCentralOption.DoNotDetach;
            opts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets));
            opts.Audit = false;

            Debug.WriteLine($"Opening local model: {localPath}");
            var doc = app.OpenDocumentFile(modelPath, opts);

            if (doc == null)
            {
                throw new InvalidOperationException($"Revit returned null document when opening local model at: {localPath}");
            }

            Debug.WriteLine($"Successfully opened local model: {doc.Title}");
            return doc;
        }
        catch (Exception ex) when (!(ex is FileNotFoundException))
        {
            throw new InvalidOperationException($"Failed to open local model '{localPath}': {ex.Message}", ex);
        }
    }

    private static Document OpenCloudModel(UIApplication uiApp, string projectGuid, string modelGuid)
    {
        try
        {
            if (!Guid.TryParse(projectGuid, out var projGuid))
            {
                throw new ArgumentException($"Invalid project GUID format: '{projectGuid}'");
            }
            
            if (!Guid.TryParse(modelGuid, out var modGuid))
            {
                throw new ArgumentException($"Invalid model GUID format: '{modelGuid}'");
            }

            var cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(ModelPathUtils.CloudRegionUS, projGuid, modGuid);
            var app = uiApp.Application;

            var openOpts = new OpenOptions();
            openOpts.DetachFromCentralOption = DetachFromCentralOption.DoNotDetach;
            openOpts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets));

            Debug.WriteLine($"Opening cloud model: {projectGuid}:{modelGuid}");
            var doc = app.OpenDocumentFile(cloudPath, openOpts);

            if (doc == null)
            {
                throw new InvalidOperationException($"Revit returned null document when opening cloud model: {projectGuid}:{modelGuid}");
            }

            Debug.WriteLine($"Successfully opened cloud model: {doc.Title}");
            return doc;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new InvalidOperationException($"Failed to open cloud model '{projectGuid}:{modelGuid}': {ex.Message}", ex);
        }
    }
}
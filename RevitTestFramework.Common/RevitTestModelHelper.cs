using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
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
        if (!string.IsNullOrEmpty(localPath))
        {
            return OpenLocalModel(uiApp, localPath);
        }
        else
        {
            return OpenCloudModel(uiApp, projectGuid!, modelGuid!);
        }
    }

    private static Document OpenLocalModel(UIApplication uiApp, string localPath)
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

        System.Diagnostics.Debug.WriteLine($"Opening model on UI thread: {localPath}");
        var doc = app.OpenDocumentFile(modelPath, opts);

        if (doc == null)
        {
            throw new InvalidOperationException($"OpenDocumentFile returned null for path: {localPath}");
        }

        System.Diagnostics.Debug.WriteLine($"Successfully opened model on UI thread: {doc.Title}");
        return doc;
    }

    private static Document OpenCloudModel(UIApplication uiApp, string projectGuid, string modelGuid)
    {
        var projGuid = new Guid(projectGuid);
        var modGuid = new Guid(modelGuid);
        var cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(ModelPathUtils.CloudRegionUS, projGuid, modGuid);
        var app = uiApp.Application;

        var openOpts = new OpenOptions();
        openOpts.DetachFromCentralOption = DetachFromCentralOption.DoNotDetach;
        openOpts.SetOpenWorksetsConfiguration(new WorksetConfiguration(WorksetConfigurationOption.CloseAllWorksets));

        System.Diagnostics.Debug.WriteLine($"Opening cloud model on UI thread: {projectGuid}:{modelGuid}");
        var doc = app.OpenDocumentFile(cloudPath, openOpts);

        if (doc == null)
        {
            throw new InvalidOperationException($"OpenDocumentFile returned null for cloud model: {projectGuid}:{modelGuid}");
        }

        System.Diagnostics.Debug.WriteLine($"Successfully opened cloud model on UI thread: {doc.Title}");
        return doc;
    }
}
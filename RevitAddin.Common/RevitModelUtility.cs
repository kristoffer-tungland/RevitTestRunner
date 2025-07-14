using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitTestFramework.Common;

namespace RevitAddin.Common;

public static class RevitModelUtility
{
    private static readonly Dictionary<string, Document> _openDocs = new();
    private const string LocalPrefix = "local:";

    public static Document EnsureModelOpen(UIApplication uiApplication, string projectGuid, string modelGuid)
    {
        if (uiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        var key = $"{projectGuid}:{modelGuid}";
        if (_openDocs.TryGetValue(key, out var doc) && doc.IsValidObject)
        {
            RevitModelService.CurrentDocument = doc;
            return doc;
        }

        var projGuid = new Guid(projectGuid);
        var modGuid = new Guid(modelGuid);
        var cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(ModelPathUtils.CloudRegionUS, projGuid, modGuid);
        var app = uiApplication.Application;
        var openOpts = new OpenOptions();
        doc = app.OpenDocumentFile(cloudPath, openOpts);
        _openDocs[key] = doc;
        RevitModelService.CurrentDocument = doc;
        return doc;
    }

    public static Document EnsureModelOpen(UIApplication uiApplication, string localPath)
    {
        if (uiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        var key = LocalPrefix + localPath;
        if (_openDocs.TryGetValue(key, out var doc) && doc.IsValidObject)
        {
            RevitModelService.CurrentDocument = doc;
            return doc;
        }

        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(localPath);
        var app = uiApplication.Application;
        var opts = new OpenOptions();
        doc = app.OpenDocumentFile(modelPath, opts);
        _openDocs[key] = doc;
        RevitModelService.CurrentDocument = doc;
        return doc;
    }
}
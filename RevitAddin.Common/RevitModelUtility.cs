using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitTestFramework.Common;

namespace RevitAddin.Common;

public static class RevitModelUtility
{
    private static readonly Dictionary<string, Document> _openDocs = new();
    private const string LocalPrefix = "local:";
    private static ModelOpeningExternalEvent? _modelOpener;
    private static UIApplication? _cachedUIApp;

    public static void Initialize(UIApplication uiApp, ModelOpeningExternalEvent modelOpener)
    {
        _cachedUIApp = uiApp;
        _modelOpener = modelOpener;
    }

    public static Document EnsureModelOpen(UIApplication uiApplication, string projectGuid, string modelGuid)
    {
        if (uiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        if (_modelOpener == null)
            throw new InvalidOperationException("Model opener not initialized. Call Initialize() first.");

        var key = $"{projectGuid}:{modelGuid}";
        if (_openDocs.TryGetValue(key, out var doc) && doc.IsValidObject)
        {
            RevitModelService.CurrentDocument = doc;
            return doc;
        }

        try
        {
            // Use ExternalEvent to ensure execution on UI thread (synchronous call)
            doc = _modelOpener.OpenModelSync(uiApplication, projectGuid, modelGuid);
            
            _openDocs[key] = doc;
            RevitModelService.CurrentDocument = doc;
            return doc;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open cloud model {projectGuid}:{modelGuid}", ex);
        }
    }

    public static Document EnsureModelOpen(UIApplication uiApplication, string localPath)
    {
        if (uiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        if (_modelOpener == null)
            throw new InvalidOperationException("Model opener not initialized. Call Initialize() first.");

        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Local path cannot be null or empty", nameof(localPath));

        // Normalize the path
        localPath = Path.GetFullPath(localPath);

        var key = LocalPrefix + localPath;
        if (_openDocs.TryGetValue(key, out var existingDoc) && existingDoc.IsValidObject)
        {
            RevitModelService.CurrentDocument = existingDoc;
            return existingDoc;
        }

        try
        {
            // Use ExternalEvent to ensure execution on UI thread (synchronous call)
            var doc = _modelOpener.OpenModelSync(uiApplication, localPath);
            
            _openDocs[key] = doc;
            RevitModelService.CurrentDocument = doc;
            return doc;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to open model at {localPath}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"{errorMsg}\nStack trace: {ex.StackTrace}");
            throw new InvalidOperationException(errorMsg, ex);
        }
    }

    public static void CleanupOpenDocuments()
    {
        foreach (var kvp in _openDocs.ToList())
        {
            if (kvp.Value?.IsValidObject == true)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Cleaning up document reference: {kvp.Value.Title}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
                }
            }
        }
        _openDocs.Clear();
        RevitModelService.CurrentDocument = null;
        _modelOpener?.Dispose();
        _modelOpener = null;
    }
}
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitTestFramework.Common;

public class ModelOpeningExternalEvent : IExternalEventHandler, IDisposable
{
    private readonly object _lockObject = new object();
    private readonly ManualResetEventSlim _waitHandle = new ManualResetEventSlim(false);
    private Document? _result;
    private Exception? _exception;
    private string? _localPath;
    private string? _projectGuid;
    private string? _modelGuid;
    private readonly ExternalEvent _externalEvent;

    public ModelOpeningExternalEvent()
    {
        // Create the ExternalEvent once during construction
        // This must be called during standard API execution context
        _externalEvent = ExternalEvent.Create(this);
    }

    public string GetName() => nameof(ModelOpeningExternalEvent);

    public void Execute(UIApplication app)
    {
        lock (_lockObject)
        {
            try
            {
                Document? doc = null;
                
                if (!string.IsNullOrEmpty(_localPath))
                {
                    doc = OpenLocalModel(app, _localPath);
                }
                else if (!string.IsNullOrEmpty(_projectGuid) && !string.IsNullOrEmpty(_modelGuid))
                {
                    doc = OpenCloudModel(app, _projectGuid, _modelGuid);
                }
                
                _result = doc ?? throw new InvalidOperationException("Failed to open document");
                _exception = null;
            }
            catch (Exception ex)
            {
                _result = null;
                _exception = ex;
            }
            finally
            {
                _waitHandle.Set();
            }
        }
    }

    public Document OpenModelSync(UIApplication uiApp, string localPath)
    {
        lock (_lockObject)
        {
            _result = null;
            _exception = null;
            _localPath = localPath;
            _projectGuid = null;
            _modelGuid = null;
            _waitHandle.Reset();

            // Raise the external event
            _externalEvent.Raise();
        }

        // Wait for the external event to complete
        _waitHandle.Wait();

        lock (_lockObject)
        {
            if (_exception != null)
                throw _exception;
            
            return _result ?? throw new InvalidOperationException("Model opening failed with no result");
        }
    }

    public Document OpenModelSync(UIApplication uiApp, string projectGuid, string modelGuid)
    {
        lock (_lockObject)
        {
            _result = null;
            _exception = null;
            _localPath = null;
            _projectGuid = projectGuid;
            _modelGuid = modelGuid;
            _waitHandle.Reset();

            // Raise the external event
            _externalEvent.Raise();
        }

        // Wait for the external event to complete
        _waitHandle.Wait();

        lock (_lockObject)
        {
            if (_exception != null)
                throw _exception;
            
            return _result ?? throw new InvalidOperationException("Model opening failed with no result");
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

    public void Dispose()
    {
        _waitHandle?.Dispose();
        _externalEvent?.Dispose();
    }
}
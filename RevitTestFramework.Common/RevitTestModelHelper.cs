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






















    private static readonly AsyncLocal<TransactionGroup?> _group = new();

    public static Document? EnsureModelAndStartGroup(
        string? localPath,
        string? projectGuid,
        string? modelGuid,
        Func<string, Document> openLocal,
        Func<string, string, Document> openCloud,
        string testName)
    {
        Document? doc = null;
        if (localPath != null)
            doc = openLocal(localPath);
        else if (projectGuid != null && modelGuid != null)
            doc = openCloud(projectGuid, modelGuid);

        if (doc != null)
        {
            var tg = new TransactionGroup(doc, $"Test: {testName}");
            tg.Start();
            _group.Value = tg;
        }
        return doc;
    }

    public static void RollBackTransactionGroup()
    {
        var tg = _group.Value;
        if (tg != null)
        {
            tg.RollBack();
            tg.Dispose();
            _group.Value = null;
        }
    }
}

public class RevitTestExecutionHandler
{
    private readonly Type _testClass;
    private readonly object[] _constructorArguments;
    private readonly MethodInfo _testMethod;
    private readonly object[] _testMethodArguments;
    
    public Exception? Exception { get; private set; }

    public RevitTestExecutionHandler(Type testClass, object[] constructorArguments, 
        MethodInfo testMethod, object[] testMethodArguments)
    {
        _testClass = testClass;
        _constructorArguments = constructorArguments;
        _testMethod = testMethod;
        _testMethodArguments = testMethodArguments;
    }

    public decimal Execute(UIApplication app)
    {
        var timer = new Stopwatch();
        timer.Start();
        
        try
        {
            // Create test instance
            var testInstance = Activator.CreateInstance(_testClass, _constructorArguments);
            
            // Invoke the test method
            var result = _testMethod.Invoke(testInstance, _testMethodArguments);
            
            // Handle async test methods
            if (result is Task task)
            {
                task.Wait();
            }
            
            timer.Stop();
            return timer.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            timer.Stop();
            Exception = ex.InnerException ?? ex;
            return timer.ElapsedMilliseconds;
        }
    }
}
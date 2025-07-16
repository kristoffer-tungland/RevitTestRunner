using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitTestFramework.Common;

public static class RevitTestModelHelper
{
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

/// <summary>
/// Abstraction for creating external events in the test framework
/// </summary>
public static class RevitTestExternalEventUtility
{
    private static Func<IExternalEventHandler, ExternalEvent>? _externalEventFactory;
    private static IRevitTestExternalEventPool? _eventPool;

    /// <summary>
    /// Initialize the external event factory (called by the addin infrastructure)
    /// </summary>
    public static void Initialize(Func<IExternalEventHandler, ExternalEvent> factory)
    {
        _externalEventFactory = factory;
    }

    /// <summary>
    /// Set the external event pool for use during test execution
    /// </summary>
    public static void SetEventPool(IRevitTestExternalEventPool pool)
    {
        _eventPool = pool;
    }

    /// <summary>
    /// Clear the external event pool
    /// </summary>
    public static void ClearEventPool()
    {
        _eventPool = null;
    }

    /// <summary>
    /// Create an external event for the given handler
    /// </summary>
    public static ExternalEvent CreateExternalEvent(IExternalEventHandler handler)
    {
        // If we have an event pool, try to use it first (this avoids creating events on background threads)
        if (_eventPool != null)
        {
            switch (handler)
            {
                case RevitModelSetupHandler setupHandler:
                    return _eventPool.GetModelSetupEvent(setupHandler);
                case RevitModelCleanupHandler cleanupHandler:
                    return _eventPool.GetModelCleanupEvent(cleanupHandler);
                case RevitTestExecutionHandler testHandler:
                    return _eventPool.GetTestExecutionEvent(testHandler);
            }
        }

        // Fallback to direct creation (this will fail if not on UI thread)
        if (_externalEventFactory == null)
            throw new InvalidOperationException("External event factory not initialized. Call Initialize() first.");
        
        return _externalEventFactory(handler);
    }
}

/// <summary>
/// Interface for external event pool
/// </summary>
public interface IRevitTestExternalEventPool
{
    ExternalEvent GetModelSetupEvent(RevitModelSetupHandler handler);
    ExternalEvent GetModelCleanupEvent(RevitModelCleanupHandler handler);
    ExternalEvent GetTestExecutionEvent(RevitTestExecutionHandler handler);
    void ReturnModelSetupEvent(ExternalEvent evt);
    void ReturnModelCleanupEvent(ExternalEvent evt);
    void ReturnTestExecutionEvent(ExternalEvent evt);
}

/// <summary>
/// External event handler for setting up Revit model on UI thread
/// </summary>
public class RevitModelSetupHandler : IExternalEventHandler
{
    private readonly string? _localPath;
    private readonly string? _projectGuid;
    private readonly string? _modelGuid;
    private readonly string _methodName;
    private TaskCompletionSource<Document?>? _tcs;

    public RevitModelSetupHandler(string? localPath, string? projectGuid, string? modelGuid, string methodName)
    {
        _localPath = localPath;
        _projectGuid = projectGuid;
        _modelGuid = modelGuid;
        _methodName = methodName;
    }

    public void SetCompletionSource(TaskCompletionSource<Document?> tcs)
    {
        _tcs = tcs;
    }

    public void Execute(UIApplication app)
    {
        try
        {
            var document = RevitTestModelHelper.EnsureModelAndStartGroup(
                _localPath,
                _projectGuid,
                _modelGuid,
                RevitModelService.OpenLocalModel!,
                RevitModelService.OpenCloudModel!,
                _methodName);
            
            _tcs?.SetResult(document);
        }
        catch (Exception ex)
        {
            _tcs?.SetException(ex);
        }
    }

    public string GetName() => nameof(RevitModelSetupHandler);
}

/// <summary>
/// External event handler for cleaning up Revit model on UI thread
/// </summary>
public class RevitModelCleanupHandler : IExternalEventHandler
{
    private TaskCompletionSource<bool>? _tcs;

    public void SetCompletionSource(TaskCompletionSource<bool> tcs)
    {
        _tcs = tcs;
    }

    public void Execute(UIApplication app)
    {
        try
        {
            RevitTestModelHelper.RollBackTransactionGroup();
            _tcs?.SetResult(true);
        }
        catch (Exception ex)
        {
            _tcs?.SetException(ex);
        }
    }

    public string GetName() => nameof(RevitModelCleanupHandler);
}

/// <summary>
/// External event handler for executing test methods on UI thread
/// </summary>
public class RevitTestExecutionHandler : IExternalEventHandler
{
    private readonly Type _testClass;
    private readonly object[] _constructorArguments;
    private readonly MethodInfo _testMethod;
    private readonly object[] _testMethodArguments;
    private TaskCompletionSource<decimal>? _tcs;
    
    public Exception? Exception { get; private set; }

    public RevitTestExecutionHandler(Type testClass, object[] constructorArguments, 
        MethodInfo testMethod, object[] testMethodArguments)
    {
        _testClass = testClass;
        _constructorArguments = constructorArguments;
        _testMethod = testMethod;
        _testMethodArguments = testMethodArguments;
    }

    public void SetCompletionSource(TaskCompletionSource<decimal> tcs)
    {
        _tcs = tcs;
    }

    public void Execute(UIApplication app)
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
            _tcs?.SetResult(timer.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            timer.Stop();
            Exception = ex.InnerException ?? ex;
            _tcs?.SetResult(timer.ElapsedMilliseconds);
        }
    }

    public string GetName() => nameof(RevitTestExecutionHandler);
}
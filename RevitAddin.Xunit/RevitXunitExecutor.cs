using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System.Text.Json;
using System.Threading.Tasks;

namespace RevitAddin.Xunit;

/// <summary>
/// Helper class to set up and manage Revit API infrastructure for testing.
/// This must be created and disposed on the main Revit UI thread.
/// </summary>
public class RevitTestInfrastructure : IDisposable
{
    public ModelOpeningExternalEvent ModelOpener { get; }
    public RevitTestExternalEventPool EventPool { get; }

    public RevitTestInfrastructure(UIApplication uiApp)
    {
        ModelOpener = new ModelOpeningExternalEvent();
        EventPool = new RevitTestExternalEventPool(uiApp);
    }

    public void Dispose()
    {
        EventPool.Dispose();
        ModelOpener.Dispose();
    }
}

public static class RevitXunitExecutor
{
    /// <summary>
    /// Sets up the required Revit API infrastructure (ExternalEvents, etc.).
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static RevitTestInfrastructure SetupInfrastructure(UIApplication uiApp)
    {
        var infrastructure = new RevitTestInfrastructure(uiApp);
        
        // Initialize the RevitModelUtility in this ALC with the new model opener
        RevitModelUtility.Initialize(uiApp, infrastructure.ModelOpener);

        // Set up model service with our local handlers
        RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
        RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
        
        // Set the event pool for the test framework to use
        RevitTestExternalEventUtility.SetEventPool(infrastructure.EventPool);

        return infrastructure;
    }

    /// <summary>
    /// Cleans up the Revit API infrastructure after tests have run.
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static void TeardownInfrastructure(RevitTestInfrastructure infrastructure)
    {
        RevitTestExternalEventUtility.ClearEventPool();
        RevitModelService.CancellationToken = CancellationToken.None;
        RevitModelUtility.CleanupOpenDocuments();
        infrastructure.Dispose();
    }

    public static async Task ExecuteTestsInRevitAsync(string commandJson, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Deserialize the command in the isolated context to avoid cross-ALC type issues
        var command = JsonSerializer.Deserialize<PipeCommand>(commandJson)
            ?? throw new InvalidOperationException("Failed to deserialize PipeCommand");

        RevitModelService.CancellationToken = cancellationToken;
        var methods = command.TestMethods;

        // Execute tests on a background thread to avoid blocking Revit UI
        // IMPORTANT: Use await instead of .Wait() to prevent UI thread deadlock
        await Task.Run(async () =>
        {
            try
            {
                // Now we can use xUnit directly since we're running in the isolated ALC!
                var assemblyElement = new XElement("assembly");
                using var controller = new XunitFrontController(AppDomainSupport.Denied, testAssemblyPath, shadowCopy: false);
                var discoveryOptions = TestFrameworkOptions.ForDiscovery();
                
                var configuration = new TestAssemblyConfiguration
                {
                    ParallelizeAssembly = false,
                    ParallelizeTestCollections = false,
                };

                var executionOptions = TestFrameworkOptions.ForExecution(configuration);

                List<ITestCase> testCases;
                var discoverySink = new TestDiscoverySink();
                controller.Find(false, discoverySink, discoveryOptions);
                discoverySink.Finished.WaitOne();
                testCases = discoverySink.TestCases.ToList();

                // Filter test cases if specific methods are requested
                if (methods != null && methods.Length > 0)
                {
                    testCases = testCases.Where(tc => methods.Contains(tc.TestMethod.TestClass.Class.Name + "." + tc.TestMethod.Method.Name)).ToList();
                }

                using var visitor = new StreamingXmlTestExecutionVisitor(writer, assemblyElement, () => cancellationToken.IsCancellationRequested);
                controller.RunTests(testCases, visitor, executionOptions);
                visitor.Finished.WaitOne();

                string resultXml = new XDocument(assemblyElement).ToString();
                var fileName = $"RevitXunitResults_{Guid.NewGuid():N}.xml";
                var resultsPath = Path.Combine(Path.GetTempPath(), fileName);
                File.WriteAllText(resultsPath, resultXml);
                writer.WriteLine("END");
                writer.Flush();
            }
            catch (Exception ex)
            {
                HandleTestExecutionException(ex, command.TestMethods, writer);
            }
        }, cancellationToken);
    }

    private static void HandleTestExecutionException(Exception ex, string[]? methods, StreamWriter writer)
    {
        try
        {
            // Create a failure result message for any tests that were supposed to run
            var failureMessage = new PipeTestResultMessage
            {
                Name = methods?.Length > 0 ? string.Join(", ", methods) : "TestExecution",
                Outcome = "Failed",
                Duration = 0,
                ErrorMessage = $"Test execution failed: {ex.Message}",
                ErrorStackTrace = ex.ToString()
            };

            // Report the failure
            var json = JsonSerializer.Serialize(failureMessage);
            writer.WriteLine(json);
            writer.WriteLine("END");
            writer.Flush();

            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Test execution failed with exception: {ex}");
        }
        catch (Exception writeEx)
        {
            // If we can't even write the error, log it
            System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Failed to write error message: {writeEx}");
            System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Original exception: {ex}");
        }
    }
}

/// <summary>
/// Pool of pre-created ExternalEvents for use in background threads
/// </summary>
public class RevitTestExternalEventPool : IRevitTestExternalEventPool, IDisposable
{
    private readonly Queue<ExternalEvent> _modelSetupEvents = new();
    private readonly Queue<ExternalEvent> _modelCleanupEvents = new();
    private readonly Queue<ExternalEvent> _testExecutionEvents = new();
    private readonly object _lock = new();
    private bool _disposed = false;

    public RevitTestExternalEventPool(UIApplication uiApp)
    {
        // Pre-create a pool of ExternalEvents while on UI thread
        // Create enough events to handle concurrent test execution
        const int poolSize = 10;

        for (int i = 0; i < poolSize; i++)
        {
            _modelSetupEvents.Enqueue(ExternalEvent.Create(new PooledRevitModelSetupHandler()));
            _modelCleanupEvents.Enqueue(ExternalEvent.Create(new PooledRevitModelCleanupHandler()));
            _testExecutionEvents.Enqueue(ExternalEvent.Create(new PooledRevitTestExecutionHandler()));
        }
    }

    public ExternalEvent GetModelSetupEvent(RevitModelSetupHandler handler)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RevitTestExternalEventPool));
            
            if (_modelSetupEvents.Count == 0)
                throw new InvalidOperationException("No model setup events available in pool");
                
            var evt = _modelSetupEvents.Dequeue();
            PooledRevitModelSetupHandler.SetCurrentHandler(handler);
            return evt;
        }
    }

    public ExternalEvent GetModelCleanupEvent(RevitModelCleanupHandler handler)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RevitTestExternalEventPool));
            
            if (_modelCleanupEvents.Count == 0)
                throw new InvalidOperationException("No model cleanup events available in pool");
                
            var evt = _modelCleanupEvents.Dequeue();
            PooledRevitModelCleanupHandler.SetCurrentHandler(handler);
            return evt;
        }
    }

    public ExternalEvent GetTestExecutionEvent(RevitTestExecutionHandler handler)
    {
        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RevitTestExternalEventPool));
            
            if (_testExecutionEvents.Count == 0)
                throw new InvalidOperationException("No test execution events available in pool");
                
            var evt = _testExecutionEvents.Dequeue();
            PooledRevitTestExecutionHandler.SetCurrentHandler(handler);
            return evt;
        }
    }

    public void ReturnModelSetupEvent(ExternalEvent evt)
    {
        lock (_lock)
        {
            if (!_disposed) 
            {
                PooledRevitModelSetupHandler.SetCurrentHandler(null);
                _modelSetupEvents.Enqueue(evt);
            }
        }
    }

    public void ReturnModelCleanupEvent(ExternalEvent evt)
    {
        lock (_lock)
        {
            if (!_disposed) 
            {
                PooledRevitModelCleanupHandler.SetCurrentHandler(null);
                _modelCleanupEvents.Enqueue(evt);
            }
        }
    }

    public void ReturnTestExecutionEvent(ExternalEvent evt)
    {
        lock (_lock)
        {
            if (!_disposed) 
            {
                PooledRevitTestExecutionHandler.SetCurrentHandler(null);
                _testExecutionEvents.Enqueue(evt);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            // Dispose all ExternalEvents
            while (_modelSetupEvents.Count > 0)
                _modelSetupEvents.Dequeue().Dispose();
            while (_modelCleanupEvents.Count > 0)
                _modelCleanupEvents.Dequeue().Dispose();
            while (_testExecutionEvents.Count > 0)
                _testExecutionEvents.Dequeue().Dispose();
        }
    }
}

/// <summary>
/// Pooled handler for model setup - uses dependency injection for actual work
/// </summary>
public class PooledRevitModelSetupHandler : IExternalEventHandler
{
    private static RevitModelSetupHandler? _currentHandler;
    private static readonly object _lock = new();

    public static void SetCurrentHandler(RevitModelSetupHandler? handler)
    {
        lock (_lock)
        {
            _currentHandler = handler;
        }
    }

    public void Execute(UIApplication app)
    {
        lock (_lock)
        {
            _currentHandler?.Execute(app);
        }
    }

    public string GetName() => nameof(PooledRevitModelSetupHandler);
}

/// <summary>
/// Pooled handler for model cleanup - uses dependency injection for actual work
/// </summary>
public class PooledRevitModelCleanupHandler : IExternalEventHandler
{
    private static RevitModelCleanupHandler? _currentHandler;
    private static readonly object _lock = new();

    public static void SetCurrentHandler(RevitModelCleanupHandler? handler)
    {
        lock (_lock)
        {
            _currentHandler = handler;
        }
    }

    public void Execute(UIApplication app)
    {
        lock (_lock)
        {
            _currentHandler?.Execute(app);
        }
    }

    public string GetName() => nameof(PooledRevitModelCleanupHandler);
}

/// <summary>
/// Pooled handler for test execution - uses dependency injection for actual work
/// </summary>
public class PooledRevitTestExecutionHandler : IExternalEventHandler
{
    private static RevitTestExecutionHandler? _currentHandler;
    private static readonly object _lock = new();

    public static void SetCurrentHandler(RevitTestExecutionHandler? handler)
    {
        lock (_lock)
        {
            _currentHandler = handler;
        }
    }

    public void Execute(UIApplication app)
    {
        lock (_lock)
        {
            _currentHandler?.Execute(app);
        }
    }

    public string GetName() => nameof(PooledRevitTestExecutionHandler);
}

// Clean xUnit integration without reflection
internal class StreamingXmlTestExecutionVisitor : XmlTestExecutionVisitor
{
    private readonly StreamWriter _writer;

    public StreamingXmlTestExecutionVisitor(StreamWriter writer, XElement assemblyElement, Func<bool> cancelThunk)
        : base(assemblyElement, cancelThunk)
    {
        _writer = writer;
    }

    private void Send(PipeTestResultMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        _writer.WriteLine(json);
        _writer.Flush();
    }

    protected override bool Visit(ITestPassed testPassed)
    {
        Send(new PipeTestResultMessage
        {
            Name = testPassed.Test.DisplayName,
            Outcome = "Passed",
            Duration = (double)testPassed.ExecutionTime
        });
        return base.Visit(testPassed);
    }

    protected override bool Visit(ITestFailed testFailed)
    {
        Send(new PipeTestResultMessage
        {
            Name = testFailed.Test.DisplayName,
            Outcome = "Failed",
            Duration = (double)testFailed.ExecutionTime,
            ErrorMessage = string.Join(Environment.NewLine, testFailed.Messages ?? Array.Empty<string>()),
            ErrorStackTrace = string.Join(Environment.NewLine, testFailed.StackTraces ?? Array.Empty<string>())
        });
        return base.Visit(testFailed);
    }

    protected override bool Visit(ITestSkipped testSkipped)
    {
        Send(new PipeTestResultMessage
        {
            Name = testSkipped.Test.DisplayName,
            Outcome = "Skipped",
            Duration = 0,
            ErrorMessage = testSkipped.Reason
        });
        return base.Visit(testSkipped);
    }
}

// TestDiscoverySink for clean xUnit integration
internal class TestDiscoverySink : global::Xunit.Sdk.LongLivedMarshalByRefObject, IMessageSink
{
    public ManualResetEvent Finished { get; } = new ManualResetEvent(false);
    public List<ITestCase> TestCases { get; } = new();

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestCaseDiscoveryMessage testCaseDiscovered:
                TestCases.Add(testCaseDiscovered.TestCase);
                break;
            case IDiscoveryCompleteMessage:
                Finished.Set();
                break;
        }
        return true;
    }
}
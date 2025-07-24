using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;
using System.Text.Json;
using System.Diagnostics;

namespace RevitAddin.Xunit;

public static class RevitXunitExecutor
{
    private static RevitTestFramework.Common.ILogger _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitXunitExecutor));
    private static StreamWriter? _currentPipeWriter;

    /// <summary>
    /// Sets the pipe writer for the current test execution session
    /// This allows SetupInfrastructure to configure pipe-aware logging for RevitTestModelHelper
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to use for log forwarding</param>
    public static void SetPipeWriter(StreamWriter? pipeWriter)
    {
        _currentPipeWriter = pipeWriter;
    }

    /// <summary>
    /// Sets up the required Revit API infrastructure (ExternalEvents, etc.).
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static void SetupInfrastructure(UIApplication uiApp)
    {
        try
        {
            // Configure pipe-aware logging if a pipe writer is available
            if (_currentPipeWriter != null)
            {
                _logger = RevitTestFramework.Common.PipeAwareLogger.ForContext(typeof(RevitXunitExecutor), _currentPipeWriter);
                
                // IMPORTANT: Set up pipe-aware logging for RevitTestModelHelper in the correct context
                RevitTestFramework.Common.RevitTestModelHelper.SetPipeAwareLogger(_currentPipeWriter);
                
                // IMPORTANT: Set up pipe-aware logging for test case runners
                RevitTestFramework.Xunit.RevitXunitTestCaseRunner.SetPipeAwareLogger(_currentPipeWriter);
                RevitTestFramework.Xunit.RevitUITestRunner.SetPipeAwareLogger(_currentPipeWriter);
                
                _logger.LogDebug("RevitTestModelHelper configured with pipe-aware logger from SetupInfrastructure");
                _logger.LogDebug("Test case runners configured with pipe-aware logger from SetupInfrastructure");
            }
            
            _logger.LogInformation("Setting up Revit test infrastructure");
            RevitTestInfrastructure.Setup(uiApp);
            _logger.LogInformation("Revit test infrastructure setup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup Revit test infrastructure");
            throw;
        }
    }

    /// <summary>
    /// Cleans up the Revit API infrastructure after tests have run.
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static void TeardownInfrastructure()
    {
        try
        {
            _logger.LogInformation("Tearing down Revit test infrastructure");
            RevitTestInfrastructure.Dispose();
            
            // Reset RevitTestModelHelper to file-only logging
            RevitTestFramework.Common.RevitTestModelHelper.SetPipeAwareLogger(null);
            
            // Reset test case runners to file-only logging
            RevitTestFramework.Xunit.RevitXunitTestCaseRunner.SetPipeAwareLogger(null);
            RevitTestFramework.Xunit.RevitUITestRunner.SetPipeAwareLogger(null);
            
            _logger.LogDebug("RevitTestModelHelper reset to file-only logging from TeardownInfrastructure");
            _logger.LogDebug("Test case runners reset to file-only logging from TeardownInfrastructure");
            
            // Reset local pipe writer reference
            _currentPipeWriter = null;
            _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitXunitExecutor));
            
            _logger.LogInformation("Revit test infrastructure teardown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to teardown Revit test infrastructure");
            throw;
        }
    }

    public static async Task ExecuteTestsInRevitAsync(string commandJson, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Upgrade to pipe-aware logger for this execution
        var pipeAwareLogger = RevitTestFramework.Common.PipeAwareLogger.ForContext(typeof(RevitXunitExecutor), writer);
        
        try
        {
            pipeAwareLogger.LogInformation($"Starting test execution for assembly: {testAssemblyPath}");
            
            // Deserialize the command in the isolated context to avoid cross-ALC type issues
            var command = JsonSerializer.Deserialize<PipeCommand>(commandJson)
                ?? throw new InvalidOperationException("Failed to deserialize PipeCommand");

            var methodsStr = command.TestMethods != null ? string.Join(", ", command.TestMethods) : "All";
            pipeAwareLogger.LogDebug($"Test execution command - Debug: {command.Debug}, Methods: {methodsStr}");

            // Handle debug mode if enabled
            if (command.Debug)
            {
                pipeAwareLogger.LogInformation("Debug mode enabled - debugger can now be attached to Revit process");
                
                // If debugger is not already attached, provide helpful information
                if (!Debugger.IsAttached)
                {
                    var processId = Process.GetCurrentProcess().Id;
                    pipeAwareLogger.LogInformation($"To debug tests, attach debugger to Revit.exe process ID: {processId}");
                    
                    // Optional: Launch debugger if possible (requires Just-In-Time debugging enabled)
                    try
                    {
                        if (Debugger.Launch())
                        {
                            pipeAwareLogger.LogInformation("Debugger launched successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        pipeAwareLogger.LogWarning($"Failed to launch debugger: {ex.Message}");
                    }
                }
                else
                {
                    pipeAwareLogger.LogInformation("Debugger is already attached - test debugging enabled");
                }
            }

            RevitTestInfrastructure.CancellationToken = cancellationToken;
            var methods = command.TestMethods;

            // Execute tests on a background thread to avoid blocking Revit UI
            // IMPORTANT: Use await instead of .Wait() to prevent UI thread deadlock
            await Task.Run(async () =>
            {
                try
                {
                    pipeAwareLogger.LogDebug("Starting xUnit test execution in background thread");
                    
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
                    pipeAwareLogger.LogDebug("Discovering test cases in assembly");
                    controller.Find(false, discoverySink, discoveryOptions);
                    discoverySink.Finished.WaitOne();
                    testCases = discoverySink.TestCases.ToList();
                    pipeAwareLogger.LogInformation($"Discovered {testCases.Count} test cases");

                    // Filter test cases if specific methods are requested
                    if (methods != null && methods.Length > 0)
                    {
                        var originalCount = testCases.Count;
                        testCases = testCases.Where(tc => methods.Contains(tc.TestMethod.TestClass.Class.Name + "." + tc.TestMethod.Method.Name)).ToList();
                        pipeAwareLogger.LogInformation($"Filtered to {testCases.Count} test cases (from {originalCount}) based on method filter");
                    }

                    pipeAwareLogger.LogInformation($"Starting execution of {testCases.Count} test cases");
                    using var visitor = new StreamingXmlTestExecutionVisitor(writer, assemblyElement, () => cancellationToken.IsCancellationRequested, pipeAwareLogger);
                    controller.RunTests(testCases, visitor, executionOptions);
                    visitor.Finished.WaitOne();

                    string resultXml = new XDocument(assemblyElement).ToString();
                    var fileName = $"RevitXunitResults_{Guid.NewGuid():N}.xml";
                    var resultsPath = Path.Combine(Path.GetTempPath(), fileName);
                    File.WriteAllText(resultsPath, resultXml);
                    
                    pipeAwareLogger.LogInformation($"Test execution completed. Results saved to: {resultsPath}");
                    
                    writer.WriteLine("END");
                    writer.Flush();
                }
                catch (Exception ex)
                {
                    pipeAwareLogger.LogError(ex, "Test execution failed in background thread");
                    HandleTestExecutionException(ex, command.TestMethods, writer, pipeAwareLogger);
                }
            }, cancellationToken);
            
            pipeAwareLogger.LogDebug("Test execution async operation completed");
        }
        catch (Exception ex)
        {
            pipeAwareLogger.LogError(ex, "ExecuteTestsInRevitAsync failed");
            throw;
        }
    }

    private static void HandleTestExecutionException(Exception ex, string[]? methods, StreamWriter writer, RevitTestFramework.Common.ILogger logger)
    {
        var methodsStr = methods != null ? string.Join(", ", methods) : "None";
        logger.LogError(ex, $"Handling test execution exception for methods: {methodsStr}");
                
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

            logger.LogDebug("Failure message written to pipe stream");
            
            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"Test execution failed with exception: {ex}");
        }
        catch (ObjectDisposedException)
        {
            // Writer was already disposed, log to file only
            logger.LogWarning("Cannot write error message to pipe - writer was already disposed");
        }
        catch (IOException ioEx) when (ioEx.Message.Contains("Pipe is broken") || ioEx.Message.Contains("pipe has been ended"))
        {
            // Pipe is broken, log to file only
            logger.LogWarning("Cannot write error message to pipe - pipe is broken");
        }
        catch (Exception writeEx)
        {
            logger.LogFatal(writeEx, $"Failed to write error message to pipe stream. Original exception: {ex}");
            
            // If we can't even write the error, log it
            System.Diagnostics.Debug.WriteLine($"Failed to write error message: {writeEx}");
            System.Diagnostics.Debug.WriteLine($"Original exception: {ex}");
        }
    }
}

// Clean xUnit integration without reflection
internal class StreamingXmlTestExecutionVisitor(StreamWriter writer, XElement assemblyElement, Func<bool> cancelThunk, RevitTestFramework.Common.ILogger? logger = null) : XmlTestExecutionVisitor(assemblyElement, cancelThunk)
{
    private readonly StreamWriter _writer = writer;
    private readonly RevitTestFramework.Common.ILogger _logger = logger ?? RevitTestFramework.Common.FileLogger.ForContext<StreamingXmlTestExecutionVisitor>();

    private void Send(PipeTestResultMessage msg)
    {
        try
        {
            var json = JsonSerializer.Serialize(msg);
            _writer.WriteLine(json);
            _writer.Flush();
        }
        catch (ObjectDisposedException)
        {
            // Writer was already disposed, log to file only
            _logger.LogWarning("Cannot send test result message to pipe - writer was already disposed");
        }
        catch (IOException ex) when (ex.Message.Contains("Pipe is broken") || ex.Message.Contains("pipe has been ended"))
        {
            // Pipe is broken, log to file only
            _logger.LogWarning("Cannot send test result message to pipe - pipe is broken");
        }
        catch (Exception ex)
        {
            // Log other exceptions but don't fail the test execution
            _logger.LogError(ex, "Error sending test result message to pipe");
        }
    }

    protected override bool Visit(ITestPassed testPassed)
    {
        _logger.LogInformation($"Test passed: {testPassed.Test.DisplayName} in {testPassed.ExecutionTime * 1000:F0}ms");
            
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
        var errorMessage = string.Join(Environment.NewLine, testFailed.Messages ?? []);
        _logger.LogWarning($"Test failed: {testFailed.Test.DisplayName} in {testFailed.ExecutionTime * 1000:F0}ms - {errorMessage}");
            
        Send(new PipeTestResultMessage
        {
            Name = testFailed.Test.DisplayName,
            Outcome = "Failed",
            Duration = (double)testFailed.ExecutionTime,
            ErrorMessage = errorMessage,
            ErrorStackTrace = string.Join(Environment.NewLine, testFailed.StackTraces ?? [])
        });
        return base.Visit(testFailed);
    }

    protected override bool Visit(ITestSkipped testSkipped)
    {
        _logger.LogInformation($"Test skipped: {testSkipped.Test.DisplayName} - {testSkipped.Reason}");
            
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
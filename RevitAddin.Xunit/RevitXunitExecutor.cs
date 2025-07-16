using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System.Text.Json;

namespace RevitAddin.Xunit;

public static class RevitXunitExecutor
{
    public static void ExecuteTestsInRevit(string commandJson, string testAssemblyPath, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Deserialize the command in the isolated context to avoid cross-ALC type issues
        var command = JsonSerializer.Deserialize<PipeCommand>(commandJson)
            ?? throw new InvalidOperationException("Failed to deserialize PipeCommand");

        // Create a new ModelOpeningExternalEvent in this ALC context
        // Since this method is called from TestCommandHandler.Execute (UI thread), 
        // we should still be in a valid API execution context
        ModelOpeningExternalEvent? modelOpener = null;
        try
        {
            modelOpener = new ModelOpeningExternalEvent();
            
            // Initialize the RevitModelUtility in this ALC with the new model opener
            RevitModelUtility.Initialize(uiApp, modelOpener);

            // Set up model service with our local handlers
            RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
            RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
            RevitModelService.CancellationToken = cancellationToken;

            var methods = command.TestMethods;

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
        finally
        {
            RevitModelService.CancellationToken = CancellationToken.None;
            // Clean up the RevitModelUtility state
            RevitModelUtility.CleanupOpenDocuments();
            
            // Dispose of the model opener created in this context
            modelOpener?.Dispose();
        }
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
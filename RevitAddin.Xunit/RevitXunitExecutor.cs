using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;
using System.Text.Json;

namespace RevitAddin.Xunit;

public static class RevitXunitExecutor
{

    /// <summary>
    /// Sets up the required Revit API infrastructure (ExternalEvents, etc.).
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static void SetupInfrastructure(UIApplication uiApp)
    {
        RevitTestInfrastructure.Setup(uiApp);
    }

    /// <summary>
    /// Cleans up the Revit API infrastructure after tests have run.
    /// This method MUST be called from the Revit UI thread.
    /// </summary>
    public static void TeardownInfrastructure()
    {
        RevitTestInfrastructure.Dispose();
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

// Clean xUnit integration without reflection
internal class StreamingXmlTestExecutionVisitor(StreamWriter writer, XElement assemblyElement, Func<bool> cancelThunk) : XmlTestExecutionVisitor(assemblyElement, cancelThunk)
{
    private readonly StreamWriter _writer = writer;

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
            ErrorMessage = string.Join(Environment.NewLine, testFailed.Messages ?? []),
            ErrorStackTrace = string.Join(Environment.NewLine, testFailed.StackTraces ?? [])
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
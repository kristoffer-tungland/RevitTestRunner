using System.Runtime.Loader;
using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System.Text.Json;

namespace RevitAddin.Xunit;

public static class RevitXunitExecutor
{
    public static void ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Set up model service with our local handlers
        RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
        RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
        RevitModelService.CancellationToken = cancellationToken;
        
        var testAssemblyPath = command.TestAssembly;
        var methods = command.TestMethods;
        var loadContext = new AssemblyLoadContext("XUnitTestContext", isCollectible: false);

        var testDir = Path.GetDirectoryName(testAssemblyPath) ?? string.Empty;
        loadContext.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(testDir, name.Name + ".dll");
            return File.Exists(candidate) ? loadContext.LoadFromAssemblyPath(candidate) : null;
        };

        loadContext.LoadFromAssemblyPath(typeof(XunitFrontController).Assembly.Location);
        loadContext.LoadFromAssemblyPath(testAssemblyPath);

        var assemblyElement = new XElement("assembly");
        using var controller = new XunitFrontController(AppDomainSupport.Denied, testAssemblyPath);
        var discoveryOptions = TestFrameworkOptions.ForDiscovery();
        var executionOptions = TestFrameworkOptions.ForExecution();

        List<ITestCase> testCases;
        using (var discoverySink = new TestDiscoverySink())
        {
            controller.Find(false, discoverySink, discoveryOptions);
            discoverySink.Finished.WaitOne();
            testCases = discoverySink.TestCases.ToList();
        }

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
        RevitModelService.CancellationToken = CancellationToken.None;
    }
}

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
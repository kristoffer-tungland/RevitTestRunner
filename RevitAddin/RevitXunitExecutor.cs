using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using RevitTestFramework.Common;
using Xunit.Sdk;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;

namespace RevitAddin;

public static class RevitXunitExecutor
{
    public static UIApplication? UiApplication { get; private set; }
    public static Document? CurrentDocument { get; private set; }
    private static readonly Dictionary<string, Document> _openDocs = new();
    private const string LocalPrefix = "local:";

    public static Document EnsureModelOpen(string projectGuid, string modelGuid)
    {
        if (UiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        var key = $"{projectGuid}:{modelGuid}";
        if (_openDocs.TryGetValue(key, out var doc) && doc.IsValidObject)
        {
            CurrentDocument = doc;
        RevitModelService.CurrentDocument = doc;
            return doc;
        }

        var projGuid = new Guid(projectGuid);
        var modGuid = new Guid(modelGuid);
        var cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(ModelPathUtils.CloudRegionUS, projGuid, modGuid);
        var app = UiApplication.Application;
        var openOpts = new OpenOptions();
        doc = app.OpenDocumentFile(cloudPath, openOpts);
        _openDocs[key] = doc;
        CurrentDocument = doc;
        RevitModelService.CurrentDocument = doc;
        return doc;
    }

    public static Document EnsureModelOpen(string localPath)
    {
        if (UiApplication == null)
            throw new InvalidOperationException("UI application not initialized");

        var key = LocalPrefix + localPath;
        if (_openDocs.TryGetValue(key, out var doc) && doc.IsValidObject)
        {
            CurrentDocument = doc;
        RevitModelService.CurrentDocument = doc;
            return doc;
        }

        var modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(localPath);
        var app = UiApplication.Application;
        var opts = new OpenOptions();
        doc = app.OpenDocumentFile(modelPath, opts);
        _openDocs[key] = doc;
        CurrentDocument = doc;
        RevitModelService.CurrentDocument = doc;
        return doc;
    }

    public static void ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        UiApplication = uiApp;
        RevitModelService.OpenLocalModel = EnsureModelOpen;
        RevitModelService.OpenCloudModel = EnsureModelOpen;
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

class StreamingXmlTestExecutionVisitor : XmlTestExecutionVisitor
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
            ErrorMessage = string.Join(System.Environment.NewLine, testFailed.Messages ?? System.Array.Empty<string>()),
            ErrorStackTrace = string.Join(System.Environment.NewLine, testFailed.StackTraces ?? System.Array.Empty<string>())
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

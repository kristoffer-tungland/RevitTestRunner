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
using RevitTestFramework;
using Xunit.Sdk;

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

    public static string ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp)
    {
        UiApplication = uiApp;
        RevitModelService.OpenLocalModel = EnsureModelOpen;
        RevitModelService.OpenCloudModel = EnsureModelOpen;
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

        using var visitor = new XmlTestExecutionVisitor(assemblyElement, () => false);
        controller.RunTests(testCases, visitor, executionOptions);
        visitor.Finished.WaitOne();

        string resultXml = new XDocument(assemblyElement).ToString();
        var fileName = $"RevitXunitResults_{Guid.NewGuid():N}.xml";
        var resultsPath = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(resultsPath, resultXml);
        return resultsPath;
    }
}

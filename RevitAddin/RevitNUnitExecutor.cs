using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Engine;

using RevitTestFramework;
namespace RevitAddin
{
    public static class RevitNUnitExecutor
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
            // isolate test assemblies without unloading them
            var loadContext = new AssemblyLoadContext("TestContext", isCollectible: false);

            // Ensure dependencies are loaded from the test assembly directory
            var testDir = Path.GetDirectoryName(testAssemblyPath) ?? string.Empty;
            loadContext.Resolving += (_, name) =>
            {
                var candidate = Path.Combine(testDir, name.Name + ".dll");
                return File.Exists(candidate) ? loadContext.LoadFromAssemblyPath(candidate) : null;
            };

            var engineAssemblyPath = typeof(TestEngineActivator).Assembly.Location;
            loadContext.LoadFromAssemblyPath(engineAssemblyPath);
            loadContext.LoadFromAssemblyPath(testAssemblyPath);

            using var engine = TestEngineActivator.CreateInstance();
            var package = new TestPackage(testAssemblyPath);
            var runner = engine.GetRunner(package);

            NUnit.Engine.TestFilter filter = NUnit.Engine.TestFilter.Empty;
            if (methods != null && methods.Length > 0)
            {
                var builder = new NUnit.Engine.TestFilterBuilder();
                foreach (var m in methods)
                    builder.AddTest(m);
                filter = builder.GetFilter();
            }

            var result = runner.Run(null, filter);

            string resultXml = result.OuterXml;
            var fileName = $"RevitNUnitResults_{Guid.NewGuid():N}.xml";
            var resultsPath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(resultsPath, resultXml);
            return resultsPath;
        }
    }
}

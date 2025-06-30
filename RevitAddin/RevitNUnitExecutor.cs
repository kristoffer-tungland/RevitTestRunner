using System.IO;
using System.Runtime.Loader;
using Autodesk.Revit.UI;
using NUnit.Engine;

namespace RevitAddin
{
    public static class RevitNUnitExecutor
    {
        public static string ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp)
        {
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
            var resultsPath = Path.Combine(Path.GetTempPath(), "RevitTestResults.xml");
            File.WriteAllText(resultsPath, resultXml);
            return resultsPath;
        }
    }
}

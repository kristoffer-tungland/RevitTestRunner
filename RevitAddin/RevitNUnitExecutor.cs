using System.IO;
using Autodesk.Revit.UI;
using NUnit.Engine;

namespace RevitAddin
{
    public static class RevitNUnitExecutor
    {
        public static string ExecuteTestsInRevit(string testAssemblyPath, UIApplication uiApp)
        {
            using var engine = TestEngineActivator.CreateInstance();
            var package = new TestPackage(testAssemblyPath);
            var runner = engine.GetRunner(package);
            var result = runner.Run(null, TestFilter.Empty);

            string resultXml = result.OuterXml;
            var resultsPath = Path.Combine(Path.GetTempPath(), "RevitTestResults.xml");
            File.WriteAllText(resultsPath, resultXml);
            return resultsPath;
        }
    }
}

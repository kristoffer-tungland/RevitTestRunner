using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using RevitAdapterCommon;
using System.Text.Json;

namespace RevitXunitAdapter
{
    [ExtensionUri("executor://RevitXunitExecutor")]
public class RevitXunitExecutor : ITestExecutor
    {
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var assembly = tests.First().Source;
            var testNames = tests.Select(t => t.FullyQualifiedName).ToArray();
            var resultPath = SendRunCommand(assembly, testNames);
            ParseResults(resultPath, frameworkHandle, assembly);
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var assembly = sources.First();
            var resultPath = SendRunCommand(assembly, Array.Empty<string>());
            ParseResults(resultPath, frameworkHandle, assembly);
        }

        private static string SendRunCommand(string assembly, string[] methods)
        {
            var command = new
            {
                Command = "RunXunitTests",
                TestAssembly = assembly,
                TestMethods = methods
            };
            return PipeClientHelper.SendCommand(command);
        }

        private static void ParseResults(string resultXmlPath, IFrameworkHandle frameworkHandle, string source)
        {
            var doc = System.Xml.Linq.XDocument.Load(resultXmlPath);
            var testCases = doc.Descendants("test");
            foreach (var test in testCases)
            {
                var fullname = test.Attribute("name")!.Value;
                var outcome = test.Attribute("result")!.Value;
                var duration = double.Parse(test.Attribute("time")?.Value ?? "0");
                var tc = new TestCase(fullname, new Uri("executor://RevitXunitExecutor"), source);
                var tr = new TestResult(tc)
                {
                    Outcome = outcome == "Pass" ? TestOutcome.Passed : outcome == "Fail" ? TestOutcome.Failed : TestOutcome.Skipped,
                    Duration = TimeSpan.FromSeconds(duration)
                };
                if (outcome == "Failed")
                {
                    var failure = test.Element("failure");
                    tr.ErrorMessage = failure?.Element("message")?.Value;
                    tr.ErrorStackTrace = failure?.Element("stack-trace")?.Value;
                }
                frameworkHandle.RecordResult(tr);
            }
        }

        public void Cancel() { }
    }
}

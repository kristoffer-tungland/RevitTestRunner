using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using RevitAdapterCommon;
using System.Text.Json;

namespace RevitNUnitAdapter
{
    [ExtensionUri("executor://RevitNUnitExecutor")]
public class RevitNUnitExecutor : ITestExecutor
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
                Command = "RunTests",
                TestAssembly = assembly,
                TestMethods = methods
            };
            return PipeClientHelper.SendCommand(command);
        }

        private static void ParseResults(string resultXmlPath, IFrameworkHandle frameworkHandle, string source)
        {
            var doc = System.Xml.Linq.XDocument.Load(resultXmlPath);
            var testCases = doc.Descendants("test-case");
            foreach (var test in testCases)
            {
                var fullname = test.Attribute("fullname")!.Value;
                var outcome = test.Attribute("result")!.Value;
                var duration = double.Parse(test.Attribute("duration")!.Value);
                var tc = new TestCase(fullname, new Uri("executor://RevitNUnitExecutor"), source);
                var tr = new TestResult(tc)
                {
                    Outcome = outcome == "Passed" ? TestOutcome.Passed : outcome == "Failed" ? TestOutcome.Failed : TestOutcome.Skipped,
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

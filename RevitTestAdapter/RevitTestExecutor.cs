using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.IO.Pipes;
using System.Text.Json;

namespace RevitTestAdapter
{
    [ExtensionUri("executor://RevitTestExecutor")]
    public class RevitTestExecutor : ITestExecutor
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
            using var client = new NamedPipeClientStream(".", "RevitTestPipe", PipeDirection.InOut);
            client.Connect();
            var command = new
            {
                Command = "RunTests",
                TestAssembly = assembly,
                TestMethods = methods
            };
            var json = JsonSerializer.Serialize(command);
            using var sw = new StreamWriter(client, leaveOpen: true);
            sw.WriteLine(json);
            sw.Flush();
            using var sr = new StreamReader(client);
            var result = sr.ReadLine() ?? string.Empty;
            return result;
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
                var tc = new TestCase(fullname, new Uri("executor://RevitTestExecutor"), source);
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

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.IO.Pipes;
using System.Text.Json;
using System.Diagnostics;

namespace RevitTestAdapter
{
    [ExtensionUri("executor://RevitTestExecutor")]
public class RevitTestExecutor : ITestExecutor
{
        private const string PipeNamePrefix = "RevitTestPipe_";
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
            using var client = ConnectToRevit();
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

        private static NamedPipeClientStream ConnectToRevit()
        {
            foreach (var proc in Process.GetProcessesByName("Revit"))
            {
                var pipeName = PipeNamePrefix + proc.Id;
                var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                try
                {
                    client.Connect(100);
                    return client;
                }
                catch
                {
                    client.Dispose();
                }
            }
            throw new InvalidOperationException("No Revit process with test pipe found.");
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

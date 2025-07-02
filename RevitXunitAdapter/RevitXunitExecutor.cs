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
            SendRunCommandStreaming(assembly, testNames, frameworkHandle);
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var assembly = sources.First();
            SendRunCommandStreaming(assembly, Array.Empty<string>(), frameworkHandle);
        }

        private static void SendRunCommandStreaming(string assembly, string[] methods, IFrameworkHandle frameworkHandle)
        {
            var command = new
            {
                Command = "RunXunitTests",
                TestAssembly = assembly,
                TestMethods = methods
            };

            PipeClientHelper.SendCommandStreaming(command, line =>
            {
                if (line == "END")
                    return;

                var msg = JsonSerializer.Deserialize<PipeTestResultMessage>(line);
                if (msg == null)
                    return;

                var tc = new TestCase(msg.Name, new Uri("executor://RevitXunitExecutor"), assembly);
                var tr = new TestResult(tc)
                {
                    Outcome = msg.Outcome == "Pass" || msg.Outcome == "Passed" ? TestOutcome.Passed : msg.Outcome == "Fail" || msg.Outcome == "Failed" ? TestOutcome.Failed : TestOutcome.Skipped,
                    Duration = TimeSpan.FromSeconds(msg.Duration),
                    ErrorMessage = msg.ErrorMessage,
                    ErrorStackTrace = msg.ErrorStackTrace
                };
                frameworkHandle.RecordResult(tr);
            });
        }

        public void Cancel() { }
    }
}

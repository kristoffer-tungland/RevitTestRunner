using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using RevitAdapterCommon;
using System.Text.Json;

namespace RevitXunitAdapter
{
    [ExtensionUri("executor://RevitXunitExecutor")]
public class RevitXunitExecutor : ITestExecutor
    {
        private CancellationTokenSource? _cts;
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            _cts = new CancellationTokenSource();
            var assembly = tests.First().Source;
            var testNames = tests.Select(t => t.FullyQualifiedName).ToArray();
            SendRunCommandStreaming(assembly, testNames, frameworkHandle, _cts.Token);
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            _cts = new CancellationTokenSource();
            var assembly = sources.First();
            SendRunCommandStreaming(assembly, Array.Empty<string>(), frameworkHandle, _cts.Token);
        }

        private static void SendRunCommandStreaming(string assembly, string[] methods, IFrameworkHandle frameworkHandle, CancellationToken token)
        {
            var command = new PipeCommand
            {
                Command = "RunXunitTests",
                TestAssembly = assembly,
                TestMethods = methods,
                CancelPipe = "RevitCancel_" + Guid.NewGuid().ToString("N")
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
            }, token);
        }

        public void Cancel() { _cts?.Cancel(); }
    }
}

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using RevitAdapterCommon;
using System.Text.Json;

namespace RevitXunitAdapter
{
    [ExtensionUri("executor://RevitXunitExecutor")]
    public class RevitXunitExecutor : ITestExecutor
    {
        private CancellationTokenSource? _cts;

        public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
        {
            if (tests == null || frameworkHandle == null) return;

            try
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Starting test execution with specific test cases");

                _cts = new CancellationTokenSource();
                var assembly = tests.First().Source;
                var testNames = tests.Select(t => t.FullyQualifiedName).ToArray();

                frameworkHandle.SendMessage(TestMessageLevel.Informational, $"RevitXunitExecutor: Running {testNames.Length} tests from {assembly}");

                SendRunCommandStreaming(assembly, testNames, frameworkHandle, _cts.Token);
            }
            catch (Exception ex)
            {
                frameworkHandle?.SendMessage(TestMessageLevel.Error, $"RevitXunitExecutor: Error in RunTests (specific): {ex}");
            }
        }

        public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
        {
            if (sources == null || frameworkHandle == null) return;

            try
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Starting test execution with sources");

                _cts = new CancellationTokenSource();
                var assembly = sources.First();

                frameworkHandle.SendMessage(TestMessageLevel.Informational, $"RevitXunitExecutor: Running all tests from {assembly}");

                SendRunCommandStreaming(assembly, [], frameworkHandle, _cts.Token);
            }
            catch (Exception ex)
            {
                frameworkHandle?.SendMessage(TestMessageLevel.Error, $"RevitXunitExecutor: Error in RunTests (sources): {ex}");
            }
        }

        private static void SendRunCommandStreaming(string assembly, string[] methods, IFrameworkHandle frameworkHandle, CancellationToken token)
        {
            try
            {
                frameworkHandle.SendMessage(TestMessageLevel.Informational, $"RevitXunitExecutor: Preparing command for assembly {assembly}");

                var command = new PipeCommand
                {
                    Command = "RunTests",
                    TestAssembly = assembly,
                    TestMethods = methods,
                    CancelPipe = "RevitCancel_" + Guid.NewGuid().ToString("N")
                };

                frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Sending command to Revit via named pipe");

                PipeClientHelper.SendCommandStreaming(command, line =>
                {
                    if (line == "END")
                    {
                        frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Received END signal from Revit");
                        return;
                    }

                    try
                    {
                        var msg = JsonSerializer.Deserialize<PipeTestResultMessage>(line);
                        if (msg == null)
                        {
                            frameworkHandle.SendMessage(TestMessageLevel.Warning, $"RevitXunitExecutor: Failed to deserialize message: {line}");
                            return;
                        }

                        var tc = new TestCase(msg.Name, new Uri("executor://RevitXunitExecutor"), assembly);
                        var tr = new TestResult(tc)
                        {
                            Outcome = msg.Outcome == "Pass" || msg.Outcome == "Passed" ? TestOutcome.Passed :
                                     msg.Outcome == "Fail" || msg.Outcome == "Failed" ? TestOutcome.Failed :
                                     TestOutcome.Skipped,
                            Duration = TimeSpan.FromSeconds(msg.Duration),
                            ErrorMessage = msg.ErrorMessage,
                            ErrorStackTrace = msg.ErrorStackTrace
                        };
                        frameworkHandle.RecordResult(tr);
                        frameworkHandle.SendMessage(TestMessageLevel.Informational, $"RevitXunitExecutor: Recorded result for {msg.Name}: {tr.Outcome}");
                    }
                    catch (Exception ex)
                    {
                        frameworkHandle.SendMessage(TestMessageLevel.Error, $"RevitXunitExecutor: Error processing result line '{line}': {ex.Message}");
                    }
                }, token);
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, $"RevitXunitExecutor: Error in SendRunCommandStreaming: {ex}");
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }
}

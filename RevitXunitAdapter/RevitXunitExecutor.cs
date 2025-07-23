using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using RevitAdapterCommon;
using RevitTestFramework.Contracts;
using System.Diagnostics;
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
                    CancelPipe = "RevitCancel_" + Guid.NewGuid().ToString("N"),
                    Debug = Debugger.IsAttached
                };

                if (command.Debug)
                {
                    frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Debugger detected - enabling debug mode for Revit test execution");
                }

                frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Sending command to Revit via named pipe");

                // For now, hardcode Revit version as "2025" as requested
                const string revitVersion = "2025";
                
                // Pass the framework handle directly - it will be converted to a logger automatically
                PipeClientHelper.SendCommandStreaming(command, line =>
                {
                    if (line == "END")
                    {
                        frameworkHandle.SendMessage(TestMessageLevel.Informational, "RevitXunitExecutor: Received END signal from Revit");
                        return;
                    }

                    try
                    {
                        // First try to deserialize as a log message
                        if (TryHandleLogMessage(line, frameworkHandle))
                        {
                            return; // Successfully handled as log message
                        }

                        // If not a log message, try to handle as test result
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
                }, token, revitVersion, frameworkHandle.ToLogger());
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Error, $"RevitXunitExecutor: Error in SendRunCommandStreaming: {ex}");
            }
        }

        /// <summary>
        /// Attempts to handle a line as a log message and forward it to the framework handle
        /// </summary>
        /// <param name="line">The line received from the pipe</param>
        /// <param name="frameworkHandle">The framework handle to forward messages to</param>
        /// <returns>True if the line was successfully handled as a log message, false otherwise</returns>
        private static bool TryHandleLogMessage(string line, IFrameworkHandle frameworkHandle)
        {
            try
            {
                var logMessage = JsonSerializer.Deserialize<PipeLogMessage>(line);
                if (logMessage == null || logMessage.Type != "LOG")
                {
                    return false; // Not a log message
                }

                // Skip DEBUG level messages - these should only be in file logs
                if (logMessage.Level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Successfully handled (by ignoring)
                }

                // Convert log level to TestMessageLevel
                var testMessageLevel = ConvertLogLevelToTestMessageLevel(logMessage.Level);
                
                // Format the log message with timestamp and source
                var formattedMessage = FormatLogMessage(logMessage);
                
                // Forward to framework handle
                frameworkHandle.SendMessage(testMessageLevel, formattedMessage);
                
                return true; // Successfully handled as log message
            }
            catch (JsonException)
            {
                // Not a valid log message JSON
                return false;
            }
            catch (Exception ex)
            {
                frameworkHandle.SendMessage(TestMessageLevel.Warning, $"RevitXunitExecutor: Error processing log message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts a log level string to TestMessageLevel
        /// </summary>
        /// <param name="logLevel">The log level string (INFO, WARN, ERROR, FATAL)</param>
        /// <returns>The corresponding TestMessageLevel</returns>
        private static TestMessageLevel ConvertLogLevelToTestMessageLevel(string logLevel)
        {
            return logLevel.ToUpperInvariant() switch
            {
                "INFO" => TestMessageLevel.Informational,
                "WARN" => TestMessageLevel.Warning,
                "ERROR" => TestMessageLevel.Error,
                "FATAL" => TestMessageLevel.Error,
                _ => TestMessageLevel.Informational
            };
        }

        /// <summary>
        /// Formats a log message for display in the test framework
        /// </summary>
        /// <param name="logMessage">The log message to format</param>
        /// <returns>The formatted message string</returns>
        private static string FormatLogMessage(PipeLogMessage logMessage)
        {
            var prefix = "Revit";
            if (!string.IsNullOrEmpty(logMessage.Source))
            {
                prefix = $"Revit.{logMessage.Source}";
            }

            var timestamp = DateTime.TryParse(logMessage.Timestamp, out var parsedTime) 
                ? parsedTime.ToString("HH:mm:ss.fff") 
                : logMessage.Timestamp;

            return $"{prefix} [{logMessage.Level}] {timestamp}: {logMessage.Message}";
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }
}

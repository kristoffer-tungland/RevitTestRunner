using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Engine;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace RevitAddin.NUnit;

public static class RevitNUnitExecutor
{
    public static void SetupInfrastructure(UIApplication uiApp)
    {
        RevitTestInfrastructure.Setup(uiApp);
    }

    public static void TeardownInfrastructure()
    {
        RevitTestInfrastructure.Dispose();
    }

    public static async Task ExecuteTestsInRevitAsync(string commandJson, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken)
    {
        var command = JsonSerializer.Deserialize<PipeCommand>(commandJson)
            ?? throw new InvalidOperationException("Failed to deserialize PipeCommand");

        RevitModelService.CancellationToken = cancellationToken;
        var methods = command.TestMethods;

        await Task.Run(() =>
        {
            try
            {
                var loadContext = AssemblyLoadContext.GetLoadContext(typeof(RevitNUnitExecutor).Assembly)!;
                var testAssembly = loadContext.LoadFromAssemblyPath(testAssemblyPath);

                var attributeMap = new Dictionary<string, RevitTestFramework.NUnit.RevitNUnitTestModelAttribute>();
                foreach (var type in testAssembly.GetTypes())
                {
                    foreach (var methodInfo in type.GetMethods())
                    {
                        var attr = methodInfo.GetCustomAttribute<RevitTestFramework.NUnit.RevitNUnitTestModelAttribute>();
                        if (attr != null)
                        {
                            var name = type.FullName + "." + methodInfo.Name;
                            attributeMap[name] = attr;
                        }
                    }
                }

                using var engine = TestEngineActivator.CreateInstance();
                var package = new TestPackage(testAssemblyPath);
                var runner = engine.GetRunner(package);

                TestFilter filter = TestFilter.Empty;
                if (methods != null && methods.Length > 0)
                {
                    var builder = new TestFilterBuilder();
                    foreach (var m in methods)
                        builder.AddTest(m);
                    filter = builder.GetFilter();
                }

                var listener = new StreamingNUnitEventListener(writer, cancellationToken, attributeMap);
                using var monitor = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _ = Task.Run(() =>
                {
                    monitor.Token.WaitHandle.WaitOne();
                    if (monitor.IsCancellationRequested)
                    {
                        try { runner.StopRun(true); } catch { }
                    }
                }, cancellationToken);

                var result = runner.Run(listener, filter);
                string resultXml = result.OuterXml;
                var fileName = $"RevitNUnitResults_{Guid.NewGuid():N}.xml";
                var resultsPath = Path.Combine(Path.GetTempPath(), fileName);
                File.WriteAllText(resultsPath, resultXml);
                writer.WriteLine("END");
                writer.Flush();
            }
            catch (Exception ex)
            {
                try
                {
                    var failureMessage = new PipeTestResultMessage
                    {
                        Name = methods?.Length > 0 ? string.Join(", ", methods) : "TestExecution",
                        Outcome = "Failed",
                        Duration = 0,
                        ErrorMessage = $"Test execution failed: {ex.Message}",
                        ErrorStackTrace = ex.ToString()
                    };
                    var json = JsonSerializer.Serialize(failureMessage);
                    writer.WriteLine(json);
                    writer.WriteLine("END");
                    writer.Flush();
                }
                catch
                {
                }
            }
            finally
            {
                RevitModelService.CancellationToken = CancellationToken.None;
            }
        }, cancellationToken);
    }
}

internal class StreamingNUnitEventListener : ITestEventListener
{
    private readonly StreamWriter _writer;
    private readonly CancellationToken _token;
    private readonly Dictionary<string, RevitTestFramework.NUnit.RevitNUnitTestModelAttribute> _attrMap;

    private Document? _document;
    private TransactionGroup? _transactionGroup;

    public StreamingNUnitEventListener(StreamWriter writer, CancellationToken token,
        Dictionary<string, RevitTestFramework.NUnit.RevitNUnitTestModelAttribute> attrMap)
    {
        _writer = writer;
        _token = token;
        _attrMap = attrMap;
    }

    public void OnTestEvent(string report)
    {
        if (_token.IsCancellationRequested)
            return;

        // Run the event processing on a background thread to avoid blocking NUnit
        _ = Task.Run(async () => await ProcessTestEventAsync(report), _token);
    }

    private async Task ProcessTestEventAsync(string report)
    {
        if (_token.IsCancellationRequested)
            return;

        try
        {
            var xml = System.Xml.Linq.XElement.Parse(report);
            
            if (xml.Name == "start-test")
            {
                await HandleTestStartAsync(xml);
            }
            else if (xml.Name == "test-case")
            {
                await HandleTestCaseAsync(xml);
            }
        }
        catch (Exception ex)
        {
            // Log the exception but don't let it escape to avoid breaking the test runner
            Debug.WriteLine($"Error processing test event: {ex.Message}");
        }
    }

    private async Task HandleTestStartAsync(System.Xml.Linq.XElement xml)
    {
        var name = xml.Attribute("fullname")?.Value ?? xml.Attribute("name")?.Value ?? string.Empty;
        
        if (_attrMap.TryGetValue(name, out var attr))
        {
            try
            {
                // Request model setup on UI thread and wait for completion
                _document = await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    return RevitTestModelHelper.OpenModel(app, attr.LocalPath, attr.ProjectGuid, attr.ModelGuid);
                });

                // Set the current document in RevitModelService so it can be injected into tests
                RevitModelService.CurrentDocument = _document;

                // Start a transaction group for the test on UI thread
                _transactionGroup = await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    var tg = new TransactionGroup(_document, $"Test: {name}");
                    tg.Start();
                    return tg;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up test model for {name}: {ex.Message}");
                // Clean up any partial state
                await CleanupResourcesAsync();
            }
        }
    }

    private async Task HandleTestCaseAsync(System.Xml.Linq.XElement xml)
    {
        var msg = new PipeTestResultMessage
        {
            Name = xml.Attribute("fullname")?.Value ?? xml.Attribute("name")?.Value ?? string.Empty,
            Outcome = xml.Attribute("result")?.Value ?? string.Empty,
            Duration = double.TryParse(xml.Attribute("duration")?.Value, out var d) ? d : 0,
        };

        if (msg.Outcome == "Failed")
        {
            var failure = xml.Element("failure");
            msg.ErrorMessage = failure?.Element("message")?.Value;
            msg.ErrorStackTrace = failure?.Element("stack-trace")?.Value;
        }

        // Write the test result
        var json = JsonSerializer.Serialize(msg);
        _writer.WriteLine(json);
        _writer.Flush();

        // Clean up transaction group on UI thread
        await CleanupResourcesAsync();
    }

    private async Task CleanupResourcesAsync()
    {
        if (_transactionGroup != null)
        {
            try
            {
                await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    try
                    {
                        // Rollback the transaction group
                        _transactionGroup.RollBack();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error rolling back transaction group: {ex.Message}");
                    }
                    finally
                    {
                        _transactionGroup.Dispose();
                        _transactionGroup = null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during transaction group cleanup: {ex.Message}");
                // Ensure we clean up the reference even if the operation failed
                _transactionGroup = null;
            }
        }

        // Reset document reference (this doesn't need UI thread access)
        _document = null;
        RevitModelService.CurrentDocument = null;
    }
}

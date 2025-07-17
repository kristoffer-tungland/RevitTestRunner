using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Engine;
using System.Text.Json;
using RevitAddin.Common;
using RevitTestFramework.Common;

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

    public StreamingNUnitEventListener(StreamWriter writer, CancellationToken token,
        Dictionary<string, RevitTestFramework.NUnit.RevitNUnitTestModelAttribute> attrMap)
    {
        _writer = writer;
        _token = token;
        _attrMap = attrMap;
    }

    private static Document OpenLocalModel(string path)
    {
        var doc = AsyncUtil.RunSync(() => RevitTestInfrastructure.RevitTask.Run(app =>
            RevitTestModelHelper.OpenModel(app, path, null, null)));
        RevitModelService.CurrentDocument = doc;
        return doc;
    }

    private static Document OpenCloudModel(string projectGuid, string modelGuid)
    {
        var doc = AsyncUtil.RunSync(() => RevitTestInfrastructure.RevitTask.Run(app =>
            RevitTestModelHelper.OpenModel(app, null, projectGuid, modelGuid)));
        RevitModelService.CurrentDocument = doc;
        return doc;
    }

    public void OnTestEvent(string report)
    {
        if (_token.IsCancellationRequested)
            return;
        try
        {
            var xml = System.Xml.Linq.XElement.Parse(report);
            if (xml.Name == "start-test")
            {
                var name = xml.Attribute("fullname")?.Value ?? xml.Attribute("name")?.Value ?? string.Empty;
                if (_attrMap.TryGetValue(name, out var attr))
                {
                    RevitTestModelHelper.EnsureModelAndStartGroup(
                        attr.LocalPath,
                        attr.ProjectGuid,
                        attr.ModelGuid,
                        OpenLocalModel,
                        OpenCloudModel,
                        name);
                }
            }
            else if (xml.Name == "test-case")
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
                var json = JsonSerializer.Serialize(msg);
                _writer.WriteLine(json);
                _writer.Flush();
                RevitTestModelHelper.RollBackTransactionGroup();
                RevitModelService.CurrentDocument = null;
            }
        }
        catch { }
    }
}

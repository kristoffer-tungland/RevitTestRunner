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
    private static UIApplication? _uiApplication;
    private static readonly Dictionary<string, Document> _openDocs = new();
    private const string LocalPrefix = "local:";

    public static void ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        _uiApplication = uiApp;
        
        // Set up model service with our local handlers
        RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
        RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
        RevitModelService.CancellationToken = cancellationToken;
        
        var testAssemblyPath = command.TestAssembly;
        var methods = command.TestMethods;
        
        // Isolate test assemblies without unloading them
        var loadContext = new AssemblyLoadContext("TestContext", isCollectible: false);

        // Ensure dependencies are loaded from the test assembly directory
        var testDir = Path.GetDirectoryName(testAssemblyPath) ?? string.Empty;
        loadContext.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(testDir, name.Name + ".dll");
            return File.Exists(candidate) ? loadContext.LoadFromAssemblyPath(candidate) : null;
        };

        var engineAssemblyPath = typeof(TestEngineActivator).Assembly.Location;
        loadContext.LoadFromAssemblyPath(engineAssemblyPath);
        loadContext.LoadFromAssemblyPath(testAssemblyPath);

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

        var listener = new StreamingNUnitEventListener(writer, cancellationToken);
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
        RevitModelService.CancellationToken = CancellationToken.None;
    }
}

internal class StreamingNUnitEventListener : ITestEventListener
{
    private readonly StreamWriter _writer;
    private readonly CancellationToken _token;

    public StreamingNUnitEventListener(StreamWriter writer, CancellationToken token)
    {
        _writer = writer;
        _token = token;
    }

    public void OnTestEvent(string report)
    {
        if (_token.IsCancellationRequested)
            return;
        try
        {
            var xml = System.Xml.Linq.XElement.Parse(report);
            if (xml.Name == "test-case")
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
            }
        }
        catch { }
    }
}
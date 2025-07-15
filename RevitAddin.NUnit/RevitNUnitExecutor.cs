using System.Runtime.Loader;
using System.Reflection;
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

    public static void ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        _uiApplication = uiApp;
        
        // Set up model service with our local handlers
        RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
        RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
        RevitModelService.CancellationToken = cancellationToken;
        
        var originalTestAssemblyPath = command.TestAssembly;
        var methods = command.TestMethods;

        // Create a temporary directory for this test run to avoid file locking issues
        string tempTestDir = Path.Combine(Path.GetTempPath(), "RevitNUnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempTestDir);

        string testAssemblyPath;
        try
        {
            // Copy the test assembly and all its dependencies to the temp directory
            testAssemblyPath = CopyTestAssemblyWithDependencies(originalTestAssemblyPath, tempTestDir);
            
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
            var testAssembly = loadContext.LoadFromAssemblyPath(testAssemblyPath);

            var attributeMap = new Dictionary<string, RevitTestFramework.NUnit.RevitNUnitTestModelAttribute>();
            foreach (var type in testAssembly.GetTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    var attr = method.GetCustomAttribute<RevitTestFramework.NUnit.RevitNUnitTestModelAttribute>();
                    if (attr != null)
                    {
                        var name = type.FullName + "." + method.Name;
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
            // Handle exceptions that occur during test execution
            try
            {
                // Create a failure result message for any tests that were supposed to run
                var failureMessage = new PipeTestResultMessage
                {
                    Name = methods?.Length > 0 ? string.Join(", ", methods) : "TestExecution",
                    Outcome = "Failed",
                    Duration = 0,
                    ErrorMessage = $"Test execution failed: {ex.Message}",
                    ErrorStackTrace = ex.ToString()
                };

                // Report the failure
                var json = JsonSerializer.Serialize(failureMessage);
                writer.WriteLine(json);
                writer.WriteLine("END");
                writer.Flush();

                // Log the exception for debugging
                System.Diagnostics.Debug.WriteLine($"RevitNUnitExecutor: Test execution failed with exception: {ex}");
            }
            catch (Exception writeEx)
            {
                // If we can't even write the error, log it
                System.Diagnostics.Debug.WriteLine($"RevitNUnitExecutor: Failed to write error message: {writeEx}");
                System.Diagnostics.Debug.WriteLine($"RevitNUnitExecutor: Original exception: {ex}");
            }
        }
        finally
        {
            // Clean up the temporary directory
            try
            {
                if (Directory.Exists(tempTestDir))
                {
                    Directory.Delete(tempTestDir, true);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup failure but don't fail the test run
                System.Diagnostics.Debug.WriteLine($"Failed to clean up temp directory {tempTestDir}: {ex.Message}");
            }
            
            RevitModelService.CancellationToken = CancellationToken.None;
        }
    }

    /// <summary>
    /// Copy the test assembly and all its dependencies to a temporary directory to avoid file locking issues
    /// </summary>
    /// <param name="originalAssemblyPath">Path to the original test assembly</param>
    /// <param name="tempDir">Temporary directory to copy files to</param>
    /// <returns>Path to the copied test assembly</returns>
    private static string CopyTestAssemblyWithDependencies(string originalAssemblyPath, string tempDir)
    {
        if (!File.Exists(originalAssemblyPath))
        {
            throw new FileNotFoundException($"Test assembly not found: {originalAssemblyPath}");
        }

        // Get the directory containing the original assembly
        string originalDir = Path.GetDirectoryName(originalAssemblyPath) ?? throw new DirectoryNotFoundException($"Could not determine directory for {originalAssemblyPath}");
        string assemblyFileName = Path.GetFileName(originalAssemblyPath);
        string tempAssemblyPath = Path.Combine(tempDir, assemblyFileName);

        // Copy the main test assembly
        File.Copy(originalAssemblyPath, tempAssemblyPath, true);

        // Copy all DLL files from the original directory (dependencies)
        string[] allDlls = Directory.GetFiles(originalDir, "*.dll");
        foreach (string dll in allDlls)
        {
            if (dll != originalAssemblyPath) // Skip the main assembly as it's already copied
            {
                string dllFileName = Path.GetFileName(dll);
                string tempDllPath = Path.Combine(tempDir, dllFileName);
                
                try
                {
                    File.Copy(dll, tempDllPath, true);
                }
                catch (Exception ex)
                {
                    // Log but continue - some dependencies might not be critical
                    System.Diagnostics.Debug.WriteLine($"Failed to copy dependency {dll}: {ex.Message}");
                }
            }
        }

        // Also copy PDB files for debugging support
        string[] allPdbs = Directory.GetFiles(originalDir, "*.pdb");
        foreach (string pdb in allPdbs)
        {
            string pdbFileName = Path.GetFileName(pdb);
            string tempPdbPath = Path.Combine(tempDir, pdbFileName);
            
            try
            {
                File.Copy(pdb, tempPdbPath, true);
            }
            catch (Exception ex)
            {
                // Log but continue - PDB files are not critical for execution
                System.Diagnostics.Debug.WriteLine($"Failed to copy PDB file {pdb}: {ex.Message}");
            }
        }

        // Copy any JSON configuration files (like deps.json, runtimeconfig.json)
        string[] configFiles = Directory.GetFiles(originalDir, "*.json");
        foreach (string configFile in configFiles)
        {
            string configFileName = Path.GetFileName(configFile);
            string tempConfigPath = Path.Combine(tempDir, configFileName);
            
            try
            {
                File.Copy(configFile, tempConfigPath, true);
            }
            catch (Exception ex)
            {
                // Log but continue - some config files might not be critical
                System.Diagnostics.Debug.WriteLine($"Failed to copy config file {configFile}: {ex.Message}");
            }
        }

        return tempAssemblyPath;
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
                        RevitModelService.OpenLocalModel!,
                        RevitModelService.OpenCloudModel!,
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
            }
        }
        catch { }
    }
}
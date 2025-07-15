using System.Runtime.Loader;
using System.Xml.Linq;
using Autodesk.Revit.UI;
using Xunit;
using Xunit.Abstractions;
using RevitAddin.Common;
using RevitTestFramework.Common;
using System.Text.Json;

namespace RevitAddin.Xunit;

public static class RevitXunitExecutor
{
    public static void ExecuteTestsInRevit(PipeCommand command, UIApplication uiApp, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Set up model service with our local handlers
        RevitModelService.OpenLocalModel = localPath => RevitModelUtility.EnsureModelOpen(uiApp, localPath);
        RevitModelService.OpenCloudModel = (projectGuid, modelGuid) => RevitModelUtility.EnsureModelOpen(uiApp, projectGuid, modelGuid);
        RevitModelService.CancellationToken = cancellationToken;
        
        var originalTestAssemblyPath = command.TestAssembly;
        var methods = command.TestMethods;

        // Create a temporary directory for this test run to avoid file locking issues
        string tempTestDir = Path.Combine(Path.GetTempPath(), "RevitXunitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempTestDir);

        string testAssemblyPath;
        try
        {
            // Copy the test assembly and all its dependencies to the temp directory
            testAssemblyPath = CopyTestAssemblyWithDependencies(originalTestAssemblyPath, tempTestDir);
            
            var loadContext = new AssemblyLoadContext("XUnitTestContext", isCollectible: false);

            var testDir = Path.GetDirectoryName(testAssemblyPath) ?? string.Empty;
            loadContext.Resolving += (_, name) =>
            {
                var candidate = Path.Combine(testDir, name.Name + ".dll");
                return File.Exists(candidate) ? loadContext.LoadFromAssemblyPath(candidate) : null;
            };

            loadContext.LoadFromAssemblyPath(typeof(XunitFrontController).Assembly.Location);
            loadContext.LoadFromAssemblyPath(testAssemblyPath);

            var assemblyElement = new XElement("assembly");
            using var controller = new XunitFrontController(AppDomainSupport.Denied, testAssemblyPath);
            var discoveryOptions = TestFrameworkOptions.ForDiscovery();
            var executionOptions = TestFrameworkOptions.ForExecution();

            List<ITestCase> testCases;
            using (var discoverySink = new TestDiscoverySink())
            {
                controller.Find(false, discoverySink, discoveryOptions);
                discoverySink.Finished.WaitOne();
                testCases = discoverySink.TestCases.ToList();
            }

            if (methods != null && methods.Length > 0)
            {
                testCases = testCases.Where(tc => methods.Contains(tc.TestMethod.TestClass.Class.Name + "." + tc.TestMethod.Method.Name)).ToList();
            }

            using var visitor = new StreamingXmlTestExecutionVisitor(writer, assemblyElement, () => cancellationToken.IsCancellationRequested);
            controller.RunTests(testCases, visitor, executionOptions);
            visitor.Finished.WaitOne();

            string resultXml = new XDocument(assemblyElement).ToString();
            var fileName = $"RevitXunitResults_{Guid.NewGuid():N}.xml";
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
                System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Test execution failed with exception: {ex}");
            }
            catch (Exception writeEx)
            {
                // If we can't even write the error, log it
                System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Failed to write error message: {writeEx}");
                System.Diagnostics.Debug.WriteLine($"RevitXunitExecutor: Original exception: {ex}");
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

internal class StreamingXmlTestExecutionVisitor : XmlTestExecutionVisitor
{
    private readonly StreamWriter _writer;

    public StreamingXmlTestExecutionVisitor(StreamWriter writer, XElement assemblyElement, Func<bool> cancelThunk)
        : base(assemblyElement, cancelThunk)
    {
        _writer = writer;
    }

    private void Send(PipeTestResultMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        _writer.WriteLine(json);
        _writer.Flush();
    }

    protected override bool Visit(ITestPassed testPassed)
    {
        Send(new PipeTestResultMessage
        {
            Name = testPassed.Test.DisplayName,
            Outcome = "Passed",
            Duration = (double)testPassed.ExecutionTime
        });
        return base.Visit(testPassed);
    }

    protected override bool Visit(ITestFailed testFailed)
    {
        Send(new PipeTestResultMessage
        {
            Name = testFailed.Test.DisplayName,
            Outcome = "Failed",
            Duration = (double)testFailed.ExecutionTime,
            ErrorMessage = string.Join(Environment.NewLine, testFailed.Messages ?? Array.Empty<string>()),
            ErrorStackTrace = string.Join(Environment.NewLine, testFailed.StackTraces ?? Array.Empty<string>())
        });
        return base.Visit(testFailed);
    }

    protected override bool Visit(ITestSkipped testSkipped)
    {
        Send(new PipeTestResultMessage
        {
            Name = testSkipped.Test.DisplayName,
            Outcome = "Skipped",
            Duration = 0,
            ErrorMessage = testSkipped.Reason
        });
        return base.Visit(testSkipped);
    }
}
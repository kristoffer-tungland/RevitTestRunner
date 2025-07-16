using System.IO.Pipes;
using System.Runtime.Loader;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;

namespace RevitAddin.Xunit;

public class TestCommandHandler : ITestCommandHandler
{
    private PipeCommand? _command;
    private NamedPipeServerStream? _pipe;
    private TaskCompletionSource? _tcs;
    private readonly ModelOpeningExternalEvent _modelOpener;

    public TestCommandHandler(ModelOpeningExternalEvent modelOpener)
    {
        _modelOpener = modelOpener ?? throw new ArgumentNullException(nameof(modelOpener));
    }

    public void SetContext(PipeCommand command, NamedPipeServerStream pipe, TaskCompletionSource tcs)
    {
        _command = command;
        _pipe = pipe;
        _tcs = tcs;
    }

    public void Execute(UIApplication app)
    {
        if (_command == null || _pipe == null || _tcs == null)
            return;

        // Initialize the model utility with the UI application and pre-created model opener
        RevitModelUtility.Initialize(app, _modelOpener);

        using var writer = new StreamWriter(_pipe, leaveOpen: true);

        using var cancelClient = new NamedPipeClientStream(".", _command.CancelPipe, PipeDirection.In);
        var cts = new CancellationTokenSource();
        try
        {
            cancelClient.Connect(100);
            _ = Task.Run(() =>
            {
                using var sr = new StreamReader(cancelClient);
                sr.ReadLine();
                cts.Cancel();
            });
        }
        catch { }

        if (_command.Command == "RunXunitTests")
        {
            // Create custom ALC and execute tests within it using reflection
            ExecuteTestsInIsolatedContext(_command, app, writer, cts.Token);
        }
        
        _tcs.SetResult();
    }

    private void ExecuteTestsInIsolatedContext(PipeCommand command, UIApplication app, StreamWriter writer, CancellationToken cancellationToken)
    {
        // Create a temporary directory for this test run to avoid file locking issues
        string tempTestDir = Path.Combine(Path.GetTempPath(), "RevitXunitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempTestDir);

        try
        {
            // Copy the test assembly and all its dependencies to the temp directory
            var testAssemblyPath = CopyTestAssemblyWithDependencies(command.TestAssembly, tempTestDir);

            // Create custom ALC that can resolve from both temp dir and RevitAddin directory
            var loadContext = new XunitTestAssemblyLoadContext(tempTestDir);
            
            // Load the RevitAddin.Xunit assembly in the custom context
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var revitAddinXunitAssembly = loadContext.LoadFromAssemblyPath(currentAssemblyPath);
            
            // Get the RevitXunitExecutor type from the custom ALC
            var executorType = revitAddinXunitAssembly.GetType("RevitAddin.Xunit.RevitXunitExecutor") 
                ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type");

            // Get the ExecuteTestsInRevit method
            var executeMethod = executorType.GetMethod("ExecuteTestsInRevit", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find ExecuteTestsInRevit method");

            // Serialize the command to avoid cross-ALC type issues
            var commandJson = System.Text.Json.JsonSerializer.Serialize(command);

            // Create parameters for the method call
            // Note: We pass only primitive types and types from the default context that are safe to cross boundaries
            var parameters = new object[]
            {
                commandJson, // Serialized PipeCommand as JSON string
                testAssemblyPath, // Direct path to copied test assembly
                app, // UIApplication is from Revit API (default context)
                writer, // StreamWriter is from System.IO (default context) 
                cancellationToken // CancellationToken is a value type
                // Remove _modelOpener to avoid cross-ALC type conversion issues
            };

            // Invoke the method in the custom ALC - now it can use xUnit directly!
            executeMethod.Invoke(null, parameters);
        }
        catch (Exception ex)
        {
            HandleTestExecutionException(ex, command.TestMethods, writer);
        }
        finally
        {
            // Clean up the temporary directory
            CleanupTempDirectory(tempTestDir);
        }
    }

    private static void HandleTestExecutionException(Exception ex, string[]? methods, StreamWriter writer)
    {
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
            var json = System.Text.Json.JsonSerializer.Serialize(failureMessage);
            writer.WriteLine(json);
            writer.WriteLine("END");
            writer.Flush();

            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Test execution failed with exception: {ex}");
        }
        catch (Exception writeEx)
        {
            // If we can't even write the error, log it
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Failed to write error message: {writeEx}");
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Original exception: {ex}");
        }
    }

    private static void CleanupTempDirectory(string tempTestDir)
    {
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

    public string GetName() => nameof(TestCommandHandler);
}

/// <summary>
/// Custom AssemblyLoadContext for loading xUnit tests in isolation
/// </summary>
internal class XunitTestAssemblyLoadContext : AssemblyLoadContext
{
    public string TestDirectory { get; }
    private readonly string _revitAddinDirectory;

    public XunitTestAssemblyLoadContext(string testDirectory) : base("XunitTestContext", isCollectible: false)
    {
        TestDirectory = testDirectory ?? throw new ArgumentNullException(nameof(testDirectory));
        
        // Get the directory where RevitAddin.Xunit is located (contains xUnit assemblies)
        _revitAddinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) 
            ?? throw new DirectoryNotFoundException("Could not determine RevitAddin.Xunit directory");
        
        // Set up assembly resolution
        Resolving += OnResolving;
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        // Don't resolve resource assemblies
        if (assemblyName.Name?.EndsWith(".resources") == true)
            return null;

        // First, try to load from the test directory (for test assemblies and their dependencies)
        var testDirPath = Path.Combine(TestDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(testDirPath))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Resolving assembly: {assemblyName.Name} from test directory: {testDirPath}");
                return LoadFromAssemblyPath(testDirPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {assemblyName.Name} from test directory: {ex.Message}");
            }
        }

        // Then, try to load from the RevitAddin directory (for xUnit assemblies and RevitAddin.Xunit)
        var revitAddinPath = Path.Combine(_revitAddinDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(revitAddinPath))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Resolving assembly: {assemblyName.Name} from RevitAddin directory: {revitAddinPath}");
                return LoadFromAssemblyPath(revitAddinPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {assemblyName.Name} from RevitAddin directory: {ex.Message}");
            }
        }

        // Check for version-specific files in test directory
        var testDirPattern = $"{assemblyName.Name}*.dll";
        var testDirCandidates = Directory.GetFiles(TestDirectory, testDirPattern);
        if (testDirCandidates.Length > 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Resolving assembly: {assemblyName.Name} from test directory (pattern match): {testDirCandidates[0]}");
                return LoadFromAssemblyPath(testDirCandidates[0]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {assemblyName.Name} from test directory (pattern): {ex.Message}");
            }
        }

        // Check for version-specific files in RevitAddin directory
        var revitAddinPattern = $"{assemblyName.Name}*.dll";
        var revitAddinCandidates = Directory.GetFiles(_revitAddinDirectory, revitAddinPattern);
        if (revitAddinCandidates.Length > 0)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Resolving assembly: {assemblyName.Name} from RevitAddin directory (pattern match): {revitAddinCandidates[0]}");
                return LoadFromAssemblyPath(revitAddinCandidates[0]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {assemblyName.Name} from RevitAddin directory (pattern): {ex.Message}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"Failed to resolve assembly: {assemblyName.Name}");
        return null;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to load from either directory
        return OnResolving(this, assemblyName);
    }
}
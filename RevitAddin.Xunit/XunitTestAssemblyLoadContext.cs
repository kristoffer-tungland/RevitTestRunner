using System.Runtime.Loader;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitAddin.Common;

namespace RevitAddin.Xunit;

/// <summary>
/// Custom AssemblyLoadContext for loading xUnit tests in isolation
/// </summary>
internal class XunitTestAssemblyLoadContext : AssemblyLoadContext, IXunitTestAssemblyLoadContext
{
    public string TestDirectory { get; }
    private readonly string _revitAddinDirectory;
    private Assembly? _revitAddinXunitAssembly;

    public XunitTestAssemblyLoadContext(string testDirectory) : base("XunitTestContext", isCollectible: false)
    {
        TestDirectory = testDirectory ?? throw new ArgumentNullException(nameof(testDirectory));

        // Get the directory where RevitAddin.Xunit is located (contains xUnit assemblies)
        _revitAddinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new DirectoryNotFoundException("Could not determine RevitAddin.Xunit directory");

        // Set up assembly resolution
        Resolving += OnResolving;
    }

    /// <summary>
    /// Loads the RevitAddin.Xunit assembly into this AssemblyLoadContext
    /// </summary>
    /// <returns>The loaded assembly</returns>
    public Assembly LoadRevitAddinTestAssembly()
    {
        if (_revitAddinXunitAssembly == null)
        {
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            _revitAddinXunitAssembly = LoadFromAssemblyPath(currentAssemblyPath);
        }
        return _revitAddinXunitAssembly;
    }

    /// <summary>
    /// Sets up the Revit test infrastructure by calling the SetupInfrastructure method via reflection
    /// </summary>
    /// <param name="app">The UIApplication to pass to the setup method</param>
    /// <returns>The infrastructure object returned by SetupInfrastructure</returns>
    public void SetupInfrastructure(UIApplication app)
    {
        var assembly = LoadRevitAddinTestAssembly();
        var executorType = assembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type");

        var setupMethod = executorType.GetMethod("SetupInfrastructure", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find SetupInfrastructure method");

        setupMethod.Invoke(null, [app]);
    }

    /// <summary>
    /// Tears down the Revit test infrastructure by calling the TeardownInfrastructure method via reflection
    /// </summary>
    /// <param name="infrastructure">The infrastructure object to tear down</param>
    public void TeardownInfrastructure()
    {
        if (_revitAddinXunitAssembly == null)
            throw new InvalidOperationException("RevitAddin.Xunit assembly has not been loaded. Call LoadRevitAddinTestAssembly first.");

        var executorType = _revitAddinXunitAssembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type for teardown");

        var teardownMethod = executorType.GetMethod("TeardownInfrastructure", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TeardownInfrastructure method");

        teardownMethod.Invoke(null, null);
    }

    /// <summary>
    /// Executes tests asynchronously by calling the ExecuteTestsInRevitAsync method via reflection
    /// </summary>
    /// <param name="command">The PipeCommand containing test execution details</param>
    /// <param name="testAssemblyPath">Path to the test assembly</param>
    /// <param name="app">UIApplication instance</param>
    /// <param name="writer">StreamWriter for output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A Task representing the async operation</returns>
    public Task ExecuteTestsAsync(PipeCommand command, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken)
    {
        var assembly = LoadRevitAddinTestAssembly();
        var executorType = assembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type");

        var executeMethod = executorType.GetMethod("ExecuteTestsInRevitAsync", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find ExecuteTestsInRevitAsync method");

        // Serialize the command to avoid cross-ALC type issues
        var commandJson = System.Text.Json.JsonSerializer.Serialize(command);

        var parameters = new object[]
        {
            commandJson,
            testAssemblyPath,
            writer,
            cancellationToken
        };

        return (Task)executeMethod.Invoke(null, parameters)!;
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
using System.Runtime.Loader;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitAddin.Common;

namespace RevitAddin.NUnit;

/// <summary>
/// Custom AssemblyLoadContext for loading NUnit tests in isolation
/// </summary>
internal class NunitTestAssemblyLoadContext : AssemblyLoadContext, ITestAssemblyLoadContext
{
    public string TestDirectory { get; }
    private readonly string _revitAddinDirectory;
    private Assembly? _revitAddinNunitAssembly;

    public NunitTestAssemblyLoadContext(string testDirectory) : base("NunitTestContext", isCollectible: false)
    {
        TestDirectory = testDirectory ?? throw new ArgumentNullException(nameof(testDirectory));

        _revitAddinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new DirectoryNotFoundException("Could not determine RevitAddin.NUnit directory");

        Resolving += OnResolving;
    }

    public Assembly LoadRevitAddinTestAssembly()
    {
        if (_revitAddinNunitAssembly == null)
        {
            var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            _revitAddinNunitAssembly = LoadFromAssemblyPath(currentAssemblyPath);
        }
        return _revitAddinNunitAssembly;
    }

    public void SetupInfrastructure(UIApplication app)
    {
        var assembly = LoadRevitAddinTestAssembly();
        var executorType = assembly.GetType("RevitAddin.NUnit.RevitNUnitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitNUnitExecutor type");

        var setupMethod = executorType.GetMethod("SetupInfrastructure", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find SetupInfrastructure method");

        setupMethod.Invoke(null, [app]);
    }

    public void TeardownInfrastructure()
    {
        if (_revitAddinNunitAssembly == null)
            throw new InvalidOperationException("RevitAddin.NUnit assembly has not been loaded. Call LoadRevitAddinTestAssembly first.");

        var executorType = _revitAddinNunitAssembly.GetType("RevitAddin.NUnit.RevitNUnitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitNUnitExecutor type for teardown");

        var teardownMethod = executorType.GetMethod("TeardownInfrastructure", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find TeardownInfrastructure method");

        teardownMethod.Invoke(null, null);
    }

    public Task ExecuteTestsAsync(PipeCommand command, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken)
    {
        var assembly = LoadRevitAddinTestAssembly();
        var executorType = assembly.GetType("RevitAddin.NUnit.RevitNUnitExecutor")
            ?? throw new InvalidOperationException("Could not find RevitNUnitExecutor type");

        var executeMethod = executorType.GetMethod("ExecuteTestsInRevitAsync", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not find ExecuteTestsInRevitAsync method");

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
        if (assemblyName.Name?.EndsWith(".resources") == true)
            return null;

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
        return OnResolving(this, assemblyName);
    }
}

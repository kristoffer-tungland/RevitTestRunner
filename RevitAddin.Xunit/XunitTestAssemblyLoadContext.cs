using System.Runtime.Loader;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitAddin.Common;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;

namespace RevitAddin.Xunit;

/// <summary>
/// Custom AssemblyLoadContext for loading xUnit tests in isolation
/// </summary>
internal class XunitTestAssemblyLoadContext : AssemblyLoadContext, ITestAssemblyLoadContext, IPipeAware
{
    public string TestDirectory { get; }
    private readonly string _revitAddinDirectory;
    private Assembly? _revitAddinXunitAssembly;
    private ILogger _logger;
    private StreamWriter? _pipeWriter;

    public XunitTestAssemblyLoadContext(string testDirectory) : base("XunitTestContext", isCollectible: false)
    {
        TestDirectory = testDirectory ?? throw new ArgumentNullException(nameof(testDirectory));

        // Get the directory where RevitAddin.Xunit is located (contains xUnit assemblies)
        _revitAddinDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new DirectoryNotFoundException("Could not determine RevitAddin.Xunit directory");

        // Start with file logger, will be upgraded to pipe-aware logger if pipe writer is provided
        _logger = FileLogger.ForContext<XunitTestAssemblyLoadContext>();
        _logger.LogTrace($"Logger initialized. Log file: {Path.Combine(_revitAddinDirectory, "Logs", $"RevitTestFramework.Common-{DateTime.Now:yyyyMMdd}.log")}");
        _logger.LogDebug($"XunitTestAssemblyLoadContext created for test directory: {TestDirectory}");
        _logger.LogDebug($"RevitAddin directory: {_revitAddinDirectory}");

        // Set up assembly resolution
        Resolving += OnResolving;
    }

    /// <summary>
    /// Sets the pipe writer for forwarding logs to the test framework
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to use for log forwarding</param>
    public void SetPipeWriter(StreamWriter pipeWriter)
    {
        _pipeWriter = pipeWriter;
        // Upgrade to pipe-aware logger
        _logger = PipeAwareLogger.ForContext<XunitTestAssemblyLoadContext>(pipeWriter);
        _logger.LogDebug("Pipe writer configured for log forwarding");
        
        // IMPORTANT: Pass the pipe writer to RevitXunitExecutor so it can set up
        // pipe-aware logging for RevitTestModelHelper in the correct assembly load context
        // This is done through reflection since we're in different contexts
        try
        {
            var assembly = LoadRevitAddinTestAssembly();
            var executorType = assembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
                ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type");

            var setPipeWriterMethod = executorType.GetMethod("SetPipeWriter", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find SetPipeWriter method");

            setPipeWriterMethod.Invoke(null, [pipeWriter]);
            _logger.LogDebug("RevitXunitExecutor.SetPipeWriter called successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set pipe writer on RevitXunitExecutor");
        }
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
            _logger.LogDebug($"Loading RevitAddin.Xunit assembly from: {currentAssemblyPath}");
            _revitAddinXunitAssembly = LoadFromAssemblyPath(currentAssemblyPath);
            _logger.LogInformation("RevitAddin.Xunit assembly loaded successfully");
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
        try
        {
            _logger.LogDebug("Setting up Revit test infrastructure");
            var assembly = LoadRevitAddinTestAssembly();
            var executorType = assembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
                ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type");

            var setupMethod = executorType.GetMethod("SetupInfrastructure", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find SetupInfrastructure method");

            setupMethod.Invoke(null, [app]);
            _logger.LogDebug("Revit test infrastructure setup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup Revit test infrastructure");
            throw;
        }
    }

    /// <summary>
    /// Tears down the Revit test infrastructure by calling the TeardownInfrastructure method via reflection
    /// </summary>
    /// <param name="infrastructure">The infrastructure object to tear down</param>
    public void TeardownInfrastructure()
    {
        try
        {
            _logger.LogDebug("Tearing down Revit test infrastructure");
            
            if (_revitAddinXunitAssembly == null)
                throw new InvalidOperationException("RevitAddin.Xunit assembly has not been loaded. Call LoadRevitAddinTestAssembly first.");

            var executorType = _revitAddinXunitAssembly.GetType("RevitAddin.Xunit.RevitXunitExecutor")
                ?? throw new InvalidOperationException("Could not find RevitXunitExecutor type for teardown");

            var teardownMethod = executorType.GetMethod("TeardownInfrastructure", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("Could not find TeardownInfrastructure method");

            teardownMethod.Invoke(null, null);
            _logger.LogDebug("Revit test infrastructure teardown completed successfully");
            
            // Note: RevitTestModelHelper reset is now handled by RevitXunitExecutor.TeardownInfrastructure
            _logger.LogDebug("RevitTestModelHelper reset is handled by RevitXunitExecutor.TeardownInfrastructure");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to teardown Revit test infrastructure");
            throw;
        }
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
        try
        {
            _logger.LogDebug($"Starting test execution for assembly: {testAssemblyPath}");
            var methodsStr = command.TestMethods != null ? string.Join(", ", command.TestMethods) : "All";
            _logger.LogDebug($"Test methods: {methodsStr}");
            
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

            _logger.LogDebug("Invoking ExecuteTestsInRevitAsync method");
            return (Task)executeMethod.Invoke(null, parameters)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to execute tests for assembly: {testAssemblyPath}");
            throw;
        }
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        // Don't resolve resource assemblies
        if (assemblyName.Name?.EndsWith(".resources") == true)
            return null;

        try
        {
            // First, try to load from the test directory (for test assemblies and their dependencies)
            var testDirPath = Path.Combine(TestDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(testDirPath))
            {
                _logger.LogTrace($"Resolving assembly: {assemblyName.Name} from test directory: {testDirPath}");
                return LoadFromAssemblyPath(testDirPath);
            }

            // Then, try to load from the RevitAddin directory (for xUnit assemblies and RevitAddin.Xunit)
            var revitAddinPath = Path.Combine(_revitAddinDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(revitAddinPath))
            {
                _logger.LogTrace($"Resolving assembly: {assemblyName.Name} from RevitAddin directory: {revitAddinPath}");
                return LoadFromAssemblyPath(revitAddinPath);
            }

            // Check for version-specific files in test directory
            var testDirPattern = $"{assemblyName.Name}*.dll";
            var testDirCandidates = Directory.GetFiles(TestDirectory, testDirPattern);
            if (testDirCandidates.Length > 0)
            {
                _logger.LogTrace($"Resolving assembly: {assemblyName.Name} from test directory (pattern match): {testDirCandidates[0]}");
                return LoadFromAssemblyPath(testDirCandidates[0]);
            }

            // Check for version-specific files in RevitAddin directory
            var revitAddinPattern = $"{assemblyName.Name}*.dll";
            var revitAddinCandidates = Directory.GetFiles(_revitAddinDirectory, revitAddinPattern);
            if (revitAddinCandidates.Length > 0)
            {
                _logger.LogTrace($"Resolving assembly: {assemblyName.Name} from RevitAddin directory (pattern match): {revitAddinCandidates[0]}");
                return LoadFromAssemblyPath(revitAddinCandidates[0]);
            }

            // Only log warnings for assemblies that we expect to find but failed to resolve
            // Skip common system assemblies and Revit API assemblies that should be resolved by the default context
            if (ShouldLogAssemblyResolutionFailure(assemblyName.Name))
            {
                _logger.LogWarning($"Failed to resolve assembly: {assemblyName.Name}");
            }
            else
            {
                _logger.LogTrace($"Assembly resolution delegated to default context: {assemblyName.Name}");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error while resolving assembly: {assemblyName.Name}");
            return null;
        }
    }

    /// <summary>
    /// Determines whether an assembly resolution failure should be logged as a warning
    /// </summary>
    /// <param name="assemblyName">The name of the assembly that failed to resolve</param>
    /// <returns>True if the failure should be logged as a warning, false if it should be logged as debug</returns>
    private static bool ShouldLogAssemblyResolutionFailure(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return false;

        // System assemblies - these are expected to be resolved by the default context
        var systemAssemblies = new[]
        {
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Text.Json",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Memory",
            "System.Diagnostics.Debug",
            "System.IO",
            "System.Reflection",
            "System.ComponentModel",
            "System.Diagnostics.Process",
            "System.Xml.XDocument", 
            "System.Globalization",
            "System.Diagnostics.TraceSource",
            "System.IO.FileSystem",
            "System.Runtime.Extensions",
            "System.Reflection.Extensions",
            "System.Collections.Concurrent",
            "System.Threading.Thread",
            "Microsoft.Extensions.DependencyInjection",
            "Microsoft.Extensions.Logging"
        };

        // Revit API assemblies - these should be resolved by the main AppDomain
        var revitAssemblies = new[]
        {
            "RevitAPI",
            "RevitAPIUI",
            "AdWindows",
            "UIFramework"
        };

        // Visual Studio and testing framework assemblies
        var vsTestingAssemblies = new[]
        {
            "Microsoft.VisualStudio.TestPlatform",
            "Microsoft.TestPlatform",
            "testhost"
        };

        return !systemAssemblies.Any(sys => assemblyName.StartsWith(sys, StringComparison.OrdinalIgnoreCase)) &&
               !revitAssemblies.Any(revit => assemblyName.StartsWith(revit, StringComparison.OrdinalIgnoreCase)) &&
               !vsTestingAssemblies.Any(vs => assemblyName.StartsWith(vs, StringComparison.OrdinalIgnoreCase));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to load from either directory
        return OnResolving(this, assemblyName);
    }
}
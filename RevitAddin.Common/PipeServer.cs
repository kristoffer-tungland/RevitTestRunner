using System.IO.Pipes;
using System.Text.Json;
using RevitTestFramework.Common;

namespace RevitAddin.Common;

public class PipeServer(string pipeName, RevitTask revitTask, Func<string, ITestAssemblyLoadContext> createLoadContext) : IDisposable
{
    private readonly string _pipeName = pipeName;
    private readonly RevitTask _revitTask = revitTask;
    private readonly Func<string, ITestAssemblyLoadContext> _createLoadContext = createLoadContext ?? throw new ArgumentNullException(nameof(createLoadContext));
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;

    public void Start()
    {
        _listenerTask = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

            using var reader = new StreamReader(server, leaveOpen: true);
            while (server.IsConnected && !_cts.IsCancellationRequested)
            {
                var json = await reader.ReadLineAsync().ConfigureAwait(false);
                if (json == null)
                    break;

                var command = JsonSerializer.Deserialize<PipeCommand>(json);
                if (command != null)
                {
                    // Create command handler and execute
                    await ProcessCommandAsync(command, server).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Processes a single command using the PipeCommandHandler pattern
    /// </summary>
    private async Task ProcessCommandAsync(PipeCommand command, NamedPipeServerStream server)
    {
        if (command.Command == "RunXunitTests" || command.Command == "RunNUnitTests")
        {

            // Create a temporary directory for this test run to avoid file locking issues
            string tempTestDir = Path.Combine(Path.GetTempPath(), command.Command, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempTestDir);

            try
            {
                // Copy the test assembly and all its dependencies to the temp directory
                var testAssemblyPath = CopyTestAssemblyWithDependencies(command.TestAssembly, tempTestDir);

                // Create the PipeCommandHandler with all dependencies
                var commandHandler = new PipeCommandHandler(command, server, _revitTask, testAssemblyPath, _createLoadContext);

                // Execute the command through the handler
                await commandHandler.ExecuteAsync().ConfigureAwait(false);
            }
            finally
            {
                // Clean up the temporary directory
                CleanupTempDirectory(tempTestDir);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            // Cancel the token source first to signal the ListenAsync loop to exit
            _cts.Cancel();
            
            // Wait for the listener task to complete with a reasonable timeout
            if (_listenerTask != null)
            {
                if (!_listenerTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    System.Diagnostics.Debug.WriteLine("PipeServer: Listener task did not complete within timeout");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PipeServer: Error during disposal: {ex.Message}");
        }
        finally
        {
            _cts.Dispose();
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
}
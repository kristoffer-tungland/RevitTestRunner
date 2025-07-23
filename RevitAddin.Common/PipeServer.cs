using System.IO.Pipes;
using System.Text.Json;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;

namespace RevitAddin.Common;

public class PipeServer(string pipeName, RevitTask revitTask, Func<string, ITestAssemblyLoadContext> createLoadContext) : IDisposable
{
    private readonly string _pipeName = pipeName;
    private readonly RevitTask _revitTask = revitTask;
    private readonly Func<string, ITestAssemblyLoadContext> _createLoadContext = createLoadContext ?? throw new ArgumentNullException(nameof(createLoadContext));
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;
    private static readonly ILogger Logger = FileLogger.ForContext<PipeServer>();

    public void Start()
    {
        Logger.LogInformation($"Starting pipe server with pipe name: {_pipeName}");
        _listenerTask = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        Logger.LogInformation("Pipe server listening for connections");
        
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Logger.LogDebug($"Waiting for client connection on pipe: {_pipeName}");
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
                Logger.LogInformation("Client connected to pipe server");

                using var reader = new StreamReader(server, leaveOpen: true);
                while (server.IsConnected && !_cts.IsCancellationRequested)
                {
                    var json = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (json == null)
                    {
                        Logger.LogDebug("Received null data from pipe, connection likely closed");
                        break;
                    }

                    Logger.LogDebug($"Received command JSON: {json}");
                    
                    var command = JsonSerializer.Deserialize<PipeCommand>(json);
                    if (command != null)
                    {
                        Logger.LogInformation($"Processing command: {command.Command} for assembly: {command.TestAssembly}");
                            
                        // Create command handler and execute
                        await ProcessCommandAsync(command, server).ConfigureAwait(false);
                    }
                    else
                    {
                        Logger.LogWarning($"Failed to deserialize pipe command from JSON: {json}");
                    }
                }
                
                Logger.LogInformation("Client disconnected from pipe server");
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Pipe server listener canceled");
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in pipe server listener");
                // Continue listening for new connections unless canceled
            }
        }
        
        Logger.LogInformation("Pipe server listener stopped");
    }

    /// <summary>
    /// Processes a single command using the PipeCommandHandler pattern
    /// </summary>
    private async Task ProcessCommandAsync(PipeCommand command, NamedPipeServerStream server)
    {
        if (command.Command == "RunTests")
        {
            Logger.LogInformation($"Processing RunTests command for assembly: {command.TestAssembly}");

            // Create a temporary directory for this test run to avoid file locking issues
            string tempTestDir = Path.Combine(Path.GetTempPath(), command.Command, Guid.NewGuid().ToString("N"));
            Logger.LogDebug($"Creating temporary test directory: {tempTestDir}");
            Directory.CreateDirectory(tempTestDir);

            try
            {
                // Copy the test assembly and all its dependencies to the temp directory
                var testAssemblyPath = CopyTestAssemblyWithDependencies(command.TestAssembly, tempTestDir);
                Logger.LogInformation($"Test assembly copied to temporary location: {testAssemblyPath}");

                // Create the PipeCommandHandler with all dependencies
                var commandHandler = new PipeCommandHandler(command, server, _revitTask, testAssemblyPath, _createLoadContext);

                // Execute the command through the handler
                Logger.LogDebug("Executing command handler");
                await commandHandler.ExecuteAsync().ConfigureAwait(false);
                Logger.LogInformation("Command handler execution completed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to process RunTests command for assembly: {command.TestAssembly}");
                throw;
            }
            finally
            {
                // Clean up the temporary directory
                Logger.LogDebug($"Cleaning up temporary test directory: {tempTestDir}");
                CleanupTempDirectory(tempTestDir);
            }
        }
        else
        {
            Logger.LogWarning($"Unknown command received: {command.Command}");
        }
    }

    public void Dispose()
    {
        Logger.LogInformation("Disposing pipe server");
        
        try
        {
            // Cancel the token source first to signal the ListenAsync loop to exit
            _cts.Cancel();
            
            // Wait for the listener task to complete with a reasonable timeout
            if (_listenerTask != null)
            {
                Logger.LogDebug("Waiting for listener task to complete");
                if (!_listenerTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    Logger.LogWarning("Pipe server listener task did not complete within timeout");
                }
                else
                {
                    Logger.LogDebug("Listener task completed successfully");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during pipe server disposal");
        }
        finally
        {
            _cts.Dispose();
            Logger.LogInformation("Pipe server disposed");
        }
    }

    private static void CleanupTempDirectory(string tempTestDir)
    {
        try
        {
            if (Directory.Exists(tempTestDir))
            {
                Directory.Delete(tempTestDir, true);
                Logger.LogDebug($"Successfully cleaned up temporary directory: {tempTestDir}");
            }
        }
        catch (Exception ex)
        {
            // Log cleanup failure but don't fail the test run
            Logger.LogWarning($"Failed to clean up temporary directory: {tempTestDir} - {ex.Message}");
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
        Logger.LogDebug($"Copying test assembly and dependencies from: {originalAssemblyPath} to: {tempDir}");
            
        if (!File.Exists(originalAssemblyPath))
        {
            var ex = new FileNotFoundException($"Test assembly not found: {originalAssemblyPath}");
            Logger.LogError(ex, "Test assembly file not found");
            throw ex;
        }

        // Get the directory containing the original assembly
        string originalDir = Path.GetDirectoryName(originalAssemblyPath) ?? throw new DirectoryNotFoundException($"Could not determine directory for {originalAssemblyPath}");
        string assemblyFileName = Path.GetFileName(originalAssemblyPath);
        string tempAssemblyPath = Path.Combine(tempDir, assemblyFileName);

        // Copy the main test assembly
        File.Copy(originalAssemblyPath, tempAssemblyPath, true);
        Logger.LogDebug($"Copied main assembly: {assemblyFileName}");

        // Copy all DLL files from the original directory (dependencies)
        string[] allDlls = Directory.GetFiles(originalDir, "*.dll");
        int copiedDlls = 0;
        foreach (string dll in allDlls)
        {
            if (dll != originalAssemblyPath) // Skip the main assembly as it's already copied
            {
                string dllFileName = Path.GetFileName(dll);
                string tempDllPath = Path.Combine(tempDir, dllFileName);

                try
                {
                    File.Copy(dll, tempDllPath, true);
                    copiedDlls++;
                }
                catch (Exception ex)
                {
                    // Log but continue - some dependencies might not be critical
                    Logger.LogWarning($"Failed to copy dependency: {dllFileName} - {ex.Message}");
                }
            }
        }
        Logger.LogDebug($"Copied {copiedDlls} dependency DLL files");

        // Also copy PDB files for debugging support
        string[] allPdbs = Directory.GetFiles(originalDir, "*.pdb");
        int copiedPdbs = 0;
        foreach (string pdb in allPdbs)
        {
            string pdbFileName = Path.GetFileName(pdb);
            string tempPdbPath = Path.Combine(tempDir, pdbFileName);

            try
            {
                File.Copy(pdb, tempPdbPath, true);
                copiedPdbs++;
            }
            catch (Exception ex)
            {
                // Log but continue - PDB files are not critical for execution
                Logger.LogWarning($"Failed to copy PDB file: {pdbFileName} - {ex.Message}");
            }
        }
        Logger.LogDebug($"Copied {copiedPdbs} PDB files for debugging support");

        // Copy any JSON configuration files (like deps.json, runtimeconfig.json)
        string[] configFiles = Directory.GetFiles(originalDir, "*.json");
        int copiedConfigs = 0;
        foreach (string configFile in configFiles)
        {
            string configFileName = Path.GetFileName(configFile);
            string tempConfigPath = Path.Combine(tempDir, configFileName);

            try
            {
                File.Copy(configFile, tempConfigPath, true);
                copiedConfigs++;
            }
            catch (Exception ex)
            {
                // Log but continue - some config files might not be critical
                Logger.LogWarning($"Failed to copy config file: {configFileName} - {ex.Message}");
            }
        }
        Logger.LogDebug($"Copied {copiedConfigs} configuration files");

        Logger.LogInformation($"Successfully copied test assembly and dependencies. Main assembly at: {tempAssemblyPath}");
        return tempAssemblyPath;
    }
}
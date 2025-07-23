using System.IO.Pipes;
using RevitTestFramework.Common;
using RevitTestFramework.Contracts;

namespace RevitAddin.Common;

/// <summary>
/// Handles PipeCommand execution by coordinating between the command, server, task completion, and RevitTask
/// </summary>
public class PipeCommandHandler(PipeCommand command, NamedPipeServerStream server, RevitTask revitTask, string testAssemblyPath,
    Func<string, ITestAssemblyLoadContext> createLoadContext)
{
    private readonly PipeCommand _command = command ?? throw new ArgumentNullException(nameof(command));
    private readonly NamedPipeServerStream _server = server ?? throw new ArgumentNullException(nameof(server));
    private readonly RevitTask _revitTask = revitTask ?? throw new ArgumentNullException(nameof(revitTask));
    private readonly string _testAssemblyPath = testAssemblyPath ?? throw new ArgumentNullException(nameof(testAssemblyPath));
    private readonly Func<string, ITestAssemblyLoadContext> _createLoadContext = createLoadContext ?? throw new ArgumentNullException(nameof(createLoadContext));

    /// <summary>
    /// Executes the pipe command by running the handler on the Revit UI thread and waiting for completion
    /// </summary>
    /// <returns>A task that completes when the command execution is finished</returns>
    public async Task ExecuteAsync()
    {
        if (_command == null || _server == null)
            return;

        StreamWriter? writer = null;
        try
        {
            writer = new StreamWriter(_server, leaveOpen: true);
            
            // Create a pipe-aware logger that forwards logs to the test framework
            var logger = PipeAwareLogger.ForContext<PipeCommandHandler>(writer);
            
            logger.LogInformation($"Starting execution of pipe command: {_command?.Command} for assembly: {_command?.TestAssembly}");

            // Set up cancellation handling
            using var cancelClient = new NamedPipeClientStream(".", _command.CancelPipe, PipeDirection.In);
            var cts = new CancellationTokenSource();
            try
            {
                logger.LogDebug($"Connecting to cancellation pipe: {_command.CancelPipe}");
                cancelClient.Connect(100);
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var sr = new StreamReader(cancelClient);
                        sr.ReadLine();
                        logger.LogInformation("Cancellation request received via pipe");
                        cts.Cancel();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning($"Error reading from cancellation pipe: {ex.Message}");
                    }
                });
                logger.LogDebug("Cancellation pipe connection established");
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to connect to cancellation pipe: {_command.CancelPipe} - {ex.Message}");
            }

            var tempTestDir = Path.GetDirectoryName(_testAssemblyPath) ?? throw new InvalidOperationException("Test assembly path is invalid.");
            logger.LogDebug($"Creating test assembly load context for directory: {tempTestDir}");
            var loadContext = _createLoadContext(tempTestDir);

            // Pass the pipe writer to the load context if it supports it
            if (loadContext is IPipeAware pipeAwareContext)
            {
                pipeAwareContext.SetPipeWriter(writer);
            }

            try
            {
                logger.LogInformation("Setting up Revit test infrastructure");
                await _revitTask.Run(loadContext.SetupInfrastructure);
                
                logger.LogInformation($"Executing tests for assembly: {_testAssemblyPath}");
                await loadContext.ExecuteTestsAsync(_command, _testAssemblyPath, writer, cts.Token);
                
                logger.LogInformation("Test execution completed successfully");
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Test execution was canceled");
                // Don't rethrow cancellation exceptions - they're expected
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Test execution failed");
                HandleTestExecutionException(ex, _command.TestMethods, writer, logger);
            }
            finally
            {
                try
                {
                    logger.LogInformation("Tearing down Revit test infrastructure");
                    // IMPORTANT: Tear down the infrastructure on the UI thread
                    if (loadContext != null)
                    {
                        loadContext.TeardownInfrastructure();
                    }
                    logger.LogDebug("Infrastructure teardown completed");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to tear down test infrastructure");
                }
            }
            
            logger.LogInformation("Pipe command execution completed");
        }
        catch (Exception ex)
        {
            // Log any top-level exceptions
            FileLogger.ForContext<PipeCommandHandler>().LogError(ex, "Error during pipe command execution");
        }
        finally
        {
            // Safely dispose the StreamWriter with proper exception handling
            if (writer != null)
            {
                try
                {
                    writer.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Writer was already disposed, ignore
                }
                catch (IOException ex) when (ex.Message.Contains("Pipe is broken") || ex.Message.Contains("pipe has been ended"))
                {
                    // Pipe is broken during disposal, this is expected during cleanup
                    FileLogger.ForContext<PipeCommandHandler>().LogDebug("Pipe was broken during StreamWriter disposal - this is expected during cleanup");
                }
                catch (Exception ex)
                {
                    // Log other unexpected exceptions but don't rethrow
                    FileLogger.ForContext<PipeCommandHandler>().LogError(ex, "Unexpected error during StreamWriter disposal");
                }
            }
        }
    }

    /// <summary>
    /// Gets the command associated with this handler
    /// </summary>
    public PipeCommand Command => _command;

    /// <summary>
    /// Gets the server stream associated with this handler
    /// </summary>
    public NamedPipeServerStream Server => _server;

    /// <summary>
    /// Gets the RevitTask associated with this handler
    /// </summary>
    public RevitTask RevitTask => _revitTask;

    private static void HandleTestExecutionException(Exception ex, string[]? methods, StreamWriter writer, ILogger logger)
    {
        var methodsStr = methods != null ? string.Join(", ", methods) : "None";
        logger.LogError(ex, $"Handling test execution exception for methods: {methodsStr}");
            
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

            logger.LogDebug("Failure message written to pipe stream");
            
            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Test execution failed with exception: {ex}");
        }
        catch (ObjectDisposedException)
        {
            // Writer was already disposed, log to file only
            logger.LogWarning("Cannot write error message to pipe - writer was already disposed");
        }
        catch (IOException ioEx) when (ioEx.Message.Contains("Pipe is broken") || ioEx.Message.Contains("pipe has been ended"))
        {
            // Pipe is broken, log to file only
            logger.LogWarning("Cannot write error message to pipe - pipe is broken");
        }
        catch (Exception writeEx)
        {
            logger.LogFatal(writeEx, $"Failed to write error message to pipe stream. Original exception: {ex}");
            
            // If we can't even write the error, log it
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Failed to write error message: {writeEx}");
            System.Diagnostics.Debug.WriteLine($"TestCommandHandler: Original exception: {ex}");
        }
    }
}

/// <summary>
/// Interface for load contexts that can accept a pipe writer for forwarding logs
/// </summary>
public interface IPipeAware
{
    void SetPipeWriter(StreamWriter pipeWriter);
}

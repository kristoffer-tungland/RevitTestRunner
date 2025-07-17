using Autodesk.Revit.UI;
using System.IO.Pipes;
using System.Threading;
using RevitTestFramework.Common;

namespace RevitAddin.Common;

/// <summary>
/// Handles PipeCommand execution by coordinating between the command, server, task completion, and RevitTask
/// </summary>
public class PipeCommandHandler
{
    private readonly PipeCommand _command;
    private readonly NamedPipeServerStream _server;
    private readonly RevitTask _revitTask;
    private readonly string _testAssemblyPath;
    private readonly Func<string, IXunitTestAssemblyLoadContext> _createLoadContext;

    public PipeCommandHandler(PipeCommand command, NamedPipeServerStream server, RevitTask revitTask, string testAssemblyPath,
        Func<string, IXunitTestAssemblyLoadContext> createLoadContext)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _revitTask = revitTask ?? throw new ArgumentNullException(nameof(revitTask));
        _testAssemblyPath = testAssemblyPath ?? throw new ArgumentNullException(nameof(testAssemblyPath));
        _createLoadContext = createLoadContext ?? throw new ArgumentNullException(nameof(createLoadContext));
    }

    /// <summary>
    /// Executes the pipe command by running the handler on the Revit UI thread and waiting for completion
    /// </summary>
    /// <returns>A task that completes when the command execution is finished</returns>
    public async Task ExecuteAsync()
    {
        if (_command == null || _server == null)
            return;

        using var writer = new StreamWriter(_server, leaveOpen: true);

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

        var tempTestDir = Path.GetDirectoryName(_testAssemblyPath) ?? throw new InvalidOperationException("Test assembly path is invalid.");
        var loadContext = _createLoadContext(tempTestDir);

        try
        {
            await _revitTask.Run(loadContext.SetupInfrastructure);
            await loadContext.ExecuteTestsAsync(_command, _testAssemblyPath, writer, cts.Token);
        }
        catch (Exception ex)
        {
            HandleTestExecutionException(ex, _command.TestMethods, writer);
        }
        finally
        {
            // IMPORTANT: Tear down the infrastructure on the UI thread
            if (loadContext != null)
            {
                loadContext.TeardownInfrastructure();
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
}

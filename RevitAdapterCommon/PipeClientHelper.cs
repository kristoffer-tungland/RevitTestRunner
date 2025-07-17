using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using RevitTestFramework.Contracts;

namespace RevitAdapterCommon;

public static class PipeClientHelper
{
    /// <summary>
    /// Connects to a Revit process using the new pipe naming format (Revit version + assembly version + process ID based)
    /// </summary>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <returns>Connected NamedPipeClientStream</returns>
    public static NamedPipeClientStream ConnectToRevit(string revitVersion)
    {
        var exceptions = new List<Exception>();

        // Try to connect to each Revit process using the new naming format
        foreach (var proc in Process.GetProcessesByName("Revit"))
        {
            try
            {
                // Try with the current assembly version and the specific process ID
                var pipeName = PipeNaming.GetCurrentProcessPipeName(revitVersion);
                // Replace the current process ID with the target Revit process ID
                var parts = pipeName.Split('_');
                if (parts.Length >= 4)
                {
                    parts[3] = proc.Id.ToString();
                    pipeName = string.Join("_", parts);
                }
                else
                {
                    // Fallback: construct the pipe name directly
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var version = assembly.GetName().Version;
                    var assemblyVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
                    pipeName = PipeNaming.GetPipeName(revitVersion, assemblyVersion, proc.Id);
                }

                var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                try
                {
                    client.Connect(100);
                    return client;
                }
                catch (Exception ex)
                {
                    exceptions.Add(new Exception($"Failed to connect to pipe '{pipeName}' for process {proc.Id}", ex));
                    client.Dispose();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(new Exception($"Error creating pipe name for process {proc.Id}", ex));
            }
        }

        // If we reach here, no connection was successful
        var aggregateException = new AggregateException("Failed to connect to any Revit process", exceptions);
        throw new InvalidOperationException($"No Revit process with test pipe found for version {revitVersion}. Tried {exceptions.Count} connections.", aggregateException);
    }

    /// <summary>
    /// Sends a command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The command to send</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    /// <returns>The response from the server</returns>
    public static string SendCommand(object command, string revitVersion)
    {
        using var client = ConnectToRevit(revitVersion);
        var json = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(client, leaveOpen: true);
        sw.WriteLine(json);
        sw.Flush();
        using var sr = new StreamReader(client);
        var result = sr.ReadLine() ?? string.Empty;
        return result;
    }

    /// <summary>
    /// Sends a streaming command using the new connection method with Revit version
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, CancellationToken cancellationToken, string revitVersion)
    {
        using var cancelServer = new NamedPipeServerStream(command.CancelPipe, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = ConnectToRevit(revitVersion);
        var json = JsonSerializer.Serialize(command);
        using var sw = new StreamWriter(client, leaveOpen: true);
        sw.WriteLine(json);
        sw.Flush();

        _ = Task.Run(async () =>
        {
            await cancelServer.WaitForConnectionAsync().ConfigureAwait(false);
            await Task.Run(() =>
            {
                cancellationToken.WaitHandle.WaitOne();
                if (cancelServer.IsConnected)
                {
                    using var cw = new StreamWriter(cancelServer);
                    cw.WriteLine("CANCEL");
                    cw.Flush();
                }
            });
        });

        using var sr = new StreamReader(client);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            handleLine(line);
            if (line == "END")
                break;
        }
    }

    /// <summary>
    /// Sends a streaming command using the new connection method without cancellation
    /// </summary>
    /// <param name="command">The pipe command to send</param>
    /// <param name="handleLine">Action to handle each line of response</param>
    /// <param name="revitVersion">The Revit version to connect to</param>
    public static void SendCommandStreaming(PipeCommand command, Action<string> handleLine, string revitVersion)
        => SendCommandStreaming(command, handleLine, CancellationToken.None, revitVersion);
}

using System.IO.Pipes;
using System.Text.Json;
using Autodesk.Revit.UI;

namespace RevitAddin.Common;

public class PipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly ExternalEvent _externalEvent;
    private readonly ITestCommandHandler _handler;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;

    public PipeServer(string pipeName, ExternalEvent externalEvent, ITestCommandHandler handler)
    {
        _pipeName = pipeName;
        _externalEvent = externalEvent;
        _handler = handler;
    }

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
                    var tcs = new TaskCompletionSource();
                    _handler.SetContext(command, server, tcs);
                    _externalEvent.Raise();
                    await tcs.Task.ConfigureAwait(false);
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listenerTask?.Wait(); } catch { }
    }
}
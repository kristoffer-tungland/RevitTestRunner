using System.IO.Pipes;
using Autodesk.Revit.UI;
using RevitAddin.Common;

namespace RevitAddin.NUnit;

public class TestCommandHandler : ITestCommandHandler
{
    private PipeCommand? _command;
    private NamedPipeServerStream? _pipe;
    private TaskCompletionSource? _tcs;

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

        if (_command.Command == "RunNUnitTests")
        {
            RevitNUnitExecutor.ExecuteTestsInRevit(_command, app, writer, cts.Token);
        }
        
        _tcs.SetResult();
    }

    public string GetName() => nameof(TestCommandHandler);
}
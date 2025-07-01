using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace RevitAddin;

public class TestCommandHandler : IExternalEventHandler
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

        var resultPath = _command.Command == "RunXunitTests"
            ? RevitXunitExecutor.ExecuteTestsInRevit(_command, app)
            : RevitNUnitExecutor.ExecuteTestsInRevit(_command, app);
        using var writer = new StreamWriter(_pipe, leaveOpen: false);
        writer.WriteLine(resultPath);
        writer.Flush();
        _pipe.Dispose();
        _tcs.SetResult();
    }

    public string GetName() => nameof(TestCommandHandler);
}

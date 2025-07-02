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

        string resultPath = _command.Command switch
        {
            "RunXunitTests" => RevitXunitExecutor.ExecuteTestsInRevit(_command, app),
            "RunNUnitTests" => RevitNUnitExecutor.ExecuteTestsInRevit(_command, app),
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(resultPath))
            return;
        using var writer = new StreamWriter(_pipe, leaveOpen: true);
        writer.WriteLine(resultPath);
        writer.Flush();
        _tcs.SetResult();
    }

    public string GetName() => nameof(TestCommandHandler);
}

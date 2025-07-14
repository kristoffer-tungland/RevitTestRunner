using System.IO.Pipes;
using Autodesk.Revit.UI;

namespace RevitAddin.Common;

public interface ITestCommandHandler : IExternalEventHandler
{
    void SetContext(PipeCommand command, NamedPipeServerStream pipe, TaskCompletionSource tcs);
}
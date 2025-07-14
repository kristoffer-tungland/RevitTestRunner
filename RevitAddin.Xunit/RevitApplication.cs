using Autodesk.Revit.UI;
using RevitAddin.Common;
using System.Diagnostics;

namespace RevitAddin.Xunit;

public class RevitApplication : IExternalApplication
{
    private PipeServer? _server;

    public Result OnStartup(UIControlledApplication application)
    {
        var handler = new TestCommandHandler();
        var extEvent = ExternalEvent.Create(handler);
        var pipeName = PipeConstants.PipeNamePrefix + Process.GetCurrentProcess().Id;
        _server = new PipeServer(pipeName, extEvent, handler);
        _server.Start();
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        _server?.Dispose();
        return Result.Succeeded;
    }
}
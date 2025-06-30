using Autodesk.Revit.UI;

namespace RevitAddin;

public class RevitApplication : IExternalApplication
{
    private PipeServer? _server;

    public Result OnStartup(UIControlledApplication application)
    {
        var handler = new TestCommandHandler();
        var extEvent = ExternalEvent.Create(handler);
        var pipeName = PipeConstants.PipeNamePrefix + System.Diagnostics.Process.GetCurrentProcess().Id;
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

using Autodesk.Revit.UI;

namespace RevitTestFramework.Common;

/// <summary>
/// Helper class to set up and manage Revit API infrastructure for testing.
/// This must be created and disposed on the main Revit UI thread.
/// </summary>
public static class RevitTestInfrastructure
{
    private static RevitTask? _revitTask;
    private static UIApplication? _uiApplication;

    public static RevitTask RevitTask { get => _revitTask ?? throw new InvalidOperationException("RevitTask is not initialized. Call Setup first."); }
    
    public static UIApplication UIApplication { get => _uiApplication ?? throw new InvalidOperationException("UIApplication is not initialized. Call Setup first."); }

    public static CancellationToken? CancellationToken { get; set; }

    public static void Setup(UIApplication uiApp)
    {
        _uiApplication = uiApp;
        _revitTask = new RevitTask();
    }

    public static void Dispose()
    {
        _revitTask?.Dispose();
        _uiApplication = null;
        CancellationToken = null;
    }
}

using Xunit;
using Xunit.Sdk;

namespace RevitTestFramework.Xunit;

/// <summary>
/// Enum that mirrors Revit API DetachFromCentralOption without requiring Revit API references
/// </summary>
public enum DetachOption
{
    /// <summary>
    /// Do not detach the file being opened from its central file.
    /// </summary>
    DoNotDetach = 0,
    
    /// <summary>
    /// Detach the file being opened from its central file.
    /// </summary>
    DetachAndPreserveWorksets = 1,
    
    /// <summary>
    /// Detach the model being opened from its central model and discard worksets/worksharing.
    /// </summary>
    DetachAndDiscardWorksets = 2,
    
    /// <summary>
    /// After opening the transmitted, workshared model, immediately resave it with its current name and clear the transmitted flag.
    /// </summary>
    ClearTransmittedSaveAsNewCentral = 3
}

[XunitTestCaseDiscoverer("RevitTestFramework.Xunit.RevitXunitTestCaseDiscoverer", "RevitTestFramework.Xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitFactAttribute : FactAttribute
{
    public string? ProjectGuid { get; }
    public string? ModelGuid { get; }
    public string? LocalPath { get; }
    
    /// <summary>
    /// Optional property to specify how to detach from central model
    /// </summary>
    public DetachOption DetachOption { get; set; } = DetachOption.DoNotDetach;
    
    /// <summary>
    /// Optional property to specify which worksets to open (by WorksetId)
    /// </summary>
    public int[]? WorksetsToOpen { get; set; }

    public RevitFactAttribute()
    {
        // Empty constructor for tests that don't need a specific model
        // or want to use the currently active model in Revit
    }

    public RevitFactAttribute(string projectGuid, string modelGuid)
    {
        ProjectGuid = projectGuid;
        ModelGuid = modelGuid;
    }

    public RevitFactAttribute(string localPath)
    {
        LocalPath = localPath;
    }
}
using Xunit;
using Xunit.Sdk;

namespace RevitTestFramework.Xunit;

[XunitTestCaseDiscoverer("RevitTestFramework.Xunit.RevitXunitTestCaseDiscoverer", "RevitTestFramework.Xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitFactAttribute : FactAttribute
{
    public string? ProjectGuid { get; }
    public string? ModelGuid { get; }
    public string? LocalPath { get; }

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
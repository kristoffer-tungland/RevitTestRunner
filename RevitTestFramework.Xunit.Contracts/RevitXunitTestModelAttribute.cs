using Xunit;
using Xunit.Sdk;

namespace RevitTestFramework.Xunit;

[XunitTestCaseDiscoverer("RevitTestFramework.Xunit.RevitXunitTestCaseDiscoverer", "RevitTestFramework.Xunit")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitXunitTestModelAttribute : FactAttribute
{
    public string? ProjectGuid { get; }
    public string? ModelGuid { get; }
    public string? LocalPath { get; }

    public RevitXunitTestModelAttribute(string projectGuid, string modelGuid)
    {
        ProjectGuid = projectGuid;
        ModelGuid = modelGuid;
    }

    public RevitXunitTestModelAttribute(string localPath)
    {
        LocalPath = localPath;
    }
}
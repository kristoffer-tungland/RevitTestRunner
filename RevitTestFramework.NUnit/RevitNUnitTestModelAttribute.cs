using NUnit.Framework;
using NUnit.Framework.Interfaces;
using RevitTestFramework.Common;

namespace RevitTestFramework.NUnit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitNUnitTestModelAttribute : NUnitAttribute, IApplyToTest
{
    public string? ProjectGuid { get; }
    public string? ModelGuid { get; }
    public string? LocalPath { get; }

    public RevitNUnitTestModelAttribute(string projectGuid, string modelGuid)
    {
        ProjectGuid = projectGuid;
        ModelGuid = modelGuid;
    }

    public RevitNUnitTestModelAttribute(string localPath)
    {
        LocalPath = localPath;
    }

    public void ApplyToTest(global::NUnit.Framework.Internal.Test test)
    {
        if (LocalPath != null)
            test.Properties.Set("RevitLocalPath", LocalPath);
        else
        {
            test.Properties.Set("RevitProjectGuid", ProjectGuid);
            test.Properties.Set("RevitModelGuid", ModelGuid);
        }
    }
}

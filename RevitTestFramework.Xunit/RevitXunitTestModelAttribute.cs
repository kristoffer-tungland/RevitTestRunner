using System;
using System.Reflection;
using Xunit.Sdk;
using RevitTestFramework.Common;

namespace RevitTestFramework.Xunit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitXunitTestModelAttribute : BeforeAfterTestAttribute
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

    public override void Before(MethodInfo methodUnderTest)
    {
        RevitTestModelHelper.EnsureModelAndStartGroup(
            LocalPath,
            ProjectGuid,
            ModelGuid,
            RevitModelService.OpenLocalModel!,
            RevitModelService.OpenCloudModel!,
            methodUnderTest.Name);
    }

    public override void After(MethodInfo methodUnderTest)
    {
        RevitTestModelHelper.RollBackTransactionGroup();
    }
}
using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace RevitTestFramework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RevitNUnitTestModelAttribute : Attribute, ITestAction
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

        public void BeforeTest(ITest test)
        {
            RevitTestModelHelper.EnsureModelAndStartGroup(
                LocalPath,
                ProjectGuid,
                ModelGuid,
                RevitModelService.OpenLocalModel!,
                RevitModelService.OpenCloudModel!,
                test.Name);
        }

        public void AfterTest(ITest test)
        {
            RevitTestModelHelper.RollBackTransactionGroup();
        }

        public ActionTargets Targets => ActionTargets.Test;
    }
}

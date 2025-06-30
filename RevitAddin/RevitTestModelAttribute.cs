using System;
using Autodesk.Revit.DB;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace RevitAddin
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RevitTestModelAttribute : Attribute, ITestAction
    {
        public string? ProjectGuid { get; }
        public string? ModelGuid { get; }
        public string? LocalPath { get; }

        public RevitTestModelAttribute(string projectGuid, string modelGuid)
        {
            ProjectGuid = projectGuid;
            ModelGuid = modelGuid;
        }

        public RevitTestModelAttribute(string localPath)
        {
            LocalPath = localPath;
        }

        public void BeforeTest(ITest test)
        {
            if (LocalPath != null)
            {
                RevitNUnitExecutor.EnsureModelOpen(LocalPath);
            }
            else if (ProjectGuid != null && ModelGuid != null)
            {
                RevitNUnitExecutor.EnsureModelOpen(ProjectGuid, ModelGuid);
            }
        }

        public void AfterTest(ITest test) { }

        public ActionTargets Targets => ActionTargets.Test;
    }
}

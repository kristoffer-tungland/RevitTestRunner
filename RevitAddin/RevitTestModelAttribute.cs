using System;

namespace RevitAddin
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RevitTestModelAttribute : Attribute
    {
        public string ProjectGuid { get; }
        public string ModelGuid { get; }

        public RevitTestModelAttribute(string projectGuid, string modelGuid)
        {
            ProjectGuid = projectGuid;
            ModelGuid = modelGuid;
        }
    }
}

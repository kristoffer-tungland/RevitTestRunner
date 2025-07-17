using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitAddin.Common
{
    public interface IXunitTestAssemblyLoadContext
    {
        string TestDirectory { get; }

        Task ExecuteTestsAsync(PipeCommand command, string testAssemblyPath, StreamWriter writer, CancellationToken cancellationToken);
        Assembly LoadRevitAddinTestAssembly();
        object SetupInfrastructure(UIApplication app);
        void TeardownInfrastructure(object infrastructure);
    }
}
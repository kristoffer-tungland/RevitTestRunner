using System.IO.Pipes;
using Autodesk.Revit.UI;

namespace RevitAddin.Common;

public interface ITestCommandHandler : IExternalEventHandler
{
    void SetContext(IXunitTestAssemblyLoadContext loaderContext);
    object? Result { get; }
}
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitAddin.NUnit
{
    public class RevitCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Example invocation when launched via named pipe
            return Result.Succeeded;
        }
    }
}
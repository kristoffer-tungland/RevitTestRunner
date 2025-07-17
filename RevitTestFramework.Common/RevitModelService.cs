using Autodesk.Revit.DB;

namespace RevitTestFramework.Common;

public static class RevitModelService
{
    public static Document? CurrentDocument { get; set; }
    public static CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}
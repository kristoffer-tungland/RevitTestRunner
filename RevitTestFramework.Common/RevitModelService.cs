namespace RevitTestFramework.Common;

public static class RevitModelService
{
    public static CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    public static Autodesk.Revit.DB.Document? CurrentDocument { get; set; }
}
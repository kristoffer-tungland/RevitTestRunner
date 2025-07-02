using System;
using Autodesk.Revit.DB;
using System.Threading;

namespace RevitTestFramework;

public static class RevitModelService
{
    public static Func<string, Document>? OpenLocalModel { get; set; }
    public static Func<string, string, Document>? OpenCloudModel { get; set; }
    public static Document? CurrentDocument { get; set; }
    public static CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}

using System;
using System.Threading;
using Autodesk.Revit.DB;

namespace RevitTestFramework;

internal static class RevitTestModelHelper
{
    private static readonly AsyncLocal<TransactionGroup?> _group = new();

    public static Document? EnsureModelAndStartGroup(
        string? localPath,
        string? projectGuid,
        string? modelGuid,
        Func<string, Document> openLocal,
        Func<string, string, Document> openCloud,
        string testName)
    {
        Document? doc = null;
        if (localPath != null)
            doc = openLocal(localPath);
        else if (projectGuid != null && modelGuid != null)
            doc = openCloud(projectGuid, modelGuid);

        if (doc != null)
        {
            var tg = new TransactionGroup(doc, $"Test: {testName}");
            tg.Start();
            _group.Value = tg;
        }
        return doc;
    }

    public static void RollBackTransactionGroup()
    {
        var tg = _group.Value;
        if (tg != null)
        {
            tg.RollBack();
            tg.Dispose();
            _group.Value = null;
        }
    }
}

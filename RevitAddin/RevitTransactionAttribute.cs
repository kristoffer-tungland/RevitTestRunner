using System;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace RevitAddin;

/// <summary>
/// Starts a transaction group on the active document before a test and rolls it back afterwards.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
public sealed class RevitTransactionAttribute : Attribute, ITestAction
{
    private static readonly AsyncLocal<TransactionGroup?> _currentGroup = new();

    public void BeforeTest(ITest test)
    {
        var uiApp = RevitNUnitExecutor.UiApplication;
        var doc = uiApp?.ActiveUIDocument?.Document;
        if (doc != null)
        {
            var tg = new TransactionGroup(doc, $"Test: {test.Name}");
            tg.Start();
            _currentGroup.Value = tg;
        }
    }

    public void AfterTest(ITest test)
    {
        var tg = _currentGroup.Value;
        if (tg != null)
        {
            tg.RollBack();
            tg.Dispose();
            _currentGroup.Value = null;
        }
    }

    public ActionTargets Targets => ActionTargets.Test;
}

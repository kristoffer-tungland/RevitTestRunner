using Xunit;
using Autodesk.Revit.DB;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for verifying rollback functionality and project parameter changes in Revit documents.
/// </summary>
public class RollbackTests
{
    /// <summary>
    /// Changes the project name parameter in the document's project information and verifies the change.
    /// </summary>
    /// <remarks>
    /// This test starts a transaction, sets the project name, commits the transaction, and asserts the new value.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void ChangeParameterValue_ShouldBeRolledBack(Document document)
    {
        using var transaction = new Transaction(document, "Change Project Name");
        transaction.Start();

        var projecInfo = document.ProjectInformation;
        using var parameter = projecInfo.get_Parameter(BuiltInParameter.PROJECT_NAME);
        Assert.NotNull(parameter);
        parameter.Set("New Project Name");
        transaction.Commit();
        Assert.Equal("New Project Name", parameter.AsString());
    }

    /// <summary>
    /// Verifies that the project name parameter in the document's project information has not been modified by a previous test.
    /// </summary>
    /// <remarks>
    /// This test checks that the project name parameter remains unchanged after a transaction is rolled back or after a new document load.
    /// </remarks>
    /// <param name="document">The Revit document to test. Must not be null.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void TestRollbackFunctionality(Document document)
    {
        var projecInfo = document.ProjectInformation;
        using var parameter = projecInfo.get_Parameter(BuiltInParameter.PROJECT_NAME);
        Assert.NotNull(parameter);
        Assert.NotEqual("New Project Name", parameter.AsString());
    }
}

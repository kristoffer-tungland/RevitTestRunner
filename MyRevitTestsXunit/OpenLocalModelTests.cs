using Xunit;
using Autodesk.Revit.DB;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for opening local and workshared Revit models and verifying workset states.
/// </summary>
public class OpenLocalModelTests
{
    /// <summary>
    /// Verifies that the specified Revit document is successfully loaded and contains the expected data.
    /// </summary>
    /// <remarks>
    /// Ensures the document is loaded, has the expected title, and contains at least one wall element.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @"%PROGRAMFILES%\Autodesk\Revit {RevitVersion}\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void OpenLocalModel_ShouldLoadDocument(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Snowdon Towers Sample Architectural", document.Title);

        var wallsCount = new FilteredElementCollector(document)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    /// <summary>
    /// Opens a workshared model with a relative path and verifies that only specific worksets are open.
    /// </summary>
    /// <remarks>
    /// Opens the model and checks that only the specified workset is open.
    /// </remarks>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @".\Project1.rvt", WorksetsToOpen = [0], CloseModel = true)]
    public void OpenWorksharedModel_ShouldLoadDocumentAndOpenWorksets(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Project1", document.Title);

        var userWorksets = new FilteredWorksetCollector(document)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets();

        Assert.NotEmpty(userWorksets);

        var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();
        Assert.Single(openWorksets);
    }

    /// <summary>
    /// Opens a workshared model and verifies that all user worksets are open.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(localPath: @".\Project1.rvt", CloseModel = true)]
    public void OpenWorksharedModel_ShouldLoadDocumentAndOpenAllWorksets(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Project1", document.Title);

        var userWorksets = new FilteredWorksetCollector(document)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets();

        Assert.NotEmpty(userWorksets);

        var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();

        Assert.Equal(userWorksets.Count, openWorksets.Count);
    }

    /// <summary>
    /// Detaches a workshared model and verifies that worksets are preserved and only specific worksets are open.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(
        localPath: @".\Project1.rvt",
        WorksetsToOpen = [0],
        DetachOption = DetachOption.DetachAndPreserveWorksets,
        CloseModel = true)]
    public void DetachAndPreserveWorksets_ShouldDetachModelAndPreserveWorksets(Document document)
    {
        Assert.NotNull(document);
        Assert.Equal("Project1_detached", document.Title);

        var userWorksets = new FilteredWorksetCollector(document)
            .OfKind(WorksetKind.UserWorkset)
            .ToWorksets();

        Assert.NotEmpty(userWorksets);

        var openWorksets = userWorksets.Where(w => w.IsOpen).ToList();
        Assert.Single(openWorksets);
    }
}

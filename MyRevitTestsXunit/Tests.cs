using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTestsXunit;

public class MyRevitTestsClass
{
    [RevitFact("proj-guid", "model-guid")]
    public void TestWalls(Document doc)
    {
        Assert.NotNull(doc);
    }

    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void TestLocalFile(Document doc)
    {
        Assert.NotNull(doc);
        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

        var wallsCount = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    [RevitFact]
    public void TestWithActiveUIApplication(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");
    }

    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void TestLocalFileAndUIApplication(UIApplication uiapp, Document doc)
    {
        Assert.NotNull(uiapp);
        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");

        Assert.NotNull(doc);
        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

        var wallsCount = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    [RevitFact]
    public void TestWithActiveDocument(Document? doc)
    {
        // This test uses the currently active document in Revit
        // If no document is active, doc will be null
        if (doc is null)
        {
            // Test can handle the case where no document is active
            Assert.True(doc is null, "No active document - test passes gracefully");
        }
        else
        {
            Assert.NotNull(doc);
            // Test can work with whatever model is currently open
            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();

            Assert.True(elements.Count >= 0, "Document should contain some elements or be empty");
        }
    }

    [RevitFact]
    public void TestWithoutDocument()
    {
        // This test doesn't require a document at all
        // It can test non-document related functionality
        Assert.True(true, "This test doesn't need a Revit document");
    }
}

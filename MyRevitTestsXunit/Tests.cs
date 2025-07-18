using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;

namespace MyRevitTestsXunit;

public class MyRevitTestsClass
{
    [RevitXunitTestModel("proj-guid", "model-guid")]
    public void TestWalls(Document doc)
    {
        Assert.NotNull(doc);
    }

    [RevitXunitTestModel(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
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

    [RevitXunitTestModel]
    public void TestWithActiveDocument(Document? doc)
    {
        // This test uses the currently active document in Revit
        // If no document is active, doc will be null
        if (doc != null)
        {
            Assert.NotNull(doc);
            // Test can work with whatever model is currently open
            var elements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements();
            
            Assert.True(elements.Count >= 0, "Document should contain some elements or be empty");
        }
        else
        {
            // Test can handle the case where no document is active
            Assert.True(true, "No active document - test passes gracefully");
        }
    }

    [RevitXunitTestModel]
    public void TestWithoutDocument()
    {
        // This test doesn't require a document at all
        // It can test non-document related functionality
        Assert.True(true, "This test doesn't need a Revit document");
    }
}

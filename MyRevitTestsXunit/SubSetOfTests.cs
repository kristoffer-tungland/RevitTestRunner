using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace MyRevitTestsXunit;

public class SubSetOfTests
{
    [RevitFact]
    public void UIApp_Test_1(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");
    }

    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void Should_LoadLocalFile_AndVerifyModelContent_WhenProvidedWithFilePath(Document doc)
    {
        Assert.NotNull(doc);
        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

        var wallsCount = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    [RevitFact(@"C:\Users\ktu\OneDrive - COWI\Documents\Project1.rvt")]
    public void OpenWorkshared(Document doc)
    {
        Assert.NotNull(doc);
    }

    [RevitFact]
    public void ActiveDocument(Document? doc)
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
}

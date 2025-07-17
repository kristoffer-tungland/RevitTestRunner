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
        Assert.Equal("Snowdon Towers Sample Architecturalfdfd", doc.Title);
    }
}

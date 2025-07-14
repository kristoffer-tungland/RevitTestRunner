using Xunit;
using RevitTestFramework.Xunit;
using RevitTestFramework.Common;

namespace MyRevitTestsXunit;

public class MyRevitTestsClass
{
    [Fact]
    [RevitXunitTestModel("proj-guid", "model-guid")]
    public void TestWalls()
    {
        Assert.NotNull(RevitModelService.CurrentDocument);
    }

    [Fact]
    [RevitXunitTestModel(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void TestLocalFile()
    {
        Assert.NotNull(RevitModelService.CurrentDocument);
    }
}

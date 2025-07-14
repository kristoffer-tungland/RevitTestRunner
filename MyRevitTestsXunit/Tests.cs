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
    [RevitXunitTestModel(@"C:\\Models\\sample.rvt")]
    public void TestLocalFile()
    {
        Assert.NotNull(RevitModelService.CurrentDocument);
    }
}

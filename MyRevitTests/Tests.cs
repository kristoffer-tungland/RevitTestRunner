using NUnit.Framework;
using Autodesk.Revit.DB;
using RevitAddin;

namespace MyRevitTests
{
    [TestFixture]
    public class MyRevitTestsClass
    {
        [Test]
        [RevitTestModel("proj-guid", "model-guid")]
        public void TestWalls()
        {
            Assert.IsNotNull(RevitNUnitExecutor.CurrentDocument);
        }

        [Test]
        [RevitTestModel(@"C:\\Models\\sample.rvt")]
        public void TestLocalFile()
        {
            Assert.IsNotNull(RevitNUnitExecutor.CurrentDocument);
        }
    }
}

using NUnit.Framework;
using Autodesk.Revit.DB;
using RevitTestFramework;

namespace MyRevitTestsNUnit
{
    [TestFixture]
    public class MyRevitTestsClass
    {
        [Test]
        [RevitNUnitTestModel("proj-guid", "model-guid")]
        public void TestWalls()
        {
            Assert.That(RevitModelService.CurrentDocument, Is.Not.Null);
        }

        [Test]
        [RevitNUnitTestModel(@"C:\\Models\\sample.rvt")]
        public void TestLocalFile()
        {
            Assert.That(RevitModelService.CurrentDocument, Is.Not.Null);
        }
    }
}

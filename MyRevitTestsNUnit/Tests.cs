using NUnit.Framework;
using RevitTestFramework.NUnit;
using RevitTestFramework.Common;
using Autodesk.Revit.DB;

namespace MyRevitTestsNUnit
{
    [TestFixture]
    public class MyRevitTestsClass
    {
        [Test]
        [RevitNUnitTestModel("proj-guid", "model-guid")]
        public void TestWalls(Document doc)
        {
            Assert.That(doc, Is.Not.Null);
        }

        [Test]
        [RevitNUnitTestModel(@"C:\\Models\\sample.rvt")]
        public void TestLocalFile(Document doc)
        {
            Assert.That(doc, Is.Not.Null);
        }
    }
}

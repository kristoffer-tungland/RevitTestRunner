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
        public void TestWalls([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.DynamicallyAccessedMemberTypes.All)] Document doc)
        {
            Assert.IsNotNull(doc);
        }
    }
}

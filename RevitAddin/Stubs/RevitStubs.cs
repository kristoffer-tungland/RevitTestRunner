namespace Autodesk.Revit.DB
{
    public class Document {}
    public class TransactionGroup : System.IDisposable
    {
        public TransactionGroup(Document doc) {}
        public void Start() {}
        public void RollBack() {}
        public void Dispose() {}
    }
}

namespace Autodesk.Revit.UI
{
    public class UIApplication
    {
        public Autodesk.Revit.DB.Document ActiveUIDocument => new Autodesk.Revit.DB.Document();
    }
}

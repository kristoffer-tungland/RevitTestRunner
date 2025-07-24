//using Xunit;
//using RevitTestFramework.Xunit;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;

//namespace MyRevitTestsXunit;

//public class MyRevitTestsClass
//{
//    [RevitFact("proj-guid", "model-guid", DetachOption = DetachOption.DetachAndPreserveWorksets, WorksetsToOpen = [311, 312])]
//    public void Should_LoadCloudModel_WhenProvidedWithProjectAndModelGuids(Document doc)
//    {
//        Assert.NotNull(doc);
//    }

//    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
//    public void Should_LoadLocalFile_AndVerifyModelContent_WhenProvidedWithFilePath(Document doc)
//    {
//        Assert.NotNull(doc);
//        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

//        var wallsCount = new FilteredElementCollector(doc)
//            .WhereElementIsNotElementType()
//            .OfClass(typeof(Wall))
//            .GetElementCount();

//        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
//    }

//    [RevitFact]
//    public void Should_ProvideUIApplication_WhenTestRequiresRevitUI(UIApplication uiapp)
//    {
//        Assert.NotNull(uiapp);
//        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");
//    }

//    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
//    public void Should_ProvideBothUIApplicationAndDocument_WhenTestRequiresBoth(UIApplication uiapp, Document doc)
//    {
//        Assert.NotNull(uiapp);
//        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");

//        Assert.NotNull(doc);
//        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

//        var wallsCount = new FilteredElementCollector(doc)
//            .WhereElementIsNotElementType()
//            .OfClass(typeof(Wall))
//            .GetElementCount();

//        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
//    }

//    [RevitFact(@"C:\Program Files\Autodesk\Revit [RevitVersion]\Samples\Snowdon Towers Sample Architectural.rvt")]
//    public void Should_ReplaceVersionPlaceholder_WhenUsingRevitVersionToken(Document doc)
//    {
//        Assert.NotNull(doc);
//        // This test demonstrates the [RevitVersion] placeholder working with different sample files
//        // The placeholder will be replaced with the actual Revit version at runtime
//    }

//    // Example demonstrating the new non-nullable DetachFromCentral property
//    [RevitFact(@"C:\Models\CentralModel.rvt", DetachOption = DetachOption.DetachAndPreserveWorksets)]
//    public void Should_DetachFromCentral_WhenDetachOptionIsSpecified(Document doc)
//    {
//        Assert.NotNull(doc);
//        // When DetachAndPreserveWorksets is used, the document should not be workshared
//        // but worksets should still be available in the detached copy
//        Assert.False(doc.IsWorkshared, "Document should be detached from central and not workshared");

//        // If the model had worksets, they should still be preserved
//        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
//        // This is just an example - actual workset validation would depend on your specific model
//    }

//    // Example demonstrating workset configuration
//    [RevitFact(@"C:\Models\WorksharedModel.rvt", WorksetsToOpen = [1, 2, 5])]
//    public void Should_OpenSpecificWorksets_WhenWorksetIdsAreSpecified(Document doc)
//    {
//        Assert.NotNull(doc);

//        // When specific worksets are configured, only those worksets should be open
//        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
//        var openWorksets = worksets.Where(w => w.IsOpen).ToList();

//        // This test would verify that only worksets 1, 2, and 5 are open
//        // The exact validation would depend on your model's workset structure
//        Assert.True(openWorksets.Count > 0, "At least some specified worksets should be open");
//    }

//    // Example combining both DetachFromCentral and WorksetsToOpen
//    [RevitFact(@"C:\Models\CentralModel.rvt",
//               DetachOption = DetachOption.DetachAndPreserveWorksets,
//               WorksetsToOpen = [1, 3])]
//    public void Should_DetachAndOpenSpecificWorksets_WhenBothOptionsAreSpecified(Document doc)
//    {
//        Assert.NotNull(doc);

//        // Document should be detached from central
//        Assert.False(doc.IsWorkshared, "Document should be detached from central");

//        // Only specified worksets should be open
//        var worksets = new FilteredWorksetCollector(doc).ToWorksets();
//        var openWorksets = worksets.Where(w => w.IsOpen).ToList();

//        // This would verify that the document is properly detached and only worksets 1 and 3 are open
//        Assert.True(openWorksets.Count > 0, "Specified worksets should be open in the detached model");
//    }

//    // Example showing default behavior (DoNotDetach)
//    [RevitFact(@"C:\Models\CentralModel.rvt")] // DetachFromCentral defaults to DetachOption.DoNotDetach
//    public void Should_UseDefaultDetachOption_WhenNotSpecified(Document doc)
//    {
//        Assert.NotNull(doc);
//        // When DetachFromCentral is not specified, it defaults to DoNotDetach
//        // The document should remain connected to central if it was a central model
//    }

//    [RevitFact]
//    public void Should_HandleNullDocument_WhenNoActiveDocumentIsAvailable(Document? doc)
//    {
//        // This test uses the currently active document in Revit
//        // If no document is active, doc will be null
//        if (doc is null)
//        {
//            // Test can handle the case where no document is active
//            Assert.True(doc is null, "No active document - test passes gracefully");
//        }
//        else
//        {
//            Assert.NotNull(doc);
//            // Test can work with whatever model is currently open
//            var elements = new FilteredElementCollector(doc)
//                .WhereElementIsNotElementType()
//                .ToElements();

//            Assert.True(elements.Count >= 0, "Document should contain some elements or be empty");
//        }
//    }

//    [RevitFact]
//    public void Should_ExitLoop_WhenCancellationIsRequested(CancellationToken cancellationToken)
//    {
//        // Use thread sleep to simulate a long-running operation
//        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation was not requested before the test started");

//        while (!cancellationToken.IsCancellationRequested)
//        {
//            // Simulate some work
//            System.Threading.Thread.Sleep(1000);
//        }

//        Assert.True(cancellationToken.IsCancellationRequested, "Cancellation was requested during the test");
//    }

//    [RevitFact]
//    public void Should_HandleNullableCancellationToken_WhenCancellationMayNotBeAvailable(CancellationToken? cancellationToken)
//    {
//        // This test demonstrates using nullable CancellationToken parameter
//        if (cancellationToken.HasValue)
//        {
//            Assert.False(cancellationToken.Value.IsCancellationRequested, "Cancellation was not requested");
//        }
//        else
//        {
//            Assert.True(cancellationToken == null, "No cancellation token was provided");
//        }
//    }

//    [RevitFact]
//    public void Should_RunSuccessfully_WhenNoRevitDocumentIsRequired()
//    {
//        // This test doesn't require a document at all
//        // It can test non-document related functionality
//        Assert.True(true, "This test doesn't need a Revit document");
//    }
//}

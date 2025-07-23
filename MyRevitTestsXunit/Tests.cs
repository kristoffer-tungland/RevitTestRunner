using Xunit;
using RevitTestFramework.Xunit;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTestsXunit;

public class MyRevitTestsClass
{
    [RevitFact("proj-guid", "model-guid")]
    public void Should_LoadCloudModel_WhenProvidedWithProjectAndModelGuids(Document doc)
    {
        Assert.NotNull(doc);
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

    [RevitFact]
    public void Should_ProvideUIApplication_WhenTestRequiresRevitUI(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");
    }

    [RevitFact(@"C:\Program Files\Autodesk\Revit 2025\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void Should_ProvideBothUIApplicationAndDocument_WhenTestRequiresBoth(UIApplication uiapp, Document doc)
    {
        Assert.NotNull(uiapp);
        Assert.True(uiapp.LoadedApplications.IsEmpty == false, "Expected at least one loaded application in the Revit UI.");

        Assert.NotNull(doc);
        Assert.Equal("Snowdon Towers Sample Architectural", doc.Title);

        var wallsCount = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .OfClass(typeof(Wall))
            .GetElementCount();

        Assert.True(wallsCount > 0, "Expected at least one wall in the model.");
    }

    [RevitFact(@"C:\Program Files\Autodesk\Revit [RevitVersion]\Samples\Snowdon Towers Sample Architectural.rvt")]
    public void Should_ReplaceVersionPlaceholder_WhenUsingRevitVersionToken(Document doc)
    {
        Assert.NotNull(doc);
        // This test demonstrates the [RevitVersion] placeholder working with different sample files
        // The placeholder will be replaced with the actual Revit version at runtime
    }

    [RevitFact]
    public void Should_HandleNullDocument_WhenNoActiveDocumentIsAvailable(Document? doc)
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

    [RevitFact]
    public void Should_Cancel_WhenCancellationRequested(CancellationToken cancellationToken)
    {
        // Use thread sleep to simulate a long-running operation
        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation was not requested before the test started");
        // Simulate a long-running operation
        System.Threading.Thread.Sleep(5000);
        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation was not requested during the test execution");
    }

    [RevitFact]
    public void Should_HandleNullableCancellationToken_WhenCancellationMayNotBeAvailable(CancellationToken? cancellationToken)
    {
        // This test demonstrates using nullable CancellationToken parameter
        if (cancellationToken.HasValue)
        {
            Assert.False(cancellationToken.Value.IsCancellationRequested, "Cancellation was not requested");
        }
        else
        {
            Assert.True(cancellationToken == null, "No cancellation token was provided");
        }
    }

    [RevitFact]
    public void Should_RunSuccessfully_WhenNoRevitDocumentIsRequired()
    {
        // This test doesn't require a document at all
        // It can test non-document related functionality
        Assert.True(true, "This test doesn't need a Revit document");
    }
}

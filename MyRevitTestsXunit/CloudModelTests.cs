using Xunit;
using Autodesk.Revit.DB;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for opening and verifying cloud models in Revit.
/// </summary>
public class CloudModelTests
{
    /// <summary>
    /// Opens a cloud model in the EMEA region with specific project and model GUIDs and verifies it is a cloud model.
    /// </summary>
    /// <param name="document">The Revit document to be tested.</param>
    [RevitFact(
        CloudRegion.EMEA, // Specify the EMEA region for the cloud model
        projectGuid: "AAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", // Project GUID for the cloud model
        modelGuid: "BBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB",  // Model GUID for the cloud model
        WorksetsToOpen = [0], // Open the first workset (Workset 1), or leave empty to open all worksets
        DetachOption = DetachOption.DetachAndPreserveWorksets, // Detach the model and preserve worksets
        CloseModel = true)] // Close the model after the test
    public void OpenCloud_EMEA_Region_ShouldLoadDocument(Document document)
    {
        Assert.NotNull(document);
        Assert.True(document.IsModelInCloud, "Expected the document to be a cloud model.");
    }

    /// <summary>
    /// Retrieves the cloud model path from the active document and outputs the project and model GUIDs for reference.
    /// </summary>
    /// <remarks>
    /// Open the cloud model manually in Revit and run this test to see the GUIDs in the test output.
    /// </remarks>
    /// <param name="document">The Revit document to be tested. Can be null if no active document.</param>
    [RevitFact]
    public void GetCloudModelPath_LetYouGetProjectAndModelGUIDs(Document? document)
    {
        if (document is null)
        {
            Assert.Null(document);
            return; // No active document, test passes gracefully
        }
        var cloudModelPath = document.GetCloudModelPath();
        Assert.NotNull(cloudModelPath);
        var projectGuid = cloudModelPath.GetProjectGUID();
        var modelGuid = cloudModelPath.GetModelGUID();
        // Make the test fail to get the GUIDs
        Assert.Equal("AAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", projectGuid.ToString());
        Assert.Equal("BBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB", modelGuid.ToString());
    }
}
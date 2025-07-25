using Xunit;
using Autodesk.Revit.UI;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for verifying the Revit application context and loaded add-ins.
/// </summary>
public class ApplicationTests
{
    /// <summary>
    /// Verifies that the Revit application is initialized and has a valid version.
    /// </summary>
    /// <param name="uiapp">The Revit application to be tested.</param>
    [RevitFact]
    public void Application_ShouldBeInitialized(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotNull(uiapp.Application);
        Assert.StartsWith("20", uiapp.Application.VersionNumber);
    }

    /// <summary>
    /// Verifies that the Revit application has the test framework loaded.
    /// </summary>
    /// <param name="uiapp">The Revit application to be tested.</param>
    [RevitFact]
    public void Application_ShouldHaveTestFrameworkLoaded(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotEmpty(uiapp.LoadedApplications);

        var loadedApplicationsNames = uiapp.LoadedApplications.OfType<IExternalApplication>().Select(app => app.GetType().Name).ToList();

        Assert.Contains("RevitXunitTestFrameworkApplication", loadedApplicationsNames, StringComparer.OrdinalIgnoreCase);
    }
}

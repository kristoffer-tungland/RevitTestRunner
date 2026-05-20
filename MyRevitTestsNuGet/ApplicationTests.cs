using Autodesk.Revit.UI;
using RevitTestFramework.Xunit;
using Xunit;

namespace MyRevitTestsNuGet;

/// <summary>
/// Verifies the Revit application context.
/// Consumes RevitXunit.TestAdapter from the GitHub Packages NuGet feed.
/// </summary>
public class ApplicationTests
{
    [RevitFact]
    public void Application_ShouldBeInitialized(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotNull(uiapp.Application);
        Assert.StartsWith("20", uiapp.Application.VersionNumber);
    }

    [RevitFact]
    public void Application_ShouldHaveTestFrameworkLoaded(UIApplication uiapp)
    {
        Assert.NotNull(uiapp);
        Assert.NotEmpty(uiapp.LoadedApplications);

        var loadedApplicationNames = uiapp.LoadedApplications
            .OfType<IExternalApplication>()
            .Select(app => app.GetType().Name)
            .ToList();

        Assert.Contains("RevitXunitTestFrameworkApplication", loadedApplicationNames, StringComparer.OrdinalIgnoreCase);
    }
}

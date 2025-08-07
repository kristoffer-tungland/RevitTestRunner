using Xunit.Abstractions;
using Xunit.Sdk;
using Autodesk.Revit.DB;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCaseDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink = diagnosticMessageSink;

    public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions,
                                                ITestMethod testMethod,
                                                IAttributeInfo factAttribute)
    {
        var projectGuid = factAttribute.GetNamedArgument<string>("ProjectGuid");
        var modelGuid = factAttribute.GetNamedArgument<string>("ModelGuid");
        var localPath = factAttribute.GetNamedArgument<string>("LocalPath");
        var detachOption = factAttribute.GetNamedArgument<DetachOption>("DetachOption");
        var worksetsToOpen = factAttribute.GetNamedArgument<int[]>("WorksetsToOpen");
        var cloudRegion = factAttribute.GetNamedArgument<CloudRegion>("CloudRegion");
        var closeModel = factAttribute.GetNamedArgument<bool>("CloseModel");
        var timeout = factAttribute.GetNamedArgument<int>("Timeout");

        // Convert from Xunit enum to Revit API enum
        var revitDetachFromCentral = (DetachFromCentralOption)(int)detachOption;

        // Convert CloudRegion enum to string to avoid dependency in RevitTestConfiguration
        var cloudRegionString = cloudRegion switch
        {
            CloudRegion.US => "US",
            CloudRegion.EMEA => "EMEA",
            _ => throw new ArgumentOutOfRangeException(nameof(cloudRegion), 
                $"Unsupported cloud region: {cloudRegion}. Supported values are US and EMEA.")
        };

        // Create configuration using the Revit API enum type and string cloud region
        var configuration = new Common.RevitTestConfiguration(
            projectGuid,
            modelGuid,
            localPath,
            revitDetachFromCentral,
            worksetsToOpen,
            cloudRegionString,
            closeModel,
            timeout);

        yield return new RevitXunitTestCase(_diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(), testMethod,
            configuration);
    }
}

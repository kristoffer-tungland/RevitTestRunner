using Xunit.Abstractions;
using Xunit.Sdk;

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

        yield return new RevitXunitTestCase(_diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(), testMethod,
            projectGuid, modelGuid, localPath);
    }
}

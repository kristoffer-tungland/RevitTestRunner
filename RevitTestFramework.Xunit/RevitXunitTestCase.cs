using Xunit.Abstractions;
using Xunit.Sdk;
using Autodesk.Revit.DB;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCase : XunitTestCase
{
    private RevitTestFramework.Common.RevitTestConfiguration _configuration;

    [Obsolete("Called by the de-serializer", true)]
    public RevitXunitTestCase() 
    {
        _configuration = new RevitTestFramework.Common.RevitTestConfiguration();
    }

    public RevitXunitTestCase(IMessageSink diagnosticMessageSink,
                               TestMethodDisplay defaultMethodDisplay,
                               ITestMethod testMethod,
                               RevitTestFramework.Common.RevitTestConfiguration configuration)
        : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
    {
        _configuration = configuration;
    }

    public override Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
    {
        var runner = new RevitXunitTestCaseRunner(this, DisplayName, SkipReason,
            constructorArguments, messageBus, aggregator,
            cancellationTokenSource, _configuration);
        return runner.RunAsync();
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);
        data.AddValue("ProjectGuid", _configuration.ProjectGuid);
        data.AddValue("ModelGuid", _configuration.ModelGuid);
        data.AddValue("LocalPath", _configuration.LocalPath);
        data.AddValue("DetachOption", _configuration.DetachFromCentral);
        data.AddValue("WorksetsToOpen", _configuration.WorksetsToOpen);
        data.AddValue("CloudRegion", _configuration.CloudRegion);
        data.AddValue("CloseModel", _configuration.CloseModel);
    }

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);
        var projectGuid = data.GetValue<string?>("ProjectGuid");
        var modelGuid = data.GetValue<string?>("ModelGuid");
        var localPath = data.GetValue<string?>("LocalPath");
        var detachFromCentral = data.GetValue<Autodesk.Revit.DB.DetachFromCentralOption>("DetachOption");
        var worksetsToOpen = data.GetValue<int[]?>("WorksetsToOpen");
        var cloudRegion = data.GetValue<string>("CloudRegion");
        var closeModel = data.GetValue<bool>("CloseModel");
        
        _configuration = new RevitTestFramework.Common.RevitTestConfiguration(
            projectGuid,
            modelGuid,
            localPath,
            detachFromCentral,
            worksetsToOpen,
            cloudRegion,
            closeModel);
    }
}

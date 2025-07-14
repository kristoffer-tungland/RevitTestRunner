using Xunit.Abstractions;
using Xunit.Sdk;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCase : XunitTestCase
{
    string? _projectGuid;
    string? _modelGuid;
    string? _localPath;

    [Obsolete("Called by the de-serializer", true)]
    public RevitXunitTestCase() { }

    public RevitXunitTestCase(IMessageSink diagnosticMessageSink,
                               TestMethodDisplay defaultMethodDisplay,
                               ITestMethod testMethod,
                               string? projectGuid,
                               string? modelGuid,
                               string? localPath)
        : base(diagnosticMessageSink, defaultMethodDisplay, testMethod)
    {
        _projectGuid = projectGuid;
        _modelGuid = modelGuid;
        _localPath = localPath;
    }

    public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
    {
        var runner = new RevitXunitTestCaseRunner(this, DisplayName, SkipReason,
            constructorArguments, messageBus, aggregator,
            cancellationTokenSource, _projectGuid, _modelGuid, _localPath);
        return await runner.RunAsync();
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);
        data.AddValue("ProjectGuid", _projectGuid);
        data.AddValue("ModelGuid", _modelGuid);
        data.AddValue("LocalPath", _localPath);
    }

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);
        _projectGuid = data.GetValue<string?>("ProjectGuid");
        _modelGuid = data.GetValue<string?>("ModelGuid");
        _localPath = data.GetValue<string?>("LocalPath");
    }
}

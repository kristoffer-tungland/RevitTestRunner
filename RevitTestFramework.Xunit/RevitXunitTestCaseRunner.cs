using Xunit.Abstractions;
using Xunit.Sdk;
using RevitTestFramework.Common;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCaseRunner : XunitTestCaseRunner
{
    private readonly string? _projectGuid;
    private readonly string? _modelGuid;
    private readonly string? _localPath;

    public RevitXunitTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason,
        object[] constructorArguments, IMessageBus messageBus,
        ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource,
        string? projectGuid, string? modelGuid, string? localPath)
        : base(testCase, displayName, skipReason, constructorArguments, Array.Empty<object>(),
               messageBus, aggregator, cancellationTokenSource)
    {
        _projectGuid = projectGuid;
        _modelGuid = modelGuid;
        _localPath = localPath;
    }

    protected override async Task<RunSummary> RunTestAsync()
    {
        var methodName = TestCase.TestMethod.Method.Name;
        RevitTestModelHelper.EnsureModelAndStartGroup(
            _localPath,
            _projectGuid,
            _modelGuid,
            RevitModelService.OpenLocalModel!,
            RevitModelService.OpenCloudModel!,
            methodName);
        try
        {
            return await base.RunTestAsync();
        }
        finally
        {
            RevitTestModelHelper.RollBackTransactionGroup();
        }
    }
}

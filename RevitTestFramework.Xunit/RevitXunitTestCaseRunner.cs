using System;
using System.Reflection;
using Autodesk.Revit.DB;
using Xunit.Abstractions;
using Xunit.Sdk;
using RevitTestFramework.Common;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCaseRunner : XunitTestCaseRunner
{
    private readonly string? _projectGuid;
    private readonly string? _modelGuid;
    private readonly string? _localPath;
    private Document? _document;

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
        // Use AsyncUtil.RunSync to avoid Revit freezing on async/await
        return AsyncUtil.RunSync(() =>
        {
            var methodName = TestCase.TestMethod.Method.Name;
            _document = RevitTestModelHelper.EnsureModelAndStartGroup(
                _localPath,
                _projectGuid,
                _modelGuid,
                RevitModelService.OpenLocalModel!,
                RevitModelService.OpenCloudModel!,
                methodName);
            try
            {
                return base.RunTestAsync();
            }
            finally
            {
                RevitTestModelHelper.RollBackTransactionGroup();
                _document = null;
            }
        });
    }

    protected override XunitTestRunner CreateTestRunner(
        ITest test,
        IMessageBus messageBus,
        Type testClass,
        object[] constructorArguments,
        MethodInfo testMethod,
        object[] testMethodArguments,
        string skipReason,
        IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        if (_document != null)
        {
            var args = new object[testMethodArguments.Length + 1];
            args[0] = _document;
            if (testMethodArguments.Length > 0)
                Array.Copy(testMethodArguments, 0, args, 1, testMethodArguments.Length);
            testMethodArguments = args;
        }

        return new XunitTestRunner(test, messageBus, testClass, constructorArguments,
            testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource);
    }
}

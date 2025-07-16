using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
        // Run the test on a background thread to avoid blocking Revit UI
        return await Task.Run(async () =>
        {
            var methodName = TestCase.TestMethod.Method.Name;
            
            // Use ExternalEvent to set up model on UI thread
            var modelSetupHandler = new RevitModelSetupHandler(_localPath, _projectGuid, _modelGuid, methodName);
            var modelSetupEvent = RevitTestExternalEventUtility.CreateExternalEvent(modelSetupHandler);
            
            try
            {
                // Request model setup on UI thread and wait for completion
                var setupResult = await RequestModelSetupAsync(modelSetupEvent, modelSetupHandler);
                _document = setupResult;
                
                if (_document == null)
                {
                    throw new InvalidOperationException($"Failed to set up Revit model for test: {methodName}");
                }

                // Now run the test with the prepared document
                return await base.RunTestAsync();
            }
            finally
            {
                // Clean up on UI thread
                var cleanupHandler = new RevitModelCleanupHandler();
                var cleanupEvent = RevitTestExternalEventUtility.CreateExternalEvent(cleanupHandler);
                await RequestCleanupAsync(cleanupEvent, cleanupHandler);
                _document = null;
            }
        });
    }

    private async Task<Document?> RequestModelSetupAsync(ExternalEvent externalEvent, RevitModelSetupHandler handler)
    {
        var tcs = new TaskCompletionSource<Document?>();
        handler.SetCompletionSource(tcs);
        
        // Request execution on UI thread
        externalEvent.Raise();
        
        // Wait for completion with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        cts.Token.Register(() => tcs.TrySetCanceled());
        
        return await tcs.Task;
    }

    private async Task RequestCleanupAsync(ExternalEvent externalEvent, RevitModelCleanupHandler handler)
    {
        var tcs = new TaskCompletionSource<bool>();
        handler.SetCompletionSource(tcs);
        
        // Request execution on UI thread
        externalEvent.Raise();
        
        // Wait for completion with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        cts.Token.Register(() => tcs.TrySetCanceled());
        
        try
        {
            await tcs.Task;
        }
        catch
        {
            // Ignore cleanup errors
        }
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

        return new RevitUITestRunner(test, messageBus, testClass, constructorArguments,
            testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource);
    }
}

/// <summary>
/// Custom test runner that ensures test method execution happens on UI thread when needed
/// </summary>
public class RevitUITestRunner : XunitTestRunner
{
    public RevitUITestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments,
        MethodInfo testMethod, object[] testMethodArguments, string skipReason,
        IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments,
               skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
    {
    }

    protected override async Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
    {
        // For Revit tests, we need to execute the actual test method on the UI thread
        var testExecutionHandler = new RevitTestExecutionHandler(TestClass, ConstructorArguments, TestMethod, TestMethodArguments);
        var testExecutionEvent = RevitTestExternalEventUtility.CreateExternalEvent(testExecutionHandler);
        
        var tcs = new TaskCompletionSource<decimal>();
        testExecutionHandler.SetCompletionSource(tcs);
        
        // Request execution on UI thread
        testExecutionEvent.Raise();
        
        // Wait for completion with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        cts.Token.Register(() => tcs.TrySetCanceled());
        
        try
        {
            var result = await tcs.Task;
            
            // If the test method threw an exception, it will be in the handler
            if (testExecutionHandler.Exception != null)
            {
                aggregator.Add(testExecutionHandler.Exception);
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            aggregator.Add(new TimeoutException($"Test method {TestMethod.Name} timed out after 10 minutes"));
            return 0;
        }
    }
}

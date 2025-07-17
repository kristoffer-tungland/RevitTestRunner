using System.Diagnostics;
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
    private TransactionGroup? _transactionGroup;

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
                        
            try
            {
                // Request model setup on UI thread and wait for completion
                _document = await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    return RevitTestModelHelper.OpenModel(app, _localPath, _projectGuid, _modelGuid);
                });

                _transactionGroup = await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    // Start a transaction group for the test
                    var tg = new TransactionGroup(_document, $"Test: {methodName}");
                    tg.Start();
                    return tg;
                });

                // Now run the test with the prepared document
                return await base.RunTestAsync();
            }
            finally
            {
                // Clean up on UI thread
                _document = null;
                await RevitTestInfrastructure.RevitTask.Run(app =>
                {
                    if (_transactionGroup != null)
                    {
                        try
                        {
                            // Rollback the transaction group
                            _transactionGroup.RollBack();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error rolling back transaction group: {ex.Message}");
                        }
                        finally
                        {
                            _transactionGroup.Dispose();
                        }
                    }
                });
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
        // Wait for completion with timeout
        //using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        //cts.Token.Register(() => tcs.TrySetCanceled());
        
        try
        {
            Exception? exception = null;

            var result = await RevitTestInfrastructure.RevitTask.Run(app =>
            {
                var timer = new Stopwatch();
                timer.Start();

                try
                {
                    // Create test instance
                    var testInstance = Activator.CreateInstance(TestClass, ConstructorArguments);

                    // Invoke the test method
                    var result = TestMethod.Invoke(testInstance, TestMethodArguments);

                    // Handle async test methods
                    if (result is Task task)
                    {
                        task.Wait();
                    }

                    timer.Stop();
                    return timer.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    exception = ex.InnerException ?? ex;
                    return timer.ElapsedMilliseconds;
                }

            });

            // If the test method threw an exception, it will be in the handler
            if (exception != null)
            {
                aggregator.Add(exception);
            }

            // Convert milliseconds to seconds
            return result / 1000m;
        }
        catch (OperationCanceledException)
        {
            aggregator.Add(new TimeoutException($"Test method {TestMethod.Name} timed out after 10 minutes"));
            return 0;
        }
    }
}

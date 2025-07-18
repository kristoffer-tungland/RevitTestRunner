using System.Diagnostics;
using System.Reflection;
using Autodesk.Revit.DB;
using Xunit.Abstractions;
using Xunit.Sdk;
using RevitTestFramework.Common;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason,
    object[] constructorArguments, IMessageBus messageBus,
    ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource,
    string? projectGuid, string? modelGuid, string? localPath) : XunitTestCaseRunner(testCase, displayName, skipReason, constructorArguments, [],
           messageBus, aggregator, cancellationTokenSource)
{
    private readonly ExceptionAggregator _aggregator = aggregator;
    private readonly string? _projectGuid = projectGuid;
    private readonly string? _modelGuid = modelGuid;
    private readonly string? _localPath = localPath;

    private Document? _document;
    private TransactionGroup? _transactionGroup;

    protected override async Task<RunSummary> RunTestAsync()
    {
        // Run the test on a background thread to avoid blocking Revit UI
        return await Task.Run(async () =>
        {
            var methodName = TestCase.TestMethod.Method.Name;
                        
            try
            {
                // Request model setup on UI thread and wait for completion
                try
                {
                    _document = await RevitTestInfrastructure.RevitTask.Run(app =>
                    {
                        return RevitTestModelHelper.OpenModel(app, _localPath, _projectGuid, _modelGuid);
                    });
                }
                catch (Exception ex)
                {
                    var unwrappedException = UnwrapException(ex);
                    throw new InvalidOperationException($"Model setup failed for test '{methodName}': {unwrappedException.Message}", unwrappedException);
                }

                try
                {
                    _transactionGroup = await RevitTestInfrastructure.RevitTask.Run(app =>
                    {
                        // Start a transaction group for the test
                        var tg = new TransactionGroup(_document, $"Test: {methodName}");
                        tg.Start();
                        return tg;
                    });
                }
                catch (Exception ex)
                {
                    var unwrappedException = UnwrapException(ex);
                    throw new InvalidOperationException($"Transaction group creation failed for test '{methodName}': {unwrappedException.Message}", unwrappedException);
                }

                // Now run the test with the prepared document
                return await base.RunTestAsync();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during test setup or execution
                var unwrappedException = UnwrapException(ex);
                Debug.WriteLine($"Error running test {methodName}: {unwrappedException.Message}");
                
                // Add the unwrapped exception to the aggregator for proper reporting
                _aggregator.Add(unwrappedException);
                
                // Let the base class handle the exception reporting
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
                            var unwrappedException = UnwrapException(ex);
                            Debug.WriteLine($"Error rolling back transaction group: {unwrappedException.Message}");
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

    /// <summary>
    /// Unwraps TargetInvocationException and other wrapper exceptions to get the actual exception
    /// </summary>
    private static Exception UnwrapException(Exception ex)
    {
        // Unwrap TargetInvocationException
        if (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
        {
            return UnwrapException(tie.InnerException);
        }
        
        // Unwrap AggregateException (single inner exception)
        if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
        {
            return UnwrapException(ae.InnerExceptions[0]);
        }
        
        // Return the original exception if no unwrapping is needed
        return ex;
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
public class RevitUITestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments,
    MethodInfo testMethod, object[] testMethodArguments, string skipReason,
    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator,
    CancellationTokenSource cancellationTokenSource) : XunitTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments,
           skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
{
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
                    // Unwrap TargetInvocationException to get the actual test exception
                    exception = UnwrapException(ex);
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

    /// <summary>
    /// Unwraps TargetInvocationException and other wrapper exceptions to get the actual exception
    /// </summary>
    private static Exception UnwrapException(Exception ex)
    {
        // Unwrap TargetInvocationException
        if (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
        {
            return UnwrapException(tie.InnerException);
        }
        
        // Unwrap AggregateException (single inner exception)
        if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
        {
            return UnwrapException(ae.InnerExceptions[0]);
        }
        
        // Return the original exception if no unwrapping is needed
        return ex;
    }
}

using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Xunit.Abstractions;
using Xunit.Sdk;
using RevitTestFramework.Common;

namespace RevitTestFramework.Xunit;

public class RevitXunitTestCaseRunner(IXunitTestCase testCase, string displayName, string skipReason,
    object[] constructorArguments, IMessageBus messageBus,
    ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource,
    string? projectGuid, string? modelGuid, string? localPath) : XunitTestCaseRunner(testCase, displayName, skipReason, constructorArguments, 
           CreateTestMethodArguments(testCase.TestMethod.Method.ToRuntimeMethod()),
           messageBus, aggregator, cancellationTokenSource)
{
    private readonly ExceptionAggregator _aggregator = aggregator;
    private readonly string? _projectGuid = projectGuid;
    private readonly string? _modelGuid = modelGuid;
    private readonly string? _localPath = localPath;

    private Document? _document;
    private TransactionGroup? _transactionGroup;

    /// <summary>
    /// Creates test method arguments with the correct values for the base constructor.
    /// This handles parameter injection for supported types.
    /// </summary>
    private static object?[] CreateTestMethodArguments(MethodInfo testMethod)
    {
        var parameters = testMethod.GetParameters();
        var args = new object?[parameters.Length];
        
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var isNullable = paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) ||
                            !paramType.IsValueType;
            
            if (paramType == typeof(UIApplication))
            {
                // Note: UIApplication will be injected later when infrastructure is available
                args[i] = null; // Placeholder - will be replaced in CreateTestRunner
            }
            else if (paramType == typeof(Document) || 
                    (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                     Nullable.GetUnderlyingType(paramType) == typeof(Document)) ||
                    (paramType == typeof(Document) && isNullable))
            {
                // Note: Document will be injected later when available
                args[i] = null; // Placeholder - will be replaced in CreateTestRunner
            }
            else if (paramType == typeof(CancellationToken))
            {
                // Use default CancellationToken for now - will be replaced in CreateTestRunner
                args[i] = CancellationToken.None;
            }
            else if (paramType == typeof(CancellationToken?) || 
                    (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                     Nullable.GetUnderlyingType(paramType) == typeof(CancellationToken)))
            {
                // Use null for nullable CancellationToken - will be replaced in CreateTestRunner
                args[i] = null;
            }
            else if (isNullable)
            {
                // For other nullable parameters, use null
                args[i] = null;
            }
            else
            {
                // For non-nullable parameters we don't support, we need to provide a default value
                // This will be caught later in CreateTestRunner with a better error message
                try
                {
                    args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
                }
                catch
                {
                    args[i] = null;
                }
            }
        }
        
        return args;
    }

    protected override async Task<RunSummary> RunTestAsync()
    {
        // Run the test on a background thread to avoid blocking Revit UI
        return await Task.Run(async () =>
        {
            var methodName = TestCase.TestMethod.Method.Name;
            var className = TestCase.TestMethod.TestClass.Class.Name;
            
            // Check if we're in debug mode - if debugger is attached, add a breakpoint opportunity
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"RevitXunitTestCaseRunner: Running test '{className}.{methodName}' in debug mode");
                
                // Only break if this is explicitly a debug test or if environment variable is set
                var forceBreak = Environment.GetEnvironmentVariable("REVIT_TEST_BREAK_ON_ALL") == "true";
                var isDebugTest = methodName.Contains("Debug", StringComparison.OrdinalIgnoreCase) || 
                                 className.Contains("Debug", StringComparison.OrdinalIgnoreCase);
                
                if (forceBreak || isDebugTest)
                {
                    Debug.WriteLine($"RevitXunitTestCaseRunner: Breaking for test '{methodName}' (ForceBreak={forceBreak}, IsDebugTest={isDebugTest})");
                    // This line serves as a potential breakpoint location for debugging test setup
                    Debugger.Break(); // This will pause execution if a debugger is attached
                }
                else
                {
                    Debug.WriteLine($"RevitXunitTestCaseRunner: Skipping break for test '{methodName}' (set REVIT_TEST_BREAK_ON_ALL=true to break on all tests)");
                }
            }
                        
            try
            {
                // Request model setup on UI thread and wait for completion
                try
                {
                    _document = await RevitTestInfrastructure.RevitTask.Run(app =>
                    {
                        return RevitTestModelHelper.OpenModel(app, _localPath, _projectGuid, _modelGuid);
                    });
                    
                    // If no document was opened and all parameters are null, 
                    // this might be a test that doesn't need a document
                    if (_document == null && string.IsNullOrEmpty(_localPath) && 
                        string.IsNullOrEmpty(_projectGuid) && string.IsNullOrEmpty(_modelGuid))
                    {
                        Debug.WriteLine($"Test '{methodName}' will run without a specific document (no active document available)");
                    }
                }
                catch (Exception ex)
                {
                    var unwrappedException = UnwrapException(ex);
                    throw new InvalidOperationException($"Model setup failed for test '{methodName}': {unwrappedException.Message}", unwrappedException);
                }

                // Only create transaction group if we have a document
                if (_document != null)
                {
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
                }

                // Add debug information before running the actual test
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine($"RevitXunitTestCaseRunner: About to execute test method '{methodName}'");
                    Debug.WriteLine($"RevitXunitTestCaseRunner: Document available: {_document != null}");
                    Debug.WriteLine($"RevitXunitTestCaseRunner: Transaction group active: {_transactionGroup != null}");
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
                
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine($"RevitXunitTestCaseRunner: Completed test '{methodName}' cleanup");
                }
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
        object?[] testMethodArguments,
        string skipReason,
        IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        var parameters = testMethod.GetParameters();
        
        // Replace placeholder values with actual injected values
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            var isNullable = paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) ||
                            !paramType.IsValueType;
            
            if (paramType == typeof(UIApplication))
            {
                // Inject UIApplication from static infrastructure
                testMethodArguments[i] = RevitTestInfrastructure.UIApplication;
            }
            else if (paramType == typeof(Document) || 
                    (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                     Nullable.GetUnderlyingType(paramType) == typeof(Document)) ||
                    (paramType == typeof(Document) && isNullable))
            {
                // Inject document if available, or null for nullable Document parameters
                testMethodArguments[i] = _document;
            }
            else if (paramType == typeof(CancellationToken))
            {
                // Inject CancellationToken from static infrastructure, or default if not available
                testMethodArguments[i] = RevitTestInfrastructure.CancellationToken ?? CancellationToken.None;
            }
            else if (paramType == typeof(CancellationToken?) || 
                    (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                     Nullable.GetUnderlyingType(paramType) == typeof(CancellationToken)))
            {
                // Inject nullable CancellationToken from static infrastructure
                testMethodArguments[i] = RevitTestInfrastructure.CancellationToken;
            }
            else if (!isNullable && testMethodArguments[i] == null)
            {
                // For non-nullable parameters we don't support, throw an exception
                throw new InvalidOperationException(
                    $"Test method '{testMethod.Name}' has unsupported parameter type '{paramType.Name}' at position {i}. " +
                    "Supported types are: UIApplication, Document, Document?, CancellationToken, CancellationToken?");
            }
            // For nullable parameters that we don't explicitly handle, leave them as null (already set)
        }

        return new RevitUITestRunner(test, messageBus, testClass, constructorArguments,
            testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource);
    }
}

/// <summary>
/// Custom test runner that ensures test method execution happens on UI thread when needed
/// </summary>
public class RevitUITestRunner(ITest test, IMessageBus messageBus, Type testClass, object[] constructorArguments,
    MethodInfo testMethod, object?[] testMethodArguments, string skipReason,
    IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes, ExceptionAggregator aggregator,
    CancellationTokenSource cancellationTokenSource) : XunitTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments,
           skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
{
    protected override async Task<decimal> InvokeTestMethodAsync(ExceptionAggregator aggregator)
    {        
        try
        {
            Exception? exception = null;

            var result = await RevitTestInfrastructure.RevitTask.Run(app =>
            {
                var timer = new Stopwatch();
                timer.Start();

                try
                {
                    // Debug support: Add breakpoint opportunity when debugger is attached
                    if (Debugger.IsAttached)
                    {
                        Debug.WriteLine($"RevitUITestRunner: About to invoke test method '{TestMethod.Name}' on UI thread");
                        Debug.WriteLine($"RevitUITestRunner: Test class: {TestClass.Name}");
                        Debug.WriteLine($"RevitUITestRunner: Arguments count: {TestMethodArguments.Length}");
                        
                        // This serves as a breakpoint location for debugging the actual test method execution
                        if (TestMethod.Name.Contains("Debug", StringComparison.OrdinalIgnoreCase) || 
                            TestClass.Name.Contains("Debug", StringComparison.OrdinalIgnoreCase))
                        {
                            Debugger.Break(); // Break only for tests that seem to be debug-related
                        }
                    }

                    // Create test instance
                    var testInstance = Activator.CreateInstance(TestClass, ConstructorArguments);

                    // Invoke the test method directly - no need for runtime argument injection
                    var result = TestMethod.Invoke(testInstance, TestMethodArguments);

                    // Handle async test methods
                    if (result is Task task)
                    {
                        task.Wait();
                    }

                    timer.Stop();
                    
                    if (Debugger.IsAttached)
                    {
                        Debug.WriteLine($"RevitUITestRunner: Test method '{TestMethod.Name}' completed successfully in {timer.ElapsedMilliseconds}ms");
                    }
                    
                    return timer.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    // Unwrap TargetInvocationException to get the actual test exception
                    exception = UnwrapException(ex);
                    
                    if (Debugger.IsAttached)
                    {
                        Debug.WriteLine($"RevitUITestRunner: Test method '{TestMethod.Name}' failed with exception: {exception.Message}");
                    }
                    
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

using System.Diagnostics;
using System.Linq;
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
    RevitTestFramework.Common.RevitTestConfiguration configuration) : XunitTestCaseRunner(testCase, displayName, skipReason, constructorArguments, 
           CreateTestMethodArguments(testCase.TestMethod.Method.ToRuntimeMethod()),
           messageBus, aggregator, cancellationTokenSource)
{
    private readonly ExceptionAggregator _aggregator = aggregator;
    private readonly RevitTestFramework.Common.RevitTestConfiguration _configuration = configuration;
    private static RevitTestFramework.Common.ILogger _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitXunitTestCaseRunner));
    private static readonly object _loggerLock = new object();

    private Document? _document;
    private TransactionGroup? _transactionGroup;

    /// <summary>
    /// Sets a pipe-aware logger for test execution
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to use for forwarding logs</param>
    public static void SetPipeAwareLogger(StreamWriter? pipeWriter)
    {
        lock (_loggerLock)
        {
            if (pipeWriter != null)
            {
                _logger = RevitTestFramework.Common.PipeAwareLogger.ForContext(typeof(RevitXunitTestCaseRunner), pipeWriter);
                _logger.LogDebug("RevitXunitTestCaseRunner: Pipe-aware logger has been configured");
            }
            else
            {
                _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitXunitTestCaseRunner));
                _logger.LogDebug("RevitXunitTestCaseRunner: Reset to file-only logging");
            }
        }
    }

    /// <summary>
    /// Gets the current logger (thread-safe)
    /// </summary>
    private static RevitTestFramework.Common.ILogger Logger
    {
        get
        {
            lock (_loggerLock)
            {
                return _logger;
            }
        }
    }

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

    /// <summary>
    /// Checks if the test method requires a Document parameter (Document or Document?)
    /// </summary>
    /// <param name="testMethod">The test method to analyze</param>
    /// <returns>True if the method has a Document or Document? parameter, false otherwise</returns>
    private static bool DoesTestMethodRequireDocument(MethodInfo testMethod)
    {
        var parameters = testMethod.GetParameters();
        
        foreach (var parameter in parameters)
        {
            var paramType = parameter.ParameterType;
            
            // Check for Document parameter
            if (paramType == typeof(Document))
            {
                Logger.LogDebug($"Found Document parameter '{parameter.Name}' in test method '{testMethod.Name}'");
                return true;
            }
            
            // Check for nullable Document parameter (Document?)
            if (paramType.IsGenericType && 
                paramType.GetGenericTypeDefinition() == typeof(Nullable<>) && 
                Nullable.GetUnderlyingType(paramType) == typeof(Document))
            {
                Logger.LogDebug($"Found nullable Document parameter '{parameter.Name}' in test method '{testMethod.Name}'");
                return true;
            }
        }
        
        Logger.LogDebug($"No Document parameters found in test method '{testMethod.Name}' - parameters: [{string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}]");
        return false;
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
                // Only break if this is explicitly a debug test or if environment variable is set
                var forceBreak = Environment.GetEnvironmentVariable("REVIT_TEST_BREAK_ON_ALL") == "true";
                var isDebugTest = methodName.Contains("Debug", StringComparison.OrdinalIgnoreCase) || 
                                 className.Contains("Debug", StringComparison.OrdinalIgnoreCase);
                
                if (forceBreak || isDebugTest)
                {
                    Logger.LogDebug($"Breaking for test '{methodName}' (ForceBreak={forceBreak}, IsDebugTest={isDebugTest})");
                    // This line serves as a potential breakpoint location for debugging test setup
                    Debugger.Break(); // This will pause execution if a debugger is attached
                }
                else
                {
                    Logger.LogDebug($"Skipping break for test '{methodName}' (set REVIT_TEST_BREAK_ON_ALL=true to break on all tests)");
                }
            }

            // Check if the test method actually requires a Document parameter
            var testMethod = TestCase.TestMethod.Method.ToRuntimeMethod();
            var requiresDocument = DoesTestMethodRequireDocument(testMethod);
            
            Logger.LogDebug($"Test '{methodName}' requires document: {requiresDocument}");
                        
            try
            {
                // Only attempt to open model if the test method requires a Document parameter
                if (requiresDocument)
                {
                    // Request model setup on UI thread and wait for completion
                    try
                    {
                        // Get the test assembly directory for relative path resolution
                        var testAssemblyDirectory = Path.GetDirectoryName(RevitTestInfrastructure.ActiveCommand.TestAssembly);

                        _document = await RevitTestInfrastructure.RevitTask.Run(app =>
                        {
                            return RevitTestModelHelper.OpenModel(app, _configuration, testAssemblyDirectory);
                        });
                        
                        // If no document was opened and all parameters are null, 
                        // this might be a test that doesn't need a document
                        if (_document == null && string.IsNullOrEmpty(_configuration.LocalPath) && 
                            string.IsNullOrEmpty(_configuration.ProjectGuid) && string.IsNullOrEmpty(_configuration.ModelGuid))
                        {
                            Logger.LogDebug($"Test '{methodName}' will run without a specific document (no active document available)");
                        }
                    }
                    catch (Exception ex)
                    {
                        var unwrappedException = UnwrapException(ex);
                        throw new InvalidOperationException($"Model setup failed for test '{methodName}': {unwrappedException.Message}", unwrappedException);
                    }
                }
                else
                {
                    Logger.LogDebug($"Skipping model opening for test '{methodName}' - no Document parameter required");
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

                // Now run the test with the prepared document
                return await base.RunTestAsync();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during test setup or execution
                var unwrappedException = UnwrapException(ex);
                Logger.LogError(unwrappedException, $"Error running test {methodName}");
                
                // Add the unwrapped exception to the aggregator for proper reporting
                _aggregator.Add(unwrappedException);
                
                // Let the base class handle the exception reporting
                return await base.RunTestAsync();
            }
            finally
            {
                // Store reference to document for potential closing before cleaning up _document field
                var documentToClose = _document;
                
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
                            Logger.LogError(unwrappedException, "Error rolling back transaction group");
                        }
                        finally
                        {
                            _transactionGroup.Dispose();
                        }
                    }
                    
                    // Close the model if CloseModel flag is set
                    if (_configuration.CloseModel && documentToClose != null)
                    {
                        try
                        {
                            Logger.LogInformation($"Closing model '{documentToClose.Title}' as requested by CloseModel flag");
                            documentToClose.Close(false); // Close without saving
                        }
                        catch (Exception ex)
                        {
                            var unwrappedException = UnwrapException(ex);
                            Logger.LogError(unwrappedException, "Error closing model");
                            // Don't throw here - this is cleanup, and we don't want to mask test results
                        }
                    }
                });
                
                if (Debugger.IsAttached)
                {
                    Logger.LogDebug($"Completed test '{methodName}' cleanup");
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
    private static RevitTestFramework.Common.ILogger _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitUITestRunner));
    private static readonly object _loggerLock = new object();

    /// <summary>
    /// Sets a pipe-aware logger for test execution
    /// </summary>
    /// <param name="pipeWriter">The pipe writer to use for forwarding logs</param>
    public static void SetPipeAwareLogger(StreamWriter? pipeWriter)
    {
        lock (_loggerLock)
        {
            if (pipeWriter != null)
            {
                _logger = RevitTestFramework.Common.PipeAwareLogger.ForContext(typeof(RevitUITestRunner), pipeWriter);
                _logger.LogDebug("RevitUITestRunner: Pipe-aware logger has been configured");
            }
            else
            {
                _logger = RevitTestFramework.Common.FileLogger.ForContext(typeof(RevitUITestRunner));
                _logger.LogDebug("RevitUITestRunner: Reset to file-only logging");
            }
        }
    }

    /// <summary>
    /// Gets the current logger (thread-safe)
    /// </summary>
    private static RevitTestFramework.Common.ILogger Logger
    {
        get
        {
            lock (_loggerLock)
            {
                return _logger;
            }
        }
    }

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
                        Logger.LogDebug($"About to invoke test method '{TestMethod.Name}' on UI thread");
                        Logger.LogDebug($"Test class: {TestClass.Name}");
                        Logger.LogDebug($"Arguments count: {TestMethodArguments.Length}");
                        
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

using Autodesk.Revit.UI;
using System.Diagnostics;
using System.Reflection;

namespace RevitTestFramework.Common;

/// <summary>
/// Thread-safe, reusable wrapper for executing tasks via Revit's ExternalEvent system.
/// ExternalEvent is created once in the constructor on Revit's main thread.
/// </summary>
public class RevitTask : IDisposable
{
    private readonly ExternalEvent _externalEvent;
    private readonly InternalHandler _handler;
    private readonly object _lock = new object();

    public RevitTask()
    {
        _handler = new InternalHandler();
        _externalEvent = ExternalEvent.Create(_handler);
    }

    /// <summary>
    /// Runs a function on the Revit UI thread and returns its result asynchronously.
    /// </summary>
    public Task<TResult> Run<TResult>(Func<UIApplication, TResult> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        var tcs = new TaskCompletionSource<TResult>();

        lock (_lock)
        {
            _handler.Set(func, tcs);
            _externalEvent.Raise();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Runs an action on the Revit UI thread asynchronously.
    /// </summary>
    public Task Run(Action<UIApplication> act)
    {
        if (act == null)
            throw new ArgumentNullException(nameof(act));

        return Run<object?>(app =>
        {
            act(app);
            return null;
        });
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        _externalEvent?.Dispose();
    }

    /// <summary>
    /// Internal handler bridges ExternalEvent and TaskCompletionSource.
    /// </summary>
    private class InternalHandler : IExternalEventHandler
    {
        private Delegate? _func;
        private object? _tcs;

        public void Set<TResult>(Func<UIApplication, TResult> func, TaskCompletionSource<TResult> tcs)
        {
            _func = func;
            _tcs = tcs;
        }

        public void Execute(UIApplication app)
        {
            if (_func == null || _tcs == null) return;

            try
            {
                var funcType = _func.GetType();
                var resultType = funcType.GenericTypeArguments.Length > 1 ? funcType.GenericTypeArguments[1] : typeof(object);
                var result = _func.DynamicInvoke(app);
                var tcsType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
                var trySetResult = tcsType.GetMethod("TrySetResult", new[] { resultType });
                trySetResult?.Invoke(_tcs, new[] { result });
            }
            catch (Exception ex)
            {
                var tcsType = _tcs.GetType();
                
                try
                {
                    var trySetException = tcsType.GetMethod("TrySetException", new[] { typeof(Exception) });
                    trySetException?.Invoke(_tcs, [ex]);
                }
                catch (Exception setEx)
                {
                    // If setting the exception fails, we can log it or handle it as needed.
                    // This is a fallback to ensure we don't leave the TaskCompletionSource in an uncompleted state.
                    Trace.WriteLine($"Failed to set exception on TaskCompletionSource: {setEx.Message}");
                }
            }
            finally
            {
                _func = null;
                _tcs = null;
            }
        }

        public string GetName() => "RevitTask.InternalHandler";
    }
}

using Autodesk.Revit.UI;
using RevitAddin.Common;

namespace RevitAddin.Xunit;

public class TestCommandHandler : ITestCommandHandler
{
    private IXunitTestAssemblyLoadContext? _loaderContext;
    
    private object? _result;
    public object Result { get => _result ?? throw new InvalidOperationException("Result is not set. Execute the command first."); }

    public void SetContext(IXunitTestAssemblyLoadContext loaderContext)
    {
        _loaderContext = loaderContext ?? throw new ArgumentNullException(nameof(loaderContext));
    }

    public void Execute(UIApplication app)
    {
        if (_loaderContext == null)
        {
            throw new InvalidOperationException("Loader context is not set. Call SetContext first.");
        }

        _result = null;
        _result = _loaderContext.SetupInfrastructure(app);
    }

    public string GetName() => nameof(TestCommandHandler);

    
}

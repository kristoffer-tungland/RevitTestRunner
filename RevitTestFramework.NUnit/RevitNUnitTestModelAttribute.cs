using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using RevitTestFramework.Common;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace RevitTestFramework.NUnit;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RevitNUnitTestModelAttribute : NUnitAttribute, IApplyToTest, IWrapTestMethod
{
    public string? ProjectGuid { get; }
    public string? ModelGuid { get; }
    public string? LocalPath { get; }

    public RevitNUnitTestModelAttribute(string projectGuid, string modelGuid)
    {
        ProjectGuid = projectGuid;
        ModelGuid = modelGuid;
    }

    public RevitNUnitTestModelAttribute(string localPath)
    {
        LocalPath = localPath;
    }

    public void ApplyToTest(global::NUnit.Framework.Internal.Test test)
    {
        if (LocalPath != null)
            test.Properties.Set("RevitLocalPath", LocalPath);
        else
        {
            test.Properties.Set("RevitProjectGuid", ProjectGuid);
            test.Properties.Set("RevitModelGuid", ModelGuid);
        }
    }

    public TestCommand Wrap(TestCommand command)
    {
        return new InjectDocumentCommand(command);
    }

    private class InjectDocumentCommand : DelegatingTestCommand
    {
        public InjectDocumentCommand(TestCommand innerCommand)
            : base(innerCommand)
        {
        }

        public override TestResult Execute(TestExecutionContext context)
        {
            var testMethod = context.CurrentTest as global::NUnit.Framework.Internal.TestMethod;
            var args = testMethod!.Arguments;

            // Get the document from RevitModelService instead of managing it here
            if (RevitModelService.CurrentDocument != null)
            {
                var newArgs = new object[args.Length + 1];
                newArgs[0] = RevitModelService.CurrentDocument;
                if (args.Length > 0)
                    Array.Copy(args, 0, newArgs, 1, args.Length);
                args = newArgs;
            }

            var methodInfo = testMethod.Method.MethodInfo;
            var parameters = methodInfo.GetParameters();
            bool lastAcceptsCt = parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken);
            if (lastAcceptsCt && !LastArgIsCancellationToken(args))
            {
                var tmp = new object[args.Length + 1];
                Array.Copy(args, tmp, args.Length);
                tmp[args.Length] = context.CancellationToken;
                args = tmp;
            }

            object? result;
            var returnValue = methodInfo.Invoke(context.TestObject, args);
            bool isAsync = typeof(Task).IsAssignableFrom(methodInfo.ReturnType) ||
                           methodInfo.GetCustomAttributes(typeof(AsyncStateMachineAttribute), false).Length > 0;
            if (isAsync && returnValue is Task t)
            {
                t.GetAwaiter().GetResult();
                result = t.GetType().GetProperty("Result")?.GetValue(t);
            }
            else
            {
                result = returnValue;
            }

            context.CurrentResult.SetResult(ResultState.Success);

            if (context.CurrentResult.AssertionResults.Count > 0)
                context.CurrentResult.RecordTestCompletion();

            return context.CurrentResult;
        }

        private static bool LastArgIsCancellationToken(object?[] args)
        {
            return args.Length != 0 && args[^1]?.GetType() == typeof(CancellationToken);
        }
    }
}

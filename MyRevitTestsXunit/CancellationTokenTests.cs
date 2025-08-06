using Xunit;
using RevitTestFramework.Xunit;

namespace MyRevitTestsXunit;

/// <summary>
/// Contains tests for verifying cancellation token support in Revit test execution.
/// </summary>
public class CancellationTokenTests
{
    /// <summary>
    /// Verifies that the cancellation token can be passed and used within a Revit context.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to be tested.</param>
    [RevitFact(Timeout = 300)]
    public void CancellationToken_ShouldBePassedAndUsed(CancellationToken cancellationToken)
    {
        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled at this point.");

        // Simulate a long-running operation
        for (int i = 0; i < 100; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            Thread.Sleep(100); // Simulate work
        }

        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled after the operation.");
    }
}

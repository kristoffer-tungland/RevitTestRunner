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
    /// This test should complete successfully within the timeout.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to be tested.</param>
    [RevitFact(Timeout = 5000)] // 5 second timeout - should be enough for this test
    public void CancellationToken_ShouldBePassedAndUsed(CancellationToken cancellationToken)
    {
        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled at this point.");

        // Simulate a short operation that should complete within timeout
        for (int i = 0; i < 10; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            Thread.Sleep(100); // Simulate work - total 1 second max
        }

        Assert.False(cancellationToken.IsCancellationRequested, "Cancellation token should not be cancelled after the operation.");
    }

    /// <summary>
    /// This test should timeout after 1 second and demonstrate that timeout functionality works.
    /// </summary>
    [RevitFact(Timeout = 1000)] // 1 second timeout
    public void TimeoutTest_ShouldTimeoutAfterOneSecond()
    {
        // This should cause a timeout since we're sleeping for 2 seconds but timeout is 1 second
        Thread.Sleep(2000);
        
        // This assertion should never be reached due to timeout
        Assert.Fail("This test should have timed out before reaching this assertion");
    }

    /// <summary>
    /// This test verifies that cancellation tokens respect timeouts when used in loops.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to be tested.</param>
    [RevitFact(Timeout = 500)] // 500ms timeout
    public void CancellationToken_ShouldRespectTimeout(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        // This loop should be cancelled by the timeout
        while (!cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(100);
            
            // Safety check - if we somehow run longer than 2 seconds, fail the test
            if ((DateTime.UtcNow - startTime).TotalSeconds > 2)
            {
                Assert.Fail("Test ran too long - timeout mechanism failed");
                break;
            }
        }
        
        // The cancellation token should have been cancelled due to timeout
        Assert.True(cancellationToken.IsCancellationRequested, "Cancellation token should have been cancelled due to timeout");
        
        // Verify the timeout happened within a reasonable timeframe (within 1 second of the 500ms timeout)
        var elapsed = DateTime.UtcNow - startTime;
        Assert.True(elapsed.TotalMilliseconds < 1500, $"Test should have been cancelled much sooner. Elapsed: {elapsed.TotalMilliseconds}ms");
    }
}

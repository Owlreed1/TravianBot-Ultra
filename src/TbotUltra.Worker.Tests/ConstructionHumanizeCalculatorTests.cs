using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ConstructionHumanizeCalculatorTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(1.1, 2)]
    [InlineData(60.1, 61)]
    public void ResolveExistingWaitSeconds_ExpiresOnceAndRoundsFutureWaitUp(
        double secondsUntilScheduled,
        int expected)
    {
        var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        var result = ConstructionHumanizeCalculator.ResolveExistingWaitSeconds(
            now,
            now.AddSeconds(secondsUntilScheduled));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateAfterFullQueue_ConsumesPlusDelayBeforeFirstSlotOpens()
    {
        var result = ConstructionHumanizeCalculator.CalculateAfterFullQueue(
            [100, 500], 100, 5, 20, 25, 1, 3, (_, _) => 10);

        Assert.Equal(100, result.QueueRetrySeconds);
        Assert.Equal(10, result.HumanizeDelaySeconds);
        Assert.Equal("before slot opens, percent 10% of 100s remaining", result.Reason);
    }

    [Fact]
    public void CalculateAfterFullQueue_CapsPercentageDelay()
    {
        var result = ConstructionHumanizeCalculator.CalculateAfterFullQueue(
            [1_000, 10_100], 1_000, 5, 20, 2, 1, 3, (_, _) => 20);

        Assert.Equal(1_000, result.QueueRetrySeconds);
        Assert.Equal(120, result.HumanizeDelaySeconds);
    }

    [Fact]
    public void CalculateAfterFullQueue_UsesNoPlusRangeWhenNoTimerRemains()
    {
        var result = ConstructionHumanizeCalculator.CalculateAfterFullQueue(
            [100], 100, 5, 20, 25, 1, 3, (min, max) =>
            {
                Assert.Equal(1, min);
                Assert.Equal(3, max);
                return 2.5;
            });

        Assert.Equal(250, result.QueueRetrySeconds);
        Assert.Equal(150, result.HumanizeDelaySeconds);
        Assert.Equal($"after slot opens, no-plus {2.5:F1}m", result.Reason);
    }

    [Fact]
    public void CalculateAfterFullQueue_ReturnsNoDelayForInvalidSlotWaitWithoutCallingRandom()
    {
        var result = ConstructionHumanizeCalculator.CalculateAfterFullQueue(
            [100], 0, 5, 20, 25, 1, 3, (_, _) => throw new Xunit.Sdk.XunitException("RNG should not be called"));

        Assert.Equal(ConstructionHumanizeDecision.None, result);
    }

    [Fact]
    public void CalculateAfterFullQueue_UsesShortestRemainingTimerAsReference()
    {
        var result = ConstructionHumanizeCalculator.CalculateAfterFullQueue(
            [900, 100, 500], 100, 5, 20, 25, 1, 3, (_, _) => 10);

        Assert.Equal(100, result.QueueRetrySeconds);
        Assert.Equal(10, result.HumanizeDelaySeconds);
    }
}

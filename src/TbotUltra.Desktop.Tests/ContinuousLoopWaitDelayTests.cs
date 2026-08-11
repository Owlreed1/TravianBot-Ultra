using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ContinuousLoopWaitDelayTests
{
    [Fact]
    public void ResolveContinuousLoopWaitDelay_ReadyVillageScan_PreemptsDeferredQueueDeadline()
    {
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

        var result = MainWindow.ResolveContinuousLoopWaitDelay(
            now,
            now.AddSeconds(620),
            null,
            DateTimeOffset.MinValue);

        Assert.Equal(TimeSpan.FromSeconds(1), result);
    }

    [Fact]
    public void ResolveContinuousLoopWaitDelay_ConstructionAvailabilityPreemptsQueueDeadline()
    {
        var now = new DateTimeOffset(2026, 8, 11, 17, 15, 40, TimeSpan.Zero);

        var result = MainWindow.ResolveContinuousLoopWaitDelay(
            now,
            now.AddMinutes(9).AddSeconds(28),
            now.AddMinutes(2).AddSeconds(40),
            null);

        Assert.Equal(TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(40)), result);
    }
}

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
            DateTimeOffset.MinValue);

        Assert.Equal(TimeSpan.FromSeconds(1), result);
    }
}

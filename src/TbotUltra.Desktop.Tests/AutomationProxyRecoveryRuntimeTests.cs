using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationProxyRecoveryRuntimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryReserve_RequiresThresholdAndSingleOwnership()
    {
        var runtime = new AutomationProxyRecoveryRuntime(new MutableTimeProvider(Now));

        Assert.False(runtime.TryReserve(consecutiveFailures: 2, failureThreshold: 3));
        Assert.True(runtime.TryReserve(consecutiveFailures: 3, failureThreshold: 3));
        Assert.False(runtime.TryReserve(consecutiveFailures: 4, failureThreshold: 3));
        runtime.Release();
        Assert.True(runtime.TryReserve(consecutiveFailures: 4, failureThreshold: 3));
    }

    [Fact]
    public void ScheduleRetry_BlocksReservationUntilItsDeadline()
    {
        var time = new MutableTimeProvider(Now);
        var runtime = new AutomationProxyRecoveryRuntime(time);
        var retry = runtime.ScheduleRetry();

        Assert.Equal(1, retry.Attempt);
        Assert.Equal(TimeSpan.FromMinutes(2), retry.Delay);
        Assert.False(runtime.TryReserve(3, 3));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(runtime.TryReserve(3, 3));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(8, 10)]
    public void ResolveRetryDelay_IsBounded(int attempt, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), AutomationProxyRecoveryRuntime.ResolveRetryDelay(attempt));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan delay) => _now += delay;
    }
}

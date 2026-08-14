using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationIdlePacingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlanBreak_SchedulesFirstAndReturnsControlledDurationWhenDue()
    {
        var time = new MutableTimeProvider(Now);
        var pacing = new AutomationIdlePacing(time, nextDouble: () => 0.5);
        var options = new BotOptions
        {
            ActionPacingIdleBreakEnabled = true,
            ActionPacingIdleBreakIntervalMinMinutes = 10,
            ActionPacingIdleBreakIntervalMaxMinutes = 10,
            ActionPacingIdleBreakDurationMinMinutes = 2,
            ActionPacingIdleBreakDurationMaxMinutes = 2,
        };

        Assert.False(pacing.PlanBreak(options, sessionAvailable: true).ShouldTakeBreak);
        time.Advance(TimeSpan.FromMinutes(10));

        var plan = pacing.PlanBreak(options, sessionAvailable: true);

        Assert.True(plan.ShouldTakeBreak);
        Assert.Equal(120, plan.DurationSeconds);
    }

    [Fact]
    public void PlanBreak_ReschedulesInsteadOfFiringWhenTheSessionIsUnavailable()
    {
        var time = new MutableTimeProvider(Now);
        var pacing = new AutomationIdlePacing(time, nextDouble: () => 0);
        var options = BreakOptions();
        pacing.PlanBreak(options, sessionAvailable: true);
        time.Advance(TimeSpan.FromMinutes(10));

        Assert.False(pacing.PlanBreak(options, sessionAvailable: false).ShouldTakeBreak);
        Assert.False(pacing.PlanBreak(options, sessionAvailable: true).ShouldTakeBreak);
    }

    [Fact]
    public void PlanBrowse_SelectsAnEnabledPageWithControlledRandomness()
    {
        var time = new MutableTimeProvider(Now);
        var pacing = new AutomationIdlePacing(time, nextDouble: () => 0, nextInt: (_, max) => max - 1);
        var options = new BotOptions
        {
            ActionPacingIdleBrowseEnabled = true,
            ActionPacingIdleBrowseIntervalMinMinutes = 10,
            ActionPacingIdleBrowseIntervalMaxMinutes = 10,
            ActionPacingIdleBrowsePageMap = true,
            ActionPacingIdleBrowsePageMessages = true,
        };
        pacing.PlanBrowse(options, sessionAvailable: true);
        time.Advance(TimeSpan.FromMinutes(10));

        var plan = pacing.PlanBrowse(options, sessionAvailable: true);

        Assert.Equal("nachrichten.php", plan.Page);
    }

    private static BotOptions BreakOptions() => new()
    {
        ActionPacingIdleBreakEnabled = true,
        ActionPacingIdleBreakIntervalMinMinutes = 10,
        ActionPacingIdleBreakIntervalMaxMinutes = 10,
        ActionPacingIdleBreakDurationMinMinutes = 2,
        ActionPacingIdleBreakDurationMaxMinutes = 2,
    };

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan delay) => _now += delay;
    }
}

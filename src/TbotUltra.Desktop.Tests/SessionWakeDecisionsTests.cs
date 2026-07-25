using TbotUltra.Desktop.Services.Orchestration;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class SessionWakeDecisionsTests
{
    [Fact]
    public void ResolveResume_resumes_continuous_loop_when_it_ran_before_sleep_and_loop_is_idle()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: true, WasQueueAutoRunning: false);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: true, autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.ResumeContinuousLoop, action);
    }

    [Fact]
    public void ResolveResume_stays_logged_in_when_loop_is_already_running()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: true, WasQueueAutoRunning: false);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: false, autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.StayLoggedIn, action);
    }

    [Fact]
    public void ResolveResume_resumes_queue_auto_run_when_it_ran_before_sleep_and_nothing_is_running()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: false, WasQueueAutoRunning: true);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: true, autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.ResumeQueueAutoRun, action);
    }

    [Fact]
    public void ResolveResume_does_not_resume_queue_auto_run_when_it_is_already_running()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: false, WasQueueAutoRunning: true);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: true, autoQueueRunning: true);

        Assert.Equal(WakeResumeAction.StayLoggedIn, action);
    }

    [Fact]
    public void ResolveResume_does_not_resume_queue_auto_run_while_a_loop_is_running()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: false, WasQueueAutoRunning: true);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: false, autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.StayLoggedIn, action);
    }

    [Fact]
    public void ResolveResume_prefers_continuous_loop_over_queue_auto_run()
    {
        var snapshot = new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: true, WasQueueAutoRunning: true);

        var action = SessionWakeDecisions.ResolveResume(snapshot, loopIdle: true, autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.ResumeContinuousLoop, action);
    }

    [Fact]
    public void ResolveResume_stays_logged_in_when_nothing_ran_before_sleep()
    {
        var action = SessionWakeDecisions.ResolveResume(
            new SleepSnapshot(WasLoggedIn: true, WasContinuousLoopRunning: false, WasQueueAutoRunning: false),
            loopIdle: true,
            autoQueueRunning: false);

        Assert.Equal(WakeResumeAction.StayLoggedIn, action);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 15)]
    [InlineData(6, 30)]
    [InlineData(7, 30)]
    [InlineData(100, 30)]
    public void NextWakeLoginRetryDelay_follows_the_backoff_ramp_then_holds(int attempt, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), SessionWakeDecisions.NextWakeLoginRetryDelay(attempt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NextWakeLoginRetryDelay_clamps_non_positive_attempts_to_the_first_step(int attempt)
    {
        Assert.Equal(TimeSpan.FromMinutes(1), SessionWakeDecisions.NextWakeLoginRetryDelay(attempt));
    }

    [Fact]
    public void ResolveAbort_does_not_abort_while_a_normal_wake_retry_is_in_flight()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: false,
            IsSessionSleeping: false,
            AccountSwitchInProgress: false,
            AppClosing: false));

        Assert.False(decision.ShouldAbort);
        Assert.Equal(string.Empty, decision.Reason);
    }

    [Fact]
    public void ResolveAbort_stops_once_logged_in()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: true,
            IsSessionSleeping: false,
            AccountSwitchInProgress: false,
            AppClosing: false));

        Assert.True(decision.ShouldAbort);
        Assert.Equal("already logged in", decision.Reason);
    }

    [Fact]
    public void ResolveAbort_stops_when_a_new_sleep_window_took_over()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: false,
            IsSessionSleeping: true,
            AccountSwitchInProgress: false,
            AppClosing: false));

        Assert.True(decision.ShouldAbort);
        Assert.Equal("session is sleeping again", decision.Reason);
    }

    [Fact]
    public void ResolveAbort_stops_during_an_account_switch()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: false,
            IsSessionSleeping: false,
            AccountSwitchInProgress: true,
            AppClosing: false));

        Assert.True(decision.ShouldAbort);
        Assert.Equal("account switch in progress", decision.Reason);
    }

    [Fact]
    public void ResolveAbort_stops_when_the_app_is_closing()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: false,
            IsSessionSleeping: false,
            AccountSwitchInProgress: false,
            AppClosing: true));

        Assert.True(decision.ShouldAbort);
        Assert.Equal("app closing", decision.Reason);
    }

    [Fact]
    public void ResolveAbort_reports_logged_in_before_any_other_reason()
    {
        var decision = SessionWakeDecisions.ResolveAbort(new WakeRetryState(
            IsLoggedIn: true,
            IsSessionSleeping: true,
            AccountSwitchInProgress: true,
            AppClosing: true));

        Assert.True(decision.ShouldAbort);
        Assert.Equal("already logged in", decision.Reason);
    }
}

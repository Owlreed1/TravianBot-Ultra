namespace TbotUltra.Desktop.Services.Orchestration;

/// <summary>
/// What was actually running when session sleep began, captured so the next wake restores the
/// same state instead of always starting the continuous loop (the toggle defaults to ON even when
/// the bot was idle). This is the account's "restore intent" for the next wake.
/// </summary>
public sealed record SleepSnapshot(
    bool WasLoggedIn,
    bool WasContinuousLoopRunning,
    bool WasQueueAutoRunning)
{
    /// <summary>Nothing to resume: the bot was logged out / idle before sleep.</summary>
    public static readonly SleepSnapshot Idle = new(false, false, false);
}

/// <summary>What the wake path should resume after a successful login.</summary>
public enum WakeResumeAction
{
    /// <summary>Logged in, but nothing was running before sleep — stay idle.</summary>
    StayLoggedIn,
    ResumeContinuousLoop,
    ResumeQueueAutoRun,
}

/// <summary>Live flags that decide whether the wake-login retry loop should give up.</summary>
public sealed record WakeRetryState(
    bool IsLoggedIn,
    bool IsSessionSleeping,
    bool AccountSwitchInProgress,
    bool AppClosing);

/// <summary>Whether the wake-login retry should stop, and the reason to log if so.</summary>
public readonly record struct WakeAbortDecision(bool ShouldAbort, string Reason);

/// <summary>
/// Pure decisions for the session wake path, extracted from <c>MainWindow.SessionPacing</c> so the
/// historically fragile "resume the right thing" and "when to stop retrying login" logic is
/// unit-testable in isolation from the WPF window and browser.
/// </summary>
public static class SessionWakeDecisions
{
    // Backoff between wake-login attempts. After the ramp it stays at the last value (30 min) with NO
    // cap on attempt count: an overnight transient (network/server timeout during the post-login
    // snapshot) must never leave the bot parked idle until morning — it keeps retrying, sparsely,
    // until login takes.
    private static readonly int[] WakeLoginRetryBackoffMinutes = { 1, 2, 5, 10, 15, 30 };

    /// <summary>
    /// After a successful wake login, decides which automation to resume. The caller only reaches
    /// this once <see cref="SleepSnapshot.WasLoggedIn"/> is true (it skips waking entirely otherwise).
    /// </summary>
    /// <param name="loopIdle">True when no continuous loop task is currently running.</param>
    /// <param name="autoQueueRunning">True when the queue auto-run is already running.</param>
    public static WakeResumeAction ResolveResume(SleepSnapshot snapshot, bool loopIdle, bool autoQueueRunning)
    {
        if (snapshot.WasContinuousLoopRunning && loopIdle)
        {
            return WakeResumeAction.ResumeContinuousLoop;
        }

        if (snapshot.WasQueueAutoRunning && !autoQueueRunning && loopIdle)
        {
            return WakeResumeAction.ResumeQueueAutoRun;
        }

        return WakeResumeAction.StayLoggedIn;
    }

    /// <summary>
    /// Wait before the next wake-login attempt. <paramref name="attempt"/> is 1-based; the delay
    /// ramps then stays at the last step for every further attempt.
    /// </summary>
    public static TimeSpan NextWakeLoginRetryDelay(int attempt)
    {
        var index = Math.Clamp(attempt - 1, 0, WakeLoginRetryBackoffMinutes.Length - 1);
        return TimeSpan.FromMinutes(WakeLoginRetryBackoffMinutes[index]);
    }

    /// <summary>
    /// Decides whether the wake-login retry should stop, and why. Logging in no longer makes sense
    /// once the user logged in manually, a new sleep window began, an account switch started, or the
    /// app is shutting down.
    /// </summary>
    public static WakeAbortDecision ResolveAbort(WakeRetryState state)
    {
        if (state.IsLoggedIn)
        {
            return new WakeAbortDecision(true, "already logged in");
        }

        if (state.IsSessionSleeping)
        {
            return new WakeAbortDecision(true, "session is sleeping again");
        }

        if (state.AccountSwitchInProgress)
        {
            return new WakeAbortDecision(true, "account switch in progress");
        }

        if (state.AppClosing)
        {
            return new WakeAbortDecision(true, "app closing");
        }

        return new WakeAbortDecision(false, string.Empty);
    }
}

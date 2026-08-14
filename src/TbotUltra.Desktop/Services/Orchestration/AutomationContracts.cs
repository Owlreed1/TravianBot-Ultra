namespace TbotUltra.Desktop.Services.Orchestration;

public enum AutomationRunMode
{
    ContinuousLoop,
    AutoQueue,
}

public enum AutomationPhase
{
    Stopped,
    Running,
    Stopping,
    Faulted,
}

public enum AutomationStopMode
{
    AfterCurrentAction,
    CancelCurrentAction,
}

public enum AutomationWakeReason
{
    QueueChanged,
    SettingsChanged,
    AutomationEnabled,
    UserRequested,
    VillageStatusRoundRequested,
}

public enum AutomationWakeResult
{
    Accepted,
    Coalesced,
    NotRunning,
    Stopping,
}

public enum AutomationDecisionChoice
{
    Approve,
    Decline,
}

public enum AutomationDecisionResult
{
    Accepted,
    AlreadyAnswered,
    UnknownRequest,
    StaleRun,
    InvalidChoice,
}

public enum AutomationActionOutcome
{
    Completed,
    Deferred,
    Blocked,
    Skipped,
}

public enum AutomationFailureKind
{
    TransientNetwork,
    AccountAccess,
    StaleBrowserGeneration,
    InvalidQueueState,
    AdapterContract,
    Unexpected,
}

public sealed record AutomationFailure(
    AutomationFailureKind Kind,
    string DiagnosticCode,
    bool IsRetryable);

public readonly record struct AutomationRunId(long Value);

public readonly record struct AutomationDecisionRequestId(Guid Value);

public readonly record struct QueueItemIdentity(Guid Value);

public sealed record AutomationPendingDecision(
    AutomationDecisionRequestId RequestId,
    string Code);

public sealed record AutomationRunContext(
    string AccountKey,
    Uri OfficialServerRoot,
    long BrowserGeneration);

public sealed record AutomationStart(
    AutomationRunMode Mode,
    AutomationRunContext Context);

public sealed record AutomationSnapshot(
    AutomationRunId? RunId,
    AutomationRunMode? Mode,
    AutomationRunContext? Context,
    AutomationPhase Phase,
    AutomationPendingDecision? PendingDecision = null)
{
    public static AutomationSnapshot Stopped { get; } = new(null, null, null, AutomationPhase.Stopped);
}

public abstract record AutomationStartResult
{
    private AutomationStartResult()
    {
    }

    public sealed record Started(AutomationRunId RunId) : AutomationStartResult;

    public sealed record AlreadyRunning(AutomationRunId RunId) : AutomationStartResult;

    public sealed record Replaced(AutomationRunId PreviousRunId, AutomationRunId RunId) : AutomationStartResult;

    public sealed record Busy : AutomationStartResult;
}

public abstract record AutomationStopResult
{
    private AutomationStopResult()
    {
    }

    public sealed record Stopped(AutomationRunId RunId) : AutomationStopResult;

    public sealed record AlreadyStopped : AutomationStopResult;
}

public abstract record AutomationEvent(
    AutomationRunId RunId,
    DateTimeOffset OccurredAt)
{
    public sealed record RunStarted(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        AutomationRunMode Mode,
        AutomationRunContext Context)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record RunStopped(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        AutomationRunMode RunMode,
        AutomationStopMode Mode)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record ActionSelected(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        QueueItemIdentity Item,
        string TaskName)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record ActionFinished(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        QueueItemIdentity Item,
        AutomationActionOutcome Outcome)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record DecisionRequested(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        AutomationPendingDecision Decision)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record DecisionAnswered(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        AutomationPendingDecision Decision,
        AutomationDecisionChoice Choice)
        : AutomationEvent(RunId, OccurredAt);

    public sealed record RunFaulted(
        AutomationRunId RunId,
        DateTimeOffset OccurredAt,
        AutomationRunMode RunMode,
        AutomationFailure Failure)
        : AutomationEvent(RunId, OccurredAt);
}

public sealed record AutomationUpdate(
    long Sequence,
    AutomationEvent Event,
    AutomationSnapshot Snapshot);

public delegate void AutomationUpdatedEventHandler(object? sender, AutomationUpdate update);

public interface IAutomationDesk
{
    AutomationSnapshot Current { get; }

    event AutomationUpdatedEventHandler? Updated;

    ValueTask<AutomationStartResult> StartAsync(
        AutomationStart request,
        CancellationToken cancellationToken = default);

    ValueTask<AutomationStopResult> StopAsync(
        AutomationStopMode mode = AutomationStopMode.AfterCurrentAction,
        CancellationToken cancellationToken = default);

    AutomationWakeResult Wake(AutomationWakeReason reason);

    AutomationDecisionResult Respond(
        AutomationDecisionRequestId requestId,
        AutomationDecisionChoice choice);
}

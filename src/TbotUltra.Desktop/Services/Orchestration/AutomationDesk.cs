namespace TbotUltra.Desktop.Services.Orchestration;

/// <summary>
/// Owns the authoritative Desktop automation run state. Automation policy is
/// migrated behind this interface in behavior-preserving vertical slices.
/// </summary>
public sealed class AutomationDesk : IAutomationDesk, IAsyncDisposable
{
    private readonly LoopController _loopController;
    private readonly IAutomationStatePort _state;
    private readonly IOfficialTravianAutomationPort _officialTravian;
    private readonly TimeProvider _timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private long _nextRunId;
    private long _nextUpdateSequence;
    private int _wakePending;
    private Task? _runTask;
    private LoopController.GateLease? _autoQueueGateLease;
    private TaskCompletionSource<AutomationDecisionChoice?>? _pendingDecisionCompletion;
    private AutomationDecisionRequestId? _lastAnsweredDecisionId;
    private AutomationSnapshot _current = AutomationSnapshot.Stopped;

    public AutomationDesk(LoopController loopController, TimeProvider? timeProvider = null)
        : this(
            loopController,
            EmptyAutomationStatePort.Instance,
            EmptyOfficialTravianAutomationPort.Instance,
            timeProvider)
    {
    }

    internal AutomationDesk(
        LoopController loopController,
        IAutomationStatePort state,
        IOfficialTravianAutomationPort officialTravian,
        TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _loopController = loopController;
        _state = state;
        _officialTravian = officialTravian;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _delayAsync = delayAsync ?? ((delay, cancellationToken) =>
            Task.Delay(delay, _timeProvider, cancellationToken));
        _loopController.AutomationStopRequested += OnAutomationStopRequested;
    }

    public AutomationSnapshot Current
    {
        get => Volatile.Read(ref _current);
        private set => Volatile.Write(ref _current, value);
    }

    public event AutomationUpdatedEventHandler? Updated;

    public async ValueTask<AutomationStartResult> StartAsync(
        AutomationStart request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Context.AccountKey);
        ArgumentNullException.ThrowIfNull(request.Context.OfficialServerRoot);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AutomationRunId? previousRunId;
            lock (_sync)
            {
                if (Current.Phase == AutomationPhase.Running
                    && Current.Mode == request.Mode
                    && Current.Context == request.Context)
                {
                    return new AutomationStartResult.AlreadyRunning(Current.RunId!.Value);
                }

                previousRunId = Current.RunId;
            }

            if (previousRunId is not null)
            {
                await StopCurrentRunAsync(
                        AutomationStopMode.CancelCurrentAction,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            LoopController.GateLease? acquiredGate = null;
            if (request.Mode == AutomationRunMode.AutoQueue)
            {
                acquiredGate = await _loopController
                    .TryAcquireQueueAutoRunGateAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (acquiredGate is null)
                {
                    _loopController.Logger.Invoke("[automation] Auto Queue start rejected: gate is busy.");
                    return new AutomationStartResult.Busy();
                }
            }

            AutomationUpdate update;
            AutomationRunId runId;
            TaskCompletionSource runStarted;
            lock (_sync)
            {
                var runToken = StartLifecycle(request.Mode);
                _autoQueueGateLease = acquiredGate;
                Interlocked.Exchange(ref _wakePending, 0);
                runId = new AutomationRunId(Interlocked.Increment(ref _nextRunId));
                Current = new AutomationSnapshot(runId, request.Mode, request.Context, AutomationPhase.Running);
                var automationEvent = new AutomationEvent.RunStarted(
                    runId,
                    _timeProvider.GetUtcNow(),
                    request.Mode,
                    request.Context);
                update = new AutomationUpdate(
                    Interlocked.Increment(ref _nextUpdateSequence),
                    automationEvent,
                    Current);
                _loopController.Logger.Invoke(
                    $"[automation] Run {runId.Value} started: mode={request.Mode}, account={request.Context.AccountKey}.");
                while (_wakeSignal.Wait(0, CancellationToken.None))
                {
                }
                runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _runTask = Task.Run(
                    async () =>
                    {
                        await runStarted.Task.ConfigureAwait(false);
                        await RunAutomationAsync(runId, request.Mode, request.Context, runToken)
                            .ConfigureAwait(false);
                    },
                    runToken);
            }

            PublishUpdate(update);
            runStarted.TrySetResult();
            return previousRunId is AutomationRunId previous
                ? new AutomationStartResult.Replaced(previous, runId)
                : new AutomationStartResult.Started(runId);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<AutomationStopResult> StopAsync(
        AutomationStopMode mode = AutomationStopMode.AfterCurrentAction,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode == AutomationStopMode.CancelCurrentAction)
        {
            lock (_sync)
            {
                if (Current.Phase == AutomationPhase.Stopping)
                {
                    CancelCurrentRun(cancelCurrentAction: true);
                    SignalWake();
                    _loopController.Logger.Invoke(
                        "[automation] Graceful stop escalated to current-action cancellation.");
                }
            }
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await StopCurrentRunAsync(mode, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async ValueTask<AutomationStopResult> StopCurrentRunAsync(
        AutomationStopMode mode,
        CancellationToken cancellationToken)
    {
        AutomationRunId runId;
        Task? runTask;
        lock (_sync)
        {
            if (Current.RunId is not AutomationRunId activeRunId)
            {
                return new AutomationStopResult.AlreadyStopped();
            }

            runId = activeRunId;
            Current = Current with { Phase = AutomationPhase.Stopping };
            _pendingDecisionCompletion?.TrySetResult(null);
            _pendingDecisionCompletion = null;
            CancelCurrentRun(mode == AutomationStopMode.CancelCurrentAction);
            SignalWake();
            Interlocked.Exchange(ref _wakePending, 0);
            runTask = _runTask;
        }

        if (runTask is not null)
        {
            try
            {
                await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // CancelCurrentAction intentionally cancels the owned run token.
            }
        }

        AutomationUpdate? update = null;
        lock (_sync)
        {
            if (Current.RunId != runId)
            {
                return new AutomationStopResult.Stopped(runId);
            }

            var stoppedMode = Current.Mode;
            Current = AutomationSnapshot.Stopped;
            var automationEvent = new AutomationEvent.RunStopped(
                runId,
                _timeProvider.GetUtcNow(),
                stoppedMode!.Value,
                mode);
            update = new AutomationUpdate(
                Interlocked.Increment(ref _nextUpdateSequence),
                automationEvent,
                Current);
            _runTask = null;
            if (stoppedMode == AutomationRunMode.ContinuousLoop)
            {
                _loopController.DisposeLoop();
            }
            ReleaseAutoQueueGate();
            _loopController.Logger.Invoke($"[automation] Run {runId.Value} stopped: mode={mode}.");
        }

        PublishUpdate(update);
        return new AutomationStopResult.Stopped(runId);
    }

    public AutomationWakeResult Wake(AutomationWakeReason reason)
    {
        lock (_sync)
        {
            if (Current.Phase == AutomationPhase.Stopping)
            {
                return AutomationWakeResult.Stopping;
            }

            if (Current.Phase != AutomationPhase.Running)
            {
                return AutomationWakeResult.NotRunning;
            }

            if (Interlocked.Exchange(ref _wakePending, 1) == 1)
            {
                return AutomationWakeResult.Coalesced;
            }

            _loopController.Logger.Invoke($"[automation] Wake requested: reason={reason}.");
            SignalWake();
            return AutomationWakeResult.Accepted;
        }
    }

    public AutomationDecisionResult Respond(
        AutomationDecisionRequestId requestId,
        AutomationDecisionChoice choice)
    {
        if (!Enum.IsDefined(choice))
        {
            return AutomationDecisionResult.InvalidChoice;
        }

        AutomationUpdate update;
        TaskCompletionSource<AutomationDecisionChoice?> completion;
        lock (_sync)
        {
            var pending = Current.PendingDecision;
            if (pending is null || _pendingDecisionCompletion is null)
            {
                return _lastAnsweredDecisionId == requestId
                    ? AutomationDecisionResult.AlreadyAnswered
                    : AutomationDecisionResult.UnknownRequest;
            }

            if (pending.RequestId != requestId)
            {
                return AutomationDecisionResult.UnknownRequest;
            }

            completion = _pendingDecisionCompletion;
            _pendingDecisionCompletion = null;
            _lastAnsweredDecisionId = requestId;
            Current = Current with { PendingDecision = null };
            update = new AutomationUpdate(
                Interlocked.Increment(ref _nextUpdateSequence),
                new AutomationEvent.DecisionAnswered(
                    Current.RunId!.Value,
                    _timeProvider.GetUtcNow(),
                    pending,
                    choice),
                Current);
        }

        PublishUpdate(update);
        completion.TrySetResult(choice);
        return AutomationDecisionResult.Accepted;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(AutomationStopMode.CancelCurrentAction).ConfigureAwait(false);
        _loopController.AutomationStopRequested -= OnAutomationStopRequested;
    }

    private void PublishUpdate(AutomationUpdate update)
    {
        var subscribers = Updated;
        if (subscribers is null)
        {
            return;
        }

        foreach (AutomationUpdatedEventHandler subscriber in subscribers.GetInvocationList())
        {
            try
            {
                subscriber(this, update);
            }
            catch (Exception ex)
            {
                _loopController.Logger.Invoke(
                    $"[automation] Update subscriber failed at sequence {update.Sequence}: {ex.Message}");
            }
        }
    }

    private async Task RunAutomationAsync(
        AutomationRunId runId,
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            while (IsCurrentRun(runId, context) && !ShouldStop())
            {
                var pass = await RunAutomationPassAsync(runId, mode, context, cancellationToken)
                    .ConfigureAwait(false);
                if (!IsCurrentRun(runId, context) || ShouldStop())
                {
                    return;
                }

                if (pass.ExecutedAction)
                {
                    continue;
                }

                if (pass.CompletedRun)
                {
                    CompleteRun(runId, mode);
                    return;
                }

                await WaitForWakeOrDeadlineAsync(pass.NextDeadline, cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref _wakePending, 0);
            }
        }
        finally
        {
            if (ShouldStop())
            {
                CompleteRun(runId, mode);
            }
        }
    }

    private async Task<AutomationPassResult> RunAutomationPassAsync(
        AutomationRunId runId,
        AutomationRunMode mode,
        AutomationRunContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _state.ReadAsync(mode, context, cancellationToken).ConfigureAwait(false);
            var now = _timeProvider.GetUtcNow();
            var action = snapshot.Candidates
                .Where(candidate => candidate.NextAttemptAt <= now)
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.NextAttemptAt)
                .FirstOrDefault();
            if (action is null || !IsCurrentRun(runId, context))
            {
                if (snapshot.IsComplete)
                {
                    return AutomationPassResult.Complete;
                }

                var candidateDeadline = snapshot.Candidates
                    .Where(candidate => candidate.NextAttemptAt > now)
                    .Select(candidate => (DateTimeOffset?)candidate.NextAttemptAt)
                    .Min();
                var nextDeadline = snapshot.NextWakeAt is DateTimeOffset explicitWake
                    && (candidateDeadline is null || explicitWake < candidateDeadline.Value)
                        ? explicitWake
                        : candidateDeadline;
                return new AutomationPassResult(false, nextDeadline);
            }

            PublishRunEvent(new AutomationEvent.ActionSelected(
                runId,
                _timeProvider.GetUtcNow(),
                new QueueItemIdentity(action.Id),
                action.TaskName));
            if (action.Decision is AutomationDecisionRequirement requirement)
            {
                var choice = await RequestDecisionAsync(runId, requirement, cancellationToken)
                    .ConfigureAwait(false);
                if (choice is null || !IsCurrentRun(runId, context))
                {
                    return AutomationPassResult.NoWork;
                }

                if (choice == AutomationDecisionChoice.Decline)
                {
                    await _state.ApplyAsync(
                            mode,
                            context,
                            new AutomationStateChange.ActionFinished(
                                action.Id,
                                AutomationActionOutcome.Skipped),
                            cancellationToken)
                        .ConfigureAwait(false);
                    PublishRunEvent(new AutomationEvent.ActionFinished(
                        runId,
                        _timeProvider.GetUtcNow(),
                        new QueueItemIdentity(action.Id),
                        AutomationActionOutcome.Skipped));
                    return new AutomationPassResult(true, null);
                }
            }

            var outcome = await _officialTravian
                .ExecuteAsync(mode, context, action, cancellationToken)
                .ConfigureAwait(false);
            if (!IsCurrentRun(runId, context))
            {
                return AutomationPassResult.NoWork;
            }

            await _state.ApplyAsync(
                    mode,
                    context,
                    new AutomationStateChange.ActionFinished(action.Id, outcome),
                    cancellationToken)
                .ConfigureAwait(false);
            PublishRunEvent(new AutomationEvent.ActionFinished(
                runId,
                _timeProvider.GetUtcNow(),
                new QueueItemIdentity(action.Id),
                outcome));
            return outcome == AutomationActionOutcome.Blocked
                ? AutomationPassResult.Complete
                : new AutomationPassResult(true, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is expected lifecycle control.
            return AutomationPassResult.NoWork;
        }
        catch (Exception ex)
        {
            _loopController.Logger.Invoke(
                $"[automation] Run {runId.Value} pass failed: type={ex.GetType().Name}.");
            AutomationUpdate? faultUpdate = null;
            lock (_sync)
            {
                if (Current.RunId == runId)
                {
                    Current = Current with { Phase = AutomationPhase.Faulted };
                    var failure = new AutomationFailure(
                        AutomationFailureKind.AdapterContract,
                        ex.GetType().Name,
                        IsRetryable: false);
                    faultUpdate = new AutomationUpdate(
                        Interlocked.Increment(ref _nextUpdateSequence),
                        new AutomationEvent.RunFaulted(
                            runId,
                            _timeProvider.GetUtcNow(),
                            mode,
                            failure),
                        Current);
                    if (mode == AutomationRunMode.AutoQueue)
                    {
                        _loopController.DisposeAutoQueueRun();
                        ReleaseAutoQueueGate();
                    }
                    else
                    {
                        _loopController.DisposeLoop();
                    }
                }
            }

            if (faultUpdate is not null)
            {
                PublishUpdate(faultUpdate);
            }
            return AutomationPassResult.NoWork;
        }
    }

    private void CompleteRun(AutomationRunId runId, AutomationRunMode mode)
    {
        AutomationUpdate? update = null;
        lock (_sync)
        {
            if (Current.RunId != runId || Current.Phase != AutomationPhase.Running)
            {
                return;
            }

            Current = AutomationSnapshot.Stopped;
            update = new AutomationUpdate(
                Interlocked.Increment(ref _nextUpdateSequence),
                new AutomationEvent.RunStopped(
                    runId,
                    _timeProvider.GetUtcNow(),
                    mode,
                    AutomationStopMode.AfterCurrentAction),
                Current);
            _runTask = null;
            if (mode == AutomationRunMode.AutoQueue)
            {
                _loopController.DisposeAutoQueueRun();
            }
            else
            {
                _loopController.DisposeLoop();
            }
            ReleaseAutoQueueGate();
            _loopController.Logger.Invoke($"[automation] Run {runId.Value} completed.");
        }

        PublishUpdate(update);
    }

    private async Task<AutomationDecisionChoice?> RequestDecisionAsync(
        AutomationRunId runId,
        AutomationDecisionRequirement requirement,
        CancellationToken cancellationToken)
    {
        AutomationUpdate update;
        TaskCompletionSource<AutomationDecisionChoice?> completion;
        lock (_sync)
        {
            if (Current.RunId != runId || Current.Phase != AutomationPhase.Running)
            {
                return null;
            }

            var decision = new AutomationPendingDecision(
                new AutomationDecisionRequestId(Guid.NewGuid()),
                requirement.Code);
            completion = new TaskCompletionSource<AutomationDecisionChoice?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingDecisionCompletion = completion;
            Current = Current with { PendingDecision = decision };
            update = new AutomationUpdate(
                Interlocked.Increment(ref _nextUpdateSequence),
                new AutomationEvent.DecisionRequested(
                    runId,
                    _timeProvider.GetUtcNow(),
                    decision),
                Current);
        }

        PublishUpdate(update);
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForWakeOrDeadlineAsync(
        DateTimeOffset? nextDeadline,
        CancellationToken cancellationToken)
    {
        if (nextDeadline is null)
        {
            await _wakeSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var remaining = nextDeadline.Value - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var wakeTask = _wakeSignal.WaitAsync(waitCancellation.Token);
        var deadlineTask = _delayAsync(remaining, waitCancellation.Token);
        await Task.WhenAny(wakeTask, deadlineTask).ConfigureAwait(false);
        await waitCancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(wakeTask, deadlineTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The losing wait is canceled after the first wake source wins.
        }
    }

    private bool ShouldStop() =>
        Current.Mode == AutomationRunMode.ContinuousLoop
            ? _loopController.LoopStopRequested
            : _loopController.QueueStopRequested;

    private void SignalWake()
    {
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A pending signal already represents every coalesced wake request.
        }
    }

    private void OnAutomationStopRequested() => SignalWake();

    private bool IsCurrentRun(AutomationRunId runId, AutomationRunContext context)
    {
        lock (_sync)
        {
            return Current.RunId == runId
                && Current.Context == context
                && Current.Phase == AutomationPhase.Running;
        }
    }

    private void PublishRunEvent(AutomationEvent automationEvent)
    {
        AutomationUpdate? update;
        lock (_sync)
        {
            if (Current.RunId != automationEvent.RunId || Current.Phase != AutomationPhase.Running)
            {
                return;
            }

            update = new AutomationUpdate(
                Interlocked.Increment(ref _nextUpdateSequence),
                automationEvent,
                Current);
        }

        PublishUpdate(update);
    }

    private CancellationToken StartLifecycle(AutomationRunMode mode)
    {
        if (mode == AutomationRunMode.ContinuousLoop)
        {
            _loopController.ClearLoopStopRequest();
            return _loopController.StartLoop("automation-desk");
        }

        _loopController.ClearQueueStopRequest();
        return _loopController.StartAutoQueueRun();
    }

    private void ReleaseAutoQueueGate()
    {
        _autoQueueGateLease?.Dispose();
        _autoQueueGateLease = null;
    }

    private void CancelCurrentRun(bool cancelCurrentAction = true)
    {
        if (Current.Mode == AutomationRunMode.ContinuousLoop)
        {
            _loopController.RequestLoopStop();
            if (cancelCurrentAction)
            {
                _loopController.CancelLoop();
            }
            return;
        }

        if (Current.Mode == AutomationRunMode.AutoQueue)
        {
            _loopController.RequestQueueStop();
            if (cancelCurrentAction)
            {
                _loopController.CancelAutoQueueRun();
            }
            _loopController.DisposeAutoQueueRun();
        }
    }

    private sealed record AutomationPassResult(
        bool ExecutedAction,
        DateTimeOffset? NextDeadline,
        bool CompletedRun = false)
    {
        internal static AutomationPassResult NoWork { get; } = new(false, null);

        internal static AutomationPassResult Complete { get; } = new(false, null, true);
    }
}

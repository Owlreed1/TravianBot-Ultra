using TbotUltra.Desktop.Services;

namespace TbotUltra.Desktop.Services.Orchestration;

internal sealed class AutomationPassRuntime
{
    private readonly VillageBatchState _villageBatch = new();
    private long _nextContinuousPassId;
    private long _currentContinuousPassId;
    private long _autoQueueRunLogId;
    private int _immediateWorkRequested;

    internal long BeginContinuousPass()
    {
        var passId = Interlocked.Increment(ref _nextContinuousPassId);
        Interlocked.Exchange(ref _currentContinuousPassId, passId);
        return passId;
    }

    internal long CurrentContinuousPassId => Volatile.Read(ref _currentContinuousPassId);

    internal long AutoQueueRunLogId => Volatile.Read(ref _autoQueueRunLogId);

    internal void BeginAutoQueueRun(long logId)
    {
        ResetVillageBatch();
        Interlocked.Exchange(ref _autoQueueRunLogId, logId);
    }

    internal void RequestImmediateWork() => Interlocked.Exchange(ref _immediateWorkRequested, 1);

    internal bool ConsumeImmediateWorkRequest() =>
        Interlocked.Exchange(ref _immediateWorkRequested, 0) == 1;

    internal bool IsImmediateWorkRequested => Volatile.Read(ref _immediateWorkRequested) == 1;

    internal VillageBatchSnapshot SnapshotVillageBatch(string? verifiedVillageKey) =>
        _villageBatch.SnapshotFor(verifiedVillageKey);

    internal void ObserveVerifiedVillage(string? villageKey) =>
        _villageBatch.ObserveVerifiedVillage(villageKey);

    internal VillageBatchSnapshot RecordVillageAttempt(
        string? targetVillageKey,
        string? verifiedVillageKey) =>
        _villageBatch.RecordAttempt(targetVillageKey, verifiedVillageKey);

    internal void ResetVillageBatch() => _villageBatch.Reset();

    internal bool CanAttemptVillageTask(int attemptsAlreadyMade) =>
        attemptsAlreadyMade < VillageBatchState.MaxAttempts;

    internal int VillageAttemptLimit => VillageBatchState.MaxAttempts;
}

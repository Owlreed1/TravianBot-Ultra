namespace TbotUltra.Desktop.Services;

internal readonly record struct VillageBatchSnapshot(string? VillageKey, int AttemptCount)
{
    internal bool HasReachedLimit => AttemptCount >= VillageBatchState.MaxAttempts;
}

/// <summary>
/// Runtime-only ownership for draining ready work in one verified browser village.
/// </summary>
internal sealed class VillageBatchState
{
    internal const int MaxAttempts = 10;

    private readonly object _gate = new();
    private string? _villageKey;
    private int _attemptCount;
    private string? _pendingTargetVillageKey;

    internal static bool ShouldKeepCurrentVillage(
        VillageBatchSnapshot snapshot,
        bool currentVillageHasReadyWork,
        bool anotherVillageHasReadyWork) =>
        currentVillageHasReadyWork
        && (!snapshot.HasReachedLimit || !anotherVillageHasReadyWork);

    internal VillageBatchSnapshot SnapshotFor(string? verifiedVillageKey)
    {
        lock (_gate)
        {
            var normalized = Normalize(verifiedVillageKey);
            return KeysEqual(_villageKey, normalized)
                ? new VillageBatchSnapshot(normalized, _attemptCount)
                : new VillageBatchSnapshot(normalized, 0);
        }
    }

    internal void ObserveVerifiedVillage(string? villageKey)
    {
        lock (_gate)
        {
            var normalized = Normalize(villageKey);
            if (normalized is null || KeysEqual(_villageKey, normalized))
            {
                return;
            }

            _villageKey = normalized;
            _attemptCount = KeysEqual(_pendingTargetVillageKey, normalized) ? 1 : 0;
            _pendingTargetVillageKey = null;
        }
    }

    internal VillageBatchSnapshot RecordAttempt(string? targetVillageKey, string? verifiedVillageKey)
    {
        lock (_gate)
        {
            var effectiveVillageKey = Normalize(targetVillageKey) ?? Normalize(verifiedVillageKey);
            if (effectiveVillageKey is null)
            {
                return new VillageBatchSnapshot(_villageKey, _attemptCount);
            }

            var verified = Normalize(verifiedVillageKey);
            if (verified is not null && !KeysEqual(verified, effectiveVillageKey))
            {
                if (!KeysEqual(_villageKey, verified))
                {
                    _villageKey = verified;
                    _attemptCount = 0;
                }

                _pendingTargetVillageKey = effectiveVillageKey;
                _attemptCount++;
                return new VillageBatchSnapshot(_villageKey, _attemptCount);
            }

            if (!KeysEqual(_villageKey, effectiveVillageKey))
            {
                _villageKey = effectiveVillageKey;
                _attemptCount = 0;
            }

            _pendingTargetVillageKey = null;
            _attemptCount++;
            return new VillageBatchSnapshot(_villageKey, _attemptCount);
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _villageKey = null;
            _attemptCount = 0;
            _pendingTargetVillageKey = null;
        }
    }

    private static bool KeysEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

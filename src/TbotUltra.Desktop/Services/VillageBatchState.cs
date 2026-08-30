namespace TbotUltra.Desktop.Services;

internal readonly record struct VillageBatchSnapshot(
    string? VillageKey,
    int AttemptCount,
    string? UrgentTargetVillageKey = null,
    bool HasUrgentPreemption = false);

/// <summary>
/// Runtime-only ownership for draining ready work in one verified browser village.
/// </summary>
internal sealed class VillageBatchState
{
    private readonly object _gate = new();
    private string? _villageKey;
    private int _attemptCount;
    private string? _pendingTargetVillageKey;
    private string? _urgentTargetVillageKey;
    private bool _hasUrgentPreemption;

    internal VillageBatchSnapshot SnapshotFor(string? verifiedVillageKey)
    {
        lock (_gate)
        {
            var normalized = Normalize(verifiedVillageKey);
            if (_hasUrgentPreemption && _villageKey is not null)
            {
                return new VillageBatchSnapshot(
                    _villageKey,
                    _attemptCount,
                    _urgentTargetVillageKey,
                    HasUrgentPreemption: true);
            }

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
            if (normalized is null)
            {
                return;
            }

            if (_hasUrgentPreemption)
            {
                if (!KeysEqual(_villageKey, normalized)
                    || !KeysEqual(_pendingTargetVillageKey, normalized))
                {
                    return;
                }
            }

            if (KeysEqual(_villageKey, normalized))
            {
                _pendingTargetVillageKey = null;
                _urgentTargetVillageKey = null;
                _hasUrgentPreemption = false;
                return;
            }

            _villageKey = normalized;
            _attemptCount = KeysEqual(_pendingTargetVillageKey, normalized) ? 1 : 0;
            _pendingTargetVillageKey = null;
            _urgentTargetVillageKey = null;
            _hasUrgentPreemption = false;
        }
    }

    internal void RecordUrgentPreemption(string? currentVillageKey, string? targetVillageKey)
    {
        lock (_gate)
        {
            var current = Normalize(currentVillageKey);
            var target = Normalize(targetVillageKey);
            if (current is null || KeysEqual(current, target))
            {
                return;
            }

            _villageKey ??= current;
            _urgentTargetVillageKey = target;
            _hasUrgentPreemption = true;
            _pendingTargetVillageKey = null;
        }
    }

    internal void CompleteUrgentPreemption(string? verifiedVillageKey)
    {
        lock (_gate)
        {
            _urgentTargetVillageKey = null;
            _hasUrgentPreemption = false;
            _pendingTargetVillageKey = null;
            var verified = Normalize(verifiedVillageKey);
            if (verified is not null && !KeysEqual(_villageKey, verified))
            {
                _villageKey = verified;
                _attemptCount = 0;
            }
        }
    }

    internal VillageBatchSnapshot RecordAttempt(string? targetVillageKey, string? verifiedVillageKey)
    {
        lock (_gate)
        {
            var target = Normalize(targetVillageKey);
            var effectiveVillageKey = target ?? Normalize(verifiedVillageKey);
            if (effectiveVillageKey is null)
            {
                return new VillageBatchSnapshot(_villageKey, _attemptCount);
            }

            var verified = Normalize(verifiedVillageKey);
            if (_hasUrgentPreemption
                && (target is null || !KeysEqual(effectiveVillageKey, _villageKey)))
            {
                return new VillageBatchSnapshot(
                    _villageKey,
                    _attemptCount,
                    _urgentTargetVillageKey,
                    HasUrgentPreemption: true);
            }

            if (verified is not null && !KeysEqual(verified, effectiveVillageKey))
            {
                if (_hasUrgentPreemption
                    && KeysEqual(_villageKey, effectiveVillageKey))
                {
                    _pendingTargetVillageKey = effectiveVillageKey;
                    _attemptCount++;
                    return new VillageBatchSnapshot(
                        _villageKey,
                        _attemptCount,
                        _urgentTargetVillageKey,
                        HasUrgentPreemption: true);
                }

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
            if (_hasUrgentPreemption && KeysEqual(_villageKey, effectiveVillageKey))
            {
                _urgentTargetVillageKey = null;
                _hasUrgentPreemption = false;
            }
            _attemptCount++;
            return new VillageBatchSnapshot(
                _villageKey,
                _attemptCount,
                _urgentTargetVillageKey,
                _hasUrgentPreemption);
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            _villageKey = null;
            _attemptCount = 0;
            _pendingTargetVillageKey = null;
            _urgentTargetVillageKey = null;
            _hasUrgentPreemption = false;
        }
    }

    private static bool KeysEqual(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

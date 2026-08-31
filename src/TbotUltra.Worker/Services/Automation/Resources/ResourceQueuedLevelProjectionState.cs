using System.Globalization;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal sealed class ResourceQueuedLevelProjectionState
{
    private readonly Dictionary<int, Entry> _entries;

    private ResourceQueuedLevelProjectionState(Dictionary<int, Entry> entries)
    {
        _entries = entries;
    }

    internal IReadOnlyDictionary<int, int> Levels => _entries.ToDictionary(pair => pair.Key, pair => pair.Value.Level);

    internal int Count => _entries.Count;

    internal static ResourceQueuedLevelProjectionState Parse(string? raw)
    {
        var entries = new Dictionary<int, Entry>();
        if (string.IsNullOrWhiteSpace(raw)
            || raw.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceQueuedLevelProjectionState(entries);
        }

        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var slotId)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var level)
                || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var reviewAtUnixSeconds)
                || slotId is < 1 or > 18
                || level is < 1 or > 40)
            {
                continue;
            }

            DateTimeOffset reviewAtUtc;
            try
            {
                reviewAtUtc = DateTimeOffset.FromUnixTimeSeconds(reviewAtUnixSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (!entries.TryGetValue(slotId, out var existing)
                || level > existing.Level
                || (level == existing.Level && reviewAtUtc > existing.ReviewAtUtc))
            {
                entries[slotId] = new Entry(level, reviewAtUtc);
            }
        }

        return new ResourceQueuedLevelProjectionState(entries);
    }

    internal void Record(int slotId, int level, DateTimeOffset reviewAtUtc)
    {
        if (slotId is < 1 or > 18 || level is < 1 or > 40)
        {
            return;
        }

        reviewAtUtc = reviewAtUtc.ToUniversalTime();
        if (_entries.TryGetValue(slotId, out var existing))
        {
            _entries[slotId] = new Entry(
                Math.Max(existing.Level, level),
                existing.ReviewAtUtc > reviewAtUtc ? existing.ReviewAtUtc : reviewAtUtc);
            return;
        }

        _entries[slotId] = new Entry(level, reviewAtUtc);
    }

    internal IReadOnlyList<string> Reconcile(
        IReadOnlyList<ResourceField> liveFields,
        bool resourceQueueObservedEmpty,
        DateTimeOffset nowUtc,
        DateTimeOffset? nextQueueReviewAtUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        var liveLevels = liveFields
            .Where(field => field.SlotId is int && field.Level is int)
            .GroupBy(field => field.SlotId!.Value)
            .ToDictionary(group => group.Key, group => group.Max(field => field.Level!.Value));
        var changes = new List<string>();

        foreach (var pair in _entries.ToArray())
        {
            var slotId = pair.Key;
            var entry = pair.Value;
            if (liveLevels.TryGetValue(slotId, out var liveLevel) && liveLevel >= entry.Level)
            {
                _entries.Remove(slotId);
                changes.Add($"slot={slotId} completed live={liveLevel} projected={entry.Level}");
                continue;
            }

            if (entry.ReviewAtUtc > nowUtc)
            {
                continue;
            }

            if (resourceQueueObservedEmpty)
            {
                _entries.Remove(slotId);
                changes.Add($"slot={slotId} cleared after expired projection and confirmed empty queue");
                continue;
            }

            var extendedReviewAt = nextQueueReviewAtUtc?.ToUniversalTime() ?? nowUtc.AddMinutes(10);
            if (extendedReviewAt <= nowUtc)
            {
                extendedReviewAt = nowUtc.AddMinutes(10);
            }
            _entries[slotId] = entry with { ReviewAtUtc = extendedReviewAt };
            changes.Add($"slot={slotId} retained while queue remains active; reviewAt={extendedReviewAt:O}");
        }

        return changes;
    }

    internal string Serialize()
    {
        if (_entries.Count == 0)
        {
            return "none";
        }

        return string.Join(",", _entries
            .OrderBy(pair => pair.Key)
            .Select(pair => string.Create(
                CultureInfo.InvariantCulture,
                $"{pair.Key}:{pair.Value.Level}:{pair.Value.ReviewAtUtc.ToUnixTimeSeconds()}")));
    }

    private sealed record Entry(int Level, DateTimeOffset ReviewAtUtc);
}

using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>
/// Stateless resource snapshot calculations. DOM reads and navigation remain in
/// <see cref="TravianClient"/>.
/// </summary>
internal static class ResourceSnapshotCalculator
{
    private static readonly string[] ResourceKeys = ["wood", "clay", "iron", "crop"];

    internal static IReadOnlyDictionary<string, double?> MergeProductionByHour(
        IReadOnlyDictionary<string, double?> live,
        IReadOnlyDictionary<string, double?>? cached)
    {
        var merged = ResourceKeys.ToDictionary(
            key => key,
            _ => (double?)null,
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in ResourceKeys)
        {
            live.TryGetValue(key, out var liveValue);
            if (liveValue is not null)
            {
                merged[key] = liveValue;
                continue;
            }

            if (cached is not null && cached.TryGetValue(key, out var cachedValue))
            {
                merged[key] = cachedValue;
            }
        }

        return merged;
    }

    internal static IReadOnlyList<ResourceField> OrderUpgradeCandidates(
        IEnumerable<ResourceField> fields,
        IReadOnlyDictionary<string, long>? stockByType,
        IReadOnlyDictionary<int, int>? queuedLevelsBySlot = null)
    {
        var actionable = fields
            .Where(field => field.SlotId is not null && field.Level is not null)
            .Select(field => new
            {
                Field = field,
                ProjectedLevel = queuedLevelsBySlot is not null
                    && queuedLevelsBySlot.TryGetValue(field.SlotId!.Value, out var queuedLevel)
                        ? Math.Max(field.Level!.Value, queuedLevel)
                        : field.Level!.Value,
            });
        return stockByType is null
            ? actionable
                .OrderBy(item => item.ProjectedLevel)
                .ThenBy(item => item.Field.SlotId ?? 999)
                .Select(item => item.Field)
                .ToList()
            : actionable
                .OrderBy(item => stockByType.TryGetValue(item.Field.FieldType, out var stock) ? stock : long.MaxValue)
                .ThenBy(item => item.ProjectedLevel)
                .ThenBy(item => item.Field.SlotId ?? 999)
                .Select(item => item.Field)
                .ToList();
    }

    internal static IReadOnlyDictionary<int, int> MergeQueuedLevelProjections(
        IReadOnlyDictionary<int, int> liveQueuedLevels,
        IReadOnlyDictionary<int, int> confirmedDuringOperation)
    {
        var merged = new Dictionary<int, int>(liveQueuedLevels);
        foreach (var (slotId, confirmedLevel) in confirmedDuringOperation)
        {
            merged[slotId] = merged.TryGetValue(slotId, out var liveLevel)
                ? Math.Max(liveLevel, confirmedLevel)
                : confirmedLevel;
        }

        return merged;
    }

    internal static IReadOnlyDictionary<int, int> BuildDorf1QueuedLevelProjections(
        IEnumerable<ResourceField> fields)
    {
        return fields
            .Where(field => field.SlotId is int && field.QueuedLevel is int)
            .GroupBy(field => field.SlotId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Max(field => field.QueuedLevel!.Value));
    }

    /// <summary>
    /// Identifies resource fields that have the same Official upgrade offer in one bulk-upgrade pass.
    /// </summary>
    internal static string BuildUpgradeOfferIdentity(string? fieldType, int level)
    {
        var normalizedFieldType = string.IsNullOrWhiteSpace(fieldType)
            ? "unknown"
            : fieldType.Trim().ToLowerInvariant();
        return $"{normalizedFieldType}|level:{level}";
    }

    internal static IReadOnlyList<ResourceStorageForecast> BuildStorageForecasts(
        IReadOnlyDictionary<string, string> resources,
        long? warehouseCapacity,
        long? granaryCapacity,
        IReadOnlyDictionary<string, double?> productionByHour)
    {
        var result = new List<ResourceStorageForecast>(ResourceKeys.Length);
        foreach (var key in ResourceKeys)
        {
            resources.TryGetValue(key, out var rawCurrent);
            var current = TravianParsing.TryParseResourceValue(rawCurrent);
            var capacity = string.Equals(key, "crop", StringComparison.OrdinalIgnoreCase)
                ? granaryCapacity
                : warehouseCapacity;

            productionByHour.TryGetValue(key, out var production);
            double? percent = null;
            if (capacity is > 0 && current is not null)
            {
                percent = Math.Clamp((double)current.Value / capacity.Value * 100.0, 0.0, 100.0);
            }

            int? secondsToFull = null;
            if (capacity is > 0 && current is not null && production is > 0)
            {
                var remaining = Math.Max(0L, capacity.Value - current.Value);
                var computedSeconds = Math.Ceiling((remaining / production.Value) * 3600.0);
                secondsToFull = computedSeconds >= int.MaxValue
                    ? int.MaxValue
                    : (int)computedSeconds;
            }

            int? secondsToEmpty = null;
            if (current is not null && production is < 0)
            {
                var computedSeconds = Math.Ceiling((current.Value / -production.Value) * 3600.0);
                secondsToEmpty = computedSeconds >= int.MaxValue
                    ? int.MaxValue
                    : Math.Max(0, (int)computedSeconds);
            }

            result.Add(new ResourceStorageForecast(
                ResourceKey: key,
                Current: current,
                Capacity: capacity,
                PercentOfCapacity: percent,
                ProductionPerHour: production,
                SecondsToFull: secondsToFull,
                SecondsToEmpty: secondsToEmpty));
        }

        return result;
    }

    internal static int ComputeUpgradeWaitSeconds(int? detectedSeconds)
    {
        var seconds = Math.Max(0, detectedSeconds ?? 0);
        return seconds == 0 ? 0 : Math.Min(seconds + 1, 12 * 60 * 60);
    }

    internal static ResourceUpgradeAffordability EvaluateUpgradeAffordability(
        long wood,
        long clay,
        long iron,
        long crop,
        IReadOnlyDictionary<string, string> resources,
        IReadOnlyDictionary<string, double?> productionByHour)
    {
        var requiredByResource = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["wood"] = wood,
            ["clay"] = clay,
            ["iron"] = iron,
            ["crop"] = crop,
        };

        long longest = 0;
        var hasUnknownWait = false;
        foreach (var (key, required) in requiredByResource)
        {
            resources.TryGetValue(key, out var currentRaw);
            var current = TravianParsing.TryParseResourceValue(currentRaw) ?? 0;
            var missing = Math.Max(0, required - current);
            if (missing <= 0)
            {
                continue;
            }

            productionByHour.TryGetValue(key, out var production);
            if (production is > 0)
            {
                var wait = (long)Math.Ceiling((missing / production.Value) * 3600d);
                longest = Math.Max(longest, Math.Max(1L, wait));
            }
            else
            {
                hasUnknownWait = true;
            }
        }

        return new ResourceUpgradeAffordability(
            hasUnknownWait ? long.MaxValue : longest,
            hasUnknownWait,
            wood + clay + iron + crop);
    }

    internal static ResourceBulkUpgradePlan BuildBulkUpgradePlan(
        IReadOnlyList<ResourceField> orderedCandidates,
        int targetLevel,
        int fallbackMax,
        IReadOnlyDictionary<int, int> queuedLevelsBySlot,
        IReadOnlyDictionary<string, string> resources,
        IReadOnlyDictionary<string, double?> productionByHour,
        long? warehouseCapacity,
        long? granaryCapacity)
    {
        var hasCompleteStock = ResourceKeys.All(key =>
            resources.TryGetValue(key, out var raw)
            && TravianParsing.TryParseResourceValue(raw) is not null);
        if (!hasCompleteStock || warehouseCapacity is not > 0 || granaryCapacity is not > 0)
        {
            return ResourceBulkUpgradePlan.Incomplete;
        }

        var effectiveTarget = Math.Min(targetLevel, fallbackMax);
        var blocked = new List<ResourceBulkUpgradeCandidate>();
        ResourceBulkUpgradeCandidate? candidateToInspect = null;
        var anyQueuedTowardTarget = false;
        foreach (var field in orderedCandidates)
        {
            if (field.SlotId is not int slotId || field.Level is not int currentLevel)
            {
                continue;
            }

            var projectedLevel = queuedLevelsBySlot.TryGetValue(slotId, out var queuedLevel)
                ? Math.Max(currentLevel, queuedLevel)
                : currentLevel;
            if (projectedLevel >= effectiveTarget)
            {
                anyQueuedTowardTarget |= currentLevel < effectiveTarget;
                continue;
            }

            var gid = ResourceFieldGid(field.FieldType);
            var offerLevel = projectedLevel + 1;
            var cost = gid is int resolvedGid
                ? BuildingCatalogService.CostFor(resolvedGid, offerLevel)
                : null;
            if (cost is null)
            {
                return ResourceBulkUpgradePlan.Incomplete;
            }

            var affordability = EvaluateUpgradeAffordability(
                cost.Wood,
                cost.Clay,
                cost.Iron,
                cost.Crop,
                resources,
                productionByHour);
            var candidate = new ResourceBulkUpgradeCandidate(
                field,
                projectedLevel,
                offerLevel,
                cost,
                affordability);
            var exceedsKnownCapacity = cost.Wood > warehouseCapacity.Value
                || cost.Clay > warehouseCapacity.Value
                || cost.Iron > warehouseCapacity.Value
                || cost.Crop > granaryCapacity.Value;
            if (affordability.TimeUntilAffordableSeconds == 0 || exceedsKnownCapacity)
            {
                candidateToInspect ??= candidate;
                continue;
            }

            blocked.Add(candidate);
        }

        var earliestBlocked = blocked
            .OrderBy(candidate => candidate.Affordability.TimeUntilAffordableSeconds)
            .FirstOrDefault();
        return new ResourceBulkUpgradePlan(
            IsComplete: true,
            CandidateToInspect: candidateToInspect,
            RecoveryCandidate: candidateToInspect is null ? blocked.FirstOrDefault() : null,
            EarliestBlockedCandidate: earliestBlocked,
            BlockedByResources: blocked,
            AnyQueuedTowardTarget: anyQueuedTowardTarget);
    }

    internal static int? ResourceFieldGid(string? fieldType) => fieldType?.Trim().ToLowerInvariant() switch
    {
        "wood" => 1,
        "clay" => 2,
        "iron" => 3,
        "crop" => 4,
        _ => null,
    };
}

internal sealed record ResourceUpgradeAffordability(
    long TimeUntilAffordableSeconds,
    bool HasUnknownWait,
    long TotalCost);

internal sealed record ResourceBulkUpgradeCandidate(
    ResourceField Field,
    int ProjectedLevel,
    int OfferLevel,
    BuildingLevelStats Cost,
    ResourceUpgradeAffordability Affordability);

internal sealed record ResourceBulkUpgradePlan(
    bool IsComplete,
    ResourceBulkUpgradeCandidate? CandidateToInspect,
    ResourceBulkUpgradeCandidate? RecoveryCandidate,
    ResourceBulkUpgradeCandidate? EarliestBlockedCandidate,
    IReadOnlyList<ResourceBulkUpgradeCandidate> BlockedByResources,
    bool AnyQueuedTowardTarget)
{
    internal static ResourceBulkUpgradePlan Incomplete { get; } = new(
        false,
        null,
        null,
        null,
        [],
        false);
}

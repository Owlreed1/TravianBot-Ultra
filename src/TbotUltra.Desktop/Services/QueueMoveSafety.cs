using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

internal enum QueueMoveTarget
{
    Up,
    Down,
    Top,
    Bottom,
}

internal sealed record QueueMovePreview(
    bool CanMove,
    IReadOnlyList<Guid> OrderedScopeIds,
    IReadOnlyList<string> Warnings,
    string? FailureReason = null);

internal static class QueueMoveSafety
{
    internal static QueueMovePreview Preview(
        IReadOnlyList<QueueItem> displayOrderedItems,
        Guid selectedId,
        QueueMoveTarget target)
        => Preview(
            displayOrderedItems,
            displayOrderedItems.Where(IsActive).Select(item => item.Id).ToList(),
            [selectedId],
            target);

    internal static QueueMovePreview Preview(
        IReadOnlyList<QueueItem> displayOrderedItems,
        IReadOnlyList<Guid> visibleIds,
        IReadOnlyCollection<Guid> selectedIds,
        QueueMoveTarget target)
    {
        ArgumentNullException.ThrowIfNull(displayOrderedItems);
        ArgumentNullException.ThrowIfNull(visibleIds);
        ArgumentNullException.ThrowIfNull(selectedIds);

        var selectedSet = selectedIds.Where(id => id != Guid.Empty).ToHashSet();
        if (selectedSet.Count == 0)
        {
            return CannotMove("Select at least one queue item.");
        }

        var selectedItems = displayOrderedItems
            .Where(item => selectedSet.Contains(item.Id) && IsActive(item))
            .ToList();
        if (selectedItems.Count != selectedSet.Count)
        {
            return CannotMove("One or more selected queue items are no longer active.");
        }

        var selected = selectedItems[0];
        if (selectedItems.Any(item => item.Group != selected.Group || item.Priority != selected.Priority))
        {
            return CannotMove("All selected queue items must have the same group and priority.");
        }

        var scope = displayOrderedItems
            .Where(item => IsActive(item)
                && item.Group == selected.Group
                && item.Priority == selected.Priority)
            .ToList();
        var scopeIds = scope.Select(item => item.Id).ToList();
        var scopeSet = scopeIds.ToHashSet();
        var visibleScopeIds = visibleIds
            .Where(scopeSet.Contains)
            .Distinct()
            .ToList();
        if (selectedSet.Any(id => !visibleScopeIds.Contains(id)))
        {
            return CannotMove("Only queue items visible for the selected village can be moved together.");
        }

        var reorderedVisibleIds = ReorderVisible(visibleScopeIds, selectedSet, target);
        if (reorderedVisibleIds.SequenceEqual(visibleScopeIds))
        {
            return CannotMove("The selected queue item(s) cannot move farther in the visible queue.");
        }

        var visibleScopeSet = visibleScopeIds.ToHashSet();
        var reorderedVisibleQueue = new Queue<Guid>(reorderedVisibleIds);
        var reorderedScopeIds = scopeIds
            .Select(id => visibleScopeSet.Contains(id) ? reorderedVisibleQueue.Dequeue() : id)
            .ToList();

        var before = scope.Select((item, index) => (item.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);
        var after = reorderedScopeIds.Select((id, index) => (id, index)).ToDictionary(entry => entry.id, entry => entry.index);
        var warnings = FindDependencies(scope)
            .Where(dependency => before[dependency.PrerequisiteId] < before[dependency.DependentId]
                && after[dependency.PrerequisiteId] > after[dependency.DependentId])
            .Select(dependency => dependency.Description)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new QueueMovePreview(true, reorderedScopeIds, warnings);
    }

    private static List<Guid> ReorderVisible(
        IReadOnlyList<Guid> visibleIds,
        IReadOnlySet<Guid> selectedIds,
        QueueMoveTarget target)
    {
        var reordered = visibleIds.ToList();
        switch (target)
        {
            case QueueMoveTarget.Up:
                for (var index = 1; index < reordered.Count; index++)
                {
                    if (selectedIds.Contains(reordered[index]) && !selectedIds.Contains(reordered[index - 1]))
                    {
                        (reordered[index - 1], reordered[index]) = (reordered[index], reordered[index - 1]);
                    }
                }
                break;
            case QueueMoveTarget.Down:
                for (var index = reordered.Count - 2; index >= 0; index--)
                {
                    if (selectedIds.Contains(reordered[index]) && !selectedIds.Contains(reordered[index + 1]))
                    {
                        (reordered[index], reordered[index + 1]) = (reordered[index + 1], reordered[index]);
                    }
                }
                break;
            case QueueMoveTarget.Top:
                reordered = reordered.Where(selectedIds.Contains)
                    .Concat(reordered.Where(id => !selectedIds.Contains(id)))
                    .ToList();
                break;
            case QueueMoveTarget.Bottom:
                reordered = reordered.Where(id => !selectedIds.Contains(id))
                    .Concat(reordered.Where(selectedIds.Contains))
                    .ToList();
                break;
        }

        return reordered;
    }

    private static QueueMovePreview CannotMove(string reason) => new(false, [], [], reason);

    private static IEnumerable<QueueDependency> FindDependencies(IReadOnlyList<QueueItem> items)
    {
        foreach (var prerequisite in items)
        {
            if (TryReadParentId(prerequisite, out var parentId)
                && items.Any(item => item.Id == parentId))
            {
                yield return new QueueDependency(
                    prerequisite.Id,
                    parentId,
                    $"'{Describe(prerequisite)}' is an automatically added requirement for '{Describe(items.First(item => item.Id == parentId))}'.");
            }

            foreach (var dependent in items.Where(item => item.Id != prerequisite.Id && TargetsSameVillage(prerequisite, item)))
            {
                if (IsConstructBeforeSameSlotUpgrade(prerequisite, dependent))
                {
                    yield return new QueueDependency(
                        prerequisite.Id,
                        dependent.Id,
                        $"'{Describe(prerequisite)}' must stay before its queued upgrade.");
                    continue;
                }

            }
        }
    }

    private static bool IsConstructBeforeSameSlotUpgrade(QueueItem prerequisite, QueueItem dependent) =>
        string.Equals(prerequisite.TaskName, "construct_building", StringComparison.OrdinalIgnoreCase)
        && BuildingConstructPayload.TryFromDictionary(prerequisite.Payload, out var construct)
        && construct is not null
        && (string.Equals(dependent.TaskName, "upgrade_building_to_level", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dependent.TaskName, "upgrade_building_to_max", StringComparison.OrdinalIgnoreCase))
        && BuildingUpgradePayload.TryFromDictionary(dependent.Payload, out var upgrade)
        && upgrade is not null
        && construct.SlotId == upgrade.SlotId;

    private static bool TryReadParentId(QueueItem item, out Guid parentId)
    {
        parentId = Guid.Empty;
        return (item.Payload.TryGetValue(BotOptionPayloadKeys.AutoAddedParentId, out var autoParent)
                && Guid.TryParse(autoParent, out parentId))
            || (item.Payload.TryGetValue(BotOptionPayloadKeys.StorageDependencyParentId, out var storageParent)
                && Guid.TryParse(storageParent, out parentId));
    }

    private static bool TargetsSameVillage(QueueItem first, QueueItem second)
    {
        first.Payload.TryGetValue(BotOptionPayloadKeys.TargetVillageKey, out var firstKey);
        second.Payload.TryGetValue(BotOptionPayloadKeys.TargetVillageKey, out var secondKey);
        if (!string.IsNullOrWhiteSpace(firstKey) || !string.IsNullOrWhiteSpace(secondKey))
        {
            return !string.IsNullOrWhiteSpace(firstKey)
                && string.Equals(firstKey, secondKey, StringComparison.OrdinalIgnoreCase);
        }

        first.Payload.TryGetValue(BotOptionPayloadKeys.TargetVillageName, out var firstName);
        second.Payload.TryGetValue(BotOptionPayloadKeys.TargetVillageName, out var secondName);
        return string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(secondName)
            || string.Equals(firstName, secondName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Describe(QueueItem item) =>
        item.Payload.GetValueOrDefault(BotOptionPayloadKeys.BuildingConstructName)
        ?? item.Payload.GetValueOrDefault(BotOptionPayloadKeys.BuildingUpgradeName)
        ?? item.DisplayName
        ?? item.TaskName;

    private static bool IsActive(QueueItem item) =>
        item.Status is QueueStatus.Pending or QueueStatus.Running or QueueStatus.Paused;

    private sealed record QueueDependency(Guid PrerequisiteId, Guid DependentId, string Description);
}

using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;

namespace TbotUltra.Desktop.Services;

internal enum QueueMoveTarget
{
    Up,
    Down,
    Top,
    Bottom,
}

internal sealed record QueueMovePreview(bool CanMove, IReadOnlyList<string> Warnings);

internal static class QueueMoveSafety
{
    internal static QueueMovePreview Preview(
        IReadOnlyList<QueueItem> displayOrderedItems,
        Guid selectedId,
        QueueMoveTarget target)
    {
        var selected = displayOrderedItems.FirstOrDefault(item => item.Id == selectedId && IsActive(item));
        if (selected is null)
        {
            return new QueueMovePreview(false, []);
        }

        var scope = displayOrderedItems
            .Where(item => IsActive(item)
                && item.Group == selected.Group
                && item.Priority == selected.Priority)
            .ToList();
        var sourceIndex = scope.FindIndex(item => item.Id == selectedId);
        var destinationIndex = target switch
        {
            QueueMoveTarget.Up => sourceIndex - 1,
            QueueMoveTarget.Down => sourceIndex + 1,
            QueueMoveTarget.Top => 0,
            QueueMoveTarget.Bottom => scope.Count - 1,
            _ => sourceIndex,
        };
        if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex >= scope.Count || destinationIndex == sourceIndex)
        {
            return new QueueMovePreview(false, []);
        }

        var reordered = scope.ToList();
        var moved = reordered[sourceIndex];
        reordered.RemoveAt(sourceIndex);
        reordered.Insert(destinationIndex, moved);

        var before = scope.Select((item, index) => (item.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);
        var after = reordered.Select((item, index) => (item.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);
        var warnings = FindDependencies(scope)
            .Where(dependency => before[dependency.PrerequisiteId] < before[dependency.DependentId]
                && after[dependency.PrerequisiteId] > after[dependency.DependentId])
            .Select(dependency => dependency.Description)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new QueueMovePreview(true, warnings);
    }

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

                if (ProvidesCatalogRequirement(prerequisite, dependent, out var requirement))
                {
                    yield return new QueueDependency(
                        prerequisite.Id,
                        dependent.Id,
                        $"'{Describe(prerequisite)}' provides the {requirement.Name} {requirement.Level}+ requirement for '{Describe(dependent)}'.");
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

    private static bool ProvidesCatalogRequirement(
        QueueItem prerequisite,
        QueueItem dependent,
        out BuildingRequirementEntry requirement)
    {
        requirement = null!;
        if (!string.Equals(dependent.TaskName, "construct_building", StringComparison.OrdinalIgnoreCase)
            || !BuildingConstructPayload.TryFromDictionary(dependent.Payload, out var construct)
            || construct is null
            || !TryReadProvidedBuilding(prerequisite, out var providedName, out var providedLevel))
        {
            return false;
        }

        requirement = BuildingCatalogService.RequirementsFor(construct.Gid)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, providedName, StringComparison.OrdinalIgnoreCase)
                && providedLevel >= candidate.Level)!;
        return requirement is not null;
    }

    private static bool TryReadProvidedBuilding(QueueItem item, out string name, out int level)
    {
        name = string.Empty;
        level = 0;
        if (string.Equals(item.TaskName, "construct_building", StringComparison.OrdinalIgnoreCase)
            && BuildingConstructPayload.TryFromDictionary(item.Payload, out var construct)
            && construct is not null)
        {
            name = construct.Name ?? BuildingCatalogService.NameForGid(construct.Gid) ?? string.Empty;
            level = 1;
            return !string.IsNullOrWhiteSpace(name);
        }

        if ((string.Equals(item.TaskName, "upgrade_building_to_level", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TaskName, "upgrade_building_to_max", StringComparison.OrdinalIgnoreCase))
            && BuildingUpgradePayload.TryFromDictionary(item.Payload, out var upgrade)
            && upgrade is not null
            && !string.IsNullOrWhiteSpace(upgrade.Name))
        {
            name = upgrade.Name;
            level = upgrade.TargetLevel ?? int.MaxValue;
            return true;
        }

        return false;
    }

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
        item.Status is QueueStatus.Pending or QueueStatus.Running or QueueStatus.Paused
        || (item.Status == QueueStatus.Failed && !item.IsRuntimeOnly);

    private sealed record QueueDependency(Guid PrerequisiteId, Guid DependentId, string Description);
}

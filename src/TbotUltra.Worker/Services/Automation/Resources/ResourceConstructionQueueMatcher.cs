using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal static class ResourceConstructionQueueMatcher
{
    internal static int HighestQueuedLevelForSlot(
        IReadOnlyList<ActiveConstruction> activeConstructions,
        int slotId,
        string resourceName,
        int currentLevel,
        bool allowUnknownSlotNameFallback = false)
    {
        var highestQueuedLevel = activeConstructions
            .Where(item => IsMatch(item, slotId, resourceName, allowUnknownSlotNameFallback))
            .Select(item => item.Level ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(currentLevel, highestQueuedLevel);
    }

    internal static int HighestQueuedLevelForSlot(
        IReadOnlyList<BuildQueueItem> buildQueue,
        int slotId,
        string resourceName,
        int currentLevel,
        bool allowUnknownSlotNameFallback = false)
    {
        var matchingLevels = buildQueue
            .Where(item =>
                item.SlotId == slotId
                || (allowUnknownSlotNameFallback
                    && item.SlotId is null
                    && BuildQueueFingerprints.TextMatchesBuilding(item.Text, resourceName)))
            .Select(item => BuildQueueFingerprints.TryReadLevel(item.Text) ?? 0);
        return Math.Max(currentLevel, matchingLevels.DefaultIfEmpty(0).Max());
    }

    internal static int HighestQueuedLevelForSlot(
        IReadOnlyList<ActiveConstruction> activeConstructions,
        IReadOnlyList<BuildQueueItem> buildQueue,
        int slotId,
        string resourceName,
        int currentLevel)
    {
        return Math.Max(
            HighestQueuedLevelForSlot(activeConstructions, slotId, resourceName, currentLevel, false),
            HighestQueuedLevelForSlot(buildQueue, slotId, resourceName, currentLevel, false));
    }

    internal static IReadOnlyList<ActiveConstruction> MatchForResourceSlot(
        IReadOnlyList<ActiveConstruction> activeConstructions,
        int? slotId,
        string resourceName)
    {
        return activeConstructions
            .Where(item => IsMatch(item, slotId, resourceName))
            .ToList();
    }

    internal static bool IsTargetAlreadyQueuedOnExactSlot(int targetLevel, int? detectedOfferLevel)
        => detectedOfferLevel is int offerLevel && offerLevel > targetLevel;

    internal static int InferHighestQueuedLevelFromExactSlotOffer(
        int highestKnownLevel,
        int? detectedOfferLevel)
    {
        return detectedOfferLevel is int offerLevel
            ? Math.Max(highestKnownLevel, offerLevel - 1)
            : highestKnownLevel;
    }

    private static bool IsMatch(
        ActiveConstruction item,
        int? slotId,
        string resourceName,
        bool allowUnknownSlotNameFallback = true)
    {
        if (item.Kind != ConstructionKind.Resource)
        {
            return false;
        }

        if (slotId is int requestedSlot && requestedSlot > 0)
        {
            if (item.SlotId is int activeSlot)
            {
                return activeSlot == requestedSlot;
            }

            return allowUnknownSlotNameFallback && BuildingNames.Same(item.Name, resourceName);
        }

        return string.IsNullOrWhiteSpace(resourceName) || BuildingNames.Same(item.Name, resourceName);
    }
}

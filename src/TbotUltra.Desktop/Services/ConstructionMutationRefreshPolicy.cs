using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

public static class ConstructionMutationRefreshPolicy
{
    public static bool CanUseCurrentDorf2Snapshot(VillageStatus status, string? expectedVillageKey)
    {
        if (!status.ActiveConstructionsFromOverview
            || !BuildingUpgradeSlotRebindPlanner.HasCompleteBuildingOverview(status)
            || !status.ActiveVillageCoordX.HasValue
            || !status.ActiveVillageCoordY.HasValue)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedVillageKey))
        {
            return true;
        }

        var observedVillageKey = VillageKey.FromCoords(
            status.ActiveVillageCoordX.Value,
            status.ActiveVillageCoordY.Value);
        return string.Equals(observedVillageKey, expectedVillageKey, StringComparison.OrdinalIgnoreCase);
    }

    public static VillageStatus MergeCurrentDorf2Snapshot(VillageStatus existing, VillageStatus current)
    {
        return existing with
        {
            ActiveVillage = current.ActiveVillage,
            Resources = current.Resources.Count > 0 ? current.Resources : existing.Resources,
            // Dorf2 does not observe field levels. Keep Desktop's coordinate-owned Dorf1 snapshot even
            // when Worker carries its own cached copy on the current-page response.
            ResourceFields = existing.ResourceFields,
            Buildings = current.Buildings,
            BuildQueue = current.BuildQueue,
            Tribe = string.IsNullOrWhiteSpace(current.Tribe)
                || string.Equals(current.Tribe, "Unknown", StringComparison.OrdinalIgnoreCase)
                    ? existing.Tribe
                    : current.Tribe,
            IsBuildingInProgress = current.IsBuildingInProgress,
            ActiveBuildCount = current.ActiveBuildCount,
            BuildQueueRemainingSeconds = current.BuildQueueRemainingSeconds,
            BuildQueueRemainingText = current.BuildQueueRemainingText,
            IsCapital = existing.IsCapital ?? current.IsCapital,
            ServerTimeUtc = current.ServerTimeUtc ?? existing.ServerTimeUtc,
            WarehouseCapacity = current.WarehouseCapacity ?? existing.WarehouseCapacity,
            GranaryCapacity = current.GranaryCapacity ?? existing.GranaryCapacity,
            ResourceStorageForecasts = current.ResourceStorageForecasts ?? existing.ResourceStorageForecasts,
            ActiveConstructions = current.ActiveConstructions,
            BuildQueueFinish = current.BuildQueueFinish,
            ActiveConstructionsFromOverview = current.ActiveConstructionsFromOverview,
            ActiveVillageCoordX = current.ActiveVillageCoordX,
            ActiveVillageCoordY = current.ActiveVillageCoordY,
        };
    }
}

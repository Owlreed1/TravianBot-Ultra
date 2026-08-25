using TbotUltra.Core.Tasks;
using TbotUltra.Core.Travian;
using TbotUltra.Desktop.Models;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Desktop.Services;

internal static class TroopTrainingOverviewIndicatorFactory
{
    private static readonly (TroopTrainingBuildingType Type, int Gid, string Letter, string Label)[] Definitions =
    [
        (TroopTrainingBuildingType.Barracks, 19, "B", "Barracks"),
        (TroopTrainingBuildingType.Stable, 20, "S", "Stable"),
        (TroopTrainingBuildingType.Workshop, 21, "W", "Workshop"),
    ];

    public static IReadOnlyList<VillageActivitySlot> Build(
        VillageStatus? status,
        TroopTrainingPayload settings,
        bool villageAutomationEnabled,
        bool buildTroopsEnabled)
    {
        var slots = new List<VillageActivitySlot>(Definitions.Length);
        foreach (var definition in Definitions)
        {
            var buildingSettings = TroopTrainingQuickSettings.BuildingPayloadFor(settings, definition.Type);
            if (!villageAutomationEnabled || !buildTroopsEnabled || !buildingSettings.Enabled)
            {
                var disabledReason = !villageAutomationEnabled
                    ? "village Auto off"
                    : !buildTroopsEnabled
                        ? "Build troops off"
                        : "building toggle off";
                slots.Add(new VillageActivitySlot
                {
                    Label = definition.Letter,
                    Tooltip = $"{definition.Label}: disabled ({disabledReason})",
                });
                continue;
            }

            var existence = ResolveBuildingExistence(status, definition.Type, definition.Gid);
            slots.Add(new VillageActivitySlot
            {
                IsActive = existence == true,
                IsWaiting = existence != true,
                Label = definition.Letter,
                Tooltip = existence switch
                {
                    true => $"{definition.Label}: enabled",
                    false => $"{definition.Label}: enabled, building missing",
                    null => $"{definition.Label}: enabled, building status not loaded",
                },
            });
        }

        return slots;
    }

    private static bool? ResolveBuildingExistence(
        VillageStatus? status,
        TroopTrainingBuildingType buildingType,
        int gid)
    {
        if (status is null)
        {
            return null;
        }

        var explicitQueueStatus = status.TroopTrainingQueues?
            .FirstOrDefault(queue => queue.BuildingType == buildingType);
        if (explicitQueueStatus is not null)
        {
            return explicitQueueStatus.Exists;
        }

        if (status.Buildings.Count == 0)
        {
            return null;
        }

        return status.Buildings.Any(building => building.Gid == gid);
    }
}

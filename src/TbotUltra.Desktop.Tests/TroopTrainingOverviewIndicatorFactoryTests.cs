using TbotUltra.Core.Tasks;
using TbotUltra.Core.Travian;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class TroopTrainingOverviewIndicatorFactoryTests
{
    [Fact]
    public void Build_UsesIndividualBuildingTogglesInsteadOfQueueActivity()
    {
        var status = Status(
            buildings:
            [
                new Building(19, "Barracks", 10, null, 19),
                new Building(20, "Stable", 10, null, 20),
                new Building(21, "Workshop", 10, null, 21),
            ],
            queues:
            [
                new TroopTrainingQueueStatus(
                    TroopTrainingBuildingType.Barracks,
                    "Barracks",
                    true,
                    19,
                    [new BuildQueueItem("Legionnaire", "12:00:00")],
                    43_200,
                    "12:00:00"),
            ]);

        var slots = TroopTrainingOverviewIndicatorFactory.Build(
            status,
            Settings(barracks: true, stable: false, workshop: true),
            villageAutomationEnabled: true,
            buildTroopsEnabled: true);

        Assert.Equal(["B", "S", "W"], slots.Select(slot => slot.Label));
        Assert.True(slots[0].IsActive);
        Assert.False(slots[1].IsActive);
        Assert.True(slots[2].IsActive);
        Assert.Equal("Barracks: enabled", slots[0].Tooltip);
        Assert.Equal("Stable: disabled (building toggle off)", slots[1].Tooltip);
    }

    [Theory]
    [InlineData(false, true, "village Auto off")]
    [InlineData(true, false, "Build troops off")]
    public void Build_RequiresVillageAutoAndBuildTroopsGroup(
        bool villageAutomationEnabled,
        bool buildTroopsEnabled,
        string reason)
    {
        var slots = TroopTrainingOverviewIndicatorFactory.Build(
            Status([new Building(19, "Barracks", 10, null, 19)]),
            Settings(barracks: true, stable: true, workshop: true),
            villageAutomationEnabled,
            buildTroopsEnabled);

        Assert.All(slots, slot =>
        {
            Assert.False(slot.IsActive);
            Assert.False(slot.IsWaiting);
            Assert.Contains(reason, slot.Tooltip, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Build_UsesYellowForEnabledMissingBuilding()
    {
        var status = Status(
            buildings: [new Building(19, "Barracks", 10, null, 19)],
            queues:
            [
                new TroopTrainingQueueStatus(
                    TroopTrainingBuildingType.Stable,
                    "Stable",
                    false,
                    null,
                    [],
                    null,
                    "Not built"),
            ]);

        var stable = TroopTrainingOverviewIndicatorFactory.Build(
            status,
            Settings(barracks: false, stable: true, workshop: false),
            villageAutomationEnabled: true,
            buildTroopsEnabled: true)[1];

        Assert.False(stable.IsActive);
        Assert.True(stable.IsWaiting);
        Assert.Equal("Stable: enabled, building missing", stable.Tooltip);
    }

    [Fact]
    public void Build_UsesYellowWhenBuildingStatusIsUnknown()
    {
        var barracks = TroopTrainingOverviewIndicatorFactory.Build(
            status: null,
            Settings(barracks: true, stable: false, workshop: false),
            villageAutomationEnabled: true,
            buildTroopsEnabled: true)[0];

        Assert.False(barracks.IsActive);
        Assert.True(barracks.IsWaiting);
        Assert.Equal("Barracks: enabled, building status not loaded", barracks.Tooltip);
    }

    private static TroopTrainingPayload Settings(bool barracks, bool stable, bool workshop) => new(
        BuildingSettings(barracks),
        BuildingSettings(stable),
        BuildingSettings(workshop),
        FallbackCooldownSeconds: 300);

    private static TroopTrainingBuildingPayload BuildingSettings(bool enabled) => new(
        enabled,
        TroopType: "",
        MaxQueueHours: "1",
        AmountMode: "maximum",
        KeepResourcesPercent: 0,
        RunMode: "always",
        MinimumTroops: 0,
        MinimumResourcesPercent: 0,
        TimedMinMinutes: 0,
        TimedMaxMinutes: 0,
        CheckWood: false,
        CheckClay: false,
        CheckIron: false,
        CheckCrop: false);

    private static VillageStatus Status(
        IReadOnlyList<Building>? buildings = null,
        IReadOnlyList<TroopTrainingQueueStatus>? queues = null) => new(
        ActiveVillage: "A",
        Villages: [],
        Resources: new Dictionary<string, string>(),
        ResourceFields: [],
        Buildings: buildings ?? [],
        BuildQueue: [],
        TroopTrainingQueues: queues);
}

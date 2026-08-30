using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BanRecoveryPlannerTests
{
    [Fact]
    public void Plan_RestoresExactSlots_OrdersInfrastructureAndCountsActiveTargets()
    {
        var before = Status("xy:1|2", 10,
            [new Building(19, "Main Building", 10, null, 15), new Building(20, "Warehouse", 8, null, 10), new Building(21, "Stable", 12, null, 20)]);
        var after = Status("xy:1|2", 6,
            [new Building(19, "Main Building", 7, null, 15), new Building(20, "Warehouse", 4, null, 10), new Building(21, "Stable", 8, null, 20)],
            [new ActiveConstruction(ConstructionKind.Building, "Stable", 9, 60, null, SlotId: 21, Gid: 20)]);

        var plan = BanRecoveryPlanner.Plan(
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = before },
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = after },
            [], []);

        Assert.Equal(14, plan.LostLevels); // field 4 + main 3 + warehouse 4 + stable 3 (active target 9)
        Assert.Equal(4, plan.Requests.Count);
        Assert.Equal("20", plan.Requests[0].Payload![BotOptionPayloadKeys.BuildingUpgradeSlotId]);
        Assert.Equal("19", plan.Requests[1].Payload![BotOptionPayloadKeys.BuildingUpgradeSlotId]);
        Assert.Equal("upgrade_resource_to_level", plan.Requests[2].TaskName);
        Assert.Equal("21", plan.Requests[3].Payload![BotOptionPayloadKeys.BuildingUpgradeSlotId]);
        Assert.All(plan.Requests, request => Assert.Equal(BotOptionPayloadKeys.AutoAddedByBanRecovery,
            request.Payload![BotOptionPayloadKeys.AutoAddedBy]));
    }

    [Fact]
    public void Plan_ReconstructsEmptyExactSlot_ButSkipsChangedSlot()
    {
        var before = Status("xy:1|2", 1,
            [new Building(19, "Main Building", 5, null, 15), new Building(20, "Warehouse", 4, null, 10)]);
        var after = Status("xy:1|2", 1,
            [new Building(19, "Empty", 0, null), new Building(20, "Granary", 2, null, 11)]);

        var plan = BanRecoveryPlanner.Plan(
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = before },
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = after },
            [], []);

        var request = Assert.Single(plan.Requests);
        Assert.Equal("construct_building", request.TaskName);
        Assert.Equal("19", request.Payload![BotOptionPayloadKeys.BuildingConstructSlotId]);
        Assert.Equal("5", request.Payload![BotOptionPayloadKeys.BuildingUpgradeTargetLevel]);
        Assert.Equal("false", request.Payload![BotOptionPayloadKeys.BuildingConstructAllowSlotFallback]);
        Assert.Contains(plan.Issues, issue => issue.Message.Contains("Granary", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_LeavesFailedVillageUntouchedAndCountsExistingConstructionQueue()
    {
        var before = Status("xy:1|2", 10, []);
        var existing = new QueueItem { TaskName = "upgrade_resource_to_level", Group = QueueGroup.Construction, Status = QueueStatus.Paused };
        var farming = new QueueItem { TaskName = "send_farmlists", Group = QueueGroup.Farming, Status = QueueStatus.Pending };

        var plan = BanRecoveryPlanner.Plan(
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = before },
            new Dictionary<string, VillageStatus>(), ["xy:1|2"], [existing, farming]);

        Assert.Empty(plan.Requests);
        Assert.Contains(plan.Issues, issue => issue.Message.Contains("scan failed", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, plan.ExistingConstructionItemsToReplace);
    }

    [Fact]
    public void Plan_ReportsVillageWithoutPreBanBaseline()
    {
        var current = Status("xy:1|2", 5, []);

        var plan = BanRecoveryPlanner.Plan(
            new Dictionary<string, VillageStatus>(),
            new Dictionary<string, VillageStatus> { ["xy:1|2"] = current }, [], []);

        Assert.Empty(plan.Requests);
        Assert.Contains(plan.Issues, issue => issue.Message.Contains("No pre-ban snapshot", StringComparison.Ordinal));
    }

    private static VillageStatus Status(
        string key,
        int fieldLevel,
        IReadOnlyList<Building> buildings,
        IReadOnlyList<ActiveConstruction>? active = null) => new(
        "Village", [new Village("Village", "/dorf1.php?newdid=1", CoordX: 1, CoordY: 2)],
        new Dictionary<string, string>(), [new ResourceField(1, "wood", "Woodcutter", fieldLevel, null)],
        buildings, [], ActiveConstructions: active, ActiveVillageCoordX: 1, ActiveVillageCoordY: 2);
}

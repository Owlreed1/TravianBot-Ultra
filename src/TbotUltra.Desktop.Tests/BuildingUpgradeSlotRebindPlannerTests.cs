using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingUpgradeSlotRebindPlannerTests
{
    [Fact]
    public void ConstructionQueueReconciliation_RemovesStaleConstructAndRebindsDependentUpgrade()
    {
        var construct = Item("construct_building", new BuildingConstructPayload(38, 22, "Academy").ToDictionary());
        var upgrade = Item("upgrade_building_to_level", new BuildingUpgradePayload(38, 5, "Academy").ToDictionary());

        var plan = ConstructionQueueReconciliation.Plan(
            Status(new Building(37, "Academy", 3, "/build.php?id=37", 22)),
            [construct, upgrade]);

        Assert.Contains(construct.Id, plan.Removals);
        var update = Assert.Single(plan.Updates);
        Assert.Equal(upgrade.Id, update.QueueItemId);
        Assert.Equal("37", update.Payload[BotOptionPayloadKeys.BuildingUpgradeSlotId]);
    }

    [Fact]
    public void ConstructionQueueReconciliation_LeavesInputPayloadsUnchanged()
    {
        var construct = Item("construct_building", new BuildingConstructPayload(38, 22, "Academy").ToDictionary());
        var upgrade = Item("upgrade_building_to_level", new BuildingUpgradePayload(38, 5, "Academy").ToDictionary());

        var plan = ConstructionQueueReconciliation.Plan(
            Status(new Building(37, "Academy", 3, "/build.php?id=37", 22)),
            [construct, upgrade]);

        Assert.Contains(construct.Id, plan.Removals);
        Assert.Equal("38", upgrade.Payload[BotOptionPayloadKeys.BuildingUpgradeSlotId]);
        Assert.Equal("37", Assert.Single(plan.Updates).Payload[BotOptionPayloadKeys.BuildingUpgradeSlotId]);
    }

    [Fact]
    public void Plan_RebindsAcademyUpgradesWhenLiveDuplicateIsInAnotherSlot()
    {
        var source = Item(
            "construct_building",
            new BuildingConstructPayload(38, 22, "Academy").ToDictionary());
        var academyLevel5 = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(38, 5, "Academy").ToDictionary());
        var academyMax = Item(
            "upgrade_building_to_max",
            new BuildingUpgradePayload(38, null, "Academy").ToDictionary());
        var otherBuilding = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(38, 5, "Smithy").ToDictionary());

        var result = BuildingUpgradeSlotRebindPlanner.Plan(
            source,
            effectiveSlotId: 37,
            [academyLevel5, academyMax, otherBuilding]);

        Assert.Equal(2, result.Count);
        Assert.All(result, rebind =>
            Assert.Equal("37", rebind.Payload[BotOptionPayloadKeys.BuildingUpgradeSlotId]));
        Assert.DoesNotContain(result, rebind => rebind.QueueItemId == otherBuilding.Id);
    }

    [Fact]
    public void ConstructionQueueReconciliation_PreservesWarehouseConstructBeforeLevel20()
    {
        var construct = Item(
            "construct_building",
            new BuildingConstructPayload(38, 10, "Warehouse").ToDictionary());
        var upgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(38, 5, "Warehouse").ToDictionary());

        var plan = ConstructionQueueReconciliation.Plan(
            Status(new Building(37, "Warehouse", 12, "/build.php?id=37", 10)),
            [construct, upgrade]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void ConstructionQueueReconciliation_PreservesWarehouseConstructAfterLevel20()
    {
        var construct = Item(
            "construct_building",
            new BuildingConstructPayload(38, 10, "Warehouse").ToDictionary());

        var plan = ConstructionQueueReconciliation.Plan(
            Status(new Building(37, "Warehouse", 20, "/build.php?id=37", 10)),
            [construct]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void PlanFromLiveStatus_RemovesWrongSlotAcademyUpgradeWhenTargetAlreadyMet()
    {
        var upgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(38, 5, "Academy").ToDictionary());
        var status = Status(new Building(37, "Academy", 5, "/build.php?id=37", 22));

        var reconciliation = Assert.Single(
            BuildingUpgradeSlotRebindPlanner.PlanFromLiveStatus(status, [upgrade]));

        Assert.True(reconciliation.TargetSatisfied);
        Assert.Equal(38, reconciliation.QueuedSlotId);
        Assert.Equal(37, reconciliation.LiveSlotId);
        Assert.Equal(5, reconciliation.LiveLevel);
    }

    [Fact]
    public void ConstructionQueueReconciliation_RemovesSatisfiedWarehouseUpgradeInExactSlot()
    {
        var payload = new BuildingUpgradePayload(19, 6, "Warehouse").ToDictionary();
        payload[BotOptionPayloadKeys.UpgradeDeferReason] = BotOptionPayloadKeys.UpgradeDeferReasonInProgress;
        var upgrade = Item("upgrade_building_to_level", payload);
        var status = Status(new Building(19, "Warehouse", 6, "/build.php?id=19", 10));

        var plan = ConstructionQueueReconciliation.Plan(status, [upgrade]);

        Assert.Contains(upgrade.Id, plan.Removals);
    }

    [Fact]
    public void PlanFromLiveStatus_DoesNotRebindWarehouseUpgradeToDifferentDuplicateSlot()
    {
        var upgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(19, 6, "Warehouse").ToDictionary());
        var status = Status(new Building(20, "Warehouse", 6, "/build.php?id=20", 10));

        var reconciliation = BuildingUpgradeSlotRebindPlanner.PlanUpgradeFromLiveStatus(status, upgrade);

        Assert.Null(reconciliation);
    }

    [Fact]
    public void PlanFromLiveStatus_RebindsWrongSlotAcademyUpgradeWhenTargetNotMet()
    {
        var upgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(38, 5, "Academy").ToDictionary());
        var status = Status(new Building(37, "Academy", 3, "/build.php?id=37", 22));

        var reconciliation = Assert.Single(
            BuildingUpgradeSlotRebindPlanner.PlanFromLiveStatus(status, [upgrade]));

        Assert.False(reconciliation.TargetSatisfied);
        Assert.Equal("37", reconciliation.Payload[BotOptionPayloadKeys.BuildingUpgradeSlotId]);
    }

    [Fact]
    public void FindExistingConstruct_FindsAcademyBeforeDesktopQueueDelay()
    {
        var construct = Item(
            "construct_building",
            new BuildingConstructPayload(38, 22, "Academy").ToDictionary());
        var status = Status(new Building(37, "Academy", 5, "/build.php?id=37", 22));

        var match = Assert.IsType<BuildingConstructLiveMatch>(
            BuildingUpgradeSlotRebindPlanner.FindExistingConstruct(status, construct));

        Assert.Equal(38, match.QueuedSlotId);
        Assert.Equal(37, match.LiveSlotId);
        Assert.Equal(5, match.LiveLevel);
    }

    [Fact]
    public void FindExistingConstruct_FindsProductionBuildingMovedToAnotherSlot()
    {
        var construct = Item(
            "construct_building",
            new BuildingConstructPayload(27, 6, "Brickyard").ToDictionary());
        var status = Status(
            new Building(27, "Sawmill", 1, "/build.php?id=27", 5),
            new Building(28, "Brickyard", 2, "/build.php?id=28", 6));

        var match = Assert.IsType<BuildingConstructLiveMatch>(
            BuildingUpgradeSlotRebindPlanner.FindExistingConstruct(status, construct));

        Assert.Equal(27, match.QueuedSlotId);
        Assert.Equal(28, match.LiveSlotId);
        Assert.Equal(2, match.LiveLevel);
    }

    [Fact]
    public void ConstructionQueueReconciliation_PreservesConditionalDuplicateUntilItsOwnSlotExists()
    {
        var firstCranny = new Building(28, "Cranny", 6, "/build.php?id=28", 23);
        var firstConstruct = Item(
            "construct_building",
            new BuildingConstructPayload(29, 23, "Cranny").ToDictionary());
        var firstUpgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(29, 10, "Cranny").ToDictionary());
        var secondConstruct = Item(
            "construct_building",
            new BuildingConstructPayload(30, 23, "Cranny").ToDictionary());
        var secondUpgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(30, 10, "Cranny").ToDictionary());

        var plan = ConstructionQueueReconciliation.Plan(
            Status(firstCranny),
            [firstConstruct, firstUpgrade, secondConstruct, secondUpgrade]);

        Assert.False(plan.HasChanges);
    }

    [Fact]
    public void PlanUpgradeFromLiveStatus_ReportsSameSlotIdentityForMissingBuildingRecovery()
    {
        var upgrade = Item(
            "upgrade_building_to_level",
            new BuildingUpgradePayload(37, 10, "Academy").ToDictionary());
        var status = Status(new Building(37, "Academy", 5, "/build.php?id=37", 22));

        var reconciliation = Assert.IsType<BuildingUpgradeLiveReconciliation>(
            BuildingUpgradeSlotRebindPlanner.PlanUpgradeFromLiveStatus(status, upgrade));

        Assert.False(reconciliation.TargetSatisfied);
        Assert.Equal(reconciliation.QueuedSlotId, reconciliation.LiveSlotId);
        Assert.Empty(BuildingUpgradeSlotRebindPlanner.PlanFromLiveStatus(status, [upgrade]));
    }

    [Fact]
    public void RepairSafety_DetectsIncompleteOverviewAndUnknownLevelIdentity()
    {
        var incomplete = Status(new Building(37, "Academy", null, "/build.php?id=37", 22));
        var complete = Status(Enumerable.Range(19, 22)
            .Select(slot => slot == 37
                ? new Building(slot, "Academy", null, $"/build.php?id={slot}", 22)
                : new Building(slot, "Empty", 0, $"/build.php?id={slot}"))
            .ToArray());

        Assert.False(BuildingUpgradeSlotRebindPlanner.HasCompleteBuildingOverview(incomplete));
        Assert.True(BuildingUpgradeSlotRebindPlanner.HasCompleteBuildingOverview(complete));
        Assert.True(BuildingUpgradeSlotRebindPlanner.HasLiveBuildingIdentity(complete, 22));
    }

    private static VillageStatus Status(params Building[] buildings) => new(
        "G1",
        [],
        new Dictionary<string, string>(),
        [],
        buildings,
        []);

    private static QueueItem Item(string taskName, Dictionary<string, string> payload) => new()
    {
        TaskName = taskName,
        Payload = payload,
        Status = QueueStatus.Pending,
    };
}

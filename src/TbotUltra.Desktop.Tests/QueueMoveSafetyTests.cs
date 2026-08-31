using TbotUltra.Core.Configuration;
using TbotUltra.Core.Tasks;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class QueueMoveSafetyTests
{
    [Fact]
    public void Preview_WarnsButAllowsMovingConstructBelowItsUpgrade()
    {
        var construct = Item("construct_building", new BuildingConstructPayload(31, 19, "Barracks").ToDictionary());
        var upgrade = Item("upgrade_building_to_level", new BuildingUpgradePayload(31, 3, "Barracks").ToDictionary());

        var preview = QueueMoveSafety.Preview([construct, upgrade], construct.Id, QueueMoveTarget.Bottom);

        Assert.True(preview.CanMove);
        Assert.Contains(preview.Warnings, warning => warning.Contains("must stay before", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_WarnsForAnAutomaticallyAddedRequirementMovedAfterItsParent()
    {
        var parent = Item("construct_building", new BuildingConstructPayload(32, 22, "Academy").ToDictionary());
        var requirementPayload = new BuildingUpgradePayload(30, 3, "Main Building").ToDictionary();
        requirementPayload[BotOptionPayloadKeys.AutoAddedParentId] = parent.Id.ToString();
        var requirement = Item("upgrade_building_to_level", requirementPayload);

        var preview = QueueMoveSafety.Preview([requirement, parent], requirement.Id, QueueMoveTarget.Bottom);

        Assert.True(preview.CanMove);
        Assert.Contains(preview.Warnings, warning => warning.Contains("automatically added requirement", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_DoesNotWarnForIndependentTasks()
    {
        var first = Item("upgrade_building_to_level", new BuildingUpgradePayload(30, 3, "Main Building").ToDictionary());
        var second = Item("upgrade_building_to_level", new BuildingUpgradePayload(31, 3, "Barracks").ToDictionary());

        var preview = QueueMoveSafety.Preview([first, second], second.Id, QueueMoveTarget.Top);

        Assert.True(preview.CanMove);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public void Preview_DoesNotWarnForUnlinkedCatalogRelatedTasks()
    {
        var barracks = Item("upgrade_building_to_level", new BuildingUpgradePayload(31, 3, "Barracks").ToDictionary());
        var academy = Item("construct_building", new BuildingConstructPayload(32, 22, "Academy").ToDictionary());

        var preview = QueueMoveSafety.Preview([barracks, academy], academy.Id, QueueMoveTarget.Top);

        Assert.True(preview.CanMove);
        Assert.Empty(preview.Warnings);
    }

    [Fact]
    public void Preview_MovesSelectedVisibleRowsAndLeavesHiddenVillageRowsInPlace()
    {
        var first = Item("status", []);
        var hidden = Item("status", []);
        var second = Item("scan_all_villages", []);
        var third = Item("account_snapshot", []);

        var preview = QueueMoveSafety.Preview(
            [first, hidden, second, third],
            [first.Id, second.Id, third.Id],
            [second.Id, third.Id],
            QueueMoveTarget.Up);

        Assert.True(preview.CanMove);
        Assert.Equal([second.Id, hidden.Id, third.Id, first.Id], preview.OrderedScopeIds);
    }

    [Fact]
    public void Preview_ToBottomPreservesSelectedOrderAndMakesSelectionContiguous()
    {
        var first = Item("status", []);
        var selectedFirst = Item("scan_all_villages", []);
        var middle = Item("account_snapshot", []);
        var selectedSecond = Item("status", []);

        var preview = QueueMoveSafety.Preview(
            [first, selectedFirst, middle, selectedSecond],
            [first.Id, selectedFirst.Id, middle.Id, selectedSecond.Id],
            [selectedFirst.Id, selectedSecond.Id],
            QueueMoveTarget.Bottom);

        Assert.True(preview.CanMove);
        Assert.Equal([first.Id, middle.Id, selectedFirst.Id, selectedSecond.Id], preview.OrderedScopeIds);
    }

    [Fact]
    public void Preview_RejectsSelectionAcrossPriorities()
    {
        var normal = Item("status", []);
        var urgent = Item("scan_all_villages", []);
        urgent.Priority = 1;

        var preview = QueueMoveSafety.Preview(
            [normal, urgent],
            [normal.Id, urgent.Id],
            [normal.Id, urgent.Id],
            QueueMoveTarget.Top);

        Assert.False(preview.CanMove);
        Assert.Contains("same group and priority", preview.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    private static QueueItem Item(string taskName, Dictionary<string, string> payload) => new()
    {
        TaskName = taskName,
        Group = QueueGroup.Construction,
        Priority = 0,
        Status = QueueStatus.Pending,
        Payload = payload,
    };
}

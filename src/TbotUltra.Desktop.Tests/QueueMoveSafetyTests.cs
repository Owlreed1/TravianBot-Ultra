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

    private static QueueItem Item(string taskName, Dictionary<string, string> payload) => new()
    {
        TaskName = taskName,
        Group = QueueGroup.Construction,
        Priority = 0,
        Status = QueueStatus.Pending,
        Payload = payload,
    };
}

using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConstructionQueueSelectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectNext_AvailableQueueKeepsClassifiedFutureQueueFullItemDeferred()
    {
        var blocker = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonQueueFull);
        blocker.Payload[BotOptionPayloadKeys.UpgradeDeferClassificationVersion] =
            ConstructionQueueState.CurrentDeferClassificationVersion;

        var result = ConstructionQueueSelector.SelectNext(
            [blocker],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.False(result.ForcedLiveValidation);
        Assert.Same(blocker, result.QueueFullBlocker);
    }

    [Fact]
    public void SelectNext_FullQueueBlocksLaterConstruction()
    {
        var blocker = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonQueueFull);
        var later = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [blocker, later],
            Now,
            ConstructionQueueAvailability.Full);

        Assert.Null(result.Item);
        Assert.Same(blocker, result.QueueFullBlocker);
    }

    [Fact]
    public void SelectNext_ResourceWaitPreservesConstructionOrder()
    {
        var resourceWait = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonResources);
        var later = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [resourceWait, later],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.Null(result.QueueFullBlocker);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_InProgressUpgradeAllResourcesHoldsLaterBuildingSlot()
    {
        var inProgress = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        inProgress.TaskName = "upgrade_all_resources_to_level";
        var next = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [inProgress, next],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.Null(result.QueueFullBlocker);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_InProgressResourceSlotFull_AllowsAvailableBuildingSlot()
    {
        var activeResource = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var building = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [activeResource, building],
            Now,
            ConstructionQueueAvailability.Full,
            availabilityForIndex: index => index == 0
                ? ConstructionQueueAvailability.Full
                : ConstructionQueueAvailability.Available);

        Assert.Same(building, result.Item);
    }

    [Fact]
    public void SelectNext_StartedRomanPrefix_AllowsNextAvailableBuildingSlot()
    {
        var resource = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var firstBuilding = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var secondBuilding = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [resource, firstBuilding, secondBuilding],
            Now,
            ConstructionQueueAvailability.Full,
            availabilityForIndex: index => index == 2
                ? ConstructionQueueAvailability.Available
                : ConstructionQueueAvailability.Full);

        Assert.Same(secondBuilding, result.Item);
    }

    [Fact]
    public void SelectNext_UnstartedCategoryFullRowHoldsLaterBuildingSlot()
    {
        var activeResource = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var waitingResource = CreateReadyItem();
        var building = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [activeResource, waitingResource, building],
            Now,
            ConstructionQueueAvailability.Full,
            availabilityForIndex: index => index < 2
                ? ConstructionQueueAvailability.Full
                : ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.Same(waitingResource, result.QueueFullBlocker);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_InProgressHoldsQueueOrderWhenQueueFull()
    {
        var inProgress = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var next = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [inProgress, next],
            Now,
            ConstructionQueueAvailability.Full);

        Assert.Null(result.Item);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_InProgressSingleLevelUpgradeAllowsNextReadyItemWhenSlotIsAvailable()
    {
        var inProgress = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var next = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [inProgress, next],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Same(next, result.Item);
    }

    [Fact]
    public void SelectNext_InProgressHoldsWhenNextBlockedByDependency()
    {
        var inProgress = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonInProgress);
        var next = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [inProgress, next],
            Now,
            ConstructionQueueAvailability.Available,
            index => index == 1);

        Assert.Null(result.Item);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_StorageCapacityDependencyHoldsQueueOrder()
    {
        var capacityWait = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonStorageCapacity);
        var dependency = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [capacityWait, dependency],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_RequirementDependencyHoldsQueueOrder()
    {
        var requirementWait = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonRequirements);
        var dependency = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [requirementWait, dependency],
            Now,
            ConstructionQueueAvailability.Available);

        Assert.Null(result.Item);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_LiveFullQueueBlocksReadyHead()
    {
        var ready = CreateReadyItem();

        var result = ConstructionQueueSelector.SelectNext(
            [ready],
            Now,
            ConstructionQueueAvailability.Full);

        Assert.Null(result.Item);
        Assert.Same(ready, result.QueueFullBlocker);
        Assert.Contains("holding queue order", result.SkipReason);
    }

    [Fact]
    public void SelectNext_UnknownQueueValidatesOnlyLegacyQueueFullImmediately()
    {
        var legacy = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonQueueFull);
        var current = CreateDeferredItem(BotOptionPayloadKeys.UpgradeDeferReasonQueueFull);
        current.Payload[BotOptionPayloadKeys.UpgradeDeferClassificationVersion] =
            ConstructionQueueState.CurrentDeferClassificationVersion;

        var legacyResult = ConstructionQueueSelector.SelectNext(
            [legacy],
            Now,
            ConstructionQueueAvailability.Unknown);
        var currentResult = ConstructionQueueSelector.SelectNext(
            [current],
            Now,
            ConstructionQueueAvailability.Unknown);

        Assert.Same(legacy, legacyResult.Item);
        Assert.True(legacyResult.ForcedLiveValidation);
        Assert.Null(currentResult.Item);
        Assert.Same(current, currentResult.QueueFullBlocker);
    }

    private static QueueItem CreateDeferredItem(string reason)
    {
        return new QueueItem
        {
            TaskName = "upgrade_building_to_level",
            Status = QueueStatus.Pending,
            NextAttemptAt = Now.AddMinutes(15),
            Payload = new Dictionary<string, string>
            {
                [BotOptionPayloadKeys.UpgradeDeferReason] = reason,
            },
        };
    }

    private static QueueItem CreateReadyItem()
    {
        return new QueueItem
        {
            TaskName = "upgrade_building_to_level",
            Status = QueueStatus.Pending,
            NextAttemptAt = Now,
        };
    }
}

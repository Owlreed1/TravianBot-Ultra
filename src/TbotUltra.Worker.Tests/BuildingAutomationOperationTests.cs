using TbotUltra.Core.Tasks;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using TbotUltra.Worker.Services.Automation;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class BuildingAutomationOperationTests
{
    [Fact]
    public async Task ExecuteAsync_RoutesConstructWithoutChangingFallbackArguments()
    {
        var client = new FakeBuildingClient();
        var operation = new BuildingAutomationOperation(client);

        var result = await operation.ExecuteAsync(
            new BuildingAutomationRequest(
                BuildingAutomationAction.Construct,
                SlotId: 28,
                Gid: 10,
                Name: "Warehouse",
                AllowSlotFallback: true,
                FallbackExcludedSlots: "19,20"),
            CancellationToken.None);

        Assert.Equal("constructed", result);
        Assert.Equal((28, 10, "Warehouse", true, "19,20"), client.ConstructRequest);
    }

    [Fact]
    public async Task ExecuteAsync_RoutesUpgradeToMaxWithConfiguredAttemptLimit()
    {
        var client = new FakeBuildingClient();
        var operation = new BuildingAutomationOperation(client);

        var result = await operation.ExecuteAsync(
            new BuildingAutomationRequest(BuildingAutomationAction.UpgradeToMax, SlotId: 21, MaxAttempts: 12),
            CancellationToken.None);

        Assert.Equal("upgraded", result);
        Assert.Equal((21, 12), client.UpgradeToMaxRequest);
    }

    [Fact]
    public async Task ResourceOperation_RoutesBulkUpgradeWithoutChangingSelection()
    {
        var client = new FakeResourceUpgradeClient();
        var operation = new ResourceAutomationOperation(client);

        var result = await operation.UpgradeAllAsync(12, "smart", "wood,crop", CancellationToken.None);

        Assert.Equal("resources upgraded", result);
        Assert.Equal((12, "smart", "wood,crop"), client.BulkRequest);
    }

    [Fact]
    public async Task HeroOperation_RoutesAllManageOptionsWithoutChangingDefaults()
    {
        var client = new FakeHeroClient();
        var operation = new HeroAutomationOperation(client);

        var result = await operation.ManageAsync(35, true, false, true, "offense", "longest", 55, CancellationToken.None);

        Assert.Equal("managed", result);
        Assert.Equal((35, true, false, true, "offense", "longest", 55), client.ManageRequest);
    }

    private sealed class FakeBuildingClient : IBuildingClient
    {
        public (int SlotId, int Gid, string Name, bool AllowFallback, string? ExcludedSlots) ConstructRequest { get; private set; }
        public (int SlotId, int MaxAttempts) UpgradeToMaxRequest { get; private set; }

        public Task<VillageStatus> ReadBuildingsStatusAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> DemolishBuildingToLevelAsync(string targetBuildingSlotOrName, int targetLevel, CancellationToken cancellationToken = default) => Task.FromResult("demolished");
        public Task<string> UpgradeBuildingToLevelAsync(int slotId, int targetLevel, CancellationToken cancellationToken = default) => Task.FromResult("upgraded");

        public Task<string> UpgradeBuildingToMaxAsync(int slotId, int maxAttempts = 30, CancellationToken cancellationToken = default)
        {
            UpgradeToMaxRequest = (slotId, maxAttempts);
            return Task.FromResult("upgraded");
        }

        public Task<string> ConstructBuildingAsync(int slotId, int gid, string name, CancellationToken cancellationToken = default, bool allowSlotFallback = false, string? fallbackExcludedSlots = null)
        {
            ConstructRequest = (slotId, gid, name, allowSlotFallback, fallbackExcludedSlots);
            return Task.FromResult("constructed");
        }

        public Task<string> UpgradeSelectedTroopsAtSmithyAsync(IReadOnlyList<SmithyTroopTarget> targets, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmithyUpgradeStatus> ReadSmithyUpgradeStatusAsync(IReadOnlyList<Building>? knownBuildings = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ReadSmithyQueueFromCurrentPageTestAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveConstruction>> ReadActiveConstructionsAsync(CancellationToken cancellationToken = default, bool allowNavigationToBuildings = true, ActiveConstructionReadMode readMode = ActiveConstructionReadMode.FreshForMutation) => throw new NotSupportedException();
        public Task<ConstructionSlotStatus> EvaluateConstructionSlotsAsync(string tribe, bool travianPlusActive, CancellationToken cancellationToken = default, bool allowNavigationToBuildings = true) => throw new NotSupportedException();
        public Task<int> WaitForConstructionSlotIfBusyAsync(ConstructionKind kind, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeResourceUpgradeClient : IResourceUpgradeClient
    {
        public (int TargetLevel, string Strategy, string? Types) BulkRequest { get; private set; }

        public Task<string> UpgradeResourceToLevelAsync(int slotId, int targetLevel, CancellationToken cancellationToken = default)
            => Task.FromResult("resource upgraded");

        public Task<string> UpgradeAllResourcesToLevelAsync(int targetLevel, string buildStrategy = "lowest_first", string? resourceTypes = null, CancellationToken cancellationToken = default)
        {
            BulkRequest = (targetLevel, buildStrategy, resourceTypes);
            return Task.FromResult("resources upgraded");
        }
    }

    private sealed class FakeHeroClient : IHeroClient
    {
        public (int MinHp, bool AutoRevive, bool AutoAssign, bool AutoOintments, string StatPriority, string AdventureOrder, int RegenPercent) ManageRequest { get; private set; }

        public Task<HeroAdventureDispatchResult> SendHeroOnAdventureAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> RefreshAdventureCountAsync(bool forceReload = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasHeroLevelUpIndicatorOnCurrentPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CheckAndReviveDeadHeroOnCurrentPageAsync(bool autoRevive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsHeroRevivingOnCurrentPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsHeroHomeOnCurrentPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> ReadHeroHpFromCurrentPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> ManageHeroAsync(int minHpForAdventure, bool autoRevive, bool autoAssignPoints, bool autoUseOintments, string statPriority, string adventurePickOrder = "shortest", int heroHpRegenPerDayPercent = 40, CancellationToken cancellationToken = default)
        {
            ManageRequest = (minHpForAdventure, autoRevive, autoAssignPoints, autoUseOintments, statPriority, adventurePickOrder, heroHpRegenPerDayPercent);
            return Task.FromResult("managed");
        }

        public Task<string> SpendHeroAttributePointsAsync(string statPriority, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HeroAttributeSnapshot> ReadHeroAttributeSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HeroInventoryResources> ReadHeroInventoryResourcesAsync(CancellationToken cancellationToken = default, bool suppressUiSync = false) => throw new NotSupportedException();
        public HeroInventoryResources? TryGetCachedHeroInventory() => null;
        public Task<string> IncreaseAdventuresToHardAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ReduceAdventuresTimeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

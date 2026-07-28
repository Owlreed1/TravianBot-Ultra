using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class VillageStatusSweepSourceTests
{
    [Fact]
    public void Dorf1Sweep_ReconcilesConstructionQueueAndRefreshesVillageList()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.ContinuousLoop.cs"));
        var methodStart = source.IndexOf(
            "private async Task MaybeRunVillageStatusSweepAsync",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "    private async Task<VillageStatus> RefreshVillageStatusSweepOptionalStatusesAsync",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var methodBody = source[methodStart..methodEnd];
        var cacheIndex = methodBody.IndexOf(
            "CacheVillageStatus(status, village.Name, triggerDeferredWaitRefresh: false);",
            StringComparison.Ordinal);
        var reconcileIndex = methodBody.IndexOf(
            "ReconcilePendingBuildingQueueWithLiveStatus(status);",
            cacheIndex,
            StringComparison.Ordinal);
        var villageUiIndex = methodBody.IndexOf(
            "SyncDashboardVillageUiFromVillages(",
            reconcileIndex,
            StringComparison.Ordinal);

        Assert.True(cacheIndex >= 0 && reconcileIndex > cacheIndex && villageUiIndex > reconcileIndex);
        Assert.DoesNotContain(
            "VillageStatusSweepDorf2Enabled",
            methodBody[cacheIndex..reconcileIndex],
            StringComparison.Ordinal);
        Assert.Contains(
            "CollectVillageStatusSweepRewardsAsync(",
            methodBody,
            StringComparison.Ordinal);
    }
}

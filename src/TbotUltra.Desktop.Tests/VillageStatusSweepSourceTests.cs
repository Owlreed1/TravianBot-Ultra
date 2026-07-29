using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class VillageStatusSweepSourceTests
{
    [Fact]
    public void ScanNowButton_ResetsDeadlineAndForcesImmediateSweep()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "SettingsWindow.xaml"));
        var continuousLoopSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.ContinuousLoop.cs"));

        Assert.Contains("Content=\"Scan now\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"VillageStatusSweepScanNowButton_Click\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ResetVillageStatusSweepSchedule();", continuousLoopSource, StringComparison.Ordinal);
        Assert.Contains("force: true", continuousLoopSource, StringComparison.Ordinal);
        Assert.Contains("_villageStatusSweepForceRequested", continuousLoopSource, StringComparison.Ordinal);
    }

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
        Assert.Contains(
            "RefreshInboxIndicatorsForVillageStatusSweepAsync(options, token);",
            methodBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DorfTooltips_DescribeTheirActualScanResponsibilities()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var xaml = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "SettingsWindow.xaml"));

        Assert.Contains(
            "Reads resources, hourly production, Warehouse and Granary capacity, population, construction queue, tasks, daily quests, unread messages and reports.",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "Reads all buildings in the village center. Required before Smithy, Barracks, Stable, Workshop, Town Hall or Brewery can be scanned.",
            xaml,
            StringComparison.Ordinal);
    }
}

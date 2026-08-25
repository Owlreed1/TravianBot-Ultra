using System;
using System.IO;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingsVillageQueueDurationSourceTests
{
    [Fact]
    public void QueueProjection_InvalidatesVillageOverviewAfterEstimateRowsAreRebuilt()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.QueueUi.Display.cs"));

        var methodStart = source.IndexOf(
            "private void RefreshQueueUi(Guid? selectId = null)",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf(
            "private QueueDisplayRows BuildQueueDisplayRows",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..nextMethod];

        Assert.Contains("InvalidateVillageOverview();", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("InvalidateVillageOverview();", StringComparison.Ordinal)
            > method.IndexOf("_queueEstimateRowsById[row.Id] = row;", StringComparison.Ordinal),
            "Village overview must be invalidated after the authoritative Queue estimate rows are rebuilt.");
    }

    [Fact]
    public void OpeningVillageOverview_RebuildsQueueEstimateProjectionFirst()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Dashboard.Panel.cs"));

        var methodStart = source.IndexOf(
            "internal void OnDashboardVillageTabSelected()",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf(
            "internal void OnDashboardClearTimersClicked",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..nextMethod];

        Assert.Contains("RefreshQueueUi();", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("RefreshQueueUi();", StringComparison.Ordinal)
            < method.IndexOf("EnsureDashboardVillagePanels();", StringComparison.Ordinal),
            "Opening Village overview must rebuild Queue's authoritative estimates before the overview reads them.");
    }

    [Fact]
    public void VillageSelection_RebuildsBuildingsQueueDurationFromFreshProjection()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.VillageWorking.cs"));

        var methodStart = source.IndexOf(
            "private void ShowSelectedVillageFromCache(VillageSelectionItem selected)",
            StringComparison.Ordinal);
        var nextMethod = source.IndexOf(
            "private void ApplySelectedVillageTribeFromCache",
            methodStart,
            StringComparison.Ordinal);
        var method = source[methodStart..nextMethod];

        Assert.Contains("RefreshQueueUi()", method, StringComparison.Ordinal);
        Assert.True(
            method.IndexOf("RefreshQueueUi()", StringComparison.Ordinal)
            > method.IndexOf("PopulateBuildingsTab(", StringComparison.Ordinal),
            "The fresh queue projection must use the newly selected village's building levels.");
        Assert.DoesNotContain("UpdateBuildingsQueueDuration();", method, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "TbotUltra.Desktop")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}

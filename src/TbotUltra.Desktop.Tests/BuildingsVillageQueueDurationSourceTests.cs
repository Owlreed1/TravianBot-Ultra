using System;
using System.IO;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class BuildingsVillageQueueDurationSourceTests
{
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

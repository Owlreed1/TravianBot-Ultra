using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ConstructionProgressNotificationSourceTests
{
    [Fact]
    public void ConfirmedTravianQueueProgress_ReachesDashboardBeforeTaskCompletion()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var resourceSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Upgrade.cs"));
        var buildingSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Buildings",
            "TravianClient.Buildings.UpgradeFlow.cs"));
        var mainWindowSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.xaml.cs"));

        Assert.Contains("PublishConstructionQueueObservationAsync", resourceSource, StringComparison.Ordinal);
        Assert.Contains("PublishConstructionQueueObservationAsync", buildingSource, StringComparison.Ordinal);
        Assert.Contains("_botService.ConstructionQueueObserved += OnConstructionQueueObserved", mainWindowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulBuildingMutation_ReusesCurrentDorf2BeforeFullRefreshFallback()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.QueueExecution.cs"));

        Assert.Contains("ReadCurrentBuildingOverviewStatusAsync", source, StringComparison.Ordinal);
        Assert.Contains("CanUseCurrentDorf2Snapshot", source, StringComparison.Ordinal);
        Assert.Contains("skipped Dorf1 navigation", source, StringComparison.Ordinal);
        Assert.Contains("falling back to full Dorf1+Dorf2 status", source, StringComparison.Ordinal);
    }
}

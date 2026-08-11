using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ActiveWorkingVillageSourceTests
{
    [Fact]
    public void QueueExecution_UsesVerifiedBrowserVillageInsteadOfQueueTarget()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var queueSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.QueueExecution.cs"));
        var switchSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Core",
            "TravianClient.Villages.Switch.cs"));

        Assert.DoesNotContain("MarkActiveWorkingVillageFromQueueItem(item)", queueSource, StringComparison.Ordinal);
        Assert.Contains("NotifyVerifiedActiveVillage(", switchSource, StringComparison.Ordinal);
    }
}
